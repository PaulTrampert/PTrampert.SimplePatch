using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// Cases for the public entry point: PatchClassBuilder caches the Emit builder's types, so what the
// Emit builder supports, consumers get, and each source type is emitted once. The shape of the patch class is covered against each
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
    public void Instance_IsACachingBuilderAroundTheEmitBuilder()
    {
        Assert.That(PatchClassBuilder.Instance, Is.TypeOf<CachingPatchClassBuilder>()
            .With.Property(nameof(CachingPatchClassBuilder.Inner)).SameAs(EmitPatchClassBuilder.Instance));
    }

    [Test]
    public void Instance_IsTheSameBuilderEachTime()
    {
        // A new decorator per call would start with an empty cache, and emit a new assembly each time.
        var first = PatchClassBuilder.Instance;

        Assert.That(PatchClassBuilder.Instance, Is.SameAs(first));
    }

    [Test]
    public void GetPatchClassFor_ReturnsTheSameTypeEachTime()
    {
        var first = PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalClassTestObject));

        Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalClassTestObject)), Is.SameAs(first),
            "Every caller should resolve a source type to one generated patch type.");
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

        // Every generation defines its own dynamic assembly, so count the loaded types that patch
        // the source type: a discarded duplicate would still show up here.
        var patchInterface = typeof(IPatchObject<>).MakeGenericType(sourceType);
        var generatedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.IsDynamic && a.GetName().Name == EmitPatchClassBuilder.AssemblyName)
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
