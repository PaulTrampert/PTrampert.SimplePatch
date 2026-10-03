using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Swashbuckle.Test.TestObjects;

// Internal, so only the Emit builder can generate its patch class. This assembly grants
// [InternalsVisibleTo] to the generated assemblies.
internal record InternalPersonTestModel
{
    [StringLength(255, MinimumLength = 3)]
    public string? Name { get; init; }

    [JsonPropertyName("nick_name")]
    public string? NickName { get; init; }

    public string Display => Name ?? "";
}
