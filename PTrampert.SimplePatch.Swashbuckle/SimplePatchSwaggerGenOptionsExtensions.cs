using PTrampert.SimplePatch;
using PTrampert.SimplePatch.Swashbuckle;
using Swashbuckle.AspNetCore.SwaggerGen;

// Matches where Swashbuckle puts its own SwaggerGenOptions extensions, so this is in scope
// wherever AddSwaggerGen is.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds SimplePatch's OpenAPI support to Swashbuckle.
/// </summary>
public static class SimplePatchSwaggerGenOptionsExtensions
{
    /// <summary>
    /// Documents routes taking an <see cref="IPatchObject{T}"/> request body with the patched
    /// model's schema, with every property optional, instead of the empty object Swashbuckle
    /// generates for the interface.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddSwaggerGen(options => options.AddSimplePatchSchemas());
    /// </code>
    /// </example>
    /// <param name="options">The Swashbuckle options to add the filter to.</param>
    /// <param name="configure">Optional adjustments to the generated schema.</param>
    /// <returns><paramref name="options"/>, for chaining.</returns>
    public static SwaggerGenOptions AddSimplePatchSchemas(
        this SwaggerGenOptions options,
        Action<SimplePatchSchemaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var schemaOptions = new SimplePatchSchemaOptions();
        configure?.Invoke(schemaOptions);

        options.SchemaFilter<PatchObjectSchemaFilter>(schemaOptions);

        if (schemaOptions.SchemaId is { } schemaId)
        {
            var inner = options.SchemaGeneratorOptions.SchemaIdSelector;
            options.SchemaGeneratorOptions.SchemaIdSelector = type =>
                type.IsInterface && type.TryGetPatchSourceType(out var sourceType)
                    ? schemaId(sourceType) ?? inner(type)
                    : inner(type);
        }

        return options;
    }
}
