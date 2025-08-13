using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Converter factory for <see cref="Optional{T}"/> types.
/// This factory creates a converter for the generic type <see cref="Optional{T}"/>
/// that can be used with System.Text.Json serialization and deserialization.
/// </summary>
/// <param name="srcType">
///     When specified with propertyName, will check the given property on srcType
///     for a <see cref="JsonConverterAttribute"/> and use that converter for the property.
///     If not specified, the converter will use the default converter for the type T.
///     If propertyName is not specified, this parameter is ignored.
/// </param>
/// <param name="propertyName">
///     When specified with srcType, will check the given property on srcType
///     for a <see cref="JsonConverterAttribute"/> and use that converter for the property.
///     If not specified, the converter will use the default converter for the type T.
///     If srcType is not specified, this parameter is ignored.
/// </param>
public class OptionalJsonConverterFactory(Type srcType = null, string propertyName = null) : JsonConverterFactory
{
    /// <summary>
    /// Returns true if the specified type is a generic type of <see cref="Optional{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <returns>True if typeToConvert is <see cref="Optional{T}"/></returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var typeArgument = typeToConvert.GetGenericArguments()[0];
        object?[] args = [options, null];
        if (srcType != null && propertyName != null)
        {
            var property = srcType.GetProperty(propertyName);
            var converterAttribute = property?.GetCustomAttribute<JsonConverterAttribute>();
            args = [options, converterAttribute];
        }
        return (JsonConverter?)Activator.CreateInstance(
            typeof(OptionalJsonConverter<>).MakeGenericType(typeArgument),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            args: args,
            culture: null);
    }
    
    private class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        private readonly JsonConverter<T> _innerConverter;
        
        public OptionalJsonConverter(JsonSerializerOptions options, JsonConverterAttribute? explicitInnerConverter)
        {
            if (explicitInnerConverter is { ConverterType: not null })
            {
                _innerConverter = Activator.CreateInstance(explicitInnerConverter.ConverterType) as JsonConverter<T>
                                  ?? throw new InvalidOperationException($"Could not create converter of type {explicitInnerConverter.ConverterType} for type {typeof(T)}");
            }
            else if (explicitInnerConverter is { ConverterType: null })
            {
                _innerConverter = options.GetConverter(typeof(T)) as JsonConverter<T>
                                  ?? throw new InvalidOperationException($"No converter found for type {typeof(T)}");
            }
            else
            {
                _innerConverter = options.GetConverter(typeof(T)) as JsonConverter<T>
                                  ?? throw new InvalidOperationException($"No converter found for type {typeof(T)}");
            }
        }

        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = _innerConverter.Read(ref reader, typeof(T), options);
            return new Optional<T>(value);
        }

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            _innerConverter.Write(writer, value.Value, options);
        }
    }
}