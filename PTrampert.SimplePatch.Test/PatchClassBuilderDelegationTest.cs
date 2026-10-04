using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// Cases for the public entry point: PatchClassBuilder hands out the Emit builder's types, so what
// the Emit builder supports, consumers get. The shape of the patch class is covered against each
// builder in PatchClassBuilderTest.
public class PatchClassBuilderDelegationTest
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters();
        return options;
    }

    [Test]
    public void GetPatchClassFor_HandsOutTheEmitBuildersTypes()
    {
        // Deliberately the obsolete constructor: separately constructed builders must still share
        // one cache, for as long as that constructor exists.
#pragma warning disable CS0618
        var fromNewInstance = new PatchClassBuilder().GetPatchClassFor(typeof(OptionalsBuilderTestObject));
#pragma warning restore CS0618

        Assert.Multiple((Action)(() =>
        {
            Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject)),
                Is.SameAs(EmitPatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject))));
            Assert.That(fromNewInstance, Is.SameAs(PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject))),
                "Every builder should resolve a source type to one generated patch type.");
        }));
    }

    [Test]
    public void Deserialize_InternalType_PatchesAndValidates()
    {
        // Through IPatchObject<T>, as a controller binds it. This assembly grants the generated
        // assemblies access with [InternalsVisibleTo] in its project file.
        var patch = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>(
            """{ "display_name": null, "initOnly": "New" }""", Options)!;
        var invalid = JsonSerializer.Deserialize<IPatchObject<InternalClassTestObject>>(
            """{ "rating": 20 }""", Options)!;
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

    [Test]
    public void GetPatchClassFor_PrivateNestedType_ThrowsNotSupported()
    {
        Assert.That(() => PatchClassBuilder.Instance.GetPatchClassFor(typeof(PrivateNestedTestObject)),
            Throws.TypeOf<NotSupportedException>()
                .With.Message.Contains(typeof(PrivateNestedTestObject).FullName));
    }

    private class PrivateNestedTestObject
    {
        public string? Name { get; set; }
    }
}
