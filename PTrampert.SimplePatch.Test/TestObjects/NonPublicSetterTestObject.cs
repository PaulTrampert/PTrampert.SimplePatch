using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

public record NonPublicSetterTestObject
{
    public string? Name { get; set; }

    public string? InitOnly { get; init; }

    public int PrivateSet { get; private set; }

    public string? ProtectedSet { get; protected set; }

    public string? InternalSet { get; internal set; }

    [JsonIgnore]
    public string? IgnoredPrivateSet { get; private set; }
}
