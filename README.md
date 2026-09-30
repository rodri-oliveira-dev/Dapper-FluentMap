# FluentMap

[![CI](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/ci.yml)
[![CodeQL](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/codeql.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_Dapper-FluentMap&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_Dapper-FluentMap)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_Dapper-FluentMap&metric=coverage)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_Dapper-FluentMap)
[![codecov](https://codecov.io/github/rodri-oliveira-dev/Dapper-FluentMap/branch/main/graph/badge.svg)](https://codecov.io/github/rodri-oliveira-dev/Dapper-FluentMap)
[![NuGet](https://img.shields.io/nuget/v/Dapper.FluentMap?logo=nuget)](https://www.nuget.org/packages/Dapper.FluentMap)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/github/license/rodri-oliveira-dev/Dapper-FluentMap)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/rodri-oliveira-dev/Dapper-FluentMap?style=flat&logo=github)](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/stargazers)

English | [Português (Brasil)](README.pt-BR.md)

FluentMap is an advanced mapping layer for Dapper. It lets you describe how .NET object properties map to database columns with fluent, strongly typed code while keeping persistence attributes out of your POCOs.

FluentMap is not an ORM. It does not track entities, build arbitrary SQL, manage connections, run migrations, provide LINQ, or replace Dapper.

## Project Status

Dapper.FluentMap is actively maintained. The maintained 3.x line continues the original project history while preserving the historical mapping model and adding newer capabilities as opt-in features.

Existing `EntityMap<T>` mappings and `FluentMapper.Initialize(...)` remain the compatibility baseline. Applications using normal root-level Dapper mappings generally do not need to rewrite working maps when upgrading from 2.x.

See [MIGRATION.md](MIGRATION.md) when moving from FluentMap 2.x.

## Key Capabilities

| Capability | Main API |
| --- | --- |
| Explicit property-to-column mapping | `EntityMap<T>`, `Map(...).ToColumn(...)` |
| Conventions and naming policies | `AddConvention(...)`, `UseNamingPolicy(...)` |
| Immutable/factory construction | constructor mapping, `ConstructUsing(...)` |
| Nested objects and component value objects | `QueryMapped*` |
| Alternate SQL shapes | mapping profiles |
| Two/three-type multi-mapping | `QueryMapped<...>(..., splitOn: ...)` |
| Multiple result sets | `QueryMultipleMapped*`, `ReadMapped*` |
| Sync/async streaming | `QueryMappedUnbuffered*` |
| Per-property conversion | property converters |
| Generated registration/materialization | `AddGeneratedMappings()` |
| Strict generated path | `UseStrictGeneratedMaterialization()`, `QueryGeneratedMapped*` |
| Isolated configuration | `FluentMapRuntime` |
| Dependency Injection | `AddFluentMap(...)` |
| Dommel persistence/write conversion | `InsertMapped*`, `UpdateMapped*` |
| Compile-time diagnostics | `FluentMap.Analyzers` |

Detailed examples are in [USAGE.md](USAGE.md).

## Installation

Install only the packages required by your application:

| Purpose | NuGet PackageId |
| --- | --- |
| Core | `Dapper.FluentMap` |
| Dommel integration | `Dapper.FluentMap.Dommel` |
| Dependency Injection | `FluentMap.DependencyInjection` |
| Roslyn analyzers | `FluentMap.Analyzers` |
| Source generators | `FluentMap.Generators` |

Core package:

```bash
dotnet add package Dapper.FluentMap
```

The `FluentMap.*` PackageIds are distribution identities only. Assemblies, namespaces and public APIs remain under `Dapper.FluentMap.*`.

Public packages target `netstandard2.0`. Supported dependency ranges and certified providers are documented in [COMPATIBILITY.md](COMPATIBILITY.md).

## Quick Start

```csharp
using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
    }
}

FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});

var customer = connection.QuerySingle<Customer>(
    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");
```

Call `FluentMapper.Initialize(...)` during application startup and treat the effective global configuration as read-only once queries begin.

For configuration validation:

```csharp
FluentMapper.Validate();
```

## Advanced Usage

Use FluentMap-controlled query APIs when mapping requires behavior beyond the historical root-level Dapper type map.

Examples include:

- nested objects and component value objects;
- profiles;
- two/three-type `splitOn` multi-mapping;
- mapped multiple result sets;
- sync/async streaming;
- property converters;
- generated and strict generated materialization;
- isolated runtimes and DI;
- Dommel persistence metadata.

See [USAGE.md](USAGE.md) for complete examples and API guidance.

## Migrating From 2.x

The 3.x line preserves the main historical source-compatible mapping path.

If your application uses `EntityMap<T>`, `FluentMapper.Initialize(...)` and normal `Dapper.Query<T>()` calls for root-level mappings, migration is usually a package upgrade followed by configuration validation and application testing.

See [MIGRATION.md](MIGRATION.md) for:

- the minimal migration path;
- Dapper and Dommel prerequisites;
- the `Ignore()`/Dommel persistence review;
- scenario-specific migration decisions;
- the migration checklist.

## Compatibility

Compatibility claims are intentionally kept outside the README so they can evolve without duplicating release-sensitive details.

See [COMPATIBILITY.md](COMPATIBILITY.md) for:

- supported Dapper and Dommel ranges;
- provider certification;
- trimming and Native AOT boundaries;
- global-state limitations;
- unsupported environments and API boundaries.

## Documentation

- [Usage guide](USAGE.md)
- [Migration from 2.x](MIGRATION.md)
- [Compatibility](COMPATIBILITY.md)
- [Changelog](CHANGELOG.md)
- [Support](SUPPORT.md)
- [Maintainer governance](MAINTAINING.md)
- [Português (Brasil)](README.pt-BR.md)

## Contributing

Keep changes small, compatible with the public API and covered by focused tests. `Dapper.FluentMap.slnx` is the preferred solution for current .NET SDKs; `Dapper.FluentMap.sln` remains available as a compatibility fallback.

Typical local validation:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./Dapper.FluentMap.slnx --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.slnx --configuration Release --no-build
```

When enabled, SonarQube Cloud participates in the CI quality gate. Repository-specific CI configuration is intentionally kept out of this README.

## License

FluentMap is licensed under the [MIT License](LICENSE).
