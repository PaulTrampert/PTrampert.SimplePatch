using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

public class PatchObjectValidatorTest
{
    private JsonSerializerOptions Options { get; set; }
    
    [SetUp]
    public void Setup()
    {
        Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        Options.AddSimplePatchConverters();
    }

    [Test]
    public void IsValid_ReturnsValidationResults_WhenPropertiesAreInvalid()
    {
        var json = """
                   {
                       "id": -5,
                       "name_field": null,
                       "ignoredProp": "This should not be included"
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<IPatchObject<OptionalsBuilderTestObject>>(json, Options);

        var validationContext = new ValidationContext(result);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(result, validationContext, validationResults, true);
        
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False, "The object should not be valid due to validation errors.");
            Assert.That(validationResults.Count, Is.EqualTo(2), "There should be two validation errors.");
            
            Assert.That(validationResults[0].ErrorMessage, Is.EqualTo("The field Id must be between 1 and 100."));
            Assert.That(validationResults[1].ErrorMessage, Is.EqualTo("The Name field is required."));
        }
    }

    [Test]
    public void ValidationSkipsValuesThatHaveNoValue()
    {
        var json = """
                   {
                       "id": -5,
                       "ignoredProp": "This should not be included"
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<IPatchObject<OptionalsBuilderTestObject>>(json, Options);

        var validationContext = new ValidationContext(result);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(result, validationContext, validationResults, true);
        
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False, "The object should not be valid due to validation errors.");
            Assert.That(validationResults.Count, Is.EqualTo(1), "There should be one validation error.");
            
            Assert.That(validationResults[0].ErrorMessage, Is.EqualTo("The field Id must be between 1 and 100."));
        }
    }
}