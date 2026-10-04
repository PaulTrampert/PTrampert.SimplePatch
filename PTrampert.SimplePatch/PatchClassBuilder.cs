using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Generates classes that implement <see cref="IPatchObject{T}"/> for a given type.
/// </summary>
/// <remarks>
/// This is the library's default builder, and the one <see cref="PatchJsonConverterFactory"/> uses.
/// It delegates to the builder that generates the classes, so that the implementation can change
/// without changing this public type.
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
    /// The generated assembly can't access <paramref name="type"/>, or a type or accessor its patch
    /// class uses. That is the case for a private or protected nested type, and for an internal one
    /// whose assembly doesn't declare
    /// <c>[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]</c>. Also thrown when no
    /// constructor can be chosen for <paramref name="type"/>, or a constructor parameter doesn't
    /// match a public property.
    /// </exception>
    /// <remarks>
    /// The patch class is emitted with Reflection.Emit into a dynamic assembly named
    /// <c>PTrampert.SimplePatch.Emitted</c>. An internal source type is supported when its assembly
    /// grants that name access with <c>[InternalsVisibleTo]</c>. The same applies to any internal
    /// property types and accessors the patch class uses.
    /// </remarks>
    public Type GetPatchClassFor(Type type) => EmitPatchClassBuilder.Instance.GetPatchClassFor(type);
}
