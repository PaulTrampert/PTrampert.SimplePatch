namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> for a given type.
/// </summary>
public interface IPatchClassBuilder
{
    /// <summary>
    /// Gets or creates a class that implements <see cref="IPatchObject{T}"/> for the specified type.
    /// The class has an <see cref="Optional{T}"/> property for each patchable property of the type,
    /// and a <c>Patch</c> method that applies the properties that are set to a target instance.
    /// </summary>
    /// <param name="type">The type to get a patch type for.</param>
    /// <returns>The generated patch type.</returns>
    /// <exception cref="NotSupportedException">The builder can't generate a patch class for <paramref name="type"/>.</exception>
    Type GetPatchClassFor(Type type);
}
