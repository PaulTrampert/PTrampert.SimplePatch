using Microsoft.AspNetCore.OpenApi;
using PTrampert.SimplePatch;
using PTrampert.SimplePatch.OpenApi;

// Matches where AddOpenApi lives, so this is in scope wherever it is.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds SimplePatch's OpenAPI support to the built-in OpenAPI document generator.
/// </summary>
public static class SimplePatchOpenApiOptionsExtensions
{
    /// <summary>
    /// Documents routes taking an <see cref="IPatchObject{T}"/> request body with the patched
    /// model's schema, with every property optional, instead of the empty object the generator
    /// produces for the interface.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenApi(options => options.AddSimplePatchSchemas());
    /// </code>
    /// </example>
    /// <param name="options">The OpenAPI options to add the transformer to.</param>
    /// <param name="configure">Optional adjustments to the generated schema.</param>
    /// <returns><paramref name="options"/>, for chaining.</returns>
    public static OpenApiOptions AddSimplePatchSchemas(
        this OpenApiOptions options,
        Action<SimplePatchSchemaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var schemaOptions = new SimplePatchSchemaOptions();
        configure?.Invoke(schemaOptions);

        options.AddSchemaTransformer(new PatchObjectSchemaTransformer(schemaOptions));

        if (schemaOptions.SchemaId is { } schemaId)
        {
            var inner = options.CreateSchemaReferenceId;
            options.CreateSchemaReferenceId = jsonTypeInfo =>
                jsonTypeInfo.Type.IsInterface && jsonTypeInfo.Type.TryGetPatchSourceType(out var sourceType)
                    ? schemaId(sourceType) ?? inner(jsonTypeInfo)
                    : inner(jsonTypeInfo);
        }

        return options;
    }
}
