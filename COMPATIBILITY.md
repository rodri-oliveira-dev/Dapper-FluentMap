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

The machine-readable source of truth is `eng/compatibility-contract.json`. CI verifies this document, package properties, compatibility lanes, migration guidance and produced NuGet dependency metadata against that contract.

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

The machine-readable provider matrix is part of `eng/compatibility-contract.json`. Each provider has one overall support level and independent Core and Dommel evidence, using these terms:

- **Dapper-compatible**: Dapper/ADO.NET compatibility is expected, but this repository has no dedicated real-database FluentMap certification evidence.
- **FluentMap Core certified**: required real-database integration tests exercise FluentMap mapping and materialization; this does not imply Dommel support.
- **FluentMap + Dommel certified**: required real-database integration tests also exercise the supported Dommel persistence path.
- **Legacy/upstream-limited**: compatibility is retained because of an upstream or historical integration, without a modern certification lane.

Current support contract:

| Provider | Dapper/ADO.NET | FluentMap Core evidence | FluentMap + Dommel evidence |
| --- | --- | --- | --- |
| SQLite | Dapper-compatible | FluentMap Core certified | FluentMap + Dommel certified |
| SQL Server | Dapper-compatible | FluentMap Core certified | FluentMap + Dommel certified |
| PostgreSQL | Dapper-compatible | FluentMap Core certified | FluentMap + Dommel certified |
| MySQL | Dapper-compatible | FluentMap Core certified | FluentMap + Dommel certified |
| MariaDB | Dapper-compatible | FluentMap Core certified | FluentMap + Dommel certified |
| Oracle | Dapper-compatible | FluentMap Core certified | Not certified |
| Firebird | Dapper-compatible | Not certified | Not certified |
| SQL Server CE | Legacy/upstream-limited | Legacy/upstream-limited | Legacy/upstream-limited |

Certification evidence:

- SQLite (`Microsoft.Data.Sqlite` 10.0.12) runs in memory and covers basic reads, immutable/nested/value-object reads, generated/runtime materialization, `QueryMultipleMapped`, `QueryMultipleMappedAsync`, sync/async streaming and Dommel persistence.
- SQL Server 2022 CU23 (`Microsoft.Data.SqlClient` 7.1.0) uses `mcr.microsoft.com/mssql/server:2022-CU23-ubuntu-22.04` in mandatory CI.
- PostgreSQL 18.6 (`Npgsql` 10.0.3) uses `postgres:18.6-bookworm` in mandatory CI.
- MySQL 8.4.11 (`MySqlConnector` 2.6.2) uses `mysql:8.4.11-oraclelinux9` in mandatory CI.
- MariaDB 11.8.9 (`MySqlConnector` 2.6.2) uses `mariadb:11.8.9-ubi9` in mandatory CI.
- Oracle Database Free 23.26.0 (`Oracle.ManagedDataAccess.Core` 23.26.301) uses `gvenzl/oracle-free:23.26.0-slim-faststart` in a mandatory Core-only CI lane. It covers explicit mapping, immutable/nested/value-object and generated/runtime materialization, sync/async streaming, parameter binding and null handling. ODP.NET does not expose the suite's multi-statement `QueryMultiple` shape, and no Oracle Dommel SQL builder is claimed.
- Provider-independent Core tests additionally use `DataTableReader` and common ADO.NET contracts, but that evidence alone never certifies a database provider.

The Oracle provider and database image are test-only dependencies, are publicly retrievable without repository credentials, and retain their respective upstream license terms. CI creates an ephemeral application schema with run-scoped credentials rather than depending on private Oracle infrastructure.

`MySqlConnector` 2.6.2 is used only by the provider-certification test project because one async ADO.NET driver can exercise both pinned MySQL and MariaDB services, targets `netstandard2.0`, and is distributed under the MIT license. This choice does not add a runtime dependency to the FluentMap packages or certify any server, driver version, or feature outside the matrix above.

Provider certification requires executable integration tests against that provider and database in the required matrix. A registered or theoretically implementable Dommel SQL builder is not certification evidence.

