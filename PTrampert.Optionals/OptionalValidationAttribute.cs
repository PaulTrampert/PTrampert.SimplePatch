using System.ComponentModel.DataAnnotations;

namespace PTrampert.Optionals;

/// <summary>
/// Validation attribute for properties of type <see cref="IOptional"/>.
/// This attribute is not intended to be used directly, but is applied by the <see cref="PatchClassBuilder"/>
/// to properties of patch objects that implement <see cref="IPatchObjectFor{T}"/>.
/// It runs the validators of the corresponding property in the original type, if the optional has a value.
/// </summary>
/// <param name="innerValidatorType">The validator type on the original class's corresponding property.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class OptionalValidationAttribute(Type innerValidatorType) : ValidationAttribute
{
    /// <inheritdoc />
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