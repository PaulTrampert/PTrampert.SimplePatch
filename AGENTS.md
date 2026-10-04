# AGENTS.md

Guidance for coding agents working in this repository. `CLAUDE.md` is a symlink to this file, so
edit this one.

## What this is

PTrampert.SimplePatch is a .NET library for flat PATCH request bodies. It tells a property that was
omitted apart from one that was explicitly set to `null` or to a value. A controller takes
`[FromBody] IPatchObject<TWriteModel>`, and the library builds the concrete class at runtime.

## Layout

Every project targets `net10.0` only. Don't add another target framework without an issue for it.

| Project | Purpose |
| --- | --- |
| `PTrampert.SimplePatch` | Core package: `Optional<T>`, `IPatchObject<T>`, `PatchClassBuilder`, JSON converters, validation |
| `PTrampert.SimplePatch.Schema` | `PatchSchemaTransform`: the OpenAPI schema rewrite shared by both integration packages |
| `PTrampert.SimplePatch.Swashbuckle` | Swashbuckle schema filter |
| `PTrampert.SimplePatch.OpenApi` | `Microsoft.AspNetCore.OpenApi` schema transformer |
| `*.Test` | NUnit test projects, one per shipped package (except `Schema`, which the integration tests cover) |
| `PTrampert.SimplePatch.Test.External` | A second assembly for the core tests, holding non-public types they use from another assembly. Not packed. |
| `PTrampert.SimplePatch.Sample` | Sample web API, not packed |

Docs are built with docfx (`docfx.json`, `index.md`, `docs/`). The API reference is generated
into `api/` from XML doc comments. `README.md` is packed into every NuGet package, so it is the
public face of the library on nuget.org.

## How it works

- `PatchClassBuilder` is a `static class`. Its `Instance` is typed `IPatchClassBuilder` and returns
  the internal `EmitPatchClassBuilder.Instance` itself, which builds the patch class with
  Reflection.Emit, one dynamic assembly per source type, and caches the result in a **static**
  dictionary.
- `PatchClassModel` decides what the class contains. The generated class has one `Optional<T>`
  property per patchable source property and a `Patch` method. Records are patched by cloning, as
  a `with` expression does. Other types go through constructor binding and then the setters or init
  accessors.
- Because the generated assembly is separate, the runtime checks its access to the source type
  and every type and accessor it uses. Every such assembly is named `PTrampert.SimplePatch.Emitted`,
  so internal source types are supported when their assembly declares
  `[InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]`, as Castle DynamicProxy does. Private and
  protected nested types aren't supported, and throw `NotSupportedException`.
- Source property attributes are carried over: `[JsonConverter]` becomes
  `[OptionalConverter]`, `[JsonPropertyName]` is copied, and each `ValidationAttribute` becomes an
  `[OptionalValidation(type, index)]` that runs only when the property is present.
- Nothing on the runtime path reads assembly files from disk, so single-file publishing works.
  Native AOT doesn't, because the library generates code at runtime.
- The internal `RoslynPatchClassBuilder` (CodeDom source compiled with Roslyn, public source types
  only) is **unused at runtime** but kept, and still tested, pending #144, which decides whether it
  becomes a compile-time source generator. It is why the core package still references
  `Microsoft.CodeAnalysis.CSharp` and `System.CodeDom`. `PatchClassBuilderTest` runs against both
  builders directly, so they can't drift apart; `PatchClassBuilderDelegationTest` covers the public
  entry point.
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
- Breaking changes are staged on a `release/<major>` branch (for example `release/2.0`). Their PRs
  target that branch and are squash-merged like any other. The release PR from `release/<major>`
  into `main` is the one PR merged with **Rebase and merge**, never squashed: the release notes
  list the commit subjects since the previous tag, so squashing it would collapse them to one line.
  Title the release PR `(MAJOR): Release <major>`, and model its description on #147:
  - an `> [!IMPORTANT]` note at the top saying to merge it with **Rebase and merge**;
  - **Included:** each PR merged into the branch, with the issues it closes. Merging into a branch
    other than `main` doesn't close issues, so this PR closes them. Give every issue its own keyword
    (`closes #1, closes #2`), because GitHub links only the first number after a keyword;
  - **Breaking changes:** what consumers have to change;
  - **Branch state:** commits behind `main`, merge commits, and the version the commits produce;
  - anything postponed to after the release, with where its open questions are recorded;
  - **Tests.**
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
- Name branches `bugfix/<issue>-<slug>` for bugs and `feature/<issue>-<slug>` otherwise. Never commit
  directly to `main`.
- Deliver one issue per PR, small enough for a reviewer to hold in their head.
- An agent never merges a PR unless the user directly asks it to.

## Worktrees

- **Worktrees go under `.claude/worktrees/` inside the primary checkout**, which git ignores.
  Never create one beside the checkout. Use
  `git worktree add .claude/worktrees/<name> -b <branch> origin/main`. `EnterWorktree` and
  sub-agent isolation already put them there. Remove a worktree with `git worktree remove` once
  its branch is merged.
- **A top-level agent** creates its own worktree before making any edits and works there, not in
  the primary checkout.
- **A sub-agent** works in the worktree it was handed and doesn't provision another. A sub-agent of
  a worktree-isolated session usually can't use a new worktree anyway.
- **Never switch the branch of a worktree you didn't create.** If a worktree is on the wrong
  branch for your task, say so and stop. Check `git branch --show-current`, not the directory name.
- **When fanning work out across several issues**, the fanning agent creates one worktree per
  issue up front, each already on the right branch. It then hands each sub-agent a path that
  already exists.

## Repository metadata

- `CLAUDE.md` is a symlink to this file. Edit `AGENTS.md`, and never replace the symlink with a
  copy.
- `.claude/commands/implement-unblocked.md` is the `/implement-unblocked` slash command. It takes
  every open, unassigned issue that meets all of these conditions:
  - no open blocker;
  - no open PR that will close it;
  - no `needs decision` or `Breaking Change` label.

  It assigns each issue to the `gh` user and moves it to *In Progress*. It then fans the batch out
  to sub-agents, one worktree and one PR per issue, following [Worktrees](#worktrees).
