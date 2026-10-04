using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> for a given type.
/// </summary>
/// <remarks>
/// This is the library's default builder, and the one <see cref="PatchJsonConverterFactory"/> uses.
/// It delegates to the builder that generates the classes, so that the implementation can change
/// without changing this public type. <see cref="UseExperimentalDynamicClassBuilder"/> selects
/// which builder that is.
/// </remarks>
public class PatchClassBuilder : IPatchClassBuilder
{
    /// <summary>
    /// The builder. Use this rather than constructing your own: all instances share one cache, so
    /// a new instance buys nothing but an allocation.
    /// </summary>
#pragma warning disable CS0618 // The obsolete constructor is how the singleton itself is built.
    public static PatchClassBuilder Instance { get; } = new();
#pragma warning restore CS0618

    /// <summary>
    /// <b>Experimental.</b> When <see langword="true"/>, <see cref="GetPatchClassFor"/> generates patch
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

    /// <summary>
    /// Creates a builder.
    /// </summary>
    [Obsolete("Use PatchClassBuilder.Instance instead. Every builder shares one cache, so a new "
              + "instance buys nothing but an allocation. This constructor will be made internal "
              + "in the next major version: "
              + "https://github.com/PaulTrampert/PTrampert.SimplePatch/issues/75")]
    public PatchClassBuilder()
    {
    }

    /// <summary>
    /// Gets or creates a class that implements <see cref="IPatchObject{T}"/> for the specified type.
    /// This class will have properties for each writable or constructor-bound property of the type, wrapped in <see cref="Optional{T}"/>.
    /// Properties that are marked with <see cref="JsonIgnoreAttribute"/> whose condition is
    /// <see cref="JsonIgnoreCondition.Always"/> will not be included in the generated class.
    /// The generated class will have a method <c>Patch</c> that takes an instance of the type and returns a new instance with
    /// the optional properties applied, built with the constructor System.Text.Json would use to deserialize the type.
    /// The method will use the <c>target</c>
    /// parameter to access the original values of the properties that are not set in the optional properties class.
    /// The generated class will be sealed and public, and will be placed in a namespace that matches
    /// the original type's namespace, with an additional ".Optionals" suffix.
    /// </summary>
    /// <param name="type">The type to get a patch type for.</param>
    /// <returns>The generated patch type.</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="type"/> is not public, or is nested in or constructed from a type that is not
    /// public, and <see cref="UseExperimentalDynamicClassBuilder"/> is off. With it on, a type or
    /// accessor the generated assembly can't access, such as a private nested type, or an internal
    /// type whose assembly doesn't grant <c>[InternalsVisibleTo]</c>.
    /// </exception>
    public Type GetPatchClassFor(Type type) => UseExperimentalDynamicClassBuilder
        ? EmitPatchClassBuilder.Instance.GetPatchClassFor(type)
        : RoslynPatchClassBuilder.Instance.GetPatchClassFor(type);
}
