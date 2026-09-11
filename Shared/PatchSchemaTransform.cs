using Microsoft.OpenApi;

namespace PTrampert.SimplePatch;

/// <summary>
/// The patch-schema transform, shared by the Swashbuckle and built-in OpenAPI integrations as a
/// linked source file so the two cannot drift. It is internal, so each package gets its own copy
/// and referencing both packages stays unambiguous.
/// </summary>
/// <remarks>
/// The patch contract for a model is the model's own contract with optional members, so the
/// schema is derived from the source model's schema rather than from the class
/// <see cref="PatchClassBuilder"/> emits. That keeps validation constraints, custom converters,
/// property naming and any tuning the application already applied to its write model, and makes
/// it impossible for the two contracts to describe different shapes.
/// </remarks>
internal static class PatchSchemaTransform
{
    /// <summary>
    /// Rewrites <paramref name="target"/> — the near-empty schema an OpenAPI generator produces for
    /// the <see cref="IPatchObject{T}"/> interface — into the patch contract for
    /// <paramref name="sourceType"/>.
    /// </summary>
    /// <param name="target">The schema to rewrite, in place.</param>
    /// <param name="source">The source model's generated schema.</param>
    /// <param name="sourceType">The patched type.</param>
    /// <param name="patchablePropertyNames">
    /// JSON names of the properties the generated patch class can actually set. The source schema
    /// also describes members the patch class omits — get-only properties, which it documents as
    /// <c>readOnly</c> — and those must not appear in a patch body.
    /// </param>
    /// <param name="options">The consumer's adjustments.</param>
    public static void Apply(
        OpenApiSchema target,
        IOpenApiSchema source,
        Type sourceType,
        ICollection<string> patchablePropertyNames,
        SimplePatchSchemaOptions options)
    {
        target.Type = source.Type;
        target.AdditionalPropertiesAllowed = source.AdditionalPropertiesAllowed;
        target.Properties = source.Properties?
            .Where(property => patchablePropertyNames.Contains(property.Key))
            .ToDictionary(property => property.Key, property => property.Value);

        // Dropping required is what makes this a partial update: every property becomes optional,
        // while each property's own schema — including its nullability — is left alone, so sending
        // null for a non-nullable member stays invalid. When the consumer turns that off they mean
        // "require what the model requires", so the source's list is carried over instead.
        target.Required = options.ClearRequired || source.Required is null
            ? null
            : new HashSet<string>(source.Required.Where(patchablePropertyNames.Contains), StringComparer.Ordinal);

        if (options.DescriptionFormat is { } descriptionFormat)
        {
            target.Description = string.Format(descriptionFormat, sourceType.Name);
        }

        if (options.Example?.Invoke(sourceType) is { } example)
        {
            target.Example = example;
        }
    }
}
