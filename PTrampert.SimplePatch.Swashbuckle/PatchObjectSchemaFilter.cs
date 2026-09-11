using System.Collections.Concurrent;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PTrampert.SimplePatch.Swashbuckle;

/// <summary>
/// Replaces the empty schema Swashbuckle generates for an <see cref="IPatchObject{T}"/> request
/// body with the patched model's schema, with every property optional.
/// </summary>
/// <remarks>
/// Register it with <c>AddSimplePatchSchemas</c> rather than
/// directly, so that the schema-id option is wired up too.
/// </remarks>
/// <param name="options">The consumer's adjustments.</param>
public class PatchObjectSchemaFilter(SimplePatchSchemaOptions options) : ISchemaFilter
{
    private static readonly ConcurrentDictionary<Type, HashSet<string>> PatchablePropertyNamesBySourceType = new();

    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // A filter can only mutate the schema in place — it cannot return a different one, and it
        // cannot swap a $ref — so the source schema is copied across rather than substituted.
        if (schema is not OpenApiSchema target)
        {
            return;
        }

        // Only the interface is documented; the generated patch class never reaches the document.
        if (!context.Type.IsInterface || !context.Type.TryGetPatchSourceType(out var sourceType))
        {
            return;
        }

        // Generates the source model's schema if the document does not have it yet, and registers
        // it as a component, so a model used by both PUT and PATCH is described exactly once.
        var generated = context.SchemaGenerator.GenerateSchema(sourceType, context.SchemaRepository);
        if (Resolve(generated, context.SchemaRepository) is not { } source)
        {
            return;
        }

        PatchSchemaTransform.Apply(
            target,
            source,
            sourceType,
            GetPatchablePropertyNames(sourceType, context),
            options);
    }

    private static HashSet<string> GetPatchablePropertyNames(Type sourceType, SchemaFilterContext context) =>
        PatchablePropertyNamesBySourceType.GetOrAdd(sourceType, _ =>
        {
            // Let Swashbuckle name the generated patch class's properties itself, so the names agree
            // with whatever JsonSerializerOptions the application configured — Swashbuckle resolves
            // those internally and exposes them to neither filters nor DI. A throwaway repository
            // keeps the Optional<T> component schemas this produces out of the real document.
            var patchType = PatchClassBuilder.Shared.GetPatchClassFor(sourceType);
            var throwaway = new SchemaRepository(context.DocumentName);
            var patchSchema = context.SchemaGenerator.GenerateSchema(patchType, throwaway);
            return Resolve(patchSchema, throwaway)?.Properties?.Keys.ToHashSet(StringComparer.Ordinal) ?? [];
        });

    private static IOpenApiSchema? Resolve(IOpenApiSchema schema, SchemaRepository repository) =>
        schema is OpenApiSchemaReference reference
            ? repository.Schemas.GetValueOrDefault(reference.Reference.Id)
            : schema;
}
