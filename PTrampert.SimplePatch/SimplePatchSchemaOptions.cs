using System.Text.Json.Nodes;

namespace PTrampert.SimplePatch;

/// <summary>
/// Controls how the OpenAPI integration packages describe an <see cref="IPatchObject{T}"/>
/// request body. The defaults produce the source model's schema with PATCH semantics applied;
/// every property here is an opt-in adjustment on top of that.
/// </summary>
/// <remarks>
/// This type lives in the core package so that the Swashbuckle and built-in OpenAPI
/// integrations configure identically and can be referenced side by side.
/// </remarks>
public class SimplePatchSchemaOptions
{
    /// <summary>
    /// Names the schema component for a patch body, given the patched type. Return null to keep
    /// the id the OpenAPI generator would pick — <c>PersonWriteModelIPatchObject</c> under
    /// Swashbuckle, <c>IPatchObjectOfPersonWriteModel</c> under the built-in generator.
    /// Set to <c>t =&gt; t.Name + "Patch"</c> for the friendlier <c>PersonWriteModelPatch</c>.
    /// Default null.
    /// </summary>
    public Func<Type, string?>? SchemaId { get; set; }

    /// <summary>
    /// Format string for the patch schema's description, where <c>{0}</c> is the patched type's
    /// name. Set to null to leave whatever description the source model's schema carries.
    /// </summary>
    public string? DescriptionFormat { get; set; } =
        "Partial update of {0}. Omitted properties are left unchanged.";

    /// <summary>
    /// Clear <c>required</c> on the patch schema. This is what makes the body a partial update,
    /// so leave it on unless you have a reason not to. Default true.
    /// </summary>
    public bool ClearRequired { get; set; } = true;

    /// <summary>
    /// Supplies the example body for a patch schema, given the patched type. Return null — the
    /// default — to let the UI synthesize one.
    /// </summary>
    /// <remarks>
    /// Worth setting. Swagger UI builds its example from <c>properties</c> and ignores
    /// <c>required</c>, so a synthesized PATCH example lists every property, which reads as
    /// "send all of these" and is what "Try it out" will submit. Supplying an example of one or
    /// two properties demonstrates the partial-update semantics instead. No default is provided
    /// because any property this library picked for you would be an arbitrary choice.
    /// </remarks>
    public Func<Type, JsonNode?>? Example { get; set; }
}
