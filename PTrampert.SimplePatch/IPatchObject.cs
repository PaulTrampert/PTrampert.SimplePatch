namespace PTrampert.SimplePatch;

/// <summary>
/// Defines a contract for patching an object of type T.
/// Instances of this interface can be obtained from the <see cref="PatchClassBuilder"/>.
/// </summary>
/// <typeparam name="T">The target object type. This should usually be a POCO or record type.</typeparam>
public interface IPatchObject<T>
{
    /// <summary>
    /// Applies the patch to the target object. The returned object is a new instance of type T with the applied changes.
    /// The target object is not modified.
    /// </summary>
    /// <param name="target">The target object to patch.</param>
    /// <returns>The patched object.</returns>
    T Patch(T target);
}