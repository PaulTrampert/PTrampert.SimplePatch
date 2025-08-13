# PTrampert.SimplePatch

Library supporting simple patch objects in .NET. 

## Overview

**PTrampert.SimplePatch** is a C# library designed to facilitate the handling of flat PATCH objects in .NET web applications. With this library, you can easily distinguish between properties that are omitted from a PATCH request and those explicitly set to `null` or a value.

This is especially useful when you want to update only specific properties of an object, leaving others unchanged. For example, if you send a PATCH request like:

```json
{
  "name": "New Name"
}
```

and your object contains additional properties besides `name`, PTrampert.SimplePatch makes it easy to ensure that only `name` is updated, and all other properties remain untouched.

## Features

- Supports "Optional" types for PATCH operations.
- Dynamically generates implementations of `IPatchObject` for your write models.
- Preserves validation attributes on properties, allowing for validation of PATCH requests.
- Handles complex types with custom JSON converters.

## Getting Started
A full sample project is available in the [PTrampert.SimplePatch.Samples](./PTrampert.SimplePatch.Sample)
* Install the library via NuGet:

```bash
dotnet add package PTrampert.SimplePatch
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
        // PTrampert.SimplePatch automatically generates an implementation of IPatchObjectFor<PersonWriteModel>
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
This will allow you to make the following HTTP request to update only the specified properties of `Person` (in this example, only the 'name' property).
```http
PATCH /person/1 HTTP/1.1
Content-Type: application/json
Content-Length: 24

{
  "name": "New Name"
}
```
