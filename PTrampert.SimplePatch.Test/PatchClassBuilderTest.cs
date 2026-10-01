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

    [TestCase(nameof(JsonIgnoreConditionsTestObject.Always), false)]
    [TestCase(nameof(JsonIgnoreConditionsTestObject.Never), true)]
    [TestCase(nameof(JsonIgnoreConditionsTestObject.WhenWritingNull), true)]
    [TestCase(nameof(JsonIgnoreConditionsTestObject.WhenWritingDefault), true)]
    public void GetPatchClassFor_ExcludesOnlyPropertiesIgnoredOnRead(string propertyName, bool patchable)
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(JsonIgnoreConditionsTestObject));

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

        var patch = JsonSerializer.Deserialize<IPatchObject<JsonIgnoreConditionsTestObject>>(json, options)!;
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
    public void GetPatchClassFor_GeneratesOnceUnderConcurrentFirstUse()
    {
        const int threadCount = 16;
        var sourceType = typeof(ConcurrentFirstUseTestObject);
        var results = new Type[threadCount];
        using var barrier = new Barrier(threadCount);
        var threads = Enumerable.Range(0, threadCount)
            .Select(i => new Thread(() =>
            {
                barrier.SignalAndWait();
                results[i] = PatchClassBuilder.Instance.GetPatchClassFor(sourceType);
            }))
            .ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        // Every generation loads its own in-memory assembly, so count the loaded types that
        // patch the source type: a discarded duplicate would still show up here.
        var patchInterface = typeof(IPatchObject<>).MakeGenericType(sourceType);
        var generatedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && string.IsNullOrEmpty(a.Location))
            .SelectMany(a => a.GetTypes())
            .Where(patchInterface.IsAssignableFrom)
            .ToList();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(results, Has.All.SameAs(results[0]));
            Assert.That(generatedTypes, Is.EquivalentTo(new[] { results[0] }),
                "Concurrent first use should generate the patch class once, not once per racing thread.");
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
    public void GetPatchClassFor_UsesTheMostDerivedDeclarationOfAHiddenProperty()
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(HiddenPropertyTestObject));

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

        var patch = JsonSerializer.Deserialize<IPatchObject<HiddenPropertyTestObject>>("""{ "value": "new" }""", options);
        var patched = patch!.Patch(new HiddenPropertyTestObject { Value = "old" });

        // FakeStringConverter proves the converter lookup resolved the hiding property too.
        Assert.That(patched.Value, Is.EqualTo("FakeString:new"));
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

    private static IPatchObject<T> Deserialize<T>(string json)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new OptionalJsonConverterFactory());
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(T));
        return (IPatchObject<T>)JsonSerializer.Deserialize(json, patchType, options)!;
    }

    [Test]
    public void GetPatchClassFor_LeavesOutStaticProperties()
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(StaticPropertyTestObject));
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
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(IndexerTestObject));
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
    public void GetPatchClassFor_ThrowsNotSupportedForInternalTypes()
    {
        var ex = Assert.Throws<NotSupportedException>(
            (Action)(() => PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalTestObject))));

        Assert.That(ex!.Message, Does.Contain(typeof(InternalTestObject).FullName).And.Contain("must be public"));
    }

    [Test]
    public void GetPatchClassFor_ThrowsNotSupportedForPrivateNestedTypes()
    {
        var ex = Assert.Throws<NotSupportedException>(
            (Action)(() => PatchClassBuilder.Instance.GetPatchClassFor(typeof(PrivateNestedTestObject))));

        Assert.That(ex!.Message, Does.Contain(typeof(PrivateNestedTestObject).FullName).And.Contain("must be public"));
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
    public void Patch_BindsGetOnlyConstructorProperties_ButLeavesOutPrivateSetters()
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(ConstructorAndPrivateSetterTestObject));
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
            (Action)(() => PatchClassBuilder.Instance.GetPatchClassFor(typeof(AmbiguousConstructorTestObject))));
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
    public void Patch_RecordWithConstructorSetGetOnlyProperty_KeepsItFromTheTarget()
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(ConstructorRecordTestObject));
        var patch = JsonSerializer.Deserialize<IPatchObject<ConstructorRecordTestObject>>(
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

    private class PrivateNestedTestObject
    {
        public string? Name { get; set; }
    }
}

internal class InternalTestObject
{
    public string? Name { get; set; }
}
