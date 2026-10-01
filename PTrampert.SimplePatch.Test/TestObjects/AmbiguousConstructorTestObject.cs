namespace PTrampert.SimplePatch.Test.TestObjects;

// Several public constructors, none parameterless or marked [JsonConstructor]: System.Text.Json
// can't deserialize this, so there is no constructor to mirror.
public class AmbiguousConstructorTestObject
{
    public AmbiguousConstructorTestObject(string name)
    {
        Name = name;
    }

    public AmbiguousConstructorTestObject(string name, int id)
    {
        Name = name;
        Id = id;
    }

    public string Name { get; }

    public int Id { get; }
}
