using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

public class CachingPatchClassBuilderTest
{
    [Test]
    public void GetPatchClassFor_CallsTheInnerBuilderOncePerType()
    {
        var inner = new CountingPatchClassBuilder();
        var builder = new CachingPatchClassBuilder(inner);

        var first = builder.GetPatchClassFor(typeof(TestObject));
        var second = builder.GetPatchClassFor(typeof(TestObject));

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(inner.CallsFor(typeof(TestObject)), Is.EqualTo(1));
        });
    }

    [Test]
    public void GetPatchClassFor_KeepsASeparateEntryForEachType()
    {
        var inner = new CountingPatchClassBuilder();
        var builder = new CachingPatchClassBuilder(inner);

        var forTestObject = builder.GetPatchClassFor(typeof(TestObject));
        var forPlainClass = builder.GetPatchClassFor(typeof(PlainClassTestObject));
        builder.GetPatchClassFor(typeof(TestObject));
        builder.GetPatchClassFor(typeof(PlainClassTestObject));

        Assert.Multiple(() =>
        {
            Assert.That(forPlainClass, Is.Not.SameAs(forTestObject));
            Assert.That(inner.CallsFor(typeof(TestObject)), Is.EqualTo(1));
            Assert.That(inner.CallsFor(typeof(PlainClassTestObject)), Is.EqualTo(1));
        });
    }

    [Test]
    public void GetPatchClassFor_CallsTheInnerBuilderOnceUnderConcurrentFirstUse()
    {
        const int threadCount = 16;
        // The delay keeps the first build running while the other threads arrive, so without
        // Lazy they would each call the inner builder.
        var inner = new CountingPatchClassBuilder { Delay = TimeSpan.FromMilliseconds(50) };
        var builder = new CachingPatchClassBuilder(inner);
        var results = new Type[threadCount];
        using var barrier = new Barrier(threadCount);
        var threads = Enumerable.Range(0, threadCount)
            .Select(i => new Thread(() =>
            {
                barrier.SignalAndWait();
                results[i] = builder.GetPatchClassFor(typeof(TestObject));
            }))
            .ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Multiple(() =>
        {
            Assert.That(inner.CallsFor(typeof(TestObject)), Is.EqualTo(1));
            Assert.That(results, Is.All.SameAs(results[0]));
        });
    }

    [Test]
    public void GetPatchClassFor_CachesAFailure()
    {
        var failure = new NotSupportedException("Can't patch this.");
        var inner = new CountingPatchClassBuilder { Throws = failure };
        var builder = new CachingPatchClassBuilder(inner);

        var first = Assert.Throws<NotSupportedException>(() => builder.GetPatchClassFor(typeof(TestObject)));
        var second = Assert.Throws<NotSupportedException>(() => builder.GetPatchClassFor(typeof(TestObject)));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.SameAs(failure));
            Assert.That(second, Is.SameAs(failure));
            Assert.That(inner.CallsFor(typeof(TestObject)), Is.EqualTo(1));
        });
    }
}
