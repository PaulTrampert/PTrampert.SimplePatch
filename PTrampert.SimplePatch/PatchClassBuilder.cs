using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Specialized;
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
    private readonly ConcurrentDictionary<Type, Type> _optionalsClasses = new();

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
        return _optionalsClasses.GetOrAdd(type, CreatePatchClass);
    }
    
    private static Type CreatePatchClass(Type type)
    {
        var unit = new CodeCompileUnit();
        var ns = new CodeNamespace($"{type.Namespace}.Optionals");
        unit.Namespaces.Add(ns);
        var className = $"{type.Name}Optionals";
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

        var newAssembly = AssemblyLoadContext.Default.LoadFromStream(ms);
        return newAssembly.GetType($"{ns.Name}.{className}")!;
    }
}