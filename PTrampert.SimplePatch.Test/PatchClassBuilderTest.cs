using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// Runs every case against each way of building a patch class, so the two builders can't drift apart.
// Cases that only one builder supports, or that test its caching, are in that builder's own fixture.
[TestFixtureSource(typeof(PatchClassBuilders), nameof(PatchClassBuilders.All))]
public class PatchClassBuilderTest(Func<Type, Type> getPatchClassFor)
{
    private Type GetPatchClassFor(Type type) => getPatchClassFor(type);

    // Deserializes straight into this fixture's patch class. Deserializing IPatchObject<T> would
    // go through PatchJsonConverterFactory, which always uses PatchClassBuilder.Instance.
    private IPatchObject<T> DeserializePatchObject<T>(string json, JsonSerializerOptions options) =>
        (IPatchObject<T>)JsonSerializer.Deserialize(json, GetPatchClassFor(typeof(T)), options)!;

    [Test]
    public void GetPatchClassFor_CopiesThePropertiesAsOptionals()
    {
        var optionalsType = GetPatchClassFor(typeof(OptionalsBuilderTestObject));
        
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
        var optionalsType = GetPatchClassFor(typeof(OptionalsBuilderTestObject));
        
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

    [TestCase(nameof(JsonIgnoreConditionsTestObject.Always), false)]
    [TestCase(nameof(JsonIgnoreConditionsTestObject.Never), true)]
    [TestCase(nameof(JsonIgnoreConditionsTestObject.WhenWritingNull), true)]
    [TestCase(nameof(JsonIgnoreConditionsTestObject.WhenWritingDefault), true)]
    public void GetPatchClassFor_ExcludesOnlyPropertiesIgnoredOnRead(string propertyName, bool patchable)
    {
        var patchType = GetPatchClassFor(typeof(JsonIgnoreConditionsTestObject));

        Assert.That(patchType.GetProperty(propertyName), patchable ? Is.Not.Null : Is.Null,
            "Only [JsonIgnore(Condition = Always)] stops System.Text.Json from deserializing a property.");
    }

    [Test]
    public void Patch_AppliesPropertiesWithConditionalJsonIgnore()
    {
        var json = """
        {
            "always": "new",
            "never": "new",
            "whenWritingNull": "new",
            "whenWritingDefault": 2
        }
        """;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters();

        var patch = DeserializePatchObject<JsonIgnoreConditionsTestObject>(json, options)!;
        var patched = patch.Patch(new JsonIgnoreConditionsTestObject
        {
            Always = "old",
            Never = "old",
            WhenWritingNull = "old",
            WhenWritingDefault = 1,
        });

        Assert.That(patched, Is.EqualTo(new JsonIgnoreConditionsTestObject
        {
            Always = "old",
            Never = "new",
            WhenWritingNull = "new",
            WhenWritingDefault = 2,
        }));
    }

    [Test]
    public void GetPatchClassFor_SupportsSourceTypesInTheGlobalNamespace()
    {
        var globalNamespaceType = typeof(GlobalNamespaceTestObject);
        Assert.That(globalNamespaceType.Namespace, Is.Null, "Guard: this test object must stay in the global namespace.");

        var patchType = GetPatchClassFor(globalNamespaceType);

        Assert.That(patchType.GetProperty(nameof(GlobalNamespaceTestObject.Name)), Is.Not.Null);
    }

    [Test]
    public void GetPatchClassFor_UsesTheMostDerivedDeclarationOfAHiddenProperty()
    {
        var patchType = GetPatchClassFor(typeof(HiddenPropertyTestObject));

        var valueProperties = patchType.GetProperties()
            .Where(p => p.Name == nameof(HiddenPropertyTestObject.Value))
            .ToList();

        Assert.That(valueProperties.Select(p => p.PropertyType), Is.EqualTo(new[] { typeof(Optional<string?>) }));
    }

    [Test]
    public void Patch_SetsTheMostDerivedDeclarationOfAHiddenProperty()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.AddSimplePatchConverters();

        var patch = DeserializePatchObject<HiddenPropertyTestObject>("""{ "value": "new" }""", options);
        var patched = patch!.Patch(new HiddenPropertyTestObject { Value = "old" });

        // FakeStringConverter proves the converter lookup resolved the hiding property too.
        Assert.That(patched.Value, Is.EqualTo("FakeString:new"));
    }

