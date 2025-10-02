using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

public class PatchJsonConverterFactoryTests
{
    private JsonSerializerOptions Options { get; set; }
    
    [SetUp]
    public void Setup()
    {
        Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        Options.AddSimplePatchConverters();
    }

    [Test]
    public void Deserialize_ToIPatchObjectFor_ReturnsObjectWithCorrectOptionalProperties()
    {
        var json = """
                   {
                       "id": 1,
                       "name_field": "Test Name",
                       "ignoredProp": "This should not be included"
                   }
                   """;
        var result = JsonSerializer.Deserialize<IPatchObject<OptionalsBuilderTestObject>>(json, Options);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            var idProp = result.GetType().GetProperty(nameof(OptionalsBuilderTestObject.Id));
            Assert.That(idProp.PropertyType, Is.EqualTo(typeof(Optional<int>)));
            Assert.That(idProp.GetValue(result), Is.EqualTo(new Optional<int>(1)));
            var nameProp = result.GetType().GetProperty(nameof(OptionalsBuilderTestObject.Name));
            Assert.That(nameProp.PropertyType, Is.EqualTo(typeof(Optional<string>)));
            Assert.That(nameProp.GetValue(result), Is.EqualTo(new Optional<string>("Test Name")));
            var ignoredProp = result.GetType().GetProperty(nameof(OptionalsBuilderTestObject.IgnoredProp));
            Assert.That(ignoredProp, Is.Null);
        }
    }
}