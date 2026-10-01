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

    private static IPatchObject<T> DeserializePatch<T>(string json, Type patchType)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new OptionalJsonConverterFactory());
        return (IPatchObject<T>)JsonSerializer.Deserialize(json, patchType, options)!;
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