    [Test]
    public void GetPatchClassFor_LeavesOutPropertiesWithoutAPublicSetter()
    {
        var patchType = GetPatchClassFor(typeof(NonPublicSetterTestObject));

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
        var patchType = GetPatchClassFor(typeof(NonPublicSetterTestObject));

        var patch = (IPatchObject<NonPublicSetterTestObject>)JsonSerializer.Deserialize(
            """{ "name": "New Name" }""", patchType, options)!;
        var patched = patch.Patch(new NonPublicSetterTestObject { Name = "Old Name", InitOnly = "Init Value" });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patched.Name, Is.EqualTo("New Name"));
            Assert.That(patched.InitOnly, Is.EqualTo("Init Value"));
        }));
    }

    [Test]
    public void Patch_SupportsNestedSourceTypes()
    {
        var patch = Deserialize<OuterTestObject.Inner>("""{ "name": "New" }""");

        Assert.That(patch.Patch(new OuterTestObject.Inner { Name = "Old", Other = "Kept" }),
            Is.EqualTo(new OuterTestObject.Inner { Name = "New", Other = "Kept" }));
    }

    [Test]
    public void Patch_SupportsGenericSourceTypes()
    {
        var patch = Deserialize<GenericTestObject<int>>("""{ "value": 2 }""");

        Assert.That(patch.Patch(new GenericTestObject<int> { Value = 1, Other = "Kept" }),
            Is.EqualTo(new GenericTestObject<int> { Value = 2, Other = "Kept" }));
    }

    [Test]
    public void Patch_SupportsPropertiesNamedAfterKeywords()
    {
        var patch = Deserialize<KeywordPropertiesTestObject>("""{ "class": "New" }""");

        Assert.That(patch.Patch(new KeywordPropertiesTestObject { @class = "Old", @event = "Ignored", Other = "Kept" }),
            Is.EqualTo(new KeywordPropertiesTestObject { @class = "New", @event = "Ignored", Other = "Kept" }));
    }

    private IPatchObject<T> Deserialize<T>(string json)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new OptionalJsonConverterFactory());
        var patchType = GetPatchClassFor(typeof(T));
        return (IPatchObject<T>)JsonSerializer.Deserialize(json, patchType, options)!;
    }

    [Test]
    public void GetPatchClassFor_LeavesOutStaticProperties()
    {
        var patchType = GetPatchClassFor(typeof(StaticPropertyTestObject));
        var patch = DeserializePatch<StaticPropertyTestObject>("""{ "Name": "New" }""", patchType);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchType.GetProperty(nameof(StaticPropertyTestObject.Shared)), Is.Null,
                "Static properties aren't part of the JSON contract, so they should not be patchable.");
            Assert.That(patch.Patch(new StaticPropertyTestObject { Name = "Old" }),
                Is.EqualTo(new StaticPropertyTestObject { Name = "New" }));
        }));
    }

    [Test]
    public void GetPatchClassFor_LeavesOutIndexers()
    {
        var patchType = GetPatchClassFor(typeof(IndexerTestObject));
        var patch = DeserializePatch<IndexerTestObject>("""{ "Name": "New" }""", patchType);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchType.GetProperty("Item"), Is.Null,
                "Indexers aren't part of the JSON contract, so they should not be patchable.");
            Assert.That(patch.Patch(new IndexerTestObject { Name = "Old" }),
                Is.EqualTo(new IndexerTestObject { Name = "New" }));
        }));
    }

    [Test]
    public void Patch_PassesConstructorParameters_FromThePatchOrTheTarget()
    {
        var optionsWithOptionals = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        optionsWithOptionals.Converters.Add(new OptionalJsonConverterFactory());
        var patchType = GetPatchClassFor(typeof(ConstructorTestObject));
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
        var patchType = GetPatchClassFor(typeof(JsonConstructorTestObject));
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
    public void Patch_BindsGetOnlyConstructorProperties_ButLeavesOutPrivateSetters()
    {
        var patchType = GetPatchClassFor(typeof(ConstructorAndPrivateSetterTestObject));
        var patch = DeserializePatch<ConstructorAndPrivateSetterTestObject>("""{ "Name": "New" }""", patchType);
        var result = patch.Patch(new ConstructorAndPrivateSetterTestObject("Old") { Color = "Red" });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchType.GetProperty(nameof(ConstructorAndPrivateSetterTestObject.Name)), Is.Not.Null);
            Assert.That(patchType.GetProperty(nameof(ConstructorAndPrivateSetterTestObject.Version)), Is.Null,
                "A private setter that isn't bound to the constructor should not be patchable.");
            Assert.That(result.Name, Is.EqualTo("New"));
            Assert.That(result.Color, Is.EqualTo("Red"));
        }));
    }

    [Test]
    public void GetPatchClassFor_RejectsAmbiguousConstructors()
    {
        Assert.Throws<NotSupportedException>(
            (Action)(() => GetPatchClassFor(typeof(AmbiguousConstructorTestObject))));
    }

    private static IPatchObject<T> DeserializePatch<T>(string json, Type patchType)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new OptionalJsonConverterFactory());
        return (IPatchObject<T>)JsonSerializer.Deserialize(json, patchType, options)!;
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
        var patch = DeserializePatchObject<PositionalRecordTestObject>(
            """{ "count": 5 }""", PatchOptions)!;

        var result = patch.Patch(new PositionalRecordTestObject("Old Name", 1));

        Assert.That(result, Is.EqualTo(new PositionalRecordTestObject("Old Name", 5)));
    }

    [Test]
    public void Patch_Record_KeepsGetOnlyProperties()
    {
        var patch = DeserializePatchObject<PositionalRecordTestObject>(
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
        var patch = DeserializePatchObject<PositionalRecordTestObject>(
            """{ "count": 5 }""", PatchOptions)!;

        var result = patch.Patch(new DerivedPositionalRecordTestObject("Name", 1, "Extra"));

        Assert.That(result, Is.EqualTo(new DerivedPositionalRecordTestObject("Name", 5, "Extra")));
    }

    [Test]
    public void Patch_RecordWithConstructorSetGetOnlyProperty_KeepsItFromTheTarget()
    {
        var patchType = GetPatchClassFor(typeof(ConstructorRecordTestObject));
        var patch = DeserializePatchObject<ConstructorRecordTestObject>(
            """{ "name": "New Name" }""", PatchOptions)!;

        var result = patch.Patch(new ConstructorRecordTestObject("Old Name", "C1"));

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchType.GetProperty(nameof(ConstructorRecordTestObject.Code)), Is.Null,
                "Records are patched with `with`, which can't assign a get-only property.");
            Assert.That(result, Is.EqualTo(new ConstructorRecordTestObject("New Name", "C1")));
        }));
    }

    [Test]
    public void Patch_NonRecordClass_StillBuildsANewInstance()
    {
        var patch = DeserializePatchObject<PlainClassTestObject>(
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

    [Test]
    public void Patch_DoesNotReadTheTargetPropertyThePatchSets()
    {
        var patch = DeserializePatchObject<ReadTrackingTestObject>("""{ "name": "New" }""", PatchOptions);
        var target = new ReadTrackingTestObject { Name = "Old", Other = "Kept" };

        var result = patch.Patch(target);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result.Name, Is.EqualTo("New"));
            Assert.That(result.Other, Is.EqualTo("Kept"));
            Assert.That(target.NameReads, Is.Zero,
                "The patch sets Name, so the target's Name should not be read.");
        }));
    }
}
