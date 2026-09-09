# Measurement and diagnostics

BenchmarkDotNet measures wall-clock time by default. Add diagnosers only when they answer the performance question.

## MemoryDiagnoser

Use `[MemoryDiagnoser]`, config APIs, or `--memory` to track managed allocations and GC activity per operation. Allocation differences can be more actionable than tiny timing differences in library code.

## DisassemblyDiagnoser

Use disassembly when JIT/codegen behavior is part of the hypothesis. It can produce assembly reports and optional source mapping.

## ThreadingDiagnoser

Use threading diagnostics for lock contention and work-item behavior when concurrency overhead is relevant.

## EventPipeProfiler

EventPipe can collect cross-platform CPU/GC/JIT traces. Profiling changes run cost and can add overhead; do not treat profiled timing as equivalent to an unprofiled benchmark unless configuration isolates profiling from measurement.

## Hardware counters

Hardware counters are platform/hardware dependent and may require elevated permissions. Use them only when cache/branch/CPU-counter evidence is necessary.

## Statistical output

Common BDN columns include:

| Column | Meaning |
|---|---|
| `Mean` | average per-operation time |
| `Error` | confidence-interval signal |
| `StdDev` | measurement spread |
| `Median` | central value less sensitive to skew |
| `Ratio` | current/baseline performance when a baseline exists |
| `Allocated` | bytes allocated per operation when memory diagnostics are enabled |

Do not report a small `Mean` delta without considering error/variance and practical size.

## Export formats

Prefer generated GitHub Markdown and CSV for human review. Add JSON only when programmatic comparison requires structured output.
