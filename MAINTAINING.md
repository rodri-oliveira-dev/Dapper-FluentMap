# Maintainer Governance

This document defines the review process for public API, nullable contracts, compatibility canaries and performance evidence. These checks supplement package validation, provider certification, security analysis and the normal test suite.

## Public API baselines

Every shipped project under `src/` has `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`. `Microsoft.CodeAnalysis.PublicApiAnalyzers` validates their union during every build, including the dedicated `Public API and nullable contracts` CI job.

For an intentional API change:

1. update implementation, XML documentation and focused tests;
2. run `dotnet format analyzers <project.csproj> --diagnostics RS0016 RS0036` to update additions and nullable annotations in `PublicAPI.Unshipped.txt`; record approved removals of shipped APIs manually in `PublicAPI.Unshipped.txt` with the `*REMOVED*` prefix;
3. review the API-file diff as a compatibility contract, including nullability markers;
4. document consumer and SemVer impact;
5. after the API ships, move its entries from `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt` in ordinal order.

Do not hand-approve a removal or signature replacement without the compatibility and SemVer review required for a public library. Package validation remains enabled and is not replaced by these files.

## Provider support claims

`eng/compatibility-contract.json` is the machine-readable source of truth for provider support. Keep Dapper/ADO.NET compatibility, FluentMap Core certification, FluentMap + Dommel certification and legacy/upstream-limited status distinct. A Core claim requires executable real-database mapping/materialization evidence in required CI; a Dommel claim additionally requires real persistence evidence for the provider's supported SQL builder path. Registration or availability of an SQL builder alone is not certification.

When changing the matrix, update the contract, required CI provider list and `COMPATIBILITY.md` together, run `eng/test-compatibility-consistency.ps1`, and preserve pinned server/client versions. Never promote a provider based only on common ADO.NET abstractions or theoretical compatibility.

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