## Experimental Compatibility Canaries

The scheduled/manual `Compatibility Canary` workflow checks the supported Dapper minimum/current lanes, resolves a newer Dapper stable or prerelease when one exists, and compiles/runs SQLite evidence against the latest resolvable stable provider clients. These lanes are early-warning experiments only: they neither expand nor reduce the certified matrix above. A canary result becomes a support claim only after the pinned required matrix, documentation and package contract are intentionally updated and reviewed.

## AOT And Trimming

Current status:

| Area | Status |
| --- | --- |
| Explicit map registration | Preferred for trimmed and Native AOT applications. |
| Generated registration | Preferred alternative to assembly scanning for maps in the current compilation. |
| Strict generated runtime | `UseStrictGeneratedMaterialization()` and `QueryGeneratedMapped*` provide a generated-only path for parameterless commands and explicitly typed `GeneratedParameters`; unsupported shapes and arbitrary parameter objects fail deterministically. |
| Assembly scanning | Reflection-based and annotated as trimming-sensitive. |
| `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped`, streaming | Annotated with trimming/dynamic-code warnings because runtime fallback can occur. |
| Full Native AOT compatibility | Not claimed. |

Trimmed smoke tests cover explicit, generated and DI scenarios. The CI Native AOT lane publishes and runs the strict generated SQLite smoke on `windows-latest`/`win-x64`, treating relevant trimming and AOT warnings as errors. Local Native AOT publishing still requires the Visual C++ linker toolchain and full Native AOT compatibility is not claimed.

Generated materializers accept their exact registered shape, safe permutations and additional reader columns that do not resolve to explicitly configured FluentMap members. Missing required columns, duplicate/ambiguous names and additional columns that would resolve to an explicit FluentMap member remain unsupported and use runtime fallback unless strict generated materialization is enabled. The strict path deliberately does not reflect over convention-only entity members; callers that need Dapper's default-member discovery must use the non-strict API. Resolution is based on reader metadata, not SQL parsing.

`GeneratedParameters` uses only public ADO.NET parameter contracts and requires an explicit `DbType` for every input. It does not inspect parameter-object members or invoke Dapper's anonymous-object parameter generation. The Native AOT smoke covers generated startup registration, parameter binding, reordered columns, a safe additional column and deterministic strict rejection. This is bounded evidence for the strict path, not a claim of full FluentMap Native AOT compatibility.

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

That isolation applies to `QueryMapped*`, `QueryGeneratedMapped*`, two/three-type `splitOn` multi-mapping including per-segment profile overloads, explicit `ConstructUsing(...)` factories, `ReadMapped*`, `QueryMultipleMapped`, streaming, profiles, converters, diagnostics and generated materializer lookup. It does not make normal Dapper queries or Dommel select a runtime per call.

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

The maintained 3.x line follows Semantic Versioning. See the GitHub releases page for the current stable version. Public compatibility remains governed by SemVer and the package/API boundaries listed above.

Checked-in `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` files govern every shipped assembly and include nullable annotations. See [MAINTAINING.md](MAINTAINING.md) for the approval and release process.

## Unsupported Environments Or Claims

- Dapper major versions outside `[2.1.79,3.0.0)` are not currently supported.
- Dommel major versions outside `[3.5.3,4.0.0)` are not currently supported.
- Full Native AOT support is not claimed.
- Provider behavior outside the exact tested server/client versions listed above is not certified.
- Dommel configuration isolation per `FluentMapRuntime` is not supported.
- Multi-mapping is supported for two or three input types. Four-or-more input types, automatic graph aggregation and generic core CRUD generation are not implemented.
- Explicit delegate construction strategies are supported by runtime materialization. The generator reports `DFM011` and strict generated mode rejects those shapes; no full Native AOT claim is made for delegate factories.
- Property write conversion is supported only by the Dommel adapter's opt-in `InsertMapped*` and `UpdateMapped*` APIs. Historical Dommel `Insert`/`Update` do not execute property converters.
- `MappedGridReader` result sets must be consumed sequentially; concurrent reads are rejected deterministically.
