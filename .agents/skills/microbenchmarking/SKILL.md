---
name: microbenchmarking
description: >
  Activate when BenchmarkDotNet is involved or when a .NET performance question requires controlled microbenchmark measurement. Covers benchmark design, BDN configuration, project setup, cost-aware execution, side-by-side comparisons, diagnostics, and interpretation. Do not use for profiling/tracing, production telemetry, or load/stress testing.
license: MIT
---

# Benchmark Authoring Guidelines

BenchmarkDotNet (BDN) is the default tool for controlled .NET microbenchmarks in this skill.

> **Dapper-FluentMap integration:** prefer the existing `benchmarks/Dapper.FluentMap.Benchmarks` project. Preserve repository dependency/versioning conventions as they actually exist; this repository currently uses explicit `PackageReference` versions rather than Central Package Management. Do not introduce CPM as incidental benchmark work. Benchmark changes must not alter public behavior merely to improve measurements.

## Benchmarks are comparative instruments

A single number has limited value. Identify the comparison axis first:

- mapping approaches;
- current vs candidate implementation;
- runtime/package versions;
- reflection/runtime mapping vs generated paths;
- input scale;
- allocation behavior;
- historical measurements.

See [references/comparison-strategies.md](references/comparison-strategies.md) before configuring non-trivial comparisons.

## Benchmark lifecycle

Choose the use case before creating code:

1. **Coverage suite** — permanent representative benchmarks.
2. **Issue investigation** — task-scoped reproduction of a performance problem.
3. **Change validation** — before/after validation for a PR.
4. **Development feedback** — temporary experiment.

Only permanent coverage-suite benchmarks should automatically become repository code. Temporary experiments should remain isolated unless explicitly requested.

## Cost awareness

Each BDN case has real wall-clock cost. `[Params]` creates Cartesian products and multiple jobs multiply the case count.

| Preset | Typical purpose |
|---|---|
| `--job Dry` | correctness/compilation validation |
| `--job Short` | quick development measurements |
| default | final normal measurements |
| `--job Medium` | higher confidence |
| `--job Long` | exceptional high-confidence runs |

Always estimate method × parameter × job case count before a large run.

## Running benchmarks

Inspect the current benchmark entry point before assuming CLI forwarding. Use a narrow filter and redirect verbose output:

```bash
dotnet run --project ./benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj -c Release -- --filter "*MethodName" --noOverwrite > benchmark.log 2>&1
```

Run a dry representative case before longer measurements.

## Writing new benchmarks

Determine the real caller scenario, comparison axis, input shape, setup/reset needs, parameter count, and whether allocation diagnostics matter.

Key invariants:

- return results when needed to prevent dead-code elimination;
- move initialization to `[GlobalSetup]`;
- do not add manual loops merely to increase measurement work;
- mark an explicit baseline;
- store inputs in fields/params rather than foldable constants;
- use seeded randomness for reproducibility;
- materialize deferred execution when execution is what should be measured;
- isolate global FluentMap/Dapper configuration so one case does not contaminate another.

See [references/writing-benchmarks.md](references/writing-benchmarks.md).

## Dependency/setup rule

BenchmarkDotNet is already present in the repository benchmark project. Do not add another benchmark project or change dependency-management strategy unless the task demonstrates a need. If dependency versions change, follow the repository's existing package-version conventions and validate the benchmark project explicitly.

## Diagnostics

Use [references/diagnosers-and-exporters.md](references/diagnosers-and-exporters.md) when timing alone is insufficient. Allocation data can be especially relevant for a mapping library.

## Validation

1. Build the benchmark project in Release.
2. Run a dry representative case.
3. Run a narrow real measurement.
4. Confirm baseline and input set.
5. Read generated Markdown/CSV results.
6. Report runtime, hardware/OS and statistical limitations.
7. Do not treat tiny differences inside measurement noise as meaningful regressions/improvements.
