# Comparing benchmark results

BenchmarkDotNet is most useful when alternatives are measured side-by-side under controlled conditions.

## Strategy 1: Side-by-side methods

When old/new implementations can coexist, define separate benchmark methods and mark one baseline:

```csharp
[Benchmark(Baseline = true)]
public int Baseline() => ExistingImplementation();

[Benchmark]
public int Candidate() => CandidateImplementation();
```

## Strategy 2: Comparing runtimes

Use multiple target frameworks/jobs only when runtime version is the comparison axis. Multi-runtime BDN runs require the benchmark project to target all compared TFMs.

## Strategy 3: Comparing NuGet package versions

Use an MSBuild property or repository-supported mechanism to vary dependency version per job. Preserve the repository's current dependency-versioning model rather than introducing a new one merely for a benchmark.

## Strategy 4: Saved build vs current source

For before/after comparisons without published packages, a saved baseline DLL can be referenced by one BDN job while another job references current source. Keep namespace/API compatibility identical so the benchmark itself is not changing between jobs.

## Strategy 5: Runtime configuration

Use multiple jobs to compare GC/JIT/runtime settings when configuration itself is under investigation. Mark exactly one job as baseline.

## Strategy 6: Input scale

Use `[Params]` or equivalent parameter sources to evaluate scaling behavior. Prefer a small, intentional set of representative sizes; avoid accidental Cartesian explosions.

## Baselines

- Method comparison: use `[Benchmark(Baseline = true)]`.
- Multi-job comparison: mark one job with `.AsBaseline()`.

Without a baseline, BDN reports absolute values but no useful ratio column.

## Statistical interpretation

A small numeric difference is not automatically meaningful. Consider variance/error and, when useful, add an explicit equivalence threshold so results can be classified as faster/same/slower instead of over-interpreting noise.
