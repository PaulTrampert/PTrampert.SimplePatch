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
    public void Patch_PassesConstructorParameters_FromThePatchOrTheTarget()
    {
        var optionsWithOptionals = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        optionsWithOptionals.Converters.Add(new OptionalJsonConverterFactory());
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(ConstructorTestObject));
        var target = new ConstructorTestObject("Old Name", "Old Secret") { Color = "Red" };

        var renamed = (IPatchObject<ConstructorTestObject>)JsonSerializer.Deserialize(
            """{ "name": "New Name" }""", patchType, optionsWithOptionals)!;
        var recolored = (IPatchObject<ConstructorTestObject>)JsonSerializer.Deserialize(
            """{ "color": "Blue" }""", patchType, optionsWithOptionals)!;
        var renamedResult = renamed.Patch(target);
        var recoloredResult = recolored.Patch(target);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchType.GetProperty(nameof(ConstructorTestObject.Secret)), Is.Null,
                "Ignored constructor-bound properties should not be included in the generated patch class");
            Assert.That(renamedResult.Name, Is.EqualTo("New Name"));
            Assert.That(renamedResult.Color, Is.EqualTo("Red"));
            Assert.That(renamedResult.Secret, Is.EqualTo("Old Secret"));
            Assert.That(recoloredResult.Name, Is.EqualTo("Old Name"));
            Assert.That(recoloredResult.Color, Is.EqualTo("Blue"));
            Assert.That(recoloredResult.Secret, Is.EqualTo("Old Secret"));
        }));
    }

    [Test]
    public void Patch_PrefersTheJsonConstructor_OverTheParameterlessOne()
    {
        var optionsWithOptionals = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        optionsWithOptionals.Converters.Add(new OptionalJsonConverterFactory());
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(JsonConstructorTestObject));
        var target = new JsonConstructorTestObject(1) { Name = "Old Name" };

        var patch = (IPatchObject<JsonConstructorTestObject>)JsonSerializer.Deserialize(
            """{ "id": 2 }""", patchType, optionsWithOptionals)!;
        var result = patch.Patch(target);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Id, Is.EqualTo(2));
            Assert.That(result.Name, Is.EqualTo("Old Name"));
            Assert.That(result.Origin, Is.EqualTo("annotated"));
        }));
    }

    [Test]
    public void GetPatchClassFor_RejectsAmbiguousConstructors()
    {
        Assert.Throws<NotSupportedException>(
            (Action)(() => PatchClassBuilder.Instance.GetPatchClassFor(typeof(AmbiguousConstructorTestObject))));
    }
}
