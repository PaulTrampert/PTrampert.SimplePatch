using PTrampert.SimplePatch;
using PTrampert.SimplePatch.Swashbuckle;
using Swashbuckle.AspNetCore.SwaggerGen;

// Matches where Swashbuckle puts AddSwaggerGen, so this is in scope wherever it is.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds SimplePatch's OpenAPI support to Swashbuckle at the service-collection level.
/// </summary>
public static class SimplePatchSwaggerGenServiceCollectionExtensions
{
    /// <summary>
    /// Documents routes taking an <see cref="IPatchObject{T}"/> request body with the patched
    /// model's schema, with every property optional, instead of the empty object Swashbuckle
    /// generates for the interface.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(t => t.Name));
    /// builder.Services.AddSimplePatchSchemas(patch => patch.SchemaId = t => t.Name + "Patch");
    /// </code>
    /// </example>
    /// <remarks>
    /// Equivalent to calling
    /// <see cref="SimplePatchSwaggerGenOptionsExtensions.AddSimplePatchSchemas(SwaggerGenOptions, Action{SimplePatchSchemaOptions})"/>
    /// inside <c>AddSwaggerGen</c>, except that <see cref="SimplePatchSchemaOptions.SchemaId"/> is
    /// applied after all of the application's configuration, so a <c>CustomSchemaIds</c> made
    /// anywhere, before or after this call, cannot override it. Use one or the other, not both.
    /// </remarks>
    /// <param name="services">The services <c>AddSwaggerGen</c> is registered with.</param>
    /// <param name="configure">Optional adjustments to the generated schema.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddSimplePatchSchemas(
        this IServiceCollection services,
        Action<SimplePatchSchemaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var schemaOptions = new SimplePatchSchemaOptions();
        configure?.Invoke(schemaOptions);

        services.ConfigureSwaggerGen(options => options.SchemaFilter<PatchObjectSchemaFilter>(schemaOptions));

        if (schemaOptions.SchemaId is { } schemaId)
        {
            // Swashbuckle copies SwaggerGenOptions into SchemaGeneratorOptions in an
            // IConfigureOptions, so a post-configure step sees the selector the application ended
            // up with.
            services.PostConfigure<SchemaGeneratorOptions>(options =>
                options.SchemaIdSelector = SimplePatchSwaggerGenOptionsExtensions.WrapSchemaIdSelector(
                    options.SchemaIdSelector, schemaId));
        }

        return services;
    }
}
