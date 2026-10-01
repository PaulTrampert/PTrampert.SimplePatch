using System.ComponentModel.DataAnnotations;

namespace PTrampert.SimplePatch.Swashbuckle.Test.TestObjects;

public class BaseTestModel
{
    [Required]
    public string? BaseProp { get; set; }
}

public class DerivedTestModel : BaseTestModel
{
    [Required]
    public string? DerivedProp { get; set; }
}
