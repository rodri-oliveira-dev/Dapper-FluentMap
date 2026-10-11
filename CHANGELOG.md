# Changelog

This project follows the spirit of [Keep a Changelog](https://keepachangelog.com/) and Semantic Versioning for the fork line.

The historical archived package history is not reconstructed here. This changelog records the new maintained fork line.

## [Unreleased]

### Added

- Added mandatory real-database FluentMap Core certification lanes for Oracle Database Free 23.26.0 with `Oracle.ManagedDataAccess.Core` 23.26.301 and Firebird 5.0.4 with `FirebirdSql.Data.FirebirdClient` 10.3.4.

### Changed

- Defined separate Dapper/ADO.NET, FluentMap Core and FluentMap + Dommel provider evidence levels. Oracle and Firebird are Core-only; SQL Server CE remains legacy/upstream-limited; SQLite, SQL Server, PostgreSQL, MySQL and MariaDB retain Core + Dommel certification.
- Added a machine-readable provider compatibility contract and consistency checks tying pinned provider versions, required CI lanes and public compatibility documentation together.

## [3.5.0] - 2026-10-01

### Added

- Added reflection-free `GeneratedParameters` for parameterized strict-generated queries, including provider-matrix and Native AOT smoke coverage.
- Added Dommel `InsertMapped*`/`UpdateMapped*` operations that execute per-property write converters while preserving persistence exclusions and Dapper type-handler precedence.
- Added three-input FluentMap-controlled multi-mapping with multiple validated `splitOn` boundaries, per-segment profiles, async execution, null-child semantics and isolated-runtime APIs.
- Added explicit `ConstructUsing(...)` factory strategies for one to four mapped values, with configuration validation and deterministic generated/strict boundaries.
- Added analyzer diagnostic `DFM016` for duplicate explicit construction strategies.
- Added checked-in nullable public API baselines for every shipped assembly, enforced by CI.
- Added report-only scheduled/manual compatibility canaries and materialization benchmark regression reporting with machine-readable artifacts.

### Changed

- Generated materializers now accept safe additional reader columns that do not resolve to explicit FluentMap members and provide specific strict diagnostics for missing, duplicate/ambiguous and explicitly mapped additional columns; Dapper convention-only discovery remains a non-strict behavior.
- Two- and three-input multi-mapping now share a single segment materialization pipeline.
- Public projects now emit deliberate nullable reference annotations using staged `Nullable=annotations`; optional query inputs, metadata and multi-mapping null-child delegates reflect actual behavior without changing CLR signatures.
- Generated value-conversion failures now surface as contextual `FluentMapConfigurationException` instances instead of leaking provider-dependent `FormatException` or `InvalidCastException` exceptions.

## [3.4.0] - 2026-09-27

### Added

- ADR Guard local tooling and ADR documentation for the NuGet PackageId architecture decision.
- A package catalog under `eng/package-catalog.json` as the authoritative distribution package inventory.
- Public adoption documentation for README, migration, compatibility and support policy.
- Release candidate readiness checklist under `.sdd/etapa-12/`.
- SPDX 2.3 release SBOM generation and GitHub SBOM attestations for the governed NuGet package family, including recovery support.

### Changed

- Mainline CI, release/recovery branch guards and badges now target `main`.
- The documented Dapper minimum, package range and CI minimum lane are aligned on `2.1.79`; the latest-stable lane is `2.1.89`.
- SQL Server 2022 CU23 and PostgreSQL 18.6 provider compatibility now run as a mandatory real-database CI lane with strict no-skip certification semantics.
- Added FluentMap-controlled two-type `QueryMapped<TFirst,TSecond,TReturn>` multi-mapping with `splitOn`, per-segment profile overloads and an isolated `FluentMapRuntime` equivalent.
- Added `QueryMultipleMappedAsync` and async `MappedGridReader.ReadMappedAsync*` APIs for ordered multiple-result-set materialization.
- Added strict generated materialization through `UseStrictGeneratedMaterialization()` and `QueryGeneratedMapped*`, with deterministic diagnostics and a Native AOT CI smoke.
- Fixed two-type multi-mapping validation so a `splitOn` in the first column is rejected as an empty first segment and duplicate split columns remain ambiguous across the full row shape.
- Made mapped multiple-result read acquisition atomic; concurrent reads on one `MappedGridReader` now fail deterministically while sequential and cancellation/disposal behavior is preserved.
- Generated materializers now support safe permutations of distinct result columns without entering the runtime materializer fallback.
- MySQL 8.4.11 and MariaDB 11.8.9 provider compatibility now run in the mandatory real-database CI lane with `MySqlConnector` 2.6.2.
- The new Dependency Injection, analyzer and generator NuGet PackageIds are now `FluentMap.DependencyInjection`, `FluentMap.Analyzers` and `FluentMap.Generators`. Project names, assemblies, namespaces and public APIs remain `Dapper.FluentMap.*`.
- Release and recovery automation now distinguish package-version existence from publisher authorization, validate existing NuGet.org artifacts before accepting them and publish only genuinely missing packages.
- README is now a concise bilingual entrypoint and delegates detailed release/adoption policy to dedicated documents.

## [3.0.3] - Published

Stable 3.0.3 packages have been published for the maintained fork line. The historical release-candidate notes below are retained as history, not as the current release status.

## [3.0.0-rc.1] - Unreleased

Historical release-candidate status retained for traceability. This section does not describe the current stable 3.0.3 package state.

### Added

- Generated materialization support for eligible explicit mappings, including runtime fallback for unsupported shapes.
- Persistence semantics for read-only, computed, insert-excluded, update-excluded and database-default columns.
- Advanced FluentMap-controlled query materialization, including `QueryMultipleMapped`, `ReadMapped*` and sync/async streaming helpers.
- Property converter metadata and read conversion in FluentMap-controlled materialization.
- Isolated immutable configuration, `FluentMapRuntime` and optional dependency injection integration.
- Roslyn analyzer and source generator packages for map-expression diagnostics and generated registration/materialization.
- Consumer smoke coverage for core, analyzer, generator, Dependency Injection, Dommel and trimmed package consumers.

### Changed

- Release versioning is hardened so default local pack produces `3.0.1-dev`, not historical `2.0.0` or accidental stable `3.0.0`.
- Release workflow uses an explicit validated package version for RC artifacts.
- Public mapping configuration can now be isolated through immutable configuration/runtime APIs while the legacy global facade remains available.
- Dommel integration honors the new persistence metadata for supported write scenarios.

### Breaking and Risky Changes

- The fork line uses package version `3.0.0-rc.1`; consumers should treat it as a major-version release candidate rather than a drop-in stable upgrade.
- New generated materialization paths and query helpers are opt-in and have runtime fallback, but they expand the public surface that must be validated before stable.
- Legacy `FluentMapper` and `SqlMapper.SetTypeMap` behavior remains global and process-wide; isolated runtime APIs do not remove that existing global state for consumers that still use it.
- Write converters are not executed by the current Dapper/Dommel write path; converter metadata is available, but write conversion remains outside the RC.1 behavior claim.

### Provider Status

- SQLite is the certified RC.1 provider path and is covered by local, CI and consumer smoke validation.
- SQL Server and PostgreSQL compatibility harnesses exist but require connection strings and are not certified by the default RC.1 gate.

### AOT and Trimming

- Trimmed package consumers for explicit and generated mapping scenarios publish and run successfully in the RC gate with no trimming warnings observed.
- Full Native AOT support is not claimed for RC.1.
- Assembly scanning and reflection-heavy configuration remain risky under trimming/AOT unless the consumer preserves required members.

### Migration Guide

- Pin all FluentMap packages to the exact same prerelease version: `3.0.0-rc.1`.
- Keep existing global `FluentMapper.Initialize` usage when source compatibility is the priority.
- Prefer `FluentMapConfigurationBuilder` and `FluentMapRuntime` for new isolated configurations or DI-based composition.
- Add `FluentMap.DependencyInjection` only when using `Microsoft.Extensions.DependencyInjection`.
- Add `FluentMap.Analyzers` and `FluentMap.Generators` as analyzer/compiler packages; they should not be consumed as runtime libraries.
- For Dommel, keep using `Dapper.FluentMap.Dommel` and verify key/default/computed/read-only metadata against real write scenarios before promoting from RC.

### Known Limitations

- Normal `Dapper.Query<T>()`, `SqlMapper.SetTypeMap` and Dommel integration remain process-wide.
- Write converters are metadata-only in the current Dapper/Dommel write path.
- SQL Server and PostgreSQL have conditional harnesses but are not certified in CI.
- Full Native AOT support is not claimed.
- SourceLink and provenance are ready in the release workflow and were validated for the previous remote RC qualification SHA; final candidate artifacts require the final commit to be pushed before remote SourceLink/provenance can be requalified.
- The RC-era SourceLink/provenance and package-signing caveats are historical; current release readiness is governed by the active workflow and compatibility documentation.

## Historical Fork Release Candidate Line

The first fork release candidate used a prerelease version such as `3.0.0-rc.1`; this section is retained only to explain the fork line's history.

Do not reuse `2.0.0` for the fork line because `Dapper.FluentMap` and `Dapper.FluentMap.Dommel` already have historical `2.0.0` packages.

[Unreleased]: https://github.com/rodri-oliveira-dev/Dapper-FluentMap/compare/v3.5.0...HEAD
[3.5.0]: https://github.com/rodri-oliveira-dev/Dapper-FluentMap/compare/v3.4.0...v3.5.0
[3.4.0]: https://github.com/rodri-oliveira-dev/Dapper-FluentMap/compare/v3.0.3...v3.4.0
[3.0.3]: https://github.com/rodri-oliveira-dev/Dapper-FluentMap/compare/v2.0.0...v3.0.3
