namespace PTrampert.SimplePatch.Test.External;

// Internal, and this assembly grants no [InternalsVisibleTo] to EmitPatchClassBuilder's generated
// assemblies, so a patch class can't name it even when the source type's own assembly grants one.
internal enum ExternalInternalColor
{
    Red,
    Blue,
}
