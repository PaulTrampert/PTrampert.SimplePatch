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

        PatchSchemaTransform.Apply(
            schema,
            source,
            sourceType,
            GetPatchablePropertyNames(sourceType, context),
            options);
    }

    private static HashSet<string> GetPatchablePropertyNames(Type sourceType, OpenApiSchemaTransformerContext context) =>
        PatchablePropertyNamesBySourceType.GetOrAdd(sourceType, _ =>
        {
            // JsonTypeInfo.Options is the same JsonSerializerOptions the document generator is
            // using, so the names here match the ones in the source model's schema exactly —
            // naming policy, [JsonPropertyName] and all.
            var patchType = PatchClassBuilder.Instance.GetPatchClassFor(sourceType);
            return context.JsonTypeInfo.Options
                .GetTypeInfo(patchType).Properties
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
        });
}
