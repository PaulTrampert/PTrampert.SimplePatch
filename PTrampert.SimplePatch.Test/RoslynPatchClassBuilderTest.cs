using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// Cases specific to the Roslyn builder that PatchClassBuilder.Instance returns by default: its
// cache, and the public-only restriction that comes from compiling C#. Cases it shares with the Emit
// builder are in PatchClassBuilderTest.
public class RoslynPatchClassBuilderTest
{
    [Test]
    public void GetPatchClassFor_CachesTheGeneratedType()
    {
        var first = RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject));
        var second = RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject));

        Assert.Multiple((Action)(() =>
        {
            Assert.That(second, Is.SameAs(first),
                "A source type should resolve to one generated patch type, rather than each call emitting its own dynamic assembly for it.");
            Assert.That(PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject)), Is.SameAs(first));
            Assert.That(RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject)), Is.SameAs(first),
                "PatchClassBuilder.Instance should be the Roslyn builder while the experimental flag is off.");
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
                results[i] = RoslynPatchClassBuilder.Instance.GetPatchClassFor(sourceType);
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
    public void GetPatchClassFor_ThrowsNotSupportedForInternalTypes()
    {
        var ex = Assert.Throws<NotSupportedException>(
            (Action)(() => RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(InternalTestObject))));

        Assert.That(ex!.Message, Does.Contain(typeof(InternalTestObject).FullName).And.Contain("must be public"));
    }

    [Test]
    public void GetPatchClassFor_ThrowsNotSupportedForPrivateNestedTypes()
    {
        var ex = Assert.Throws<NotSupportedException>(
            (Action)(() => RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(PrivateNestedTestObject))));

        Assert.That(ex!.Message, Does.Contain(typeof(PrivateNestedTestObject).FullName).And.Contain("must be public"));
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
