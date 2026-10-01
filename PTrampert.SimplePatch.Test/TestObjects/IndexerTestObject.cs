namespace PTrampert.SimplePatch.Test.TestObjects;

public record IndexerTestObject
{
    public string this[int index]
    {
        get => "";
        set { }
    }

    public string? Name { get; init; }
}
