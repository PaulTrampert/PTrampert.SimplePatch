using System.ComponentModel.DataAnnotations;

namespace PTrampert.Optionals;

public static class PatchObjectValidator
{
    public static IEnumerable<ValidationResult>? IsValid<T>(IPatchObjectFor<T> obj, ValidationContext validationContext)
    {
        var properties = obj.GetType().GetProperties()
            .Where(p => p.CanRead && p.PropertyType.IsSubclassOf(typeof(IOptional)));
        var targetType = typeof(T);
        foreach (var property in properties)
        {
            var value = (IOptional)property.GetValue(obj)!;
            if (!value.HasValue) continue;

            var validationAttributes = targetType.GetProperty(property.Name)
                ?.GetCustomAttributes(typeof(ValidationAttribute), true)
                .Cast<ValidationAttribute>() ?? [];
            foreach (var validationAttribute in validationAttributes)
            {
                var validationResult = validationAttribute.GetValidationResult(value.UntypedValue, validationContext);
                if (validationResult != null && validationResult != ValidationResult.Success)
                {
                    yield return validationResult;
                }
            }
        }
    }
}