using System.Text.Json.Serialization;

namespace PTrampert.Optionals.Test.TestObjects;

public record OptionalsBuilderTestObject
{
    public int Id { get; init; }
    
    [JsonPropertyName("name_field")]
    public string Name { get; init; }
    
    [JsonIgnore]
    public string IgnoredProp { get; init; }
}