using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

public class OptionalConverterAttribute(Type srcType, string propertyName) 
    : JsonConverterAttribute
{
    public override JsonConverter? CreateConverter(Type typeToConvert)
    {
        return new OptionalJsonConverterFactory(srcType, propertyName);
    }
}