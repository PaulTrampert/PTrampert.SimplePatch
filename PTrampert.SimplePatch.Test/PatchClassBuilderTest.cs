using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

public class PatchClassBuilderTest
{
    [Test]
    public void GetPatchClassFor_CopiesThePropertiesAsOptionals()
    {
        var optionalsType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject));
        
        Assert.Multiple((Action)(() =>
        {
            Assert.That(optionalsType.GetProperty(nameof(OptionalsBuilderTestObject.Id)), Is.Not.Null);
            Assert.That(optionalsType.GetProperty(nameof(OptionalsBuilderTestObject.Name)), Is.Not.Null);
            Assert.That(optionalsType.GetProperty(nameof(OptionalsBuilderTestObject.IgnoredProp)), Is.Null, 
                "Ignored properties should not be included in the generated optionals class");
        }));
    }
    
    [Test]
    public void DynamicOptionalsClass_CanBeCreatedAndUsed()
    {
        var json = """
        {
            "id": 1,
            "name_field": "Test Name",
            "ignoredProp": "This should not be included",
            "fakeStringProp": "Fake Value"
        }
        """;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new OptionalJsonConverterFactory());
        var optionalsType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject));
        
        var instance = JsonSerializer.Deserialize(json, optionalsType, options) as IPatchObject<OptionalsBuilderTestObject>;

        var objectToPatch = new OptionalsBuilderTestObject
        {
            Id = 2,
            Name = "Old Name",
            IgnoredProp = "This should not be changed"
        };
        
        Assert.That(instance.Patch(objectToPatch), Is.EqualTo(new OptionalsBuilderTestObject
        {
            Id = 1,
            Name = "Test Name",
            IgnoredProp = "This should not be changed", // Ignored properties should not be set
            FakeStringProp = "FakeString:Fake Value"
        }));
    }

    [Test]
    public void GetPatchClassFor_SharesGeneratedTypesAcrossBuilders()
    {
        // Deliberately the obsolete constructor: the point of this test is that separately
        // constructed builders still share one cache, for as long as that constructor exists.
#pragma warning disable CS0618
        var first = new PatchClassBuilder().GetPatchClassFor(typeof(OptionalsBuilderTestObject));
        var second = new PatchClassBuilder().GetPatchClassFor(typeof(OptionalsBuilderTestObject));
#pragma warning restore CS0618

        Assert.Multiple((Action)(() =>
        {
            Assert.That(second, Is.SameAs(first),
                "Every builder should resolve a source type to one generated patch type, rather than each emitting its own dynamic assembly for it.");
            Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject)), Is.SameAs(first));
        }));
    }

    [Test]
    public void GetPatchClassFor_SupportsSourceTypesInTheGlobalNamespace()
    {
        var globalNamespaceType = typeof(GlobalNamespaceTestObject);
        Assert.That(globalNamespaceType.Namespace, Is.Null, "Guard: this test object must stay in the global namespace.");

        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(globalNamespaceType);

        Assert.That(patchType.GetProperty(nameof(GlobalNamespaceTestObject.Name)), Is.Not.Null);
    }

    [Test]
    public void GetPatchClassFor_LeavesOutPropertiesWithoutAPublicSetter()
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(NonPublicSetterTestObject));

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchType.GetProperty(nameof(NonPublicSetterTestObject.Name)), Is.Not.Null);
            Assert.That(patchType.GetProperty(nameof(NonPublicSetterTestObject.InitOnly)), Is.Not.Null,
                "Public init accessors should be patchable");
            Assert.That(patchType.GetProperty(nameof(NonPublicSetterTestObject.PrivateSet)), Is.Null);
            Assert.That(patchType.GetProperty(nameof(NonPublicSetterTestObject.ProtectedSet)), Is.Null);
            Assert.That(patchType.GetProperty(nameof(NonPublicSetterTestObject.InternalSet)), Is.Null);
            Assert.That(patchType.GetProperty(nameof(NonPublicSetterTestObject.IgnoredPrivateSet)), Is.Null);
        }));
    }

    [Test]
    public void DynamicOptionalsClass_PatchesTypesWithNonPublicSetters()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new OptionalJsonConverterFactory());
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(NonPublicSetterTestObject));

        var patch = (IPatchObject<NonPublicSetterTestObject>)JsonSerializer.Deserialize(
            """{ "name": "New Name" }""", patchType, options)!;
        var patched = patch.Patch(new NonPublicSetterTestObject { Name = "Old Name", InitOnly = "Init Value" });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patched.Name, Is.EqualTo("New Name"));
            Assert.That(patched.InitOnly, Is.EqualTo("Init Value"));
        }));
    }
}
