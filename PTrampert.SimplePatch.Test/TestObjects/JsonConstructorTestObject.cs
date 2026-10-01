using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

// [JsonConstructor] takes precedence over the parameterless constructor, as in System.Text.Json.
public class JsonConstructorTestObject
{
    public JsonConstructorTestObject()
    {
        Origin = "parameterless";
    }

    [JsonConstructor]
    public JsonConstructorTestObject(int id)
    {
        Id = id;
        Origin = "annotated";
    }

    public int Id { get; }

    [JsonIgnore]
    public string Origin { get; }

    public string? Name { get; set; }
}
