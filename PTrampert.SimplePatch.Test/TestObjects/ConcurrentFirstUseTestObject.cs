namespace PTrampert.SimplePatch.Test.TestObjects;

// Used only by the concurrent first use test, so its patch class is never cached before that
// test runs.
public record ConcurrentFirstUseTestObject
{
    public string? Name { get; init; }
}
