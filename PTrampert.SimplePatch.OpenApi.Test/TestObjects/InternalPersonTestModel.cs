using System.ComponentModel.DataAnnotations;

namespace PTrampert.SimplePatch.OpenApi.Test.TestObjects;

// Internal, to check that internal write models are patchable. The test project grants the
// generated assemblies access with InternalsVisibleTo in its project file.
internal record InternalPersonTestModel
{
    [StringLength(255, MinimumLength = 3)]
    public string? Name { get; init; }

    public string? Email { get; init; }
}
