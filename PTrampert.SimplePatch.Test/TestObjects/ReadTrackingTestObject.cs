namespace PTrampert.SimplePatch.Test.TestObjects;

// Counts reads of Name, to show the patch class only reads the target's value when the patch
// leaves the property out.
public class ReadTrackingTestObject
{
    private string? name;

    public string? Name
    {
        get
        {
            NameReads++;
            return name;
        }
        set => name = value;
    }

    public string? Other { get; set; }

    // A private setter keeps this out of the patch class, so patching never reads it either.
    public int NameReads { get; private set; }
}
