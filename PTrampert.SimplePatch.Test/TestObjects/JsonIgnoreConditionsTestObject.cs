using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

public record JsonIgnoreConditionsTestObject
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string? Always { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Never { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WhenWritingNull { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WhenWritingDefault { get; init; }
}
