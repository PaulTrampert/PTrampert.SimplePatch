using System.ComponentModel.DataAnnotations;

namespace PTrampert.Optionals;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class OptionalValidationAttribute(Type innerValidatorType) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IOptional optional)
        {
            return new ValidationResult("Value must be of type IOptional.", new[] { validationContext.MemberName });
        }

        if (!validationContext.ObjectType.IsPatchObjectType())
        {
            return new ValidationResult("OptionalValidationAttribute is only supported for patch objects.", new[] { validationContext.MemberName });
        }

        if (!optional.HasValue)
        {
            return ValidationResult.Success;
        }

        var patchObjectType = validationContext.ObjectType.GetPatchObjectType();
        var innerAttribute = patchObjectType.GetProperty(validationContext.MemberName)
            ?.GetCustomAttributes(innerValidatorType, true)
            .Cast<ValidationAttribute>()
            .FirstOrDefault();
        return innerAttribute?.GetValidationResult(optional.UntypedValue, validationContext);
    }
}