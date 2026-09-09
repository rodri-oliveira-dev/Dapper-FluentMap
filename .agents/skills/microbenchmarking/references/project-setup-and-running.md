# Running benchmarks

This reference covers how to build, run, and read BenchmarkDotNet results.

## Entry points

**BenchmarkSwitcher** forwards CLI arguments and otherwise can prompt interactively. Agents should always provide `--filter` to avoid hanging:

```csharp
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
```

**BenchmarkRunner** honors CLI args only when an overload receives them. Check the current entry point before assuming flags work.

## Existing benchmark project

Prefer `benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj`. Create another benchmark project only when the existing project cannot represent the requested measurement.

## Build and run strategy

```bash
dotnet build ./benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj -c Release
dotnet run --project ./benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj -c Release --no-build -- --filter "*MethodName" --noOverwrite > benchmark.log 2>&1
```

Read generated Markdown reports first; inspect the verbose log only for errors or unexpected behavior.

## Useful CLI flags

| Flag | Purpose |
|---|---|
| `--filter "*"` | run all benchmarks without interactive prompt |
| `--filter "*MethodName"` | run a focused method set |
| `--list flat` | list discoverable benchmark names |
| `--job Dry` | validate compilation/execution quickly |
| `--job Short` | development measurement |
| `--artifacts ./path` | control result location |
| `--noOverwrite` | preserve previous result files |
| `--exporters json` | add JSON output when needed |
| `--keepFiles` | retain generated project for build diagnosis |

## Dry-run validation

Always validate setup before a full benchmark suite:

```bash
dotnet run --project ./benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj -c Release --no-build -- --filter "*" --job Dry --noOverwrite
```

A dry run proves setup/compilation/execution, not performance.
