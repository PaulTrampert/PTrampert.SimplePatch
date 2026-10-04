using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace PTrampert.SimplePatch.Benchmarks;

/// <summary>
/// The cost of the first patch class a process generates, which includes loading and
/// JIT-compiling the builder and its dependencies. In an app this lands on the first PATCH request
/// or OpenAPI document that needs a patch class.
/// </summary>
/// <remarks>
/// <see cref="RunStrategy.ColdStart"/> with one invocation per launch measures exactly one call in
/// a fresh process; the launches give the spread.
/// </remarks>
[SimpleJob(RunStrategy.ColdStart, launchCount: 10, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
public class ColdGenerationBenchmarks
{
    /// <summary>Generates the first patch class with <c>RoslynPatchClassBuilder</c>, the default.</summary>
    [Benchmark(Baseline = true)]
    public Type Roslyn() => RoslynPatchClassBuilder.CreatePatchClass(typeof(TestObjects.Small));

    /// <summary>Generates the first patch class with the experimental <c>EmitPatchClassBuilder</c>.</summary>
    [Benchmark]
    public Type Emit() => EmitPatchClassBuilder.CreatePatchClass(typeof(TestObjects.Small));
}
