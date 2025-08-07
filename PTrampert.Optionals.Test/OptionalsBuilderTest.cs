using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.Optionals.Test.TestObjects;

namespace PTrampert.Optionals.Test;

public class OptionalsBuilderTest
{
    [Test]
    public void CreateOptionalsClassSource_CopiesThePropertiesAsOptionals()
    {
        var builder = new OptionalsBuilder();

        var optionalsType = builder.CreateOptionalsClass(typeof(OptionalsBuilderTestObject));
        
        Assert.Multiple(() =>
        {
            Assert.That(optionalsType.GetProperty(nameof(OptionalsBuilderTestObject.Id)), Is.Not.Null);
            Assert.That(optionalsType.GetProperty(nameof(OptionalsBuilderTestObject.Name)), Is.Not.Null);
            Assert.That(optionalsType.GetProperty(nameof(OptionalsBuilderTestObject.IgnoredProp)), Is.Null, 
                "Ignored properties should not be included in the generated optionals class");
        });
    }
    
    [Test]
    public void DynamicOptionalsClass_CanBeCreatedAndUsed()
    {
        var json = """
        {
            "id": 1,
            "name_field": "Test Name",
            "ignoredProp": "This should not be included"
        }
        """;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new OptionalJsonConverterFactory());
        var builder = new OptionalsBuilder();
        var optionalsType = builder.CreateOptionalsClass(typeof(OptionalsBuilderTestObject));
        
        var instance = JsonSerializer.Deserialize(json, optionalsType, options) as IApplyOptionals<OptionalsBuilderTestObject>;
        
        Assert.That(instance.Apply(new OptionalsBuilderTestObject()), Is.EqualTo(new OptionalsBuilderTestObject
        {
            Id = 1,
            Name = "Test Name",
            IgnoredProp = null // Ignored properties should not be set
        }));
    }
}