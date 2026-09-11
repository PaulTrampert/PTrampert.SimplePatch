// Deliberately in the global namespace: patch class generation used to fail for source types
// without one, which minimal-API apps declaring models in Program.cs hit immediately.
public record GlobalNamespaceTestObject
{
    public string? Name { get; init; }
}
