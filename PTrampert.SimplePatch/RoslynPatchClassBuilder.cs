using System.CodeDom;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;

namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> by generating C# with CodeDom and
/// compiling it with Roslyn into an in-memory assembly.
/// </summary>
/// <remarks>
/// <para>
/// Not used at runtime: <see cref="PatchClassBuilder"/> delegates to <see cref="EmitPatchClassBuilder"/>.
/// It is kept, and still tested against the same cases, pending the decision in
/// https://github.com/PaulTrampert/PTrampert.SimplePatch/issues/144 on whether it becomes a
/// compile-time source generator.
/// </para>
/// <para>
/// The compiled assembly is separate from the source type's, so it can only name public types.
/// <see cref="EmitPatchClassBuilder"/> builds the same class without that restriction.
/// </para>
/// </remarks>
internal sealed class RoslynPatchClassBuilder : IPatchClassBuilder
{
    private const string ApplyTargetParamName = "target";

    /// <summary>
    /// Namespace used for patch classes generated from source types that are not themselves
    /// in a namespace.
    /// </summary>
    private const string GlobalNamespaceFallback = "PTrampert.SimplePatch.Generated";

    /// <summary>
    /// The builder. It holds no state, so there is no reason for a second instance.
    /// </summary>
    public static RoslynPatchClassBuilder Instance { get; } = new();

    private RoslynPatchClassBuilder()
    {
    }

    /// <summary>
    /// Creates the patch class for <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    /// Compiles and loads a new assembly on every call. Wrap the builder in a
    /// <see cref="CachingPatchClassBuilder"/> to build each type once.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// <paramref name="type"/> is not public, or is nested in or constructed from a type that is not
    /// public, or <see cref="PatchClassModel.For"/> can't model it.
    /// </exception>
    public Type GetPatchClassFor(Type type)
    {
        return CreatePatchClass(type);
    }
    
    private static Type CreatePatchClass(Type type)
    {
        // The patch class is compiled into its own assembly, which can only refer to public types.
        // IsVisible is false if the type, any declaring type, or any generic type argument isn't public.
        if (!type.IsVisible)
        {
            throw new NotSupportedException(
                $"Cannot create a patch class for '{type.FullName}' because it is not public. Patch source "
                + "types must be public, as must any types they are nested in and any generic type arguments.");
        }

        // The Patch method body is a hand-written snippet, so every name in it has to be formatted
        // as C# here; CodeDom only does that for the parts of the class it generates itself.
        var provider = new CSharpCodeProvider();
        var unit = new CodeCompileUnit();
        var namespaceRoot = string.IsNullOrEmpty(type.Namespace) ? GlobalNamespaceFallback : type.Namespace;
        var ns = new CodeNamespace($"{namespaceRoot}.Optionals");
        unit.Namespaces.Add(ns);
        // type.Name can contain characters that aren't valid in an identifier, such as the ` in
        // Gen`1. Dropping them is safe because the random suffix keeps the name unique.
        var typeName = new string(type.Name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        var className = $"{typeName}_Optionals_{Path.GetRandomFileName().Replace('.', '_')}";
        var classType = new CodeTypeDeclaration(className)
        {
            IsClass = true,
            TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed,
        };
        ns.Types.Add(classType);

        classType.BaseTypes.Add(typeof(IPatchObject<>).MakeGenericType(type));
        var applyMethod = new CodeMemberMethod
        {
            Name = nameof(IPatchObject<object>.Patch),
            ReturnType = new CodeTypeReference(type),
            Attributes = MemberAttributes.Public | MemberAttributes.Final,
            Parameters =
            {
                new CodeParameterDeclarationExpression(type, ApplyTargetParamName)
            }
        };
        classType.Members.Add(applyMethod);
        
        var model = PatchClassModel.For(type);
        var patchedValues = new Dictionary<PropertyInfo, string>();

        foreach (var optionalProperty in model.OptionalProperties)
        {
            var property = optionalProperty.Property;
            var optionalType = typeof(Optional<>).MakeGenericType(property.PropertyType);

            var backingField = new CodeMemberField(optionalType, $"_{property.Name}")
            {
                Attributes = MemberAttributes.Private
            };

            var codegenProperty = new CodeMemberProperty
            {
                Name = property.Name,
                Type = new CodeTypeReference(optionalType),
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                HasGet = true,
                GetStatements =
                {
                    new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(),
                        backingField.Name))
                },
                HasSet = true,
                SetStatements =
                {
                    new CodeAssignStatement(
                        new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backingField.Name),
                        new CodePropertySetValueReferenceExpression())
                },
            };
            
            if (optionalProperty.HasConverter)
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(OptionalConverterAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(type)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(property.Name))));
            }

            if (optionalProperty.JsonPropertyName is { } jsonPropertyName)
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(JsonPropertyNameAttribute)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(jsonPropertyName))));
            }
            
            foreach (var validator in optionalProperty.Validators)
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(OptionalValidationAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(validator.ValidatorType)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(validator.Index)))
                );
            }
            
            classType.Members.Add(backingField);
            classType.Members.Add(codegenProperty);
            
            var propertyName = provider.CreateEscapedIdentifier(property.Name);
            patchedValues[property] = $"this.{backingField.Name}.{nameof(Optional<object>.HasValue)} ? this.{backingField.Name}.{nameof(Optional<object>.Value)} : {ApplyTargetParamName}.{propertyName}";
        }
        
        // The clone made by `with` already carries the ignored properties over.
        if (!model.IsRecord)
        {
            foreach (var ignoredProperty in model.IgnoredProperties)
            {
                var propertyName = provider.CreateEscapedIdentifier(ignoredProperty.Name);
                patchedValues[ignoredProperty] = $"{ApplyTargetParamName}.{propertyName}";
            }
        }

        // Constructor-bound properties go to the constructor; the rest go in the object initializer.
        var initString = new StringBuilder();
        if (model.IsRecord)
        {
            initString.AppendLine($"{ApplyTargetParamName} with {{");
        }
        else
        {
            initString.Append($"new {provider.GetTypeOutput(new CodeTypeReference(type))}(");
            initString.Append(string.Join(", ", model.ConstructorProperties.Select(p => $"({patchedValues[p]})")));
            initString.AppendLine(") {");
        }
        foreach (var property in patchedValues.Keys.Except(model.ConstructorProperties))
        {
            initString.AppendLine($"{provider.CreateEscapedIdentifier(property.Name)} = {patchedValues[property]},");
        }

        initString.AppendLine("};");
        
        var applyMethodBody = new CodeMethodReturnStatement(new CodeSnippetExpression(initString.ToString()));
        applyMethod.Statements.Add(applyMethodBody);
        
        var writer = new StringWriter();
        provider.GenerateCodeFromCompileUnit(unit, writer, null);
        var source = writer.ToString();
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var assemblyName = Path.GetRandomFileName();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            throw new Exception(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        }

        ms.Seek(0, SeekOrigin.Begin);

        var callingContext = AssemblyLoadContext.GetLoadContext(type.Assembly);
        var newAssembly = callingContext?.LoadFromStream(ms) ?? AssemblyLoadContext.Default.LoadFromStream(ms);
        
        return newAssembly.GetType($"{ns.Name}.{className}")!;
    }
}
