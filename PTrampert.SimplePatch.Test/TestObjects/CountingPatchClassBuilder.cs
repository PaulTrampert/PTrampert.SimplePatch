namespace PTrampert.SimplePatch.Test.TestObjects;

/// <summary>
/// A fake inner builder for <see cref="CachingPatchClassBuilder"/>: it counts the calls for each
/// source type, and either returns a stand-in patch type or throws.
/// </summary>
public sealed class CountingPatchClassBuilder : IPatchClassBuilder
{
    private readonly Dictionary<Type, int> _calls = new();

    /// <summary>When set, every call throws this rather than returning a type.</summary>
    public Exception? Throws { get; init; }

    /// <summary>
    /// How long each call takes, so that concurrent first callers overlap inside the build.
    /// </summary>
    public TimeSpan Delay { get; init; }

    public int CallsFor(Type type)
    {
        lock (_calls)
        {
            return _calls.GetValueOrDefault(type);
        }
    }

    public Type GetPatchClassFor(Type type)
    {
        lock (_calls)
        {
            _calls[type] = _calls.GetValueOrDefault(type) + 1;
        }

        Thread.Sleep(Delay);
        if (Throws is { } exception)
        {
            throw exception;
        }

        return typeof(List<>).MakeGenericType(type);
    }
}
