using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals;

/// <summary>
/// JsonConverterFactory for creating converters for patch objects.
/// This factory generates a converter for types implementing <see cref="IPatchObjectFor{T}"/>.
/// The converter serializes and deserializes the patch object, allowing it to be used
/// with System.Text.Json serialization and deserialization.
/// The patch object is a type that can apply changes to an instance of type T, where T is typically a POCO or record type.
/// </summary>
public class PatchJsonConverterFactory : JsonConverterFactory
{
    private readonly PatchClassBuilder _patchClassBuilder = new();

    /// <summary>
    /// Returns true if the specified type is a generic type of <see cref="IPatchObjectFor{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <returns>True if typeToConvert is <see cref="IPatchObjectFor{T}"/></returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(IPatchObjectFor<>);
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var baseObjectType = typeToConvert.GetGenericArguments()[0];
        var concreteTypeToConvert = _patchClassBuilder.GetPatchClassFor(baseObjectType);
        return (JsonConverter?)Activator.CreateInstance(
            typeof(PatchObjectJsonConverter<,>).MakeGenericType(concreteTypeToConvert, baseObjectType),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            args: [],
            culture: null);
    }

    private class PatchObjectJsonConverter<TPatch, TObj> : JsonConverter<TPatch> where TPatch : IPatchObjectFor<TObj>
    {
        public override TPatch? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TPatch>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TPatch value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}