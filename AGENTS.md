# AGENTS.md

Guidance for coding agents working in this repository. `CLAUDE.md` is a symlink to this file.

## What this is

PTrampert.SimplePatch is a .NET library for flat PATCH request bodies. It tells a property that was
omitted apart from one that was explicitly set to `null` or to a value. A controller takes
`[FromBody] IPatchObject<TWriteModel>`, and the library builds the concrete class at runtime.

## Layout

| Project | Target | Purpose |
| --- | --- | --- |
| `PTrampert.SimplePatch` | net8.0 | Core package: `Optional<T>`, `IPatchObject<T>`, `PatchClassBuilder`, JSON converters, validation |
| `PTrampert.SimplePatch.Schema` | net8.0 | `PatchSchemaTransform`: the OpenAPI schema rewrite shared by both integration packages |
| `PTrampert.SimplePatch.Swashbuckle` | net8.0 | Swashbuckle schema filter |
| `PTrampert.SimplePatch.OpenApi` | net10.0 | `Microsoft.AspNetCore.OpenApi` schema transformer. It is net10.0 only because it needs `GetOrCreateSchemaAsync`. |
| `*.Test` | match their subject | NUnit test projects, one per shipped package (except `Schema`, which the integration tests cover) |
| `PTrampert.SimplePatch.Sample` | net8.0 | Sample web API, not packed |

Docs are built with docfx (`docfx.json`, `index.md`, `docs/`). The API reference is generated
into `api/` from XML doc comments. `README.md` is packed into every NuGet package, so it is the
public face of the library on nuget.org.

## How it works

- `PatchClassBuilder.GetPatchClassFor(type)` generates C# source with CodeDom, compiles it with
  Roslyn into its own in-memory assembly, and caches the result in a **static** dictionary.
  `PatchClassBuilder.Instance` is the only instance to use. The public constructor is obsolete.
- The generated class has one `Optional<T>` property per patchable source property and a `Patch`
  method. Records are patched with a `with` expression. Other types go through constructor binding
  and an object initializer.
- Because the generated assembly is separate, it can only reference **public** types and public
  setters or init accessors. Non-public source types throw `NotSupportedException`.
- Source property attributes are carried over: `[JsonConverter]` becomes
  `[OptionalConverter]`, `[JsonPropertyName]` is copied, and each `ValidationAttribute` becomes an
  `[OptionalValidation(type, index)]` that runs only when the property is present.
- `JsonOptionsExtensions.AddSimplePatchConverters` registers `OptionalJsonConverterFactory` and
  `PatchJsonConverterFactory`.
- The OpenAPI packages build the patch schema from the **source model's** schema, not from the
  generated class, so the two contracts can't drift. See
  `docs/proposals/openapi-contract-generation.md` for the design record.

## Build and test

The SDK is pinned by `global.json` (10.0.x, rolling forward). Run from the repository root:

```bash
dotnet build
dotnet test
```

To build the docs, run `dotnet tool restore`, then `dotnet docfx docfx.json`. Output goes to
`_site/`, which git ignores.

CI (`.github/workflows/dotnet-library.yml`) uses a shared workflow from
`PaulTrampert/github-workflows` to build, test, and publish to NuGet on merge to `main`.

## Conventions

- **PR titles must start with `(MAJOR)`, `(MINOR)` or `(PATCH)`.** A CI check enforces this, and
  the prefix drives the released version. Use the form `(PATCH): Short imperative summary`.
  Choose the level by the change's effect on the public API of the shipped packages.
- PR descriptions explain cause, fix, and the alternatives that were rejected, and finish with test
  results. Reference the issue with `Closes #N`.
- Public API changes should be additive within a major version. Mark a member `[Obsolete]` with a
  link to the tracking issue before removing it.
- Every public member has XML doc comments. `GenerateDocumentationFile` is on, and the docs site is
  built from them.
- Comments explain *why*, especially constraints that aren't obvious from the code (for example,
  why the generated assembly can only see public types). Match the comment density of the
  surrounding code.
- Tests use NUnit 4 with the constraint model (`Assert.That`). Test-only types go in the test
  project's `TestObjects/` folder. Add a regression test that fails before the fix.
- Don't let the build's warning set grow.
- Keep `README.md` and `docs/` in step with behaviour changes.
- Git worktrees go under `.claude/worktrees/`, which git ignores.
