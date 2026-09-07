# Compatibility

This document describes what is currently validated by this repository. It avoids claims that are only supported by design intent.

## Package Matrix

| Package | TFM | Status |
| --- | --- | --- |
| `Dapper.FluentMap` | `netstandard2.0` | Core package. |
| `Dapper.FluentMap.Dommel` | `netstandard2.0` | Optional Dommel integration. |
| `Dapper.FluentMap.DependencyInjection` | `netstandard2.0` | Optional DI integration. |
| `Dapper.FluentMap.Analyzers` | `netstandard2.0` | Roslyn analyzer package. |
| `Dapper.FluentMap.Generators` | `netstandard2.0` | Roslyn source generator package. |

Tests, provider compatibility tests, AOT smoke projects and benchmarks currently run on `net10.0`. That does not raise the minimum TFM for consumers.

## Dapper

Current package range:

```text
Dapper [2.1.79,3.0.0)
```

Validated in the current matrix:

| Dapper | Status | Notes |
| --- | --- | --- |
| `2.1.79` | Validated | Current minimum and matrix lane used by the repository. |

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
| SQLite (`Microsoft.Data.Sqlite`) | Validated | Automated provider compatibility tests cover basic reads, nested/value-object reads, `QueryMultipleMapped`, sync/async streaming and Dommel persistence. |
| Provider-independent ADO.NET readers | Validated for core behavior | Tests use `DataTableReader` and common ADO.NET contracts. |
| SQL Server (`Microsoft.Data.SqlClient`) | Not certified | Conditional harness exists via `DFM_SQLSERVER_CONNECTION_STRING`, but it is not executed in CI by default. |
| PostgreSQL (`Npgsql`) | Not certified | Conditional harness exists via `DFM_POSTGRESQL_CONNECTION_STRING`, but it is not executed in CI by default. |
| MySQL/MariaDB | Not validated | Dommel builder registration exists by design; no automated provider lane is present. |
| SQL Server CE | Legacy/upstream-limited | Dommel builder remains registered for compatibility; no modern validation lane is present. |

Provider certification requires real integration tests against that provider and database. A Dommel SQL builder being registered is not the same as provider certification.

## AOT And Trimming

Current status:

| Area | Status |
| --- | --- |
| Explicit map registration | Preferred for trimmed and Native AOT applications. |
| Generated registration | Preferred alternative to assembly scanning for maps in the current compilation. |
| Assembly scanning | Reflection-based and annotated as trimming-sensitive. |
| `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped`, streaming | Annotated with trimming/dynamic-code warnings because runtime fallback can occur. |
| Full Native AOT compatibility | Not claimed. |

Trimmed smoke tests have passed for explicit, generated and DI scenarios with known warnings. Native AOT publish/run has not been validated locally because the environment lacked the native linker toolchain.

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

That isolation applies to `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped`, streaming, profiles, converters, diagnostics and generated materializer lookup. It does not make normal Dapper queries or Dommel select a runtime per call.

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

Stable release readiness still requires a formal fork-owned API/binary compatibility baseline after the first release candidate.

## Unsupported Environments Or Claims

- Dapper major versions outside `[2.1.79,3.0.0)` are not currently supported.
- Dommel major versions outside `[3.5.3,4.0.0)` are not currently supported.
- Full Native AOT support is not claimed.
- Provider behavior that has not been validated by real integration tests is not certified.
- Dommel configuration isolation per `FluentMapRuntime` is not supported.
- `QueryMultipleMappedAsync`, Dapper multi-mapping with `splitOn`, graph aggregation and CRUD generation are not implemented.
