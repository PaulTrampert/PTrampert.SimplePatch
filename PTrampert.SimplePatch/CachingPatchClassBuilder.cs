using System.Collections.Concurrent;

namespace PTrampert.SimplePatch;

/// <summary>
/// Caches the patch classes another <see cref="IPatchClassBuilder"/> builds, so that builder only
/// has to build them. It calls the inner builder only the first time it is asked for a type.
/// </summary>
/// <remarks>
/// The cache belongs to this instance rather than being static, so whoever owns the instance
/// decides how long it lives. Once the inner builder throws for a type, the exception is cached
/// too, and later calls for that type rethrow it without calling the inner builder again.
/// </remarks>
/// <param name="inner">The builder that builds a patch class on a cache miss.</param>
internal sealed class CachingPatchClassBuilder(IPatchClassBuilder inner) : IPatchClassBuilder
{
    // Lazy (ExecutionAndPublication) because GetOrAdd may run its factory on several threads at
    // once; Lazy makes them all wait on one build rather than each calling the inner builder.
    private readonly ConcurrentDictionary<Type, Lazy<Type>> _patchClasses = new();

    /// <inheritdoc />
    public Type GetPatchClassFor(Type type)
    {
        return _patchClasses.GetOrAdd(type, t => new Lazy<Type>(() => inner.GetPatchClassFor(t))).Value;
    }
}
