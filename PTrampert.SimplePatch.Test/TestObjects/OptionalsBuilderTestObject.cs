using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

public record OptionalsBuilderTestObject
{
    [System.ComponentModel.DataAnnotations.Range(1, 100)]
    public int Id { get; init; }
    
    [JsonPropertyName("name_field")]
    [Required]
    public string Name { get; init; }
    
    [JsonIgnore]
    public string IgnoredProp { get; init; }
    
    [JsonConverter(typeof(FakeStringConverter))]
    public string FakeStringProp { get; init; }
}