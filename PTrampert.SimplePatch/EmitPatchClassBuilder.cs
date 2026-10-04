using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> by emitting IL with
/// Reflection.Emit, rather than compiling C# with Roslyn as <see cref="RoslynPatchClassBuilder"/> does.
/// <see cref="PatchClassBuilder"/> delegates to it. Unlike the Roslyn builder, it supports internal
/// source types, provided their assembly grants <c>[InternalsVisibleTo]</c> to <see cref="AssemblyName"/>.
/// </summary>
/// <remarks>
/// Roslyn checks accessibility when it compiles, so the separate assembly it builds can't name a
/// non-public type. The runtime still checks access when it loads emitted IL, so the emitted
/// assembly needs a grant too. It gets one the way Castle DynamicProxy's does: every assembly it
/// emits has the same fixed name, which the consuming assembly names in
/// <c>[InternalsVisibleTo]</c>. The undocumented <c>[IgnoresAccessChecksTo]</c> would need no
/// grant, but it isn't officially supported (https://github.com/dotnet/runtime/issues/37875).
/// <c>[InternalsVisibleTo]</c> doesn't reach <c>private</c> or <c>protected</c> members, so private
/// nested source types aren't supported. The class emitted here has the same shape as the one
/// <see cref="RoslynPatchClassBuilder"/> compiles: both are built from <see cref="PatchClassModel"/>.
/// </remarks>
internal sealed class EmitPatchClassBuilder : IPatchClassBuilder
{
    /// <summary>
    /// The name of every assembly this builder emits. An assembly whose internal types are patched
    /// declares <c>[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]</c>.
    /// </summary>
    public const string AssemblyName = "PTrampert.SimplePatch.Emitted";

    private const string GlobalNamespaceFallback = "PTrampert.SimplePatch.Generated";

    // Separate from RoslynPatchClassBuilder's cache, so each builder hands out only the types it built.
    // Lazy for the same reason as there: concurrent first use should emit one assembly, not one per thread.
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> PatchClasses = new();

    /// <summary>
    /// The builder. Its cache is static, so there is no reason for a second instance.
    /// </summary>
    public static EmitPatchClassBuilder Instance { get; } = new();

    private EmitPatchClassBuilder()
    {
    }

    /// <summary>
    /// Gets or creates the patch class for <paramref name="type"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <see cref="PatchClassModel.For"/> can't model <paramref name="type"/>, one of its patched
    /// properties has no getter, or the patch class would name a type or getter that the emitted
    /// assembly can't access.
    /// </exception>
    public Type GetPatchClassFor(Type type)
    {
        return PatchClasses.GetOrAdd(type, t => new Lazy<Type>(() => CreatePatchClass(t))).Value;
    }

    private static Type CreatePatchClass(Type type)
    {
        var model = PatchClassModel.For(type);
        EnsureAccessible(model);

        // Each source type gets its own assembly, so the patch type's name can't collide with
        // another and needs neither a random suffix nor cleaning up into a C# identifier. The
        // assemblies all share one name, because that name is what [InternalsVisibleTo] grants.
        // Load it where the source type lives, as RoslynPatchClassBuilder does with its compiled assembly.
        using var contextScope = AssemblyLoadContext.EnterContextualReflection(type.Assembly);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName(AssemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(AssemblyName);

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
    /// Throws if the patch class would name a type or call a getter that the emitted assembly
    /// can't access. Otherwise the runtime would only fail when it loads the type or first runs
    /// <c>Patch</c>, with an error that doesn't say how to fix it.
    /// </summary>
    /// <remarks>
    /// The patch class names the source type and the types it is built from, and every patched
    /// property's type (<see cref="Optional{T}"/> names it) and declaring type. Setters and
    /// constructors need no check, because <see cref="PatchClassModel"/> only uses public ones.
    /// </remarks>
    private static void EnsureAccessible(PatchClassModel model)
    {
        var visited = new HashSet<Type>();

        void Visit(Type? type)
        {
            if (type == null || type.IsGenericParameter || !visited.Add(type))
            {
                return;
            }

            // An array or constructed generic type is accessible if its parts are, and they are
            // visited below.
            var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (!type.HasElementType && !IsAccessible(definition))
            {
                throw new NotSupportedException(
                    $"Cannot create a patch class for '{model.SourceType.FullName}' because it uses "
                    + $"'{type.FullName}', which the generated assembly can't access. "
                    + DescribeFix(type.Assembly, "Make the type public, or internal"));
            }

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

            // Patch reads the target's value of any property the patch leaves out.
            if (property.GetMethod is { } getter && !IsAccessible(getter))
            {
                throw new NotSupportedException(
                    $"Cannot create a patch class for '{model.SourceType.FullName}' because the getter of "
                    + $"'{property.Name}' isn't accessible to the generated assembly. "
                    + DescribeFix(getter.Module.Assembly, "Make the getter public, or internal"));
            }
        }
    }

    private static string DescribeFix(Assembly assembly, string makeItAccessible) =>
        $"{makeItAccessible} with [assembly: InternalsVisibleTo(\"{AssemblyName}\")] in "
        + $"'{assembly.GetName().Name}'.";

    /// <summary>
    /// Whether code in the emitted assembly can name <paramref name="type"/>: it is public, or it
    /// and every type it is nested in are at least internal, in an assembly that grants
    /// <c>[InternalsVisibleTo]</c> to <see cref="AssemblyName"/>.
    /// </summary>
    private static bool IsAccessible(Type type)
    {
        if (type.IsVisible)
        {
            return true;
        }

        for (var current = type; current != null; current = current.DeclaringType)
        {
            if (current.IsNested
                && !(current.IsNestedPublic || current.IsNestedAssembly || current.IsNestedFamORAssem))
            {
                return false;
            }
        }

        return GrantsInternalsAccess(type.Assembly);
    }

    private static bool IsAccessible(MethodInfo method) =>
        method.IsPublic || ((method.IsAssembly || method.IsFamilyOrAssembly) && GrantsInternalsAccess(method.Module.Assembly));

    private static bool GrantsInternalsAccess(Assembly assembly) =>
        assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Any(a => new System.Reflection.AssemblyName(a.AssemblyName).Name == AssemblyName);

    /// <summary>
    /// Defines the <see cref="Optional{T}"/> backing field and property for one source property,
    /// with the attributes <see cref="RoslynPatchClassBuilder"/> gives it, and returns the field.
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
    /// Emits <c>Patch(T target)</c>. It builds the result as <see cref="RoslynPatchClassBuilder"/>'s C#
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

        // As in the C# RoslynPatchClassBuilder generates, the clone made by `with` already carries the
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
