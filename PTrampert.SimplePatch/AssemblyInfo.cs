using System.Runtime.CompilerServices;

// PatchSchemaTransform is shared with the OpenAPI integration packages, which reference this one.
// It stays internal: it is an implementation detail of those packages, not public API here.
[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Swashbuckle")]
[assembly: InternalsVisibleTo("PTrampert.SimplePatch.OpenApi")]
