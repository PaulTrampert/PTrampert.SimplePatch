using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PTrampert.SimplePatch;

/// <summary>
/// Extensions to easily add the required JSON converters to <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonOptionsExtensions
{
    /// <summary>
    /// Adds the converters required for (de)serializing <see cref="Optional{T}"/> and <see cref="IPatchObject{T}"/> types.
    /// Also makes serialization omit every <see cref="Optional{T}"/> property that has no value, so an unset
    /// optional is written as "not sent" rather than as null.
    /// </summary>
    /// <param name="options">The options to add converters to.</param>
    public static void AddSimplePatchConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new OptionalJsonConverterFactory());
        options.Converters.Add(new PatchJsonConverterFactory());
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(SkipUnsetOptionals);
    }

    // A converter can't omit the property it converts (the name is already written when it runs),
    // so unset optionals are skipped at the contract level instead.
    private static void SkipUnsetOptionals(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (!property.PropertyType.IsGenericType ||
                property.PropertyType.GetGenericTypeDefinition() != typeof(Optional<>))
                continue;

            var existing = property.ShouldSerialize;
            property.ShouldSerialize = (obj, value) =>
                ((IOptional)value!).HasValue && (existing?.Invoke(obj, value) ?? true);
        }
    }
}
