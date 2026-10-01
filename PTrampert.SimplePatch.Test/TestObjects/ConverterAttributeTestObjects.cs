using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

public enum Color
{
    Red,
    Blue
}

public record StringEnumTestObject
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Color Color { get; init; }
}

public record NullableStringEnumTestObject
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Color? Color { get; init; }
}

public class FakeStringConverterAttribute : JsonConverterAttribute
{
    public override JsonConverter? CreateConverter(Type typeToConvert)
    {
        return new FakeStringConverter();
    }
}

public record CustomConverterAttributeTestObject
{
    [FakeStringConverter]
    public string? Value { get; init; }
}
