using BenchmarkDotNet.Attributes;

namespace PTrampert.SimplePatch.Benchmarks;

/// <summary>
/// The steady-state cost of generating one patch class, once each builder's own code and its
/// dependencies (Roslyn, CodeDom, Reflection.Emit) are loaded and JIT-compiled.
/// </summary>
/// <remarks>
/// Calls the builders' uncached <c>CreatePatchClass</c>, because <c>GetPatchClassFor</c> would
/// return the cached class after the first call. Every invocation loads another assembly that is
/// never unloaded, which is fine for a benchmark process. The iteration counts are fixed so a CI
/// run takes a few minutes; BenchmarkDotNet's defaults would let Roslyn's slow cases run far longer.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 15)]
public class WarmGenerationBenchmarks
{
    private Type _type = null!;

    /// <summary>The source type to generate a patch class for.</summary>
    [Params(SourceType.Small, SourceType.Medium, SourceType.Large)]
    public SourceType SourceType { get; set; }

    /// <summary>Resolves <see cref="SourceType"/> outside the measured code.</summary>
    [GlobalSetup]
    public void Setup() => _type = SourceType.ToType();

    /// <summary>Generates the patch class with <c>RoslynPatchClassBuilder</c>, the default.</summary>
    [Benchmark(Baseline = true)]
    public Type Roslyn() => RoslynPatchClassBuilder.CreatePatchClass(_type);

    /// <summary>Generates the patch class with the experimental <c>EmitPatchClassBuilder</c>.</summary>
    [Benchmark]
    public Type Emit() => EmitPatchClassBuilder.CreatePatchClass(_type);
}
