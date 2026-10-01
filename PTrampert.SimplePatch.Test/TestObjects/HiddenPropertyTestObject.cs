using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

public class HiddenPropertyTestObjectBase
{
    public int Value { get; init; }
}

// Hides the base Value with a different type, so reflection sees two properties named Value.
// System.Text.Json uses only this one; the patch class and its lookups must agree.
public class HiddenPropertyTestObject : HiddenPropertyTestObjectBase
{
    [MaxLength(20)]
    [JsonConverter(typeof(FakeStringConverter))]
    public new string? Value { get; init; }
}
