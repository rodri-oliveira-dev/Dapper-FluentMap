# Changelog

This project follows the spirit of [Keep a Changelog](https://keepachangelog.com/) and Semantic Versioning for the fork line.

The historical archived package history is not reconstructed here. This changelog records the new maintained fork line.

## [Unreleased]

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
- Added FluentMap-controlled two-type `QueryMapped<TFirst,TSecond,TReturn>` multi-mapping with `splitOn` and an isolated `FluentMapRuntime` equivalent.
- Added `QueryMultipleMappedAsync` and async `MappedGridReader.ReadMappedAsync*` APIs for ordered multiple-result-set materialization.
- Added strict generated materialization through `UseStrictGeneratedMaterialization()` and `QueryGeneratedMapped*`, with deterministic diagnostics and a Native AOT CI smoke.
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
