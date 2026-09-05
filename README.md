# FluentMap

English | [Português (Brasil)](README.pt-BR.md)

FluentMap is an advanced mapping layer for Dapper. It lets you describe how .NET object properties map to database columns with fluent, strongly typed code, while keeping persistence attributes out of your POCOs.

FluentMap is not an ORM. It does not track entities, build arbitrary SQL, manage connections, run migrations, provide LINQ, or replace Dapper. Use it when Dapper's default name-based mapping is not enough and the mapping rules should live outside the model.

## Project Status

Dapper.FluentMap is being actively modernized again. Version 3.0 continues the original project history while preserving the core FluentMap mapping model and compatibility path.

The 3.0 line modernizes the library and adds new opt-in capabilities without turning FluentMap into an ORM or requiring existing applications to adopt the new APIs.

## Coming from FluentMap 2.x?

If you used FluentMap before and are returning to the project, the important compatibility points are:

- existing `EntityMap<T>` mappings remain supported;
- `FluentMapper.Initialize(...)` remains supported;
- normal `Dapper.Query<T>()` calls continue to work with root-level mappings installed through the historical static API;
- most existing root-level maps should not need source changes;
- most 3.0 capabilities are opt-in, so you do not need to rewrite working mappings just because newer APIs exist.

See [MIGRATION.md](MIGRATION.md) for the recommended 2.x to 3.0 migration path and the behavioral differences worth reviewing.

## What's New in 3.0

FluentMap 3.0 modernizes the original project without changing its core purpose. In addition to the historical fluent mapping API, the 3.0 line adds opt-in support for:

- immutable constructor mapping improvements;
- nested object and value object materialization;
- mapping profiles for alternate SQL shapes;
- mapped `QueryMultiple`, unbuffered reads and async streaming;
- property conversion metadata and diagnostics;
- source-generated map registration and supported materializers;
- Roslyn analyzers for mapping diagnostics;
- isolated immutable configuration and dependency injection;
- richer persistence metadata consumed by the Dommel integration;
- trimming/AOT-aware registration and diagnostics;
- modern compatibility tests, provider harnesses, benchmarks, CI and package validation.

Existing mappings remain the compatibility baseline. Adopt the newer APIs only when they solve a concrete problem.

## Positioning

Use FluentMap for:

- explicit property-to-column maps;
- conventions and naming policies;
- ignored properties;
- immutable constructor mapping;
- opt-in nested object and value object materialization;
- mapping profiles for alternate SQL shapes;
- generated map registration/materialization where supported;
- persistence metadata consumed by integrations such as Dommel;
- isolated configuration and dependency injection for FluentMap-controlled materialization.

Do not use FluentMap as an ORM, CRUD framework, query builder, unit of work, or database abstraction.

## Installation

Install the package that matches the feature set you need:

| Package | Purpose |
| --- | --- |
| `Dapper.FluentMap` | Core mapping API and Dapper integration. |
| `Dapper.FluentMap.Dommel` | Optional Dommel integration for table, key and generated-column mapping. |
| `Dapper.FluentMap.DependencyInjection` | Optional `Microsoft.Extensions.DependencyInjection` integration. |
| `Dapper.FluentMap.Analyzers` | Roslyn analyzers for statically provable mapping mistakes. |
| `Dapper.FluentMap.Generators` | Source generator for build-time map registration and generated materializers. |

```bash
dotnet add package Dapper.FluentMap
```

The public packages target `netstandard2.0`. See [COMPATIBILITY.md](COMPATIBILITY.md) before adopting a release candidate.

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
    }
}

FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});

var customer = connection.QuerySingle<Customer>(
    "SELECT 7 AS customer_id, 'Ada' AS Name;");
```

Call `FluentMapper.Initialize(...)` during application startup and treat the effective global configuration as read-only once queries begin.

## Mapping

Create maps by deriving from `EntityMap<TEntity>`:

```csharp
public sealed class ProductMap : EntityMap<Product>
{
    public ProductMap()
    {
        Map(product => product.Id).ToColumn("product_id");
        Map(product => product.Name).ToColumn("product_name", caseSensitive: false);
        Map(product => product.TransientValue).Ignore();
    }
}
```

Explicit mappings take precedence over conventions. Unmapped root members fall back to Dapper's normal behavior.

Conventions and naming policies cover repeated patterns:

```csharp
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Naming;

public sealed class PrefixConvention : Convention
{
    public PrefixConvention()
    {
        Properties().Configure(property => property.HasPrefix("col"));
    }
}

