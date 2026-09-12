using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

public class TypeExtensionsTest
{
    [Test]
    public void TryGetPatchSourceType_ResolvesTheOpenInterface()
    {
        var result = typeof(IPatchObject<OptionalsBuilderTestObject>).TryGetPatchSourceType(out var sourceType);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(sourceType, Is.EqualTo(typeof(OptionalsBuilderTestObject)));
        }));
    }

    [Test]
    public void TryGetPatchSourceType_ResolvesAGeneratedPatchClass()
    {
        var patchType = PatchClassBuilder.Instance.GetPatchClassFor(typeof(OptionalsBuilderTestObject));

        var result = patchType.TryGetPatchSourceType(out var sourceType);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(sourceType, Is.EqualTo(typeof(OptionalsBuilderTestObject)));
        }));
    }

    [Test]
    public void TryGetPatchSourceType_IsFalseForUnrelatedTypes()
    {
        var result = typeof(OptionalsBuilderTestObject).TryGetPatchSourceType(out var sourceType);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(sourceType, Is.Null);
        }));
    }
}
