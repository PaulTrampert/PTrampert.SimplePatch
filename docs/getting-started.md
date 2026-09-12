
# Getting Started with PTrampert.SimplePatch

This guide will help you quickly integrate PTrampert.SimplePatch into your .NET project and start building PATCH endpoints for your RESTful APIs. PTrampert.SimplePatch generates patch objects at runtime, so you only need to define your write model and use the provided interfaces and converters.

## Installation

Add the NuGet package to your project:

```sh
dotnet add package PTrampert.SimplePatch
```

## Basic Usage

1. **Register JSON Converters**

   During application startup, register the SimplePatch converters using the provided extension method:

   ```csharp
   builder.Services.AddControllers()
      .AddJsonOptions(options => options.JsonSerializerOptions.AddSimplePatchConverters());
   ```

2. **Define Your Write Model**

   Create your write model as you normally would, using validation and deserialization attributes as needed:

   ```csharp
   public class PersonWriteModel
   {
      [Required]
      public string FirstName { get; set; }

      [Required]
      public string LastName { get; set; }

      public int Age { get; set; }
   }
   ```

3. **Define Your PATCH Route**

   In your controller, define a PATCH endpoint that takes `IPatchObject<WriteModel>` as the request body. The patch object will be generated at runtime and only contain the properties sent by the client.

   ```csharp
   [HttpPatch("/api/people/{id}")]
   public IActionResult PatchPerson(int id, [FromBody] IPatchObject<PersonWriteModel> patch)
   {
      var person = db.People.Find(id);
      if (person == null) return NotFound();

      // Apply the patch and get the updated model
      var updatedPerson = patch.Patch(person);
      db.People.Update(updatedPerson);
      db.SaveChanges();
      return Ok(updatedPerson);
   }
   ```

4. **Document the Endpoint in OpenAPI**

   An OpenAPI generator describes the `IPatchObject<T>` parameter from the interface, which has no
   properties, so the request body is documented as an empty object. Add the integration package
   for your generator to document the patched model's schema instead, with every property
   optional:

   ```sh
   dotnet add package PTrampert.SimplePatch.Swashbuckle
   ```

   ```csharp
   builder.Services.AddSwaggerGen(options => options.AddSimplePatchSchemas(patch =>
   {
      // Swagger UI's generated example lists every property, which reads as "send all of these".
      // An explicit example shows the partial-update semantics instead.
      patch.Example = _ => new JsonObject { ["firstName"] = "New Name" };
   }));
   ```

   On .NET 10 and later, `PTrampert.SimplePatch.OpenApi` does the same for the built-in generator:

   ```csharp
   builder.Services.AddOpenApi(options => options.AddSimplePatchSchemas());
   ```

   See the [README](../README.md#openapi) for the full set of options.
