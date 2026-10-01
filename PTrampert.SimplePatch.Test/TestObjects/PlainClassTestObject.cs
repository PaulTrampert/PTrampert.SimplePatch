using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

// A non-record class, patched through `new T { … }` rather than `with`.
public class PlainClassTestObject
{
    public string? Name { get; set; }

    [JsonIgnore]
    public string? IgnoredProp { get; set; }
}
