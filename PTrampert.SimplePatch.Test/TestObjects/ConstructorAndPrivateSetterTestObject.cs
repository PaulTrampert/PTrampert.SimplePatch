namespace PTrampert.SimplePatch.Test.TestObjects;

// Constructor binding has to see get-only properties, even though only properties with a public
// setter are patchable otherwise. Version has a private setter and isn't in the constructor, so it
// stays out of the patch class.
public class ConstructorAndPrivateSetterTestObject
{
    public ConstructorAndPrivateSetterTestObject(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public int Version { get; private set; }

    public string? Color { get; set; }
}
