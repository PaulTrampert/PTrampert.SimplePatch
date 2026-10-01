using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Test.TestObjects;

// A class, not a record, whose only public constructor takes parameters. The parameter names
// differ in case from the properties they bind to, as System.Text.Json allows.
public class ConstructorTestObject
{
    public ConstructorTestObject(string NAME, string? secret)
    {
        Name = NAME;
        Secret = secret;
    }

    public string Name { get; }

    [JsonIgnore]
    public string? Secret { get; }

    public string? Color { get; set; }
}
