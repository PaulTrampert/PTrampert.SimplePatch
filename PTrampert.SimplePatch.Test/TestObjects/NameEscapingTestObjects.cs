using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

// Source types whose names have to be escaped in the generated Patch method body.

public record OuterTestObject
{
    public record Inner
    {
        public string? Name { get; init; }
        public string? Other { get; init; }
    }
}

public record GenericTestObject<T>
{
    public T? Value { get; init; }
    public string? Other { get; init; }
}

public record KeywordPropertiesTestObject
{
    public string? @class { get; init; }

    [JsonIgnore]
    public string? @event { get; init; }

    public string? Other { get; init; }
}
