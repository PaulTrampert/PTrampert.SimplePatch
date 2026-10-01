namespace PTrampert.SimplePatch.Test.TestObjects;

public record StaticPropertyTestObject
{
    public static string? Shared { get; set; }

    public string? Name { get; init; }
}
