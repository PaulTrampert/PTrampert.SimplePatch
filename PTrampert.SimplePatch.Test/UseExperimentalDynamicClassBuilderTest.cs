using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// The flag is process-wide, so these tests must not overlap with others that build patch classes
// through PatchClassBuilder, and each one puts the flag back afterwards.
[NonParallelizable]
public class UseExperimentalDynamicClassBuilderTest
{
    [TearDown]
    public void TearDown() => PatchClassBuilder.UseExperimentalDynamicClassBuilder = false;

    [Test]
    public void IsOffByDefault()
    {
        Assert.That(PatchClassBuilder.UseExperimentalDynamicClassBuilder, Is.False);
    }

    [Test]
    public void Instance_IsTheRoslynBuilderWhenOff()
    {
        Assert.That(PatchClassBuilder.Instance, Is.SameAs(RoslynPatchClassBuilder.Instance));
    }

    [Test]
    public void Instance_IsTheEmitBuilderWhenOn()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;

        Assert.That(PatchClassBuilder.Instance, Is.SameAs(EmitPatchClassBuilder.Instance));
    }

    [Test]
    public void Instance_FollowsTheFlagWhenItIsTurnedBackOff()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;
        _ = PatchClassBuilder.Instance;
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = false;

        Assert.That(PatchClassBuilder.Instance, Is.SameAs(RoslynPatchClassBuilder.Instance),
            "The flag is settable, so Instance must read it on every access rather than once.");
    }

    [Test]
    public void GetPatchClassFor_UsesTheEmitBuilderWhenOn()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;

        Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject)),
            Is.SameAs(EmitPatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject))));
    }

    [Test]
    public void GetPatchClassFor_UsesTheRoslynBuilderWhenTurnedBackOff()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;
        PatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject));
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = false;

        Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject)),
            Is.SameAs(RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(PlainClassTestObject))),
            "Each builder keeps its own cache, so the Emit builder's type must not leak into the Roslyn path.");
    }

    [Test]
    public void GetPatchClassFor_InternalTypeWhenOff_ThrowsNamingTheFlag()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalTestObject)));

        Assert.That(ex!.Message, Does.Contain(nameof(PatchClassBuilder.UseExperimentalDynamicClassBuilder)));
    }

    [Test]
    public void Deserialize_InternalTypeWhenOn_PatchesAndValidates()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;
        // New options, so System.Text.Json hasn't cached a converter for the type from another test.
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters();

        var patch = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>(
            """{ "display_name": null, "initOnly": "New" }""", options)!;
        var invalid = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>(
            """{ "rating": 20 }""", options)!;
        var result = patch.Patch(new InternalClassTestObject { Name = "Old", InitOnly = "Old", Rating = 3 });
        var validationResults = new List<ValidationResult>();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.Null, "An explicit null should be applied.");
            Assert.That(result.InitOnly, Is.EqualTo("New"));
            Assert.That(result.Rating, Is.EqualTo(3), "A property the patch leaves out should keep its value.");
            Assert.That(Validator.TryValidateObject(patch, new ValidationContext(patch), validationResults, true),
                Is.True);
            Assert.That(Validator.TryValidateObject(invalid, new ValidationContext(invalid), validationResults, true),
                Is.False, "The source property's [Range] should run on the patch.");
        }));
    }
}
