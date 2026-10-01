using System.ComponentModel.DataAnnotations;

namespace PTrampert.SimplePatch.Test.TestObjects;

/// <summary>
/// A validator that can appear more than once on a property, with different arguments each time.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ForbiddenSubstringAttribute(string substring) : ValidationAttribute
{
    public string Substring { get; } = substring;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        return value is string s && s.Contains(Substring)
            ? new ValidationResult($"The {validationContext.MemberName} field must not contain '{Substring}'.")
            : ValidationResult.Success;
    }
}
