using System.Runtime.CompilerServices;

// PatchClassBuilder's constructor is internal — PatchClassBuilder.Instance is the only builder
// consumers need. The tests construct builders directly to prove that separate instances still
// share one cache.
[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Test")]
