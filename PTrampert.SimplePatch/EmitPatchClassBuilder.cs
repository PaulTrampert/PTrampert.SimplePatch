using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> by emitting IL with
/// Reflection.Emit, rather than compiling C# with Roslyn as <see cref="PatchClassBuilder"/> does.
/// Unlike that builder, it supports source types that aren't public.
/// </summary>
/// <remarks>
/// Roslyn checks accessibility when it compiles, so the separate assembly it builds can't name a
/// non-public type. IL has no compile-time accessibility check, and the runtime skips its own
/// checks for the assemblies a dynamic assembly lists in <c>[IgnoresAccessChecksTo]</c>. The class
/// emitted here has the same shape as the one <see cref="PatchClassBuilder"/> compiles: both are
/// built from <see cref="PatchClassModel"/>.
/// </remarks>
internal static class EmitPatchClassBuilder
{
    private const string GlobalNamespaceFallback = "PTrampert.SimplePatch.Generated";

    // The runtime recognizes this attribute by its full name alone, and the BCL doesn't ship a
    // public one, so each dynamic assembly defines its own. It has no Microsoft Learn page
    // (https://github.com/dotnet/runtime/issues/37875), but the runtime's own DispatchProxy relies
    // on it the same way, and dotnet/runtime tests it in
    // src/tests/reflection/RefEmit/EmittingIgnoresAccessChecksToAttributeIsRespected.cs.
    private const string IgnoresAccessChecksToAttributeName =
        "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute";

    // Separate from PatchClassBuilder's cache, so each builder hands out only the types it built.
    // Lazy for the same reason as there: concurrent first use should emit one assembly, not one per thread.
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> PatchClasses = new();

    /// <summary>
    /// Gets or creates the patch class for <paramref name="type"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <see cref="PatchClassModel.For"/> can't model <paramref name="type"/>, or one of its patched
    /// properties has no getter.
    /// </exception>
    public static Type GetPatchClassFor(Type type)
    {
        return PatchClasses.GetOrAdd(type, t => new Lazy<Type>(() => CreatePatchClass(t))).Value;
    }

