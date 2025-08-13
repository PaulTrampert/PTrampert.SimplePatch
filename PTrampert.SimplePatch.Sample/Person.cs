using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch.Sample;

public record PersonReadModel : PersonWriteModel
{
    public required int Id { get; init; }
}

public record PersonWriteModel
{
    // For PATCH requests, Required will only be enforced if the property is present in the request body.
    [Required]
    [StringLength(255, MinimumLength = 3)]
    public required string Name { get; init; }
    
    public DateTime DateOfBirth { get; init; }
    
    // Validation attributes can be used to enforce rules on the email field.
    [EmailAddress]
    public string? Email { get; init; }
    
    // Using a custom JSON converter to handle phone number serialization and deserialization
    [JsonConverter(typeof(PhoneNumberJsonConverter))]
    public PhoneNumber? PhoneNumber { get; init; }
}

public record PhoneNumber(
    [StringLength(3, MinimumLength = 3)]
    string AreaCode,
    
    [StringLength(8, MinimumLength = 7)]
    string Number
);