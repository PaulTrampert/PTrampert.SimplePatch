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

    private static readonly JsonSerializerOptions PatchOptions = CreatePatchOptions();

    private static JsonSerializerOptions CreatePatchOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters();
        return options;
    }

    [Test]
    public void Patch_PositionalRecord_ReplacesOnlyTheSentProperties()
    {
        var patch = JsonSerializer.Deserialize<IPatchObject<PositionalRecordTestObject>>(
            """{ "count": 5 }""", PatchOptions)!;

        var result = patch.Patch(new PositionalRecordTestObject("Old Name", 1));

        Assert.That(result, Is.EqualTo(new PositionalRecordTestObject("Old Name", 5)));
    }

    [Test]
    public void Patch_Record_KeepsGetOnlyProperties()
    {
        var patch = JsonSerializer.Deserialize<IPatchObject<PositionalRecordTestObject>>(
            """{ "name": "New Name" }""", PatchOptions)!;

        var result = patch.Patch(new PositionalRecordTestObject("Old Name", 1));

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.EqualTo("New Name"));
            Assert.That(result.Tag, Is.EqualTo("Old Name:tag"),
                "A get-only property has nothing in the patch, so it should keep the target's value.");
        }));
    }

    [Test]
    public void Patch_Record_KeepsTheTargetsDerivedRuntimeType()
    {
        var patch = JsonSerializer.Deserialize<IPatchObject<PositionalRecordTestObject>>(
            """{ "count": 5 }""", PatchOptions)!;

        var result = patch.Patch(new DerivedPositionalRecordTestObject("Name", 1, "Extra"));

        Assert.That(result, Is.EqualTo(new DerivedPositionalRecordTestObject("Name", 5, "Extra")));
    }

    [Test]
    public void Patch_NonRecordClass_StillBuildsANewInstance()
    {
        var patch = JsonSerializer.Deserialize<IPatchObject<PlainClassTestObject>>(
            """{ "name": "New Name" }""", PatchOptions)!;
        var target = new PlainClassTestObject { Name = "Old Name", IgnoredProp = "Kept" };

        var result = patch.Patch(target);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result, Is.Not.SameAs(target));
            Assert.That(result.Name, Is.EqualTo("New Name"));
            Assert.That(result.IgnoredProp, Is.EqualTo("Kept"));
        }));
    }
}