FluentMapper.Initialize(config =>
{
    config.AddConvention<PrefixConvention>().ForEntity<Customer>();
    config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
        .ForEntity<Order>();
});
```

Available naming policies include `Identity`, `SnakeCase`, `Prefix(...)`, `Suffix(...)`, `Custom(...)`, `Then(...)`, `WithPrefix(...)` and `WithSuffix(...)`.

## Immutable Types

FluentMap participates in Dapper constructor mapping for root-level explicit mappings:

```csharp
public sealed class Customer
{
    public Customer(int id, string fullName)
    {
        Id = id;
        FullName = fullName;
    }

    public int Id { get; }
    public string FullName { get; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.FullName).ToColumn("full_name");
    }
}
```

Use `QueryMapped*` when FluentMap must construct nested immutable objects or value objects.

## Nested Objects

Nested member paths use the same `Map(...)` API:

```csharp
public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Address.City).ToColumn("city");
    }
}

var customer = connection.QueryMappedSingle<Customer>(
    "SELECT 7 AS customer_id, 'Sao Paulo' AS city;");
```

Nested object materialization is opt-in through `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped` and streaming helpers. Normal `Dapper.Query<T>()` remains root-level Dapper materialization.

## Value Objects

For scalar value objects mapped as one database value, prefer a Dapper `TypeHandler<T>`:

```csharp
Map(customer => customer.Cpf).ToColumn("cpf");
```

For value objects mapped through components, FluentMap-controlled materialization can call matching public constructors:

```csharp
public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Cpf.Number).ToColumn("cpf");
    }
}

var customer = connection.QueryMappedSingle<Customer>(
    "SELECT 1 AS customer_id, '12345678909' AS cpf;");
```

Factory methods are not used by the current materializer.

## Profiles

Profiles are opt-in mappings for the same entity under different SQL shapes:

```csharp
using Dapper.FluentMap.Mapping;

public sealed class LegacyProfile : IMappingProfile
{
}

public sealed class LegacyCustomerMap :
    EntityMap<Customer>,
    IProfileMap<LegacyProfile>
{
    public LegacyCustomerMap()
    {
        Map(customer => customer.Id).ToColumn("id");
        Map(customer => customer.Name).ToColumn("legal_name");
    }
}

FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
    config.AddProfile<LegacyCustomerMap>();
});

var legacy = connection.QueryMappedSingle<Customer, LegacyProfile>(
    "SELECT 7 AS id, 'Legacy Ltd.' AS legal_name;");
```

Profiles are selected per FluentMap-controlled query. They do not replace the global Dapper type map for the entity.

## Generated Materialization

Install `Dapper.FluentMap.Generators` when you want generated registration for maps in the current compilation:

```bash
dotnet add package Dapper.FluentMap.Generators
```

Then call the generated extension:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddGeneratedMappings();
});
```

The generator emits `AddMap<TMap>()` and `AddProfile<TMap>()` calls for eligible maps. For supported explicit mappings it can also register generated row materializers for the ordered column shape, including flat properties, nested paths, constructor-built value objects and statically supported read converters.

Generated materialization is an optimization. Unsupported maps, dynamic shapes, shape mismatches, instance/delegate converters and some advanced patterns use the runtime fallback.

## Persistence Semantics

Persistence metadata describes write participation without changing read materialization:

```csharp
Map(product => product.CreatedAt)
    .ToColumn("created_at")
    .DatabaseDefaultOnInsert();

Map(product => product.UpdatedAt)
    .ToColumn("updated_at")
    .ReadOnly();

Map(product => product.Total)
    .ToColumn("total")
    .Computed();
```

`Ignore()` keeps its historical meaning: the property is not materialized by FluentMap and is not part of generated persistence metadata. For database values that should still be selected but not written, use `ReadOnly()`, `Computed()`, `DatabaseDefaultOnInsert()`, `ExcludeFromInsert()` or `ExcludeFromUpdate()`.

The core package stores metadata. Dommel is the current package that consumes it for generated `INSERT` and `UPDATE` behavior.

## QueryMultiple / Streaming

Use FluentMap query helpers when materialization must honor nested mappings, value objects, profiles, converters or generated materializers:

```csharp
var customers = connection.QueryMapped<Customer>(sql);
var customer = connection.QueryMappedSingle<Customer>(sql);
var legacy = connection.QueryMappedSingle<Customer, LegacyProfile>(legacySql);
```

For multiple result sets:

```csharp
using var multi = connection.QueryMultipleMapped(sql);

var customers = multi.ReadMapped<Customer>();
var orders = multi.ReadMapped<Order>();
```

`ReadMapped*` consumes result sets sequentially and buffers the current result set.

For incremental processing:

```csharp
foreach (var customer in connection.QueryMappedUnbuffered<Customer>(sql))
{
    Process(customer);
}
```

Async streaming is available on `DbConnection`:

