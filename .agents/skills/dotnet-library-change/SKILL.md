---
name: dotnet-library-change
description: Use this skill when changing Dapper-FluentMap production code, public contracts, project files, PackageIds, package metadata, dependencies, or related tests. Do not use for pure CI/release work or behavior-preserving refactoring as the primary task.
license: MIT
---

# Purpose

Guide small, safe changes to this public multi-package .NET library while preserving build, tests, packaging, and public compatibility.

# When To Use

- Changes under `src/**`.
- Behavior changes requiring tests under `test/**`.
- Changes to `.csproj`, targets, dependencies, public types/members, XML docs, or package metadata.
- Any task that may affect package identity, assembly identity, namespace, dependency ranges, trimming/AOT annotations, analyzers, generators, or consumer compatibility.

# When Not To Use

- Behavior-preserving refactoring is the main task: use `dotnet-refactoring-engineer`.
- Workflows, release, NuGet publishing, or recovery are the main task: use `ci-release-governance`.
- Reviewing an existing PR/diff is the task: use `dotnet-pr-review`.
- The task changes only agent guidance or documentation with no library behavior impact.

# Process

1. Read root `AGENTS.md` and the files directly related to the requested change.
2. Identify the affected package(s): `Dapper.FluentMap`, `Dapper.FluentMap.Dommel`, `Dapper.FluentMap.DependencyInjection`, `Dapper.FluentMap.Analyzers`, or `Dapper.FluentMap.Generators`.
3. Determine whether the change affects public API, source/binary/behavior compatibility, target frameworks, dependency ranges, Dapper/Dommel integration, global state/cache, package output, or documentation.
4. Preserve independent identities: project name, path, assembly name, namespace, and NuGet `PackageId` are not interchangeable.
5. Implement the smallest coherent change using existing project patterns.
6. Add or update tests for observable behavior changes; use regression tests for bug fixes.
7. Update consumer documentation and changelog only when the public contract or documented behavior changes.
8. For dependency, target, PackageId, metadata, or pack output changes, combine with `ci-release-governance`.
9. Run targeted validation first, then broader solution or package validation when risk requires it.
10. Review the full diff for unrelated modernization, formatting churn, project metadata drift, or weakened validation.

# Repository Baseline

- Public packages currently target `netstandard2.0`.
- The shared package version base is `FluentMapPackageVersionPrefix` in `Directory.Build.props`, with an explicit `Version` override in release workflows.
- Package IDs currently include:
  - `Dapper.FluentMap`
  - `Dapper.FluentMap.Dommel`
  - `Dapper.FluentMap.DependencyInjection`
  - `Dapper.FluentMap.Analyzers`
  - `Dapper.FluentMap.Generators`
- Dependency versions are declared where the current project files declare them; this repository does not currently use Central Package Management.
- `Dapper.FluentMap.slnx` is preferred for current SDK validation; `Dapper.FluentMap.sln` remains a compatibility fallback.

# Validation

Use the commands from `AGENTS.md` that match the risk. As a starting point:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./Dapper.FluentMap.slnx --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.slnx --configuration Release --no-build
```

When package output, metadata, dependency ranges, targets, or release compatibility are affected, also run pack validation using the actual `eng` scripts documented in `AGENTS.md`.

# Restrictions

- Do not change `PackageId`, targets, dependency ranges, authors, license, URLs, Source Link/provenance, or package readme/icon behavior without explicit scope.
- Do not add package-management systems, lock-file policy, source generators, analyzers, nullable, NativeAOT/trimming claims, or SDK upgrades as incidental work.
- Do not make internals public just for tests.
- Do not reduce warnings, tests, analyzers, audit, or package validation to get a green run.
- Do not publish packages, create tags, or trigger releases unless explicitly requested.

# Quality Bar

A good library change resolves the requested behavior with a focused diff, preserves unrelated contracts, tests the relevant observable behavior, and keeps build/test/pack expectations consistent with the repository baseline.
