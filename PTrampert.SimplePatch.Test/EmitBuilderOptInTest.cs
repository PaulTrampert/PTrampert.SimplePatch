using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// The opt-in to the Emit builder is process-wide and can't be turned off through the public API,
// so every test here restores the default afterwards, and none may run alongside another test that
// goes through PatchClassBuilder.Instance.
[NonParallelizable]
public class EmitBuilderOptInTest
{
    [SetUp]
    [TearDown]
    public void UseTheDefaultBuilder() => PatchClassBuilder.UseEmitBuilder = false;

    private static JsonSerializerOptions CreateOptions(bool useExperimentalDynamicClassBuilder)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters(useExperimentalDynamicClassBuilder);
        return options;
    }

    [Test]
    public void OptIn_RoutesInstanceToTheEmitBuilder()
    {
        CreateOptions(useExperimentalDynamicClassBuilder: true);

        Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalClassTestObject)),
            Is.SameAs(EmitPatchClassBuilder.GetPatchClassFor(typeof(InternalClassTestObject))));
    }

    [Test]
    public void OptIn_DoesNotReuseAPatchClassRoslynBuiltBeforeIt()
    {
        var roslynBuilt = PatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject));

        CreateOptions(useExperimentalDynamicClassBuilder: true);
        var afterOptIn = PatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject));

        Assert.Multiple((Action)(() =>
        {
            Assert.That(afterOptIn, Is.Not.SameAs(roslynBuilt),
                "Each builder keeps its own cache, so the Emit path shouldn't hand out a Roslyn-built type.");
            Assert.That(afterOptIn, Is.SameAs(EmitPatchClassBuilder.GetPatchClassFor(typeof(PlainClassTestObject))));
        }));
    }

    [Test]
    public void OptIn_StaysOnWhenOtherOptionsDoNotAskForIt()
    {
        CreateOptions(useExperimentalDynamicClassBuilder: true);
        CreateOptions(useExperimentalDynamicClassBuilder: false);
        new JsonSerializerOptions().AddSimplePatchConverters();

        Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalClassTestObject)),
            Is.SameAs(EmitPatchClassBuilder.GetPatchClassFor(typeof(InternalClassTestObject))));
    }

    [Test]
    public void OptingOut_KeepsTheRoslynBuilder()
    {
        CreateOptions(useExperimentalDynamicClassBuilder: false);

        Assert.That(PatchClassBuilder.UseEmitBuilder, Is.False);
    }

    [Test]
    public void WithoutOptIn_NonPublicTypesThrowNamingTheFlag()
    {
        CreateOptions(useExperimentalDynamicClassBuilder: false);

        var ex = Assert.Throws<NotSupportedException>(
            () => PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalClassTestObject)));

        Assert.That(ex!.Message, Does.Contain("must be public").And.Contain("useExperimentalDynamicClassBuilder"));
    }

    [Test]
    public void OptIn_PatchesANonPublicTypeEndToEnd()
    {
        var options = CreateOptions(useExperimentalDynamicClassBuilder: true);

        var patch = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>(
            """{ "display_name": null, "initOnly": "New", "converted": "value" }""", options)!;
        var result = patch.Patch(new InternalClassTestObject
        {
            Name = "Old", InitOnly = "Old", Rating = 3, Converted = "Old",
        });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.Null, "An explicit null should be applied.");
            Assert.That(result.InitOnly, Is.EqualTo("New"));
            Assert.That(result.Rating, Is.EqualTo(3), "A property the patch leaves out should keep its value.");
            Assert.That(result.Converted, Is.EqualTo("Internal:value"));
        }));
    }

    [Test]
    public void OptIn_ValidatesANonPublicTypeEndToEnd()
    {
        var options = CreateOptions(useExperimentalDynamicClassBuilder: true);
        var invalid = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>("""{ "rating": 20 }""", options)!;
        var omitted = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>("""{ "initOnly": "x" }""", options)!;
        var invalidResults = new List<ValidationResult>();

        var invalidIsValid = Validator.TryValidateObject(invalid, new ValidationContext(invalid), invalidResults, true);
        var omittedIsValid = Validator.TryValidateObject(omitted, new ValidationContext(omitted), [], true);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(invalidIsValid, Is.False);
            Assert.That(invalidResults.Select(r => r.ErrorMessage),
                Is.EqualTo(new[] { "The field Rating must be between 1 and 10." }));
            Assert.That(omittedIsValid, Is.True, "Validation should skip a property the patch leaves out.");
        }));
    }
}
