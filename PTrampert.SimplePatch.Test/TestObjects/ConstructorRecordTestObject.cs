namespace PTrampert.SimplePatch.Test.TestObjects;

// A non-positional record whose only constructor sets a get-only property. `with` can't assign
// Code, so records skip constructor binding and Code is carried over by the clone instead.
public record ConstructorRecordTestObject
{
    public ConstructorRecordTestObject(string name, string code)
    {
        Name = name;
        Code = code;
    }

    public string Name { get; init; }

    public string Code { get; }
}
