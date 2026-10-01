using System.ComponentModel.DataAnnotations;

namespace PTrampert.SimplePatch.Test.TestObjects;

public class OverriddenPropertyBaseTestObject
{
    [StringLength(5)]
    public virtual string? Name { get; set; }
}

public class OverriddenPropertyTestObject : OverriddenPropertyBaseTestObject
{
    public override string? Name { get; set; }
}
