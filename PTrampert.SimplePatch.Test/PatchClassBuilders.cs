namespace PTrampert.SimplePatch.Test;

/// <summary>
/// The ways of building a patch class, as fixture arguments for tests that run against each one.
/// </summary>
/// <remarks>
/// Each builder is wrapped in a <see cref="CachingPatchClassBuilder"/>, as it is at runtime, so the
/// shared suite builds each source type once per builder instead of on every call. The suite still
/// tests the inner builder's output, since the cache only hands back what that builder built.
/// The wrappers are static so that every enumeration of <see cref="All"/> shares one cache each.
/// </remarks>
public static class PatchClassBuilders
{
    private static readonly IPatchClassBuilder Roslyn = new CachingPatchClassBuilder(RoslynPatchClassBuilder.Instance);
    private static readonly IPatchClassBuilder Emit = new CachingPatchClassBuilder(EmitPatchClassBuilder.Instance);

    public static IEnumerable<TestFixtureData> All()
    {
        yield return new TestFixtureData(Roslyn)
            .SetArgDisplayNames("Roslyn");
        yield return new TestFixtureData(Emit)
            .SetArgDisplayNames("Emit");
    }
}
