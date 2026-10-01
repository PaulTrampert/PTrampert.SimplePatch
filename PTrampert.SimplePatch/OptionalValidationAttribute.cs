using System.ComponentModel.DataAnnotations;

namespace PTrampert.SimplePatch;

/// <summary>
/// Validation attribute for properties of type <see cref="IOptional"/>.
/// This attribute is not intended to be used directly, but is applied by the <see cref="PatchClassBuilder"/>
/// to properties of patch objects that implement <see cref="IPatchObject{T}"/>.
/// It runs the validators of the corresponding property in the original type, if the optional has a value.
/// </summary>
/// <param name="innerValidatorType">The validator type on the original class's corresponding property.</param>
/// <param name="innerValidatorIndex">
/// Which of the original property's validators of exactly <paramref name="innerValidatorType"/> to run,
/// counting from zero, for properties that carry the same validator type more than once.
/// </param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class OptionalValidationAttribute(Type innerValidatorType, int innerValidatorIndex) : ValidationAttribute
{
    /// <summary>
    /// Wraps the first validator of type <paramref name="innerValidatorType"/> on the original property.
    /// </summary>
    /// <param name="innerValidatorType">The validator type on the original class's corresponding property.</param>
    public OptionalValidationAttribute(Type innerValidatorType) : this(innerValidatorType, 0)
    {
    }

    /// <summary>
    /// Identifies the wrapped validator: the attribute type, the inner validator type, and its index.
    /// </summary>
    /// <remarks>
    /// <see cref="System.ComponentModel.TypeDescriptor"/>, which
    /// <see cref="Validator"/> reads attributes through, keeps only one attribute per <see cref="Attribute.TypeId"/>.
    /// The default, the attribute's own type, would collapse every <see cref="OptionalValidationAttribute"/> on a
    /// property into one. Keying on the inner type alone would still collapse repeats of one validator type, and
    /// returning <c>this</c> would too, because <see cref="Attribute.Equals(object)"/> compares field values.
    /// Inner type plus index is exactly what distinguishes the attributes <see cref="PatchClassBuilder"/> emits.
    /// </remarks>
    public override object TypeId => (typeof(OptionalValidationAttribute), innerValidatorType, innerValidatorIndex);

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
        var innerAttribute = patchObjectType.GetMostDerivedProperty(validationContext.MemberName)
            ?.GetCustomAttributes(innerValidatorType, true)
            .Where(attribute => attribute.GetType() == innerValidatorType)
            .Cast<ValidationAttribute>()
            .ElementAtOrDefault(innerValidatorIndex);
        return innerAttribute?.GetValidationResult(optional.UntypedValue, validationContext);
    }
}
