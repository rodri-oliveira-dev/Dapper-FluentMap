# Maintainer Governance

This document defines the review process for public API, nullable contracts, compatibility canaries and performance evidence. These checks supplement package validation, provider certification, security analysis and the normal test suite.

## Public API baselines

Every shipped project under `src/` has `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`. `Microsoft.CodeAnalysis.PublicApiAnalyzers` validates their union during every build, including the dedicated `Public API and nullable contracts` CI job.

For an intentional API change:

1. update implementation, XML documentation and focused tests;
2. run `dotnet format <project.csproj> analyzers --diagnostics RS0016 RS0017` to update `PublicAPI.Unshipped.txt`;
3. review the API-file diff as a compatibility contract, including nullability markers;
4. document consumer and SemVer impact;
5. after the API ships, move its entries from `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt` in ordinal order.

Do not hand-approve a removal or signature replacement without the compatibility and SemVer review required for a public library. Package validation remains enabled and is not replaced by these files.

## Nullable contracts

Maintained public projects compile with C# nullable annotations enabled. Warning enforcement is staged as `Nullable=annotations` because enabling full flow analysis currently exposes substantial internal legacy debt; this prevents a blanket suppression migration. Public signatures must still model actual behavior deliberately.

In particular, optional query parameters and transactions are nullable, ignored generated columns expose a nullable member path, optional profiles/converters are nullable, and multi-mapping delegates receive nullable child segments for all-`NULL` LEFT JOIN segments. Nullable annotation changes preserve CLR binary signatures but can create or remove consumer compiler warnings, so they require API-baseline review and changelog notes.

## Compatibility canaries

`Compatibility Canary` is scheduled and manually runnable. It records:

- the minimum and latest supported Dapper lanes;
- a forward-looking Dapper stable/prerelease when NuGet exposes one newer than the certified lane;
- compilation and SQLite evidence against the latest resolvable stable provider clients.

The canary is early-warning evidence. A green canary does not add a supported version, and a red canary does not remove one. Only the pinned required CI/provider matrix and `COMPATIBILITY.md` define certification. Investigate failures, record upstream incompatibilities and intentionally update the supported matrix only through a separately reviewed change.

## Performance reporting

`Performance Regression Report` runs the materialization hot paths on a scheduled/manual Windows hosted runner, exports BenchmarkDotNet CSV/JSON/Markdown/HTML, compares ShortRun results with `benchmarks/baselines/materialization-windows-net10.json`, and uploads both raw and comparison artifacts for 90 days.

The initial signals are report-only: +35% mean time or +15% allocated bytes is highlighted for review. Two initial Windows ShortRun measurements showed substantial timing variance while allocation counts remained stable; scheduled hosted-runner history is not yet sufficient for a hard gate. Reproduce a signal on controlled hardware before treating it as a regression. Update the checked-in baseline only after reviewing the code change, environment/runtime change, repeated measurements and generated comparison artifact.
