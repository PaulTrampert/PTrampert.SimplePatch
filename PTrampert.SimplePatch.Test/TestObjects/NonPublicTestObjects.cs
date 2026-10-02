using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.SimplePatch.Test.External;

namespace PTrampert.SimplePatch.Test.TestObjects;

// Non-public patch source types, which only the Emit builder supports. Private nested ones have to
// be nested in the test fixture itself, so they are in EmitPatchClassBuilderTest.

internal class InternalClassTestObject
{
    [JsonPropertyName("display_name")]
    public string? Name { get; set; }

    public string? InitOnly { get; init; }

    [System.ComponentModel.DataAnnotations.Range(1, 10)]
    public int Rating { get; set; }

    [JsonConverter(typeof(InternalFakeStringConverter))]
    public string? Converted { get; set; }
}

internal class InternalFakeStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        $"Internal:{reader.GetString()}";

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

internal class InternalPrimaryConstructorTestObject(string name)
{
    public string Name { get; } = name;

    public string? Color { get; set; }
}

internal struct InternalStructTestObject
{
    public string? Name { get; set; }

    public int Count { get; set; }
}

internal class ExternalPropertyTypeTestObject
{
    public ExternalInternalColor Color { get; set; }

    public string? Other { get; set; }
}
