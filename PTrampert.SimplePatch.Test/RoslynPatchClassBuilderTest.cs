using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

// Cases specific to the Roslyn builder, which isn't used at runtime but is kept pending #144: that it
// leaves caching to CachingPatchClassBuilder, and the public-only restriction that comes from
// compiling C#. Cases it shares with the Emit builder are in PatchClassBuilderTest.
public class RoslynPatchClassBuilderTest
{
    // Caching is CachingPatchClassBuilder's job, so the builder itself compiles a new class each time.
    [Test]
    public void GetPatchClassFor_BuildsANewClassOnEachCall()
    {
        var first = RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject));

        Assert.That(RoslynPatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject)), Is.Not.SameAs(first));
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
