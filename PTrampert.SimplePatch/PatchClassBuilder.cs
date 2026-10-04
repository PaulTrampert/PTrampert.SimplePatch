using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Provides the library's builder of classes that implement <see cref="IPatchObject{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Instance"/> is the library's default builder, and the one
/// <see cref="PatchJsonConverterFactory"/> and the OpenAPI integrations use.
/// <see cref="UseExperimentalDynamicClassBuilder"/> selects which builder that is. The builders
/// themselves are internal, so that the implementation can change without changing the public API.
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
    // Returns the selected builder itself, rather than a PatchClassBuilder that forwards each call,
    // so callers dispatch straight to it. Computed on every read because the flag is settable.
    /// <summary>
    /// The builder that <see cref="UseExperimentalDynamicClassBuilder"/> selects: the
    /// Reflection.Emit builder when it is on, otherwise the Roslyn builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The setting is read each time this property is, so read it where you build a patch class
    /// rather than keeping the builder it returns: a kept builder doesn't follow later changes to the
    /// setting.
    /// </para>
    /// <para>
    /// Its <see cref="IPatchClassBuilder.GetPatchClassFor"/> throws
    /// <see cref="NotSupportedException"/> when the type is not public, or is nested in or constructed
    /// from a type that is not public, and <see cref="UseExperimentalDynamicClassBuilder"/> is off.
    /// With it on, it throws for a type or accessor the generated assembly can't access, such as a
    /// private nested type, or an internal type whose assembly doesn't grant
    /// <c>[InternalsVisibleTo]</c>.
    /// </para>
    /// </remarks>
    public static IPatchClassBuilder Instance => UseExperimentalDynamicClassBuilder
        ? EmitPatchClassBuilder.Instance
        : RoslynPatchClassBuilder.Instance;

    /// <summary>
    /// <b>Experimental.</b> When <see langword="true"/>, <see cref="Instance"/> generates patch
    /// classes with Reflection.Emit instead of compiling C# with Roslyn. The Emit builder also
    /// supports source types that aren't public. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The setting is process-wide, so it applies to <see cref="PatchJsonConverterFactory"/> and the
    /// OpenAPI integration packages alike. Set it once at startup, before any patch class is built.
    /// Each builder keeps its own cache, but System.Text.Json and the OpenAPI document generators
    /// cache the types they have already resolved, so changing the setting later doesn't replace
    /// patch types that are already in use.
    /// </para>
    /// <para>
    /// An <c>internal</c> source type is supported when its assembly declares
    /// <c>[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]</c>, which grants access to
    /// the generated assemblies. The same applies to any internal property types and accessors the
    /// patch class uses. Private and protected nested types aren't supported.
    /// </para>
    /// <para>
    /// The Emit builder is planned to replace the Roslyn builder in the next major version, which will
    /// remove this setting: https://github.com/PaulTrampert/PTrampert.SimplePatch/issues/126
    /// </para>
    /// </remarks>
    public static bool UseExperimentalDynamicClassBuilder { get; set; }
}
