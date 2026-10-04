using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Benchmarks.TestObjects;

/// <summary>
/// A positional record, patched with a <c>with</c> expression, whose properties carry every kind of
/// attribute the builders copy onto the patch class.
/// </summary>
public record Medium(
    [property: Required, StringLength(50)] string Name,
    [property: Range(0, 150)] int Age,
    [property: JsonPropertyName("email_address"), EmailAddress] string? Email,
    DateTime? Birthday,
    decimal Balance,
    Guid Id,
    List<string>? Tags,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DayOfWeek Day,
    double Score,
    string? Notes);
