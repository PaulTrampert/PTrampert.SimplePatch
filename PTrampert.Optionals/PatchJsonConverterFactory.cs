using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals;

public class PatchJsonConverterFactory : JsonConverterFactory
{
    private readonly PatchClassBuilder _patchClassBuilder = new();

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(IPatchObjectFor<>);
    }

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