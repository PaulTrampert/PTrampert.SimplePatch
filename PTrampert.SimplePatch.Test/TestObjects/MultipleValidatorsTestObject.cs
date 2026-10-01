using System.ComponentModel.DataAnnotations;

namespace PTrampert.SimplePatch.Test.TestObjects;

public record MultipleValidatorsTestObject
{
    [Required, MaxLength(10)]
    public string? RequiredFirst { get; init; }

    [MaxLength(10), Required]
    public string? MaxLengthFirst { get; init; }

    [ForbiddenSubstring("foo"), ForbiddenSubstring("bar")]
    public string? Tag { get; init; }
}