```csharp
await foreach (var customer in connection.QueryMappedUnbufferedAsync<Customer>(
    sql,
    cancellationToken))
{
    await ProcessAsync(customer, cancellationToken);
}
```

Streaming keeps the underlying reader open until enumeration completes or the enumerator is disposed.

## Property Converters

Property converters are configured per mapped property and run only during FluentMap-controlled materialization:

```csharp
public sealed class ProductMap : EntityMap<Product>
{
    public ProductMap()
    {
        Map(product => product.Status)
            .ToColumn("status_code")
            .ConvertFromDatabaseUsing<ProductStatusConverter, string>();
    }
}

public sealed class ProductStatusConverter :
    IReadPropertyConverter<string, ProductStatus>
{
    public ProductStatus ConvertFromDatabase(string value)
    {
        return value == "A" ? ProductStatus.Active : ProductStatus.Inactive;
    }
}
```

Read conversion precedence in FluentMap-controlled materialization is:

```text
null/DBNull handling
    -> property read converter
    -> Dapper TypeHandler<TProperty>
    -> FluentMap default conversion
```

Write converter metadata can be configured, but it is not currently executed by Dapper or Dommel writes.

## Isolated Configuration / DI

The historical static API remains supported:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});
```

For multiple FluentMap-controlled configurations in the same process, build immutable configurations and use their runtimes:

```csharp
using Dapper.FluentMap.Configuration;

var runtime = new FluentMapConfigurationBuilder()
    .AddMap<CustomerMap>()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryMappedSingle<Customer>(
    connection,
    "SELECT 7 AS customer_id, 'Ada' AS Name;");
```

Install `Dapper.FluentMap.DependencyInjection` for DI registration:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
    builder.Configure(config => config.AddGeneratedMappings());
});
```

The DI package registers `ImmutableFluentMapConfiguration` and `FluentMapRuntime` as singletons. It does not register database connections, repositories, Dommel bridges or global Dapper type maps.

## AOT / Trimming

FluentMap has partial trimming/AOT readiness, not full Native AOT compatibility:

| Area | Status |
| --- | --- |
| Explicit registration with `AddMap<TMap>()` | Preferred for trimming and Native AOT scenarios. |
| Generated registration with `AddGeneratedMappings()` | Preferred alternative to assembly scanning for maps in the current compilation. |
| Assembly scanning | Reflection-based and annotated as trimming-sensitive. |
| `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped`, streaming | Annotated as trimming/dynamic-code sensitive because runtime fallback can occur. |

Do not treat the package as fully Native AOT safe unless your application validates the exact query path and deployment mode.

## Compatibility

Current compatibility documentation lives in [COMPATIBILITY.md](COMPATIBILITY.md).

Short version:

- public packages target `netstandard2.0`;
- tests currently run on `net10.0`;
- Dapper range is `[2.1.79,3.0.0)`, with `2.1.79` validated in the current matrix;
- Dommel range is `[3.5.3,4.0.0)` for the optional Dommel package;
- SQLite is validated by automated provider tests;
- SQL Server and PostgreSQL have conditional harnesses but are not certified in CI yet;
- MySQL/MariaDB is not validated;
- SQL Server CE remains legacy/upstream-limited.

For users moving from FluentMap 2.x, see [MIGRATION.md](MIGRATION.md).

## Current Limitations

- `FluentMapper.Initialize(...)`, normal `Dapper.Query<T>()` and Dommel integrations use process-wide global state.
- Isolated runtimes apply to FluentMap-controlled materialization, not to normal Dapper queries or Dommel.
- Dommel uses global `DommelMapper` resolvers/builders.
- `QueryMultipleMapped` is sequential and buffered per result set; there is no `QueryMultipleMappedAsync`.
- `QueryMultipleMapped` is not Dapper multi-mapping with `splitOn`.
- FluentMap does not aggregate joined rows into graphs or maintain identity maps.
- Write converters are metadata-only in the current Dapper/Dommel write path.
- Generated materializers cover a supported subset and can fall back to runtime materialization.
- Assembly scanning and runtime fallback are trimming/AOT-sensitive.
- Value object construction uses compatible public constructors, not factory methods.

## More Documentation

- [Migration from 2.x](MIGRATION.md)
- [Compatibility](COMPATIBILITY.md)
- [Support](SUPPORT.md)
- [Changelog](CHANGELOG.md)
- [Português (Brasil)](README.pt-BR.md)

## Contributing

Keep changes small, compatible with the public API and covered by focused tests. Typical local validation:

```bash
dotnet restore ./Dapper.FluentMap.sln
dotnet build ./Dapper.FluentMap.sln --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.sln --configuration Release --no-build
```

## License

FluentMap is licensed under the [MIT License](LICENSE).
