using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PTrampert.SimplePatch.Test.External;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// Cases only the Emit builder supports: source types that aren't public. Cases it shares with the
// Roslyn builder are in PatchClassBuilderTest.
public class EmitPatchClassBuilderTest
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters();
        return options;
    }

    private static IPatchObject<T> Deserialize<T>(string json) =>
        (IPatchObject<T>)JsonSerializer.Deserialize(json, EmitPatchClassBuilder.GetPatchClassFor(typeof(T)), Options)!;

    [Test]
    public void GetPatchClassFor_ReturnsTheSameTypeEachTime()
    {
        var first = EmitPatchClassBuilder.GetPatchClassFor(typeof(InternalClassTestObject));

        Assert.That(EmitPatchClassBuilder.GetPatchClassFor(typeof(InternalClassTestObject)), Is.SameAs(first));
    }

    [Test]
    public void Patch_InternalClass_AppliesSetAndExplicitNullValues()
    {
        var patch = Deserialize<InternalClassTestObject>(
            """{ "display_name": null, "initOnly": "New", "converted": "value" }""");

        var result = patch.Patch(new InternalClassTestObject
        {
            Name = "Old", InitOnly = "Old", Rating = 3, Converted = "Old",
        });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.Null, "An explicit null should be applied.");
            Assert.That(result.InitOnly, Is.EqualTo("New"));
            Assert.That(result.Rating, Is.EqualTo(3), "A property the patch leaves out should keep its value.");
            Assert.That(result.Converted, Is.EqualTo("Internal:value"),
                "The internal [JsonConverter] on the source property should be used.");
        }));
    }

    [Test]
    public void Validate_InternalClass_RunsTheSourceValidatorsOnSetProperties()
    {
        var invalid = Deserialize<InternalClassTestObject>("""{ "rating": 20 }""");
        var omitted = Deserialize<InternalClassTestObject>("""{ "initOnly": "x" }""");
        var invalidResults = new List<ValidationResult>();
        var omittedResults = new List<ValidationResult>();

        var invalidIsValid = Validator.TryValidateObject(invalid, new ValidationContext(invalid), invalidResults, true);
        var omittedIsValid = Validator.TryValidateObject(omitted, new ValidationContext(omitted), omittedResults, true);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(invalidIsValid, Is.False);
            Assert.That(invalidResults.Select(r => r.ErrorMessage),
                Is.EqualTo(new[] { "The field Rating must be between 1 and 10." }));
            Assert.That(omittedIsValid, Is.True, "Validation should skip a property the patch leaves out.");
        }));
    }

    [Test]
    public void Patch_PrivateNestedClass()
    {
        var patch = Deserialize<PrivateNestedTestObject>("""{ "name": "New" }""");

        var result = patch.Patch(new PrivateNestedTestObject { Name = "Old", Other = "Kept" });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.EqualTo("New"));
            Assert.That(result.Other, Is.EqualTo("Kept"));
        }));
    }

    [Test]
    public void Patch_PrivatePositionalRecord_KeepsTheTargetsOtherValues()
    {
        var patch = Deserialize<PrivatePositionalRecordTestObject>("""{ "count": 5 }""");

        var result = patch.Patch(new PrivatePositionalRecordTestObject("Old", 1));

        Assert.That(result, Is.EqualTo(new PrivatePositionalRecordTestObject("Old", 5)));
    }

    [Test]
    public void Patch_InternalPrimaryConstructorClass_BindsGetOnlyPropertiesThroughTheConstructor()
    {
        var patch = Deserialize<InternalPrimaryConstructorTestObject>("""{ "name": "New" }""");

        var result = patch.Patch(new InternalPrimaryConstructorTestObject("Old") { Color = "Red" });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.EqualTo("New"));
            Assert.That(result.Color, Is.EqualTo("Red"));
        }));
    }

    [Test]
    public void Patch_InternalStruct()
    {
        var patch = Deserialize<InternalStructTestObject>("""{ "count": 2 }""");

        var result = patch.Patch(new InternalStructTestObject { Name = "Kept", Count = 1 });

        Assert.That(result, Is.EqualTo(new InternalStructTestObject { Name = "Kept", Count = 2 }));
    }

    [Test]
    public void Patch_PropertyOfANonPublicTypeFromAnotherAssembly()
    {
        var patch = Deserialize<ExternalPropertyTypeTestObject>("""{ "color": 1 }""");

        var result = patch.Patch(new ExternalPropertyTypeTestObject { Color = ExternalInternalColor.Red, Other = "Kept" });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Color, Is.EqualTo(ExternalInternalColor.Blue));
            Assert.That(result.Other, Is.EqualTo("Kept"));
        }));
    }

    private class PrivateNestedTestObject
    {
        public string? Name { get; set; }

        public string? Other { get; set; }
    }

    private record PrivatePositionalRecordTestObject(string Name, int Count);
}
