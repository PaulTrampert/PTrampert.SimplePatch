using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PTrampert.Optionals.Sample;

public class PhoneNumberJsonConverter : JsonConverter<PhoneNumber>
{
    public const string RegexStr = @"^\((?<AreaCode>\d{3})\)(?<NumberStr>\d{3}-?\d{4})$";

    private Regex _phoneNumberRegex = new(RegexStr);
    
    public override PhoneNumber? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var phoneNumberStr = reader.GetString();
        if (string.IsNullOrEmpty(phoneNumberStr))
        {
            return null;
        }
        var match = _phoneNumberRegex.Match(phoneNumberStr);
        if (!match.Success)
        {
            throw new JsonException($"Invalid phone number format: {phoneNumberStr}");
        }
        var areaCode = match.Groups["AreaCode"].Value;
        var numberStr = match.Groups["NumberStr"].Value;
        return new PhoneNumber(areaCode, numberStr);
    }

    public override void Write(Utf8JsonWriter writer, PhoneNumber value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"({value.AreaCode}){value.Number}");
    }
}