    private static Type CreatePatchClass(Type type)
    {
        var model = PatchClassModel.For(type);

        // Each source type gets its own assembly, so the patch type's name can't collide with
        // another and needs neither a random suffix nor cleaning up into a C# identifier.
        // Load it where the source type lives, as PatchClassBuilder does with its compiled assembly.
        using var contextScope = AssemblyLoadContext.EnterContextualReflection(type.Assembly);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"PTrampert.SimplePatch.Emitted.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assembly.GetName().Name!);

        // The grant has to be in place before the patch type is created, because the runtime checks
        // access when it loads the type (the interface it implements names the source type).
        var ignoresAccessChecksTo = DefineIgnoresAccessChecksToAttribute(module);
        foreach (var referencedAssembly in GetReferencedAssemblies(model))
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                ignoresAccessChecksTo, [referencedAssembly.GetName().Name!]));
        }

        var namespaceRoot = string.IsNullOrEmpty(type.Namespace) ? GlobalNamespaceFallback : type.Namespace;
        var patchInterface = typeof(IPatchObject<>).MakeGenericType(type);
        var typeBuilder = module.DefineType(
            $"{namespaceRoot}.Optionals.{type.Name}_Optionals",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
            typeof(object),
            [patchInterface]);
        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

        var fields = new Dictionary<PropertyInfo, FieldBuilder>();
        foreach (var optionalProperty in model.OptionalProperties)
        {
            fields[optionalProperty.Property] = DefineOptionalProperty(typeBuilder, model, optionalProperty);
        }

        DefinePatchMethod(typeBuilder, patchInterface, model, fields);

        return typeBuilder.CreateType();
    }

    /// <summary>
    /// Defines an <c>IgnoresAccessChecksToAttribute(string assemblyName)</c> in <paramref name="module"/>
    /// and returns its constructor.
    /// </summary>
    private static ConstructorInfo DefineIgnoresAccessChecksToAttribute(ModuleBuilder module)
    {
        var attributeBuilder = module.DefineType(
            IgnoresAccessChecksToAttributeName,
            TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
            typeof(Attribute));
        attributeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AttributeUsageAttribute).GetConstructor([typeof(AttributeTargets)])!,
            [AttributeTargets.Assembly],
            [typeof(AttributeUsageAttribute).GetProperty(nameof(AttributeUsageAttribute.AllowMultiple))!],
            [true]));

        var constructor = attributeBuilder.DefineConstructor(
            MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]);
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);

        return attributeBuilder.CreateType().GetConstructor([typeof(string)])!;
    }

    /// <summary>
    /// The assemblies whose types the patch class names in its signatures or its <c>Patch</c>
    /// method: the source type and the types it is built from, and every patched property's type
    /// (<see cref="Optional{T}"/> names it) and declaring type.
    /// </summary>
    private static HashSet<Assembly> GetReferencedAssemblies(PatchClassModel model)
    {
        var assemblies = new HashSet<Assembly>();
        var visited = new HashSet<Type>();

        void Visit(Type? type)
        {
            if (type == null || !visited.Add(type))
            {
                return;
            }

            assemblies.Add(type.Assembly);
            Visit(type.DeclaringType);
            if (type.HasElementType)
            {
                Visit(type.GetElementType());
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    Visit(argument);
                }
            }
        }

        Visit(model.SourceType);
        foreach (var property in model.OptionalProperties.Select(p => p.Property)
                     .Concat(model.ConstructorProperties)
                     .Concat(model.IgnoredProperties))
        {
            Visit(property.PropertyType);
            Visit(property.DeclaringType);
        }

        return assemblies;
    }

    /// <summary>
    /// Defines the <see cref="Optional{T}"/> backing field and property for one source property,
    /// with the attributes <see cref="PatchClassBuilder"/> gives it, and returns the field.
    /// </summary>
    private static FieldBuilder DefineOptionalProperty(
        TypeBuilder typeBuilder, PatchClassModel model, OptionalPropertyModel optionalProperty)
    {
        var property = optionalProperty.Property;
        var optionalType = typeof(Optional<>).MakeGenericType(property.PropertyType);
        var field = typeBuilder.DefineField($"_{property.Name}", optionalType, FieldAttributes.Private);
        var propertyBuilder = typeBuilder.DefineProperty(property.Name, PropertyAttributes.None, optionalType, null);
        const MethodAttributes accessorAttributes =
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

        var getter = typeBuilder.DefineMethod($"get_{property.Name}", accessorAttributes, optionalType, Type.EmptyTypes);
        var il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
        propertyBuilder.SetGetMethod(getter);

        var setter = typeBuilder.DefineMethod($"set_{property.Name}", accessorAttributes, typeof(void), [optionalType]);
        il = setter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        propertyBuilder.SetSetMethod(setter);

        if (optionalProperty.HasConverter)
        {
            propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(OptionalConverterAttribute).GetConstructor([typeof(Type), typeof(string)])!,
                [model.SourceType, property.Name]));
        }

        if (optionalProperty.JsonPropertyName is { } jsonPropertyName)
        {
            propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(JsonPropertyNameAttribute).GetConstructor([typeof(string)])!,
                [jsonPropertyName]));
        }

        foreach (var validator in optionalProperty.Validators)
        {
            propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(OptionalValidationAttribute).GetConstructor([typeof(Type), typeof(int)])!,
                [validator.ValidatorType, validator.Index]));
        }

        return field;
    }

    /// <summary>
    /// Emits <c>Patch(T target)</c>. It builds the result as <see cref="PatchClassBuilder"/>'s C#
    /// does: a <c>with</c> clone for a record, otherwise the chosen constructor followed by the
    /// setters for the remaining properties.
    /// </summary>
    private static void DefinePatchMethod(
        TypeBuilder typeBuilder, Type patchInterface, PatchClassModel model, Dictionary<PropertyInfo, FieldBuilder> fields)
    {
        var type = model.SourceType;
        var method = typeBuilder.DefineMethod(
            nameof(IPatchObject<object>.Patch),
            MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig
            | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            type,
            [type]);
        method.DefineParameter(1, ParameterAttributes.None, "target");
        typeBuilder.DefineMethodOverride(method, patchInterface.GetMethod(nameof(IPatchObject<object>.Patch))!);
        var il = method.GetILGenerator();

        // As in the C# PatchClassBuilder generates, the clone made by `with` already carries the
        // ignored properties over, so only a newly constructed instance has to copy them.
        var assigned = model.OptionalProperties.Select(p => p.Property)
            .Concat(model.IsRecord ? [] : model.IgnoredProperties)
            .Except(model.ConstructorProperties)
            .ToList();

        if (model.IsRecord)
        {
            // target with { P = ... }: clone, then call each init accessor on the clone.
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance)!);
            il.Emit(OpCodes.Castclass, type);
            foreach (var property in assigned)
            {
                il.Emit(OpCodes.Dup);
                EmitPatchedValue(il, model, fields, property);
                il.Emit(OpCodes.Callvirt, GetSetter(property));
            }
        }
        else if (!type.IsValueType)
        {
            // new T(...) { P = ... }
            foreach (var property in model.ConstructorProperties)
            {
                EmitPatchedValue(il, model, fields, property);
            }

            il.Emit(OpCodes.Newobj, model.Constructor!);
            foreach (var property in assigned)
            {
                il.Emit(OpCodes.Dup);
                EmitPatchedValue(il, model, fields, property);
                il.Emit(OpCodes.Callvirt, GetSetter(property));
            }
        }
        else
        {
            // A struct is built in a local, because its setters need the address of the instance.
            var result = il.DeclareLocal(type);
            if (model.Constructor is { } constructor)
            {
                foreach (var property in model.ConstructorProperties)
                {
                    EmitPatchedValue(il, model, fields, property);
                }

                il.Emit(OpCodes.Newobj, constructor);
                il.Emit(OpCodes.Stloc, result);
            }
            else
            {
                il.Emit(OpCodes.Ldloca, result);
                il.Emit(OpCodes.Initobj, type);
            }

            foreach (var property in assigned)
            {
                il.Emit(OpCodes.Ldloca, result);
                EmitPatchedValue(il, model, fields, property);
                il.Emit(OpCodes.Call, GetSetter(property));
            }

            il.Emit(OpCodes.Ldloc, result);
        }

        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Pushes the patched value of <paramref name="property"/>:
    /// <c>_P.HasValue ? _P.Value : target.P</c>, or just <c>target.P</c> if the patch class has no
    /// <see cref="Optional{T}"/> for it. <c>target.P</c> is only read when the patch doesn't set P.
    /// </summary>
    private static void EmitPatchedValue(
        ILGenerator il, PatchClassModel model, Dictionary<PropertyInfo, FieldBuilder> fields, PropertyInfo property)
    {
        if (!fields.TryGetValue(property, out var field))
        {
            EmitTargetValue(il, model.SourceType, property);
            return;
        }

        var useTarget = il.DefineLabel();
        var end = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, field);
        il.Emit(OpCodes.Call, field.FieldType.GetProperty(nameof(Optional<object>.HasValue))!.GetMethod!);
        il.Emit(OpCodes.Brfalse, useTarget);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, field);
        il.Emit(OpCodes.Call, field.FieldType.GetProperty(nameof(Optional<object>.Value))!.GetMethod!);
        il.Emit(OpCodes.Br, end);
        il.MarkLabel(useTarget);
        EmitTargetValue(il, model.SourceType, property);
        il.MarkLabel(end);
    }

    /// <summary>Pushes <c>target.P</c>.</summary>
    private static void EmitTargetValue(ILGenerator il, Type type, PropertyInfo property)
    {
        var getter = property.GetMethod ?? throw new NotSupportedException(
            $"Cannot create a patch class for '{type.FullName}' because property '{property.Name}' has no getter, "
            + "so a patch that leaves it out can't keep the target's value.");
        if (type.IsValueType)
        {
            il.Emit(OpCodes.Ldarga_S, (byte)1);
            il.Emit(OpCodes.Call, getter);
        }
        else
        {
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, getter);
        }
    }

    // PatchClassModel only assigns properties through a public setter or init accessor.
    private static MethodInfo GetSetter(PropertyInfo property) => property.SetMethod!;
}
