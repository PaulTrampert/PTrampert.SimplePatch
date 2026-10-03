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
- Documents PATCH routes properly in OpenAPI, via optional Swashbuckle and
  `Microsoft.AspNetCore.OpenApi` integration packages.

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
* Use `IPatchObject<PersonWriteModel>` to create an optional object for PATCH operations:

```csharp
    [HttpPatch("{id:int}")]
    public ActionResult<PersonReadModel> PatchPerson(
        int id,
        // PTrampert.SimplePatch automatically generates an implementation of IPatchObject<PersonWriteModel>
        [FromBody] IPatchObject<PersonWriteModel> patchObject)
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

### Sending patches from .NET

`AddSimplePatchConverters()` also makes serialization omit every `Optional<T>` property that has no
value, so `Optional<T>` bodies are safe to send from a client too. An unset property is left out, an
explicit `null` is written as `null`, and any other value (including `0` or `false`) is written as is:

```csharp
public record PersonPatch
{
    public Optional<string> Name { get; init; }
    public Optional<string?> Email { get; init; }
    public Optional<DateTime> DateOfBirth { get; init; }
}

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
options.AddSimplePatchConverters();

JsonSerializer.Serialize(new PersonPatch { Name = "New Name", Email = null }, options);
// {"name":"New Name","email":null}
```

No `DefaultIgnoreCondition` is needed for this, so it doesn't affect your other types.

### Internal write models (experimental)

By default the patch class is compiled with Roslyn into its own assembly, which can only see public
types, so a write model must be public and `IPatchObject<T>` throws `NotSupportedException` for
anything else. To use `internal` write models, opt in to the experimental Reflection.Emit builder,
and let the generated assemblies see your internals:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions
        .AddSimplePatchConverters(useExperimentalDynamicClassBuilder: true));
```

```csharp
// In the assembly that declares the write models.
[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]
```

- The choice applies to the **whole process**, not just to the options you pass, so the OpenAPI
  integrations use the same builder. Once turned on, it stays on: passing `false`, or calling
  `AddSimplePatchConverters()`, doesn't turn it off.
- `private` and `protected` nested types are still not supported, because `InternalsVisibleTo`
  doesn't reach them.
- Strong-named assemblies can't name the unsigned `PTrampert.SimplePatch.Emitted` as a friend yet.

## OpenAPI

Out of the box, an OpenAPI generator describes a `[FromBody] IPatchObject<T>` parameter from the
interface, which has no properties. The request body ends up documented as an empty object —
useless to a reader, and strict enough to reject every legal PATCH:

```json
"PersonWriteModelIPatchObject": { "type": "object", "additionalProperties": false }
```

Install the integration package for your generator to document the patched model's schema instead,
with every property optional.

### Swashbuckle

```bash
dotnet add package PTrampert.SimplePatch.Swashbuckle
```

```csharp
builder.Services.AddSwaggerGen(options => options.AddSimplePatchSchemas());
```

### Microsoft.AspNetCore.OpenApi (.NET 10+)

```bash
dotnet add package PTrampert.SimplePatch.OpenApi
```

```csharp
builder.Services.AddOpenApi(options => options.AddSimplePatchSchemas());
```

Either way the PATCH body is now described with the model's own properties, validation constraints
and converters, and nothing marked required:

```json
"PersonWriteModelIPatchObject": {
  "type": "object",
  "properties": {
    "name":        { "maxLength": 255, "minLength": 3, "type": "string" },
    "dateOfBirth": { "type": "string", "format": "date-time" },
    "email":       { "type": "string", "format": "email", "nullable": true },
    "phoneNumber": { "$ref": "#/components/schemas/PhoneNumber" }
  },
  "additionalProperties": false,
  "description": "Partial update of PersonWriteModel. Omitted properties are left unchanged."
}
```

Because the patch schema is derived from the model's own schema, anything you already do to tune
that model — a `MapType`, a schema filter, XML comments — applies to the PATCH body too, and the
two can never describe different shapes.

### Setting an example

Swagger UI builds its example from the schema's properties and ignores `required`, so by default
the PATCH example lists *every* property — which reads as "send all of these", and is what
**Try it out** will submit. Supply an example to show the partial-update semantics instead:

```csharp
builder.Services.AddSwaggerGen(options => options.AddSimplePatchSchemas(patch =>
{
    patch.Example = _ => new JsonObject { ["name"] = "New Name" };
}));
```

### Other options

`AddSimplePatchSchemas` takes a `SimplePatchSchemaOptions`:

| Option | Default | Effect |
| --- | --- | --- |
| `Example` | null | Example body for the patch schema. See above. |
| `SchemaId` | null | Names the patch schema component. `t => t.Name + "Patch"` gives `PersonWriteModelPatch` instead of `PersonWriteModelIPatchObject`. |
| `DescriptionFormat` | `"Partial update of {0}. Omitted properties are left unchanged."` | The patch schema's description. Null leaves it alone. |
| `ClearRequired` | true | Drops `required`, which is what makes the body a partial update. |

#### `SchemaId` and your own schema-id selector

With the built-in generator, `SchemaId` takes effect whatever `CreateSchemaReferenceId` you set,
before or after `AddSimplePatchSchemas`.

Swashbuckle picks a schema's id before any filter runs, so the `SwaggerGenOptions` extension
applies `SchemaId` by wrapping whichever selector is in place when it is called. A
`CustomSchemaIds` call made *after* it replaces the wrapper and silently drops `SchemaId`. Either
call `AddSimplePatchSchemas` after `CustomSchemaIds`, or use the service-collection overload,
which applies `SchemaId` after all of your configuration regardless of order:

```csharp
builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(t => t.Name));
builder.Services.AddSimplePatchSchemas(patch => patch.SchemaId = t => t.Name + "Patch");
```

Use one overload or the other, not both.
