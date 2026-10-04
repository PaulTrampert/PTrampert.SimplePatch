using PTrampert.SimplePatch.Benchmarks.TestObjects;

namespace PTrampert.SimplePatch.Benchmarks;

/// <summary>
/// The source type shapes the benchmarks generate patch classes for. An enum, rather than a
/// <see cref="Type"/> parameter, so the results table shows a short name.
/// </summary>
public enum SourceType
{
    /// <summary><see cref="TestObjects.Small"/>: a class with three properties.</summary>
    Small,

    /// <summary><see cref="TestObjects.Medium"/>: a positional record with ten properties and carried-over attributes.</summary>
    Medium,

    /// <summary><see cref="TestObjects.Large"/>: a class with forty properties.</summary>
    Large,
}

internal static class SourceTypeExtensions
{
    public static Type ToType(this SourceType sourceType) => sourceType switch
    {
        SourceType.Small => typeof(Small),
        SourceType.Medium => typeof(Medium),
        SourceType.Large => typeof(Large),
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null),
    };
}
