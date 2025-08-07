namespace PTrampert.Optionals.Test.TestObjects;

internal record TestObject
{
    public Optional<string> StringProp { get; init; }
    public Optional<int> IntProp { get; init; }
    public Optional<int?> NullableIntProp { get; init; }
    public Optional<TestNestedObject> NestedObjectProp { get; init; }
}