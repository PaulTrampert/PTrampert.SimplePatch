using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals.Test.TestObjects;

public class FakeStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = reader.GetString();
        return $"FakeString:{result}";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"FakeString:{value}");
    }
}