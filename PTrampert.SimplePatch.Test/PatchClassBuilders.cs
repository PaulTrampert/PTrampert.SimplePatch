namespace PTrampert.SimplePatch.Test;

/// <summary>
/// The ways of building a patch class, as fixture arguments for tests that run against each one.
/// </summary>
public static class PatchClassBuilders
{
    public static IEnumerable<TestFixtureData> All()
    {
        yield return new TestFixtureData(PatchClassBuilder.Instance)
            .SetArgDisplayNames("Roslyn");
        yield return new TestFixtureData(EmitPatchClassBuilder.Instance)
            .SetArgDisplayNames("Emit");
    }
}
