using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.OpenApi.Test.TestObjects;

public record PersonTestModel
{
    [Required]
    [StringLength(255, MinimumLength = 3)]
    public required string Name { get; init; }

    public DateTime DateOfBirth { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    [JsonPropertyName("nick_name")]
    public string? NickName { get; init; }

    [JsonIgnore]
    public string? Secret { get; init; }

    // Get-only, so the generated patch class cannot set it. The source model's schema still
    // describes it, marked readOnly.
    public string Display => Name;
}
