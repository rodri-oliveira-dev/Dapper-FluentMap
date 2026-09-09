# AGENTS.md

## Purpose

This repository maintains **Dapper.FluentMap**, a public multi-package .NET library that provides fluent, strongly typed mapping between POCO properties and database columns used by Dapper, keeping persistence attributes out of domain models.

Agent work must be small, correct, reproducible, and compatible with existing public behavior. Respond in Portuguese unless the user explicitly asks otherwise.

## Non-Negotiable Rules

| Area | Rule |
| --- | --- |
| Branching | The default development and release branch is `master`. Never modify `master` directly; create or use a task branch. |
| Scope | Prefer the smallest cohesive change. Do not mix functional change, modernization, dependency updates, refactoring, and release work unless the task explicitly spans them. |
| Public library | Preserve source, binary, and behavioral compatibility unless a breaking change is explicitly requested, justified, tested, documented, and versioned. |
| Publishing | Do not publish packages, create tags, create GitHub Releases, or run release/recovery workflows unless explicitly requested. |
| Secrets | Never commit secrets, tokens, API keys, certificates, or credential material. |
| Identity | Project name, project path, assembly name, C# namespace, and NuGet `PackageId` are independent identities. Never rename one merely because another changes; determine explicitly which identity the task intends to change. |
| Targets | Public packages currently preserve `netstandard2.0`. Do not change targets, multi-targeting, SDK, nullable, AOT, analyzers, test framework, or the repository's dependency-versioning model as incidental work. |
| Dapper | Use public Dapper contracts only. Do not copy Dapper internals or assume unit metadata tests prove end-to-end Dapper materialization. |

## Repository Map

| Path | Role |
| --- | --- |
| `src/Dapper.FluentMap/` | Core package and default production scope. |
| `src/Dapper.FluentMap.Dommel/` | Optional Dommel integration; change only when requested or demonstrably required by a core contract change. |
| `src/Dapper.FluentMap.DependencyInjection/` | Optional DI integration. |
| `src/Dapper.FluentMap.Analyzers/` | Roslyn analyzer package. |
| `src/Dapper.FluentMap.Generators/` | Source generator package. |
| `test/**` | Unit, integration, provider, analyzer/generator, DI, generated-registration, and AOT smoke tests. |
| `benchmarks/**` | BenchmarkDotNet benchmarks; not part of ordinary validation unless the task concerns performance. |
| `eng/**` | Validation, release, rollback, Sonar, package-catalog, and consumer-smoke helper scripts. |
| `.github/workflows/**` | CI, release, release recovery, and Sonar automation. |
| `.agents/skills/**` | Repository-local agent skills; load only those relevant to the current task. |

Prefer `Dapper.FluentMap.slnx` for current SDK workflows; `Dapper.FluentMap.sln` remains a compatibility fallback.

## Sources Of Truth

Read only what the task needs, in this order:

1. `AGENTS.md`
2. Relevant `.agents/skills/*/SKILL.md`
3. User request, issue, PR, or diff
4. `README.md`, `COMPATIBILITY.md`, `MIGRATION.md`, `CHANGELOG.md` when behavior or compatibility is involved
5. `Dapper.FluentMap.slnx` / `Dapper.FluentMap.sln`
6. Relevant `.csproj`, `Directory.Build.props`, `Directory.Build.targets`, `global.json`, `NuGet.Config`
7. Relevant source and test files
8. Relevant workflows and `eng` scripts when CI, packaging, or release is involved

Do not load the whole repository by default. Search first with `rg` / `rg --files`, then open the narrow files that define the contract, implementation, tests, or automation being changed.

## Skill Routing

Read `AGENTS.md` first. Load only the skills relevant to the current task; do not load every skill by default. Multiple skills may be combined when the task genuinely spans multiple concerns. Repository-specific rules in this file take precedence over generic guidance in a skill. Skills must inspect the current repository rather than assuming their source-template baseline still applies.

| Task | Primary skill |
| --- | --- |
| Implement a well-defined issue | `dotnet-issue-implementation` |
| Production/library/project/PackageId change | `dotnet-library-change` |
| Behavior-preserving refactoring | `dotnet-refactoring-engineer` |
| Pull request/diff review | `dotnet-pr-review` |
| CI, packaging, NuGet, versioning, release or recovery semantics | `ci-release-governance` |
| GitHub Actions YAML authoring/structural validation | `authoring-github-workflows` |
| NuGet.org OIDC/trusted-publishing review or diagnosis | `nuget-trusted-publishing` |
| Find caller-visible behaviors existing tests would miss | `test-gap-analysis` |
| BenchmarkDotNet/performance comparison | `microbenchmarking` |
| `Directory.Build.*` / MSBuild organization | `directory-build-organization` |
| Diagnose unclear MSBuild failures from `.binlog` | `binlog-failure-analysis` |

Pair skills when concerns overlap. Examples:

- release workflow change: `ci-release-governance` + `authoring-github-workflows`; add `nuget-trusted-publishing` when NuGet.org OIDC is involved;
- performance-sensitive library change: `dotnet-library-change` + `microbenchmarking` when measurement is required;
- implementation with uncertain test protection: implementation/refactoring skill + `test-gap-analysis`;
- build-property refactor: `directory-build-organization` + `dotnet-library-change` when package/public compatibility can be affected;
- opaque MSBuild failure: `binlog-failure-analysis`, then the relevant implementation/build skill once the cause is established.

## FluentMap Behavior Rules

- Keep the core focused on mapping registration, property/column resolution, conventions, naming policies, Dapper member resolution, diagnostics, and predictable composition.
- Do not turn FluentMap into an ORM, CRUD layer, SQL generator, query builder, connection abstraction, unit of work, migration tool, or entity tracker.
- Default mapping precedence should remain explicit mapping, then configured convention, then Dapper default behavior. If a path does not currently prove that order, test it before changing it.
- `Ignore()` and persistence metadata must preserve documented semantics across core and Dommel consumers.
- Nested mappings and value objects require end-to-end materialization support. Do not claim support based only on expression parsing or reflection metadata.

## Compatibility And SemVer

Treat these as compatibility-sensitive: public signatures, type names, namespaces, constructors, interfaces, `FluentMapper.Initialize`, `EntityMap`, `PropertyMap`, conventions, type maps, observable exception types, target frameworks, dependency ranges, package metadata, and case-sensitive/case-insensitive resolution behavior.

Before a breaking change:

1. Describe the current contract and consumer impact.
2. Explain why compatibility cannot be preserved.
3. Add or update tests and documentation.
4. Propose the required SemVer impact.
5. Do not implement unless the user explicitly requested that direction.

Bug fixes may correct incorrect behavior, but they need regression tests and a clear explanation of changed observable behavior.

## Dapper, State, And Cache

This repository has global configuration paths through FluentMap, Dapper `SqlMapper.SetTypeMap`, and Dommel. Changes in that area must consider thread safety, repeated initialization, test isolation, cache invalidation, concurrent registration, multiple entities/profiles, and startup-style initialization used by consumers.

- Do not add mutable global state without a specific reason.
- Prefer immutable descriptors after configuration is complete.
- Use structured cache keys that include every option affecting resolution, such as type, column name, profile, comparison, and naming policy.
- Define and test cache invalidation when behavior can change after registration.
- Restore global type maps/configuration in tests and benchmarks that mutate them.

## Expressions And Reflection

Expression parsing must work with the actual member represented by the expression, including legitimate conversions from `Expression<Func<TEntity, object>>`. Validate that the final member is a supported property, reject invalid expressions with useful diagnostics, preserve compatible inherited-property behavior, and avoid resolving by terminal name alone when homonyms or nested paths can collide.

## Tests

Use the smallest test layer that protects the risk:

| Risk | Preferred validation |
| --- | --- |
| Expression parsing, metadata, duplicates, conventions, cache, validation | Unit tests. |
| Dapper materialization, constructors, type maps, query helpers | Integration tests with an in-memory/ephemeral provider when possible. |
| Provider-specific behavior | Provider compatibility tests; do not require external services for the default suite. |
| Analyzer/generator behavior | Roslyn analyzer/generator tests. |
| Packaging and consumer experience | Pack validation plus `eng/consumer-smoke/run-consumer-smoke.ps1` when relevant. |
| Performance regression/optimization | Existing BenchmarkDotNet project with a controlled baseline; do not infer performance from unit-test duration. |

Do not weaken tests to get a green suite: no unjustified `Skip`, removed asserts, sleeps, time/network coupling, order dependence, or over-mocking of FluentMap itself.

## Validation Commands

Start with the closest validation. For documentation-only or agent-governance changes, prefer file existence, content searches, diff review, and targeted lint checks; do not run expensive build/test suites unless needed.

Core baseline:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./src/Dapper.FluentMap/Dapper.FluentMap.csproj --configuration Release --no-restore
dotnet test ./test/Dapper.FluentMap.Tests/Dapper.FluentMap.Tests.csproj --configuration Release
```

Full solution baseline:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./Dapper.FluentMap.slnx --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.slnx --configuration Release --no-build
```

Workflow validation when `.github/workflows/**` changes:

```bash
python -m check_jsonschema --builtin-schema vendor.github-workflows .github/workflows/*.yml
```

Use `authoring-github-workflows` for additional `actionlint` validation when structural/expression risk warrants it.

Packaging baseline when package output, metadata, compatibility, or release is affected:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./Dapper.FluentMap.slnx --configuration Release --no-restore
dotnet pack ./Dapper.FluentMap.slnx --configuration Release --no-build --output ./artifacts/packages
./eng/validate-package-metadata.ps1 -PackageDirectory './artifacts/packages'
./eng/validate-release-artifacts.ps1 -PackageDirectory './artifacts/packages' -Version <version> -ManifestPath './artifacts/release-metadata/artifact-manifest.json' -Repository <owner/repo> -RepositoryUrl <url> -Commit <sha> -Branch <ref>
```

Benchmark validation when performance is in scope:

```bash
dotnet build ./benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj --configuration Release
dotnet run --project ./benchmarks/Dapper.FluentMap.Benchmarks/Dapper.FluentMap.Benchmarks.csproj --configuration Release --no-build -- --filter "*RelevantBenchmark*" --job Dry
```

If the local SDK/runtime cannot run a required target, do not change project targets to work around the machine. Report the command, failure, and what remains unvalidated.

## Packaging And Release

The repository currently publishes multiple NuGet packages:

- `Dapper.FluentMap`
- `Dapper.FluentMap.Dommel`
- `FluentMap.DependencyInjection`
- `FluentMap.Analyzers`
- `FluentMap.Generators`

`eng/package-catalog.json` is the package identity catalog and separates project identity from NuGet `PackageId`. `Directory.Build.props` defines shared package metadata and `FluentMapPackageVersionPrefix` (currently `3.0.1`) with a local/dev suffix when no explicit version is supplied. The repository currently uses explicit `PackageReference` versions rather than Central Package Management. `Directory.Build.targets` blocks unsafe historical package versions. Release behavior lives in the actual workflows under `.github/workflows/`; inspect them before changing or documenting release behavior.

NuGet.org publishing uses OIDC Trusted Publishing through `NuGet/login`, a protected `release` environment, and job-scoped `id-token: write`. Preserve this model; do not introduce a long-lived NuGet API key as a shortcut.

Package or release changes require compatibility, provenance, artifact-set, rollback/recovery, and SemVer review. Do not alter PackageIds, versioning, authors, license, URLs, README/icon packaging, Source Link/provenance, or NuGet metadata without explicit scope.

## Documentation

Update docs when public API, initialization, behavior, compatibility, supported targets, package/dependency behavior, deprecations, or release flow changes. `README.md` is the entry point; examples must compile or accurately reflect the real API. Do not document unimplemented features.

## Git

Use Conventional Commits (`fix:`, `feat:`, `refactor:`, `test:`, `docs:`, `chore:`, `ci:`). Review the complete diff before committing. Do not push, open PRs, create tags, create releases, or publish packages unless the user explicitly requested it.

## Final Report

When finishing a task, report the objective, files changed, behavior preserved or changed, validations performed and results, build/test/pack status when applicable, remaining risks or limitations, and commit SHA when a commit was created.
