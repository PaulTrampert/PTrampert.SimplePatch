using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;

namespace PTrampert.Optionals;

public class OptionalsBuilder
{
    private const string ApplyTargetParamName = "target";
    private readonly Dictionary<Type, Type> _optionalsClasses = new();

    public Type CreateOptionalsClass(Type type)
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

        classType.BaseTypes.Add(typeof(IApplyOptionals<>).MakeGenericType(type));
        var applyMethod = new CodeMemberMethod
        {
            Name = nameof(IApplyOptionals<object>.Apply),
            ReturnType = new CodeTypeReference(type),
            Attributes = MemberAttributes.Public | MemberAttributes.Final,
            Parameters =
            {
                new CodeParameterDeclarationExpression(type, ApplyTargetParamName)
            }
        };
        classType.Members.Add(applyMethod);
        var srcTypeProperties = type.GetProperties()
            .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);
        
        var initString = new StringBuilder($"new {type.FullName} {{{Environment.NewLine}");

        foreach (var property in srcTypeProperties)
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

            if (property.GetCustomAttribute<JsonPropertyNameAttribute>() is { } jsonPropertyName)
            {
                codegenProperty.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(JsonPropertyNameAttribute)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(jsonPropertyName.Name))));
            }
            classType.Members.Add(backingField);
            classType.Members.Add(codegenProperty);
            
            initString.AppendLine($"{property.Name} = this.{backingField.Name}.{nameof(Optional<object>.HasValue)} ? this.{backingField.Name}.{nameof(Optional<object>.Value)} : {ApplyTargetParamName}.{property.Name},");
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