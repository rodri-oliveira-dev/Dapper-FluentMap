# BenchmarkDotNet Execution and Configuration

## Build and execution

BDN generates a standalone project containing the measurement harness, builds it once per unique build configuration, and runs each case in its own process. Cases are partitioned into separate builds when they differ in runtime, toolchain, platform, JIT, GC settings, build configuration, or MSBuild arguments.

`[Params]` creates a Cartesian product: methods × parameter combinations × jobs. Estimate this before running a full suite.

## Execution stages per case

Typical throughput runs include:

1. **Process launch + JIT** — initial compilation is excluded from steady-state results.
2. **Pilot** — BDN calibrates invocation count per iteration.
3. **Warmup** — runtime/JIT reaches stable behavior.
4. **Actual** — measured iterations used for statistics.

Exact defaults can change across BDN versions; prefer the installed package's behavior over assumptions from memory.

## Job presets

| Preset | Purpose |
|---|---|
| `Dry` | validate compilation/execution, not performance |
| `Short` | fast development feedback |
| Default | normal steady-state measurement |
| `Medium` | higher confidence |
| `Long` | expensive, high-confidence runs |

## Run strategies

- **Throughput** — steady-state microbenchmarking; normal default.
- **ColdStart** — first-call/startup behavior; measured invocation includes startup/JIT effects.
- **Monitoring** — macro-style single-invocation measurement with configurable warmup.

## Jobs and mutators

A job defines a complete run configuration. Mutators such as warmup or iteration attributes alter each applicable job rather than creating an independent comparison job.

Common controls include warmup count, iteration count, invocation count, iteration time, launch count, target runtime, environment variables, and GC mode. Do not fix them without a reason; BDN adaptive defaults are usually safer than arbitrary manual tuning.

## Unroll factor

BDN may unroll benchmark invocations internally to reduce harness overhead. `UnrollFactor` is engine configuration; `OperationsPerInvoke` declares that one benchmark invocation performs multiple logical operations. They solve different problems.

When `[IterationSetup]` or `[IterationCleanup]` is used, BDN normally shifts toward single-invocation iterations. That can make very fast operations noisy; prefer `[GlobalSetup]` where possible.

## Execution environment

Multiple runtimes, GC settings, or environment-variable configurations create distinct jobs and multiply total run cost. Use them only when they are the actual comparison axis.
