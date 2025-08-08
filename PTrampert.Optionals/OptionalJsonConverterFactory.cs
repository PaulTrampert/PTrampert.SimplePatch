using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals;

/// <summary>
/// Converter factory for <see cref="Optional{T}"/> types.
/// This factory creates a converter for the generic type <see cref="Optional{T}"/>
/// that can be used with System.Text.Json serialization and deserialization.
/// </summary>
public class OptionalJsonConverterFactory : JsonConverterFactory
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
        return (JsonConverter?)Activator.CreateInstance(
            typeof(OptionalJsonConverter<>).MakeGenericType(typeArgument),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            args: [options],
            culture: null);
    }
    
    private class OptionalJsonConverter<T>(JsonSerializerOptions options) : JsonConverter<Optional<T>>
    {
        private readonly JsonConverter<T> innerConverter = options.GetConverter(typeof(T)) as JsonConverter<T>
                                                           ?? throw new InvalidOperationException($"No converter found for type {typeof(T)}");

        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = innerConverter.Read(ref reader, typeof(T), options);
            return new Optional<T>(value);
        }

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            innerConverter.Write(writer, value.Value, options);
        }
    }
}