using System.Collections.Concurrent;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PTrampert.SimplePatch.OpenApi;

/// <summary>
/// Replaces the empty schema the built-in OpenAPI generator produces for an
/// <see cref="IPatchObject{T}"/> request body with the patched model's schema, with every
/// property optional.
/// </summary>
/// <remarks>
/// Register it with <c>AddSimplePatchSchemas</c> rather than directly, so that the schema-id
/// option is wired up too.
/// </remarks>
/// <param name="options">The consumer's adjustments.</param>
public class PatchObjectSchemaTransformer(SimplePatchSchemaOptions options) : IOpenApiSchemaTransformer
{
    private static readonly ConcurrentDictionary<Type, HashSet<string>> PatchablePropertyNamesBySourceType = new();

    /// <inheritdoc />
    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.JsonTypeInfo.Type;

        // Only the interface is documented; the generated patch class never reaches the document.
        if (!type.IsInterface || !type.TryGetPatchSourceType(out var sourceType))
        {
            return;
        }

        var source = await context.GetOrCreateSchemaAsync(sourceType, cancellationToken: cancellationToken);

        ApplyPatchSemantics(
            schema,
            source,
            sourceType,
            GetPatchablePropertyNames(sourceType, context),
            options);
    }

    // The mirror of this method lives in PTrampert.SimplePatch.Swashbuckle's schema filter. It cannot be
    // shared through the core package without giving every consumer of PTrampert.SimplePatch a
    // Microsoft.OpenApi dependency, so the two are kept in step by mirrored test suites instead.
    private static void ApplyPatchSemantics(
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

    private static HashSet<string> GetPatchablePropertyNames(Type sourceType, OpenApiSchemaTransformerContext context) =>
        PatchablePropertyNamesBySourceType.GetOrAdd(sourceType, _ =>
        {
            // JsonTypeInfo.Options is the same JsonSerializerOptions the document generator is
            // using, so the names here match the ones in the source model's schema exactly —
            // naming policy, [JsonPropertyName] and all.
            var patchType = PatchClassBuilder.Shared.GetPatchClassFor(sourceType);
            return context.JsonTypeInfo.Options
                .GetTypeInfo(patchType).Properties
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
        });
}
