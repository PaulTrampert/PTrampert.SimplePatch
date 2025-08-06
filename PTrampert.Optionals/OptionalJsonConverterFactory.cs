using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals;

public class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        return typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    }

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