using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals.Sample;

public record PersonReadModel : PersonWriteModel
{
    public required int Id { get; init; }
}

public record PersonWriteModel
{
    [Required]
    [StringLength(255, MinimumLength = 3)]
    public required string Name { get; init; }
    
    public DateTime DateOfBirth { get; init; }
    
    [EmailAddress]
    public string? Email { get; init; }
    
    [JsonConverter(typeof(PhoneNumberJsonConverter))]
    public PhoneNumber? PhoneNumber { get; init; }
}

public record PhoneNumber(
    [StringLength(3, MinimumLength = 3)]
    string AreaCode,
    
    [StringLength(8, MinimumLength = 7)]
    string Number
);