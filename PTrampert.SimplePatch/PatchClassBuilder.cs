using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Provides the library's builder of classes that implement <see cref="IPatchObject{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Instance"/> is the library's default builder, and the one
/// <see cref="PatchJsonConverterFactory"/> and the OpenAPI integrations use. The builders themselves
/// are internal, so that the implementation can change without changing the public API.
/// </para>
/// <para>
/// For a given type, the generated class has an <see cref="Optional{T}"/> property for each writable
/// or constructor-bound property of the type. Properties marked with
/// <see cref="JsonIgnoreAttribute"/> whose condition is <see cref="JsonIgnoreCondition.Always"/> are
/// left out. Its <c>Patch</c> method takes an instance of the type and returns a new instance with the
/// properties that are set applied, built with the constructor System.Text.Json would use to
/// deserialize the type, and with the <c>target</c>'s values for the properties that aren't set. The
/// generated class is sealed and public, and is placed in a namespace that matches the original
/// type's namespace, with an additional ".Optionals" suffix.
/// </para>
/// </remarks>
public static class PatchClassBuilder
{
    // Returns the builder itself, rather than a PatchClassBuilder that forwards each call, so callers
    // dispatch straight to it.
    /// <summary>
    /// The builder, which emits each patch class with Reflection.Emit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The patch class is emitted into a dynamic assembly named <c>PTrampert.SimplePatch.Emitted</c>.
    /// An internal source type is supported when its assembly grants that name access with
    /// <c>[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]</c>. The same applies to any
    /// internal property types and accessors the patch class uses.
    /// </para>
    /// <para>
    /// Its <see cref="IPatchClassBuilder.GetPatchClassFor"/> throws
    /// <see cref="NotSupportedException"/> when the generated assembly can't access the type, or a
    /// type or accessor its patch class uses. That is the case for a private or protected nested type,
    /// and for an internal one whose assembly doesn't grant access. It also throws when no constructor
    /// can be chosen for the type, or a constructor parameter doesn't match a public property.
    /// </para>
    /// </remarks>
    public static IPatchClassBuilder Instance => EmitPatchClassBuilder.Instance;
}
