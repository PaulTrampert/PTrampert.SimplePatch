using System.Text.Json;

namespace PTrampert.SimplePatch;

/// <summary>
/// Extensions to easily add the required JSON converters to <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonOptionsExtensions
{
    /// <summary>
    /// Adds the converters required for (de)serializing <see cref="Optional{T}"/> and <see cref="IPatchObject{T}"/> types.
    /// </summary>
    /// <param name="options">The options to add converters to.</param>
    public static void AddSimplePatchConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new OptionalJsonConverterFactory());
        options.Converters.Add(new PatchJsonConverterFactory());
    }
}