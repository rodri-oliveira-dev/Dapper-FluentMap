# Changelog

This project follows the spirit of [Keep a Changelog](https://keepachangelog.com/) and Semantic Versioning for the fork line.

The historical archived package history is not reconstructed here. This changelog records the new maintained fork line.

## [Unreleased]

### Added

- Public adoption documentation for README, migration, compatibility and support policy.
- Release candidate readiness checklist under `.sdd/etapa-12/`.

### Changed

- README is now a concise bilingual entrypoint and delegates detailed release/adoption policy to dedicated documents.

## [3.0.0-rc.1] - Unreleased

Release candidate status: ready for publication qualification. The date remains
`Unreleased` until the packages are actually published.

### Added

- Generated materialization support for eligible explicit mappings, including runtime fallback for unsupported shapes.
- Persistence semantics for read-only, computed, insert-excluded, update-excluded and database-default columns.
- Advanced FluentMap-controlled query materialization, including `QueryMultipleMapped`, `ReadMapped*` and sync/async streaming helpers.
- Property converter metadata and read conversion in FluentMap-controlled materialization.
- Isolated immutable configuration, `FluentMapRuntime` and optional dependency injection integration.
- Roslyn analyzer and source generator packages for map-expression diagnostics and generated registration/materialization.
- Consumer smoke coverage for core, analyzer, generator, Dependency Injection, Dommel and trimmed package consumers.

### Changed

- Release versioning is hardened so default local pack produces `3.0.0-dev`, not historical `2.0.0` or accidental stable `3.0.0`.
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
- Add `Dapper.FluentMap.DependencyInjection` only when using `Microsoft.Extensions.DependencyInjection`.
- Add `Dapper.FluentMap.Analyzers` and `Dapper.FluentMap.Generators` as analyzer/compiler packages; they should not be consumed as runtime libraries.
- For Dommel, keep using `Dapper.FluentMap.Dommel` and verify key/default/computed/read-only metadata against real write scenarios before promoting from RC.

### Known Limitations

- Normal `Dapper.Query<T>()`, `SqlMapper.SetTypeMap` and Dommel integration remain process-wide.
- Write converters are metadata-only in the current Dapper/Dommel write path.
- SQL Server and PostgreSQL have conditional harnesses but are not certified in CI.
- Full Native AOT support is not claimed.
- SourceLink and provenance are ready in the release workflow and were validated for the previous remote RC qualification SHA; final candidate artifacts require the final commit to be pushed before remote SourceLink/provenance can be requalified.
- Stable release remains blocked on fork-owned API/binary baseline, package signing/SBOM decision and RC feedback.

## Fork Release Candidate Line

The first fork release candidate is expected to use a prerelease version such as `3.0.0-rc.1`, unless API compatibility review proves a different versioning path is safer.

Do not reuse `2.0.0` for the fork line because `Dapper.FluentMap` and `Dapper.FluentMap.Dommel` already have historical `2.0.0` packages.
