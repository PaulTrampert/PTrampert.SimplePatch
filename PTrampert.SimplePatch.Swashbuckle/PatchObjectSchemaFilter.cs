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

        ApplyPatchSemantics(
            target,
            source,
            sourceType,
            GetPatchablePropertyNames(sourceType, context),
            options);
    }

    // The mirror of this method lives in PTrampert.SimplePatch.OpenApi's transformer. It cannot be
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
