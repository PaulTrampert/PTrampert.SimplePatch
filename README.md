# PTrampert.Optionals

Library supporting "Optional" objects, distinct from nullable. Possibly useful for PATCH routes.

## Overview

**PTrampert.Optionals** is a C# library designed to facilitate the handling of flat PATCH objects in .NET web applications. With this library, you can easily distinguish between properties that are omitted from a PATCH request and those explicitly set to `null` or a value.

This is especially useful when you want to update only specific properties of an object, leaving others unchanged. For example, if you send a PATCH request like:

```json
{
  "name": "New Name"
}
```

and your object contains additional properties besides `name`, PTrampert.Optionals makes it easy to ensure that only `name` is updated, and all other properties remain untouched.

## Features

- Supports "Optional" types for PATCH operations.
- Distinguishes between omitted properties and properties set to `null`.
- Designed for use in .NET web APIs.
- Streamlines partial updates to complex objects.

## Getting Started
A full sample project is available in the [PTrampert.Optionals.Samples](./PTrampert.Optionals.Sample)
* Install the library via NuGet:

```bash
dotnet add package PTrampert.Optionals
```
* Create your write model class
```csharp
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
```
* Use `IPatchObjectFor<PersonWriteModel>` to create an optional object for PATCH operations:

```csharp
    [HttpPatch("{id:int}")]
    public ActionResult<PersonReadModel> PatchPerson(
        int id,
        // PTrampert.Optionals automatically generates an implementation of IPatchObjectFor<PersonWriteModel>
        [FromBody] IPatchObjectFor<PersonWriteModel> patchObject)
    {
        // Validation is preserved on the patch object, so we can check ModelState
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        var existingPerson = _db.GetPersonById(id);
        if (existingPerson == null)
            return NotFound();
        
        var patchedPerson = patchObject.Patch(existingPerson);
        var updatedPerson = _db.UpdatePerson(id, patchedPerson);
        
        return updatedPerson!;
    }
```