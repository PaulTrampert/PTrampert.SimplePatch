# Proposal: OpenAPI contract generation for `IPatchObject<T>`

Status: implemented — see `PTrampert.SimplePatch.Swashbuckle` and `PTrampert.SimplePatch.OpenApi`.
This document is kept as the record of the design decision and the alternatives weighed.
Applies to: `PTrampert.SimplePatch` 1.x

One thing changed during implementation: the built-in-OpenAPI package targets **net10.0 only**, not
net9.0 as proposed in §3. Obtaining the source model's schema from a transformer needs
`OpenApiSchemaTransformerContext.GetOrCreateSchemaAsync`, which .NET 10 added and .NET 9 does not
have. Supporting .NET 9 would need a different mechanism, and .NET 9's OpenAPI.NET 1.6 object model
would also fork the shared transform. Swashbuckle covers .NET 8 and 9 in the meantime.

## 1. The problem

A route that takes `[FromBody] IPatchObject<PersonWriteModel>` is documented from the
*interface*. Neither Swashbuckle nor the built-in `Microsoft.AspNetCore.OpenApi`
generator knows about the class `PatchClassBuilder` emits at runtime, and the interface
has no properties — only a `Patch` method. The sample app today emits:

```json
"PersonWriteModelIPatchObject": {
  "type": "object",
  "additionalProperties": false
}
```

This is worse than merely uninformative. `additionalProperties: false` with no
`properties` describes a body that permits **no** members at all, so a strict validator,
a contract test, or a generated client will reject every legal PATCH request. Generated
clients get a marker class with no members; consumers have to read the source to learn
what they may send.

The desired document for that same route is the write model's schema with PATCH
semantics applied:

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

## 2. The central design decision

There are two ways to produce that schema, and the choice drives everything else.

**Option A — describe the generated patch class.** Hand `PatchClassBuilder`'s runtime
type to the schema generator and teach it to unwrap `Optional<T>`.

**Option B — derive the schema from the source model `T`.** Ask the generator for the
schema it already produces for `PersonWriteModel`, then apply the patch transform:
drop `required`, drop properties the patch class cannot set, add a description.

**This proposal recommends Option B.** Option A has to re-solve four problems that the
generator has already solved correctly for the source model:

| Concern | Option A | Option B |
| --- | --- | --- |
| `Optional<T>` properties | documents as `{ hasValue, value }` unless separately unwrapped, in *both* generators | never seen |
| Schema id | `PersonWriteModel_Optionals_xj1k2h3g` — the random name from `Path.GetRandomFileName()` leaks into the public contract, and changes every run | derived from `T` |
| Validation constraints | `PatchClassBuilder` rewrites `[StringLength]` into `[OptionalValidation(typeof(StringLengthAttribute))]`, which no schema generator recognizes, so `minLength`/`maxLength`/`format` are all lost | inherited from `T`'s schema |
| `[JsonConverter]` properties | the generated property carries `[OptionalConverter]`, not the original converter | inherited from `T`'s schema |

Option B also composes. If a consumer already tunes their write model's schema — a
`MapType<PhoneNumber>`, a schema filter, an XML comment — the patch schema picks the
tuning up for free, because it is built from that same schema. That coupling is
semantically right: *the patch contract for a model is the model's contract with
optional members*, so the two should never be able to drift.

The one thing Option B must still consult the generated class for is **which properties
are patchable**. `PatchClassBuilder` skips properties where `CanWrite` is false, but
the source model's schema includes them (marked `readOnly`). So the transform
intersects the source schema's properties with the patch class's JSON property names,
resolved through the application's own `JsonSerializerOptions` so that any naming policy
is respected.

## 3. Packaging

Keep the core package free of any OpenAPI dependency and ship two integration packages,
each referencing it:

| Package | Target | Depends on | Hook |
| --- | --- | --- | --- |
| `PTrampert.SimplePatch.Swashbuckle` | net8.0 | `Swashbuckle.AspNetCore.SwaggerGen` | `ISchemaFilter` |
| `PTrampert.SimplePatch.OpenApi` | net9.0, net10.0 | `Microsoft.AspNetCore.OpenApi` | `IOpenApiSchemaTransformer` |

Two packages rather than one multi-targeted package, because Swashbuckle 10 moved from
Microsoft.OpenApi 1.6.x to 2.x, where `OpenApiSchema` became `IOpenApiSchema`,
`ISchemaFilter.Apply` takes `IOpenApiSchema`, and a `$ref` is an `OpenApiSchemaReference`
rather than a schema with a `Reference` property. Supporting Swashbuckle 6–9 as well
means `#if`-ing across that split; a separate package can simply declare the range it
supports — recommendation is to target Swashbuckle 10 only, matching the sample.
Both integration packages are small (roughly 50 lines each), so duplicating the
transform logic between them costs less than straddling the API break.

What the integration packages share through that reference is `SimplePatchSchemaOptions`,
which needs nothing but `System.Text.Json`. The transform itself cannot be shared the same
way: it takes `Microsoft.OpenApi` types, so hosting it in the core package would hand a
`Microsoft.OpenApi` dependency to every consumer of `PTrampert.SimplePatch`, OpenAPI user
or not. It is therefore duplicated — about a dozen mechanical lines in each package, kept
in step by mirrored test suites. A third package holding just the transform would restore
the single copy, at the cost of one more thing to version and publish; not worth it at this
size.

Consumers opt in with a single call:

```csharp
// Swashbuckle
builder.Services.AddSwaggerGen(o => o.AddSimplePatchSchemas());

// .NET 9+ built-in
builder.Services.AddOpenApi(o => o.AddSimplePatchSchemas());
```

with an options overload for the tuneable parts:

```csharp
public sealed class SimplePatchSchemaOptions
{
    /// <summary>Names the patch schema. Default: leave the generator's id alone.</summary>
    public Func<Type, string>? SchemaId { get; set; }

    /// <summary>Format string for the schema description; {0} is the source type name.
    /// Set to null to leave the description alone.</summary>
    public string? DescriptionFormat { get; set; }
        = "Partial update of {0}. Omitted properties are left unchanged.";

    /// <summary>Clear <c>required</c> on the patch schema. Default true.</summary>
    public bool ClearRequired { get; set; } = true;

    /// <summary>Supplies the example body for a patch schema. Default null, which lets
    /// the UI synthesize one from the properties — see §7.</summary>
    public Func<Type, JsonNode?>? Example { get; set; }
}
```

## 4. Changes required in the core package

These are prerequisites, not nice-to-haves.

1. **Share the patch class cache.** `PatchJsonConverterFactory` news up its own private
   `PatchClassBuilder`. An OpenAPI integration that news up a second one compiles a
   *second* dynamic assembly for the same source type — wasted Roslyn work at startup,
   and two types that are structurally identical but not reference-equal. Make the
   `_optionalsClasses` cache `static`, or expose a `PatchClassBuilder.Shared` singleton
   and have both the converter factory and the integrations use it.

2. **Make the patch-type reflection helpers public.** `TypeExtensions.IsPatchObjectType`
   and `GetPatchObjectType` are `internal`, and they answer a slightly different
   question than the integrations ask — they test whether a *concrete* type implements
   `IPatchObject<>`, while a schema filter is handed the open interface
   `IPatchObject<PersonWriteModel>` itself. Add and make public:

   ```csharp
   public static bool TryGetPatchSourceType(this Type type, [NotNullWhen(true)] out Type? sourceType);
   ```

   returning true for both `IPatchObject<T>` and any concrete implementation of it.

3. **Fix `PatchClassBuilder` for types in the global namespace.** Found while
   prototyping: when `type.Namespace` is null the generated namespace is `".Optionals"`,
   and compilation fails with `error CS1001: Identifier expected`. Minimal-API apps
   routinely declare models in `Program.cs`, so this is hit immediately by the audience
   most likely to adopt the built-in OpenAPI integration. Fall back to a fixed
   namespace such as `SimplePatch.Optionals` when `Namespace` is null. This is an
   independent bug and could be fixed ahead of this work.

## 5. Implementation

Both implementations below were prototyped against this repository's sample app and
produce the verified output in §6.

### 5.1 Swashbuckle (`ISchemaFilter`)

```csharp
public sealed class PatchObjectSchemaFilter(
    IOptions<JsonOptions> jsonOptions,
    SimplePatchSchemaOptions options) : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema target) return;
        if (!context.Type.TryGetPatchSourceType(out var sourceType)) return;

        // Generate (and register) the source model's schema, then resolve the $ref.
        var generated = context.SchemaGenerator.GenerateSchema(sourceType, context.SchemaRepository);
        if (Resolve(generated, context.SchemaRepository) is not { } source) return;

        var patchType = PatchClassBuilder.Shared.GetPatchClassFor(sourceType);
        var patchable = jsonOptions.Value.JsonSerializerOptions
            .GetTypeInfo(patchType).Properties
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        target.Type = source.Type;
        target.AdditionalPropertiesAllowed = source.AdditionalPropertiesAllowed;
        target.Properties = source.Properties?
            .Where(kvp => patchable.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (options.ClearRequired) target.Required = null;
        if (options.DescriptionFormat is { } format)
            target.Description = string.Format(format, sourceType.Name);
    }

    private static IOpenApiSchema? Resolve(IOpenApiSchema schema, SchemaRepository repository) =>
        schema is OpenApiSchemaReference reference
            ? repository.Schemas.GetValueOrDefault(reference.Reference.Id)
            : schema;
}
```

A schema filter mutates the schema in place and cannot swap a `$ref` for another, which
is exactly why the transform copies members across rather than returning the source
schema. Property values are copied by reference, so `phoneNumber` keeps pointing at the
shared `#/components/schemas/PhoneNumber` component rather than being duplicated.

### 5.2 Built-in OpenAPI, .NET 9+ (`IOpenApiSchemaTransformer`)

```csharp
public sealed class PatchObjectSchemaTransformer(SimplePatchSchemaOptions options)
    : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.JsonTypeInfo.Type.TryGetPatchSourceType(out var sourceType)) return;

        var source = await context.GetOrCreateSchemaAsync(sourceType, cancellationToken: cancellationToken);
        var patchType = PatchClassBuilder.Shared.GetPatchClassFor(sourceType);
        var patchable = /* same JsonTypeInfo lookup as above */;

        schema.Type = source.Type;
        schema.Properties = source.Properties?
            .Where(kvp => patchable.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (options.ClearRequired) schema.Required = null;
        if (options.DescriptionFormat is { } format)
            schema.Description = string.Format(format, sourceType.Name);
    }
}
```

The two differ only in how the source schema is obtained and in the OpenAPI object
model's mutability rules; the transform itself is identical, and each package carries its
own copy as a private method — see §3 for why it is not shared through the core package.
Their test suites mirror each other so that a change to one copy and not the other shows
up as a failure.

## 6. Verified output

Prototyped against `PTrampert.SimplePatch.Sample` (Swashbuckle 10.2.3, Microsoft.OpenApi
2.7.5) and against a minimal-API app on .NET 10 using `AddOpenApi`.

Before — Swashbuckle:

```json
"PersonWriteModelIPatchObject": { "type": "object", "additionalProperties": false }
```

After — Swashbuckle, with a get-only `Display` property added to `PersonWriteModel` to
exercise the patchable-property filter:

```json
"PersonWriteModel": {
  "required": ["name"],
  "type": "object",
  "properties": {
    "name":        { "maxLength": 255, "minLength": 3, "type": "string" },
    "dateOfBirth": { "type": "string", "format": "date-time" },
    "display":     { "type": "string", "nullable": true, "readOnly": true },
    "email":       { "type": "string", "format": "email", "nullable": true },
    "phoneNumber": { "$ref": "#/components/schemas/PhoneNumber" }
  },
  "additionalProperties": false
},
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

`required` is gone, the read-only `display` is gone, validation constraints and the
`$ref` to the shared `PhoneNumber` component survive.

After — .NET 10 `AddOpenApi` (OpenAPI 3.1):

```json
"IPatchObjectOfPersonWriteModel": {
  "type": "object",
  "properties": {
    "name":        { "maxLength": 255, "minLength": 3, "type": "string" },
    "dateOfBirth": { "type": "string", "format": "date-time" },
    "email":       { "type": ["null", "string"] }
  },
  "description": "Partial update of PersonWriteModel. Omitted properties are left unchanged."
}
```

## 7. Swagger UI

Swagger UI renders entirely from the emitted document, so it needs no integration of its
own — fixing the schema fixes the UI. Verified by driving the sample app's `/swagger`
page in a headless browser, before and after the filter.

### Schema tab

Today the PATCH request body renders as an empty model:

```
PersonWriteModelIPatchObject {
}
```

With the filter it renders the full model, including the description, the
`maxLength`/`minLength` constraints on `name`, `string($email)` and `nullable: true` on
`email`, and a link through to the shared `PhoneNumber` model.

The detail that matters most for usability: `name` renders **without** the red `*`
required marker, while the PUT operation — built from the same `PersonWriteModel` —
still renders `name*`. A reader comparing the two operations in the UI can see at a
glance that PATCH accepts a subset. That is exactly the signal §8 argues for, and it
comes for free from clearing `required`.

### Example Value tab and "Try it out"

Today the example is `{}`, so "Try it out" prefills an empty body and the user has to
hand-write the JSON from knowledge the document does not contain. After the fix the
example is a real body.

**One wrinkle, and it is worth planning for.** Swagger UI synthesizes its example from
`properties` and ignores `required`, so the generated PATCH example lists *every*
property:

```json
{
  "name": "string",
  "dateOfBirth": "2026-09-11T03:04:47.930Z",
  "email": "user@example.com",
  "phoneNumber": { "areaCode": "string", "number": "string" }
}
```

For a PUT that is correct. For a PATCH it reads as "send all of these", which is the
opposite of the point, and a user who clicks Try it out will send a full replacement.
Nothing in the schema can prevent that — it is how Swagger UI builds examples for any
object schema.

The mitigation is to set `Example` on the patch schema, which Swagger UI honours over
its synthesized one. Verified: setting `target.Example` to a single-property object
makes both the Example Value tab and the Try it out prefill show just that property.

This argues for a fourth option:

```csharp
/// <summary>Supplies the example body for a patch schema. Return null to let the
/// UI synthesize one from the properties (which will list all of them).</summary>
public Func<Type, JsonNode?>? Example { get; set; }
```

Recommendation: ship it **off by default**, because any single-property example the
library synthesizes has to pick a property arbitrarily, and a wrong-looking example is
worse than a verbose one. Document the one-liner prominently instead — this is the
first thing a consumer will want to tune, and it is the only part of the Swagger UI
experience the schema alone cannot get right.

### Caveats

* The above is the Swashbuckle path, which emits OpenAPI 3.0.4.
* The built-in .NET 9+ generator emits 3.1, and Swagger UI can be pointed at that
  document instead. The bundle Swashbuckle 10.2.3 ships is 3.1-aware, so the same
  rendering applies; an older pinned `swagger-ui` may not be.
* Redoc, Scalar, and generated clients read the same document, so they benefit
  identically. None of them were tested.

## 8. Semantics worth stating explicitly

* **Omitted vs. null.** OpenAPI has no vocabulary for "absent means unchanged" beyond
  leaving a property out of `required`; that is precisely what the transform produces.
  The `description` carries the rest of the meaning for human readers. There is no
  need for `nullable: true` on every property — that would wrongly tell clients they
  may send `null` for a non-nullable member.
* **Nullability is inherited, not widened.** `name` stays non-nullable, so
  `{"name": null}` remains invalid in the document, matching the runtime behaviour
  where the `[Required]` validator runs whenever the property is present.
* **`additionalProperties: false` is inherited and is correct here** — a PATCH body
  should still reject members the model does not define.
* **Inaccuracies in the source schema are inherited too.** The sample's `PhoneNumber`
  serializes through a custom converter to the string `"(123)456-7890"`, but its schema
  documents an object, because Swashbuckle cannot see through a `JsonConverter`. The
  patch schema reproduces that, which is the right behaviour: fixing it once on the
  write model (`o.MapType<PhoneNumber>(() => new OpenApiSchema { Type = "string" })`)
  fixes both PUT and PATCH. Worth calling out in the docs so it is not mistaken for a
  SimplePatch bug.

## 9. Open questions

1. **Schema id.** `PersonWriteModelIPatchObject` (Swashbuckle) and
   `IPatchObjectOfPersonWriteModel` (built-in) are both serviceable but leak the
   interface name into generated client types. Should `AddSimplePatchSchemas` default
   to renaming these to `PersonWriteModelPatch`? Renaming means wrapping the
   `SchemaIdSelector` / `CreateSchemaReferenceId` in the consumer's options, which is a
   little more intrusive than a filter. Recommendation: ship it off by default,
   `options.SchemaId = t => t.Name + "Patch"` to enable.
2. **NSwag.** The same transform fits NSwag's `ISchemaProcessor`. Worth a third package
   only if there is demand; the proposal does not include it.
3. **Source generator (longer term).** A generator that emits
   `PersonWriteModelPatch : IPatchObject<PersonWriteModel>` at compile time would make
   correct OpenAPI the default for anyone who declares the concrete type as the
   parameter, and would also buy trimming/AOT support and remove Roslyn from the
   runtime dependency set. The filter/transformer proposed here stays useful for
   interface-typed parameters, so the two are complementary and this proposal does not
   block that direction.

## 10. Plan

1. Core: shared `PatchClassBuilder` cache, public `TryGetPatchSourceType`, global
   namespace fix (§4). Unit tests for each.
2. `PTrampert.SimplePatch.Swashbuckle` with `AddSimplePatchSchemas` and
   `SimplePatchSchemaOptions`. Tests drive the filter over a real `SchemaRepository`
   and assert the emitted schema, including the read-only and `[JsonIgnore]` cases,
   and that `Example` reaches the emitted document when supplied.
3. `PTrampert.SimplePatch.OpenApi` for net9.0/net10.0, same tests against a
   `WebApplicationFactory` fetching `/openapi/v1.json`.
4. Wire the sample app to the Swashbuckle package, add an end-to-end assertion on the
   PATCH route's request body schema, and document the setup in
   `docs/getting-started.md` — including the `Example` one-liner, since the Swagger UI
   example is the one part of the experience the schema alone cannot get right (§7).
