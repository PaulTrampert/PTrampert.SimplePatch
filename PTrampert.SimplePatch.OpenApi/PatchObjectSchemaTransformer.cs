using System.Collections.Concurrent;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PTrampert.SimplePatch.OpenApi;

/// <summary>
/// Replaces the empty schema the built-in OpenAPI generator produces for an
/// <see cref="IPatchObject{T}"/> request body with the patched model's schema, with every
/// property optional.
/// </summary>
/// <param name="options">The consumer's adjustments.</param>
public class PatchObjectSchemaTransformer(SimplePatchSchemaOptions options) : IOpenApiSchemaTransformer
{
    // The metadata key the generator reads a schema's component id from. Its own constant for it,
    // OpenApiConstants.SchemaId, is internal.
    private const string SchemaIdMetadataKey = "x-schema-id";

    // Per instance, not static: the names follow the host's naming policy, and AddSimplePatchSchemas
    // creates one transformer per OpenApiOptions, so each host gets its own cache.
    private readonly ConcurrentDictionary<Type, HashSet<string>> _patchablePropertyNamesBySourceType = new();

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

        // The generator only turns this metadata into a component id after every transformer has
        // run, so setting it here takes effect whatever CreateSchemaReferenceId the application
        // configured, and whenever it configured it.
        if (options.SchemaId?.Invoke(sourceType) is { } schemaId)
        {
            schema.Metadata ??= new Dictionary<string, object>();
            schema.Metadata[SchemaIdMetadataKey] = schemaId;
        }
    }

    private HashSet<string> GetPatchablePropertyNames(Type sourceType, OpenApiSchemaTransformerContext context) =>
        _patchablePropertyNamesBySourceType.GetOrAdd(sourceType, _ =>
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
