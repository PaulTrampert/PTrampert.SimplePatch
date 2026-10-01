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

    [Test]
    public void Deserialize_NullForPropertyWithCustomConverter_DoesNotPassNullToConverter()
    {
        var json = """{ "fakeStringProp": null }""";
        var source = JsonSerializer.Deserialize<OptionalsBuilderTestObject>(json, Options);
        var result = JsonSerializer.Deserialize<IPatchObject<OptionalsBuilderTestObject>>(json, Options);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(source!.FakeStringProp, Is.Null);
            var prop = result!.GetType().GetProperty(nameof(OptionalsBuilderTestObject.FakeStringProp));
            Assert.That(prop!.GetValue(result), Is.EqualTo(new Optional<string?>(null)));
        }
    }

    [Test]
    public void Serialize_NullForPropertyWithCustomConverter_WritesNullWithoutCallingConverter()
    {
        var patch = JsonSerializer.Deserialize<IPatchObject<OptionalsBuilderTestObject>>(
            """{ "fakeStringProp": null }""", Options);

        var json = JsonSerializer.Serialize((object)patch!, Options);

        Assert.That(json, Is.EqualTo("""
        {
          "fakeStringProp": null
        }
        """));
    }

    [Test]
    public void PatchFor_PropertyWithConverterFactory_ReadsAndWritesSameJsonAsSourceModel()
    {
        AssertPatchMatchesSourceModel<StringEnumTestObject, Color>(
            """{ "color": "Blue" }""", nameof(StringEnumTestObject.Color), Color.Blue, Options);
    }

    [Test]
    public void PatchFor_NullablePropertyWithConverterFactoryForUnderlyingType_ReadsAndWritesSameJsonAsSourceModel()
    {
        AssertPatchMatchesSourceModel<NullableStringEnumTestObject, Color?>(
            """{ "color": "Blue" }""", nameof(NullableStringEnumTestObject.Color), Color.Blue, Options);
    }

    [Test]
    public void PatchFor_NullablePropertyWithConverterFactoryForUnderlyingType_ReadsNull()
    {
        var patch = JsonSerializer.Deserialize<IPatchObject<NullableStringEnumTestObject>>("""{ "color": null }""", Options);

        Assert.That(patch!.GetType().GetProperty(nameof(NullableStringEnumTestObject.Color))!.GetValue(patch),
            Is.EqualTo(new Optional<Color?>(null)));
    }

    [Test]
    public void PatchFor_PropertyWithCustomConverterAttribute_ReadsAndWritesSameJsonAsSourceModel()
    {
        AssertPatchMatchesSourceModel<CustomConverterAttributeTestObject, string?>(
            """{ "value": "test" }""", nameof(CustomConverterAttributeTestObject.Value), "FakeString:test", Options);
    }

    private static void AssertPatchMatchesSourceModel<TModel, TValue>(string json, string propertyName, TValue expected, JsonSerializerOptions options)
    {
        var model = JsonSerializer.Deserialize<TModel>(json, options);
        var patch = JsonSerializer.Deserialize<IPatchObject<TModel>>(json, options);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(typeof(TModel).GetProperty(propertyName)!.GetValue(model), Is.EqualTo(expected));
            Assert.That(patch!.GetType().GetProperty(propertyName)!.GetValue(patch), Is.EqualTo(new Optional<TValue>(expected)));
            Assert.That(JsonSerializer.Serialize((object)patch, options), Is.EqualTo(JsonSerializer.Serialize(model, options)));
        }
    }
}
