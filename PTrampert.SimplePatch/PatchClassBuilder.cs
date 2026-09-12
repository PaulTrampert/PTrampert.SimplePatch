using System.CodeDom;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;

namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> for a given type.
/// </summary>
public class PatchClassBuilder
{
    private const string ApplyTargetParamName = "target";

    /// <summary>
    /// Namespace used for patch classes generated from source types that are not themselves
    /// in a namespace.
    /// </summary>
    private const string GlobalNamespaceFallback = "PTrampert.SimplePatch.Generated";

    // Static so that every builder — the one used by PatchJsonConverterFactory, and any the
    // OpenAPI integrations or user code create — resolves a given source type to the same
    // generated patch type, instead of each emitting its own dynamic assembly for it.
    private static readonly ConcurrentDictionary<Type, Type> OptionalsClasses = new();

    /// <summary>
    /// The builder. Use this rather than constructing your own: all instances share one cache, so
    /// a new instance buys nothing but an allocation.
    /// </summary>
#pragma warning disable CS0618 // The obsolete constructor is how the singleton itself is built.
    public static PatchClassBuilder Instance { get; } = new();
#pragma warning restore CS0618

    /// <summary>
    /// Creates a builder.
    /// </summary>
    [Obsolete("Use PatchClassBuilder.Instance instead. Every builder shares one cache, so a new "
              + "instance buys nothing but an allocation. This constructor will be made internal "
              + "in the next major version: "
              + "https://github.com/PaulTrampert/PTrampert.SimplePatch/issues/75")]
    public PatchClassBuilder()
    {
    }

    /// <summary>
    /// Gets or creates a class that implements <see cref="IPatchObject{T}"/> for the specified type.
    /// This class will have properties for each writable property of the type, wrapped in <see cref="Optional{T}"/>.
    /// Properties that are marked with <see cref="JsonIgnoreAttribute"/> will not be included in the generated class.
    /// The generated class will have a method <c>Patch</c> that takes an instance of the type and returns a new instance with
    /// the optional properties applied. The method will use the <c>target</c>
    /// parameter to access the original values of the properties that are not set in the optional properties class.
    /// The generated class will be sealed and public, and will be placed in a namespace that matches
    /// the original type's namespace, with an additional ".Optionals" suffix.
    /// </summary>
    /// <param name="type">The type to get a patch type for.</param>
    /// <returns>The generated patch type.</returns>
    public Type GetPatchClassFor(Type type)
    {
        return OptionalsClasses.GetOrAdd(type, CreatePatchClass);
    }
    
    private static Type CreatePatchClass(Type type)
    {
        var unit = new CodeCompileUnit();
        var namespaceRoot = string.IsNullOrEmpty(type.Namespace) ? GlobalNamespaceFallback : type.Namespace;
        var ns = new CodeNamespace($"{namespaceRoot}.Optionals");
        unit.Namespaces.Add(ns);
        var className = $"{type.Name}_Optionals_{Path.GetRandomFileName().Replace('.', '_')}";
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
        
        var sourceProperties = type.GetProperties();
        var ignoredProperties = sourceProperties
            .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() != null);
        var optionalProperties = sourceProperties
            .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);
        
        var initString = new StringBuilder($"new {type.FullName} {{{Environment.NewLine}");

        foreach (var property in optionalProperties)
        {
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
            
            if (property.GetCustomAttribute<JsonConverterAttribute>() is { } jsonConverterAttribute)
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(OptionalConverterAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(type)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(property.Name))));
            }

            if (property.GetCustomAttribute<JsonPropertyNameAttribute>() is { } jsonPropertyName)
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(JsonPropertyNameAttribute)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(jsonPropertyName.Name))));
            }
            
            foreach (var validationAttribute in property.GetCustomAttributes<ValidationAttribute>())
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(OptionalValidationAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(validationAttribute.GetType())))
                );
            }
            
            classType.Members.Add(backingField);
            classType.Members.Add(codegenProperty);
            
            initString.AppendLine($"{property.Name} = this.{backingField.Name}.{nameof(Optional<object>.HasValue)} ? this.{backingField.Name}.{nameof(Optional<object>.Value)} : {ApplyTargetParamName}.{property.Name},");
        }
        
        foreach (var ignoredProperty in ignoredProperties)
        {
            initString.AppendLine($"{ignoredProperty.Name} = {ApplyTargetParamName}.{ignoredProperty.Name},");
        }

        initString.AppendLine("};");
        
        var applyMethodBody = new CodeMethodReturnStatement(new CodeSnippetExpression(initString.ToString()));
        applyMethod.Statements.Add(applyMethodBody);
        
        var provider = new CSharpCodeProvider();
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