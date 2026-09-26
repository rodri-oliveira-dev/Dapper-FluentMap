# Compatibility

This document describes what is currently validated by this repository. It avoids claims that are only supported by design intent.

## Package Matrix

| Package | TFM | Status |
| --- | --- | --- |
| `Dapper.FluentMap` | `netstandard2.0` | Core package. |
| `Dapper.FluentMap.Dommel` | `netstandard2.0` | Optional Dommel integration. |
| `FluentMap.DependencyInjection` | `netstandard2.0` | Optional DI integration. |
| `FluentMap.Analyzers` | `netstandard2.0` | Roslyn analyzer package. |
| `FluentMap.Generators` | `netstandard2.0` | Roslyn source generator package. |

Tests, provider compatibility tests, AOT smoke projects and benchmarks currently run on `net10.0`. That does not raise the minimum TFM for consumers.

The `FluentMap.*` PackageIds are NuGet distribution identities. Assemblies and namespaces remain under `Dapper.FluentMap.*` for source and binary compatibility.

## Dapper

Current package range:

```text
Dapper [2.1.79,3.0.0)
```

Validated in the current matrix:

| Dapper | Status | Notes |
| --- | --- | --- |
| `2.1.79` | Validated | Current minimum supported version and minimum CI matrix lane. |
| `2.1.89` | Validated | Current latest-stable CI matrix lane. |

Known risk: `Dapper.FluentMap` uses public Dapper APIs for type maps/readers, but TypeHandler interoperability depends on resolving `SqlMapper.TypeHandlerCache<T>.Parse(object)` by reflection. This is covered by tests and remains the highest-risk Dapper compatibility boundary.

## Dommel

Current package range for `Dapper.FluentMap.Dommel`:

```text
Dommel [3.5.3,4.0.0)
```

Validated in the current matrix:

| Dommel | Dapper | Status |
| --- | --- | --- |
| `3.5.3` | `2.1.79` | Validated by the current Dommel integration tests. |

Dommel integration is optional and process-wide. It uses global `DommelMapper` resolvers/builders and does not participate in isolated `FluentMapRuntime` configuration.

## Providers

Provider support is split into certification levels:

| Provider | Status | Evidence |
| --- | --- | --- |
| SQLite (`Microsoft.Data.Sqlite` 10.0.12) | Validated | Automated provider compatibility tests cover basic reads, immutable/nested/value-object reads, generated/runtime materialization, `QueryMultipleMapped`, `QueryMultipleMappedAsync`, sync/async streaming and Dommel persistence. |
| Provider-independent ADO.NET readers | Validated for core behavior | Tests use `DataTableReader` and common ADO.NET contracts. |
| SQL Server 2022 CU23 (`Microsoft.Data.SqlClient` 7.1.0) | CI certified | Mandatory CI provider lane uses `mcr.microsoft.com/mssql/server:2022-CU23-ubuntu-22.04` and fails if the service or connection string is unavailable. |
| PostgreSQL 18.6 (`Npgsql` 10.0.3) | CI certified | Mandatory CI provider lane uses `postgres:18.6-bookworm` and fails if the service or connection string is unavailable. |
| MySQL 8.4.11 (`MySqlConnector` 2.6.2) | CI certified | Mandatory CI provider lane uses `mysql:8.4.11-oraclelinux9` and exercises reads, generated/runtime materialization, multiple results, streaming and Dommel persistence. |
| MariaDB 11.8.9 (`MySqlConnector` 2.6.2) | CI certified | Mandatory CI provider lane uses `mariadb:11.8.9-ubi9` with the same strict no-skip provider contract. |
| SQL Server CE | Legacy/upstream-limited | Dommel builder remains registered for compatibility; no modern validation lane is present. |

Provider certification requires real integration tests against that provider and database. A Dommel SQL builder being registered is not the same as provider certification.

## AOT And Trimming

Current status:

| Area | Status |
| --- | --- |
| Explicit map registration | Preferred for trimmed and Native AOT applications. |
| Generated registration | Preferred alternative to assembly scanning for maps in the current compilation. |
| Strict generated runtime | `UseStrictGeneratedMaterialization()` and `QueryGeneratedMapped*` provide a generated-only path; unsupported shapes fail deterministically. |
| Assembly scanning | Reflection-based and annotated as trimming-sensitive. |
| `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped`, streaming | Annotated with trimming/dynamic-code warnings because runtime fallback can occur. |
| Full Native AOT compatibility | Not claimed. |

Trimmed smoke tests cover explicit, generated and DI scenarios. The CI Native AOT lane publishes and runs the strict generated SQLite smoke on `windows-latest`/`win-x64`, treating relevant trimming and AOT warnings as errors. Local Native AOT publishing still requires the Visual C++ linker toolchain and full Native AOT compatibility is not claimed.

Generated materializers accept their exact registered shape and safe permutations when column names are distinct. Missing, additional or duplicate-column shapes remain unsupported and use runtime fallback unless strict generated materialization is enabled. Resolution is based on reader metadata, not SQL parsing.

## Global State Limitations

The historical static bridge remains process-wide:

- `FluentMapper.Initialize(...)` publishes global FluentMap state;
- normal `Dapper.Query<T>()` uses Dapper's global `SqlMapper.SetTypeMap` per entity type;
- Dommel uses global `DommelMapper` resolvers/builders.

Use `ImmutableFluentMapConfiguration` and `FluentMapRuntime` for isolated FluentMap-controlled materialization:

```csharp
var runtime = new FluentMapConfigurationBuilder()
    .AddMap<CustomerMap>()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryMappedSingle<Customer>(connection, sql);
```

That isolation applies to `QueryMapped*`, `QueryGeneratedMapped*`, two-type `splitOn` multi-mapping, `ReadMapped*`, `QueryMultipleMapped`, streaming, profiles, converters, diagnostics and generated materializer lookup. It does not make normal Dapper queries or Dommel select a runtime per call.

## API Compatibility

The fork preserves the main historical source-compatible API surface where possible:

- `FluentMapper.Initialize(...)`;
- `EntityMap<TEntity>`;
- `PropertyMap`;
- `Map(...).ToColumn(...)`;
- `Ignore()`;
- conventions;
- Dapper type map bridge;
- Dommel mapping types.

The fork also adds public APIs for profiles, naming policies, generated materializers, persistence metadata, property converters, query helpers, immutable configuration, isolated runtime and DI.

The fork-owned 3.0 line has published stable packages through 3.0.3. Public compatibility remains governed by SemVer and the package/API boundaries listed above.

## Unsupported Environments Or Claims

- Dapper major versions outside `[2.1.79,3.0.0)` are not currently supported.
- Dommel major versions outside `[3.5.3,4.0.0)` are not currently supported.
- Full Native AOT support is not claimed.
- Provider behavior outside the exact tested server/client versions listed above is not certified.
- Dommel configuration isolation per `FluentMapRuntime` is not supported.
- Three-or-more-type multi-mapping, graph aggregation and CRUD generation are not implemented.
