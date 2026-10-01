namespace PTrampert.SimplePatch.Test;

/// <summary>
/// The ways of building a patch class, as fixture arguments for tests that run against each one.
/// </summary>
public static class PatchClassBuilders
{
    public static IEnumerable<TestFixtureData> All()
    {
        yield return new TestFixtureData((Func<Type, Type>)PatchClassBuilder.Instance.GetPatchClassFor)
            .SetArgDisplayNames("Roslyn");
        yield return new TestFixtureData((Func<Type, Type>)EmitPatchClassBuilder.GetPatchClassFor)
            .SetArgDisplayNames("Emit");
    }
}
