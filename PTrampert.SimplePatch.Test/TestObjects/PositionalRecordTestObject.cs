namespace PTrampert.SimplePatch.Test.TestObjects;

// Positional records have no parameterless constructor, so they can't be built with `new T { … }`.
public record PositionalRecordTestObject(string Name, int Count)
{
    // Get-only, so the patch class has no property for it; patching should keep its value.
    public string Tag { get; } = Name + ":tag";
}

public record DerivedPositionalRecordTestObject(string Name, int Count, string Extra)
    : PositionalRecordTestObject(Name, Count);
