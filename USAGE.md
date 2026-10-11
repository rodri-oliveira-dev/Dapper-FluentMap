# FluentMap Usage Guide

This guide contains practical examples for the maintained FluentMap 3.x line.

For a short introduction and installation instructions, start with [README.md](README.md). For upgrades from FluentMap 2.x, see [MIGRATION.md](MIGRATION.md). For supported dependency ranges, providers and AOT boundaries, see [COMPATIBILITY.md](COMPATIBILITY.md).

## Basic Mapping

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

Register maps during application startup:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<ProductMap>();
});

FluentMapper.Validate();
```

Normal Dapper calls continue to use the global Dapper type-map bridge for root-level mappings:

```csharp
var products = connection.Query<Product>(sql);
```

## Conventions And Naming Policies

Repeated naming rules can be expressed with conventions:

```csharp
using Dapper.FluentMap.Conventions;

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
});
```

Naming policies provide common transformations:

```csharp
using Dapper.FluentMap.Naming;

FluentMapper.Initialize(config =>
{
    config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
        .ForEntity<Order>();
});
```

Explicit mappings take precedence over conventions and naming policies. Unmapped root members fall back to Dapper's normal behavior.

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

Use FluentMap-controlled query APIs when immutable construction also involves nested objects or value objects.

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

Nested materialization is opt-in. Normal `Dapper.Query<T>()` remains root-level materialization.

## Value Objects

For a value object stored as a single database value, prefer a Dapper `TypeHandler<T>` when the representation is global for the type:

```csharp
Map(customer => customer.Cpf).ToColumn("cpf");
```

For value objects mapped through components, use FluentMap-controlled materialization:

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

The runtime materializer uses compatible public constructors by default. A map can opt into an explicit factory when construction cannot follow that rule:

```csharp
public CustomerMap()
{
    Map(customer => customer.Id).ToColumn("customer_id");
    Map(customer => customer.Name).ToColumn("customer_name");
    ConstructUsing(customer => customer.Id, customer => customer.Name, Customer.Restore);
}
```

`ConstructUsing` supports one to four explicitly mapped root-property values, validates every binding, and is preserved by isolated runtimes. Nested property paths are rejected during configuration because explicit factories currently bind only the root materialization node. Call `ConstructUsing` directly from the map constructor: the source generator analyzes constructor invocations and does not follow helper-method calls. Delegate factories are runtime-materialization strategies: the source generator registers the map but reports `DFM011` and does not emit a generated materializer. Strict generated mode therefore rejects that shape instead of silently using the factory through reflection.

## Mapping Profiles

Profiles let the same entity use alternate SQL shapes without replacing its default global Dapper type map:

```csharp
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

var customer = connection.QueryMappedSingle<Customer, LegacyProfile>(
    "SELECT 7 AS id, 'Legacy Ltd.' AS legal_name;");
```

Profiles are selected per FluentMap-controlled query.

## FluentMap-Controlled Queries

Use `QueryMapped*` when materialization must honor nested mappings, value objects, profiles, converters or generated materializers:

```csharp
var customers = connection.QueryMapped<Customer>(sql);
var customer = connection.QueryMappedSingle<Customer>(sql);
var legacy = connection.QueryMappedSingle<Customer, LegacyProfile>(legacySql);
```

These APIs complement normal Dapper queries; they do not replace them for simple root-level mappings.

## Multi-Mapping

For rows that contain two mapped entities, use explicit `splitOn` and a composition delegate:

```csharp
var rows = connection.QueryMapped<Customer, Order, CustomerOrder>(
    sql,
    (customer, order) => new CustomerOrder(customer, order),
    splitOn: "order_id");
```

Each segment is materialized through FluentMap. Per-segment profile overloads are available when each segment needs a different profile.

For common `LEFT JOIN` scenarios, if all columns in the second segment are `NULL`, the second argument can represent an absent child instead of forcing an invalid object.

This API performs row splitting; it does not aggregate repeated rows into one-to-many object graphs.

Three input segments are also supported through the same segment pipeline, including async and isolated-runtime variants:

```csharp
var rows = connection.QueryMapped<Customer, Order, Shipment, CustomerOrderShipment>(
    sql,
    (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
    splitOn: "order_id,shipment_id");
```

Three-type calls require two comma-separated, unique boundaries in result order. Empty, missing, duplicated, ambiguous or out-of-order boundaries fail deterministically. Any all-`NULL` child segment is passed as `null`. The supported public arity is deliberately capped at three input types; compose rows manually for wider shapes.

## Multiple Result Sets

Use mapped multiple-result APIs when each result set needs FluentMap-controlled materialization:

```csharp
using var multi = connection.QueryMultipleMapped(sql);

var customers = multi.ReadMapped<Customer>();
var orders = multi.ReadMapped<Order>();
```

Async variants are available:

```csharp
await using var multi = await connection.QueryMultipleMappedAsync(
    sql,
    cancellationToken: cancellationToken);

var customers = await multi.ReadMappedAsync<Customer>(cancellationToken);
var orders = await multi.ReadMappedAsync<Order>(cancellationToken);
```

Result sets are consumed in order. Concurrent reads on the same `MappedGridReader` are rejected deterministically.

## Streaming

For incremental synchronous processing:

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

Streaming keeps the underlying reader open until enumeration finishes or the enumerator is disposed.

## Property Converters

Property converters apply to a specific mapped property during FluentMap-controlled materialization:

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
        return value == "A"
            ? ProductStatus.Active
            : ProductStatus.Inactive;
    }
}
```

Read conversion precedence is:

```text
null/DBNull handling
    -> property read converter
    -> Dapper TypeHandler<TProperty>
    -> FluentMap default conversion
```

Normal `Dapper.Query<T>()` does not execute property converters. Use Dapper `TypeHandler<T>` for type-wide conversion.

Dommel write conversion is opt-in through `InsertMapped*` and `UpdateMapped*` from `Dapper.FluentMap.Dommel`:

```csharp
var id = connection.InsertMapped(product);
product.Id = Convert.ToInt32(id);
connection.UpdateMapped(product);
```

Only properties participating in the selected operation are converted. Converter output is then passed to Dapper, so a registered Dapper type handler for the converter output type runs after the property converter. Converter failures include entity, property, column and operation context and never retry with the original value. Historical Dommel `Insert`/`Update` remain unchanged because Dommel exposes no per-property parameter hook.

## Persistence Metadata And Dommel

Persistence metadata controls write participation while keeping values readable:

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

Semantics:

| Mapping | Read | Insert | Update |
| --- | --- | --- | --- |
| default | yes | yes | yes |
| `Ignore()` | no | no | no |
| `ReadOnly()` | yes | no | no |
| `Computed()` | yes | no | no |
| `DatabaseDefaultOnInsert()` | yes | no | yes |
| `ExcludeFromInsert()` | yes | no | yes |
| `ExcludeFromUpdate()` | yes | yes | no |

The core package stores the metadata. `Dapper.FluentMap.Dommel` consumes it for supported generated `INSERT` and `UPDATE` behavior.

Configure Dommel through the historical global integration:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<ProductMap>();
    config.ForDommel();
});
```

`DommelEntityMap<TEntity>`, `IsKey()`, `IsIdentity()` and `SetGeneratedOption(...)` remain the Dommel-specific mapping surface.

Provider support for this optional package is intentionally narrower than FluentMap Core support. This repository certifies Dommel persistence with SQLite, SQL Server, PostgreSQL, MySQL and MariaDB. MySQL and MariaDB share the `MySqlConnection`/`MySqlSqlBuilder` path. Oracle and Firebird are Core-only because Dommel 3.5.3 has no built-in SQL builder for either provider. The SQL Server CE builder registration remains available for compatibility, but it is legacy/upstream-limited and has no modern certification lane.

Advanced consumers can register an `ISqlBuilder` through Dommel's public `DommelMapper.AddSqlBuilder(Type, ISqlBuilder)` or `AddSqlBuilder(string, ISqlBuilder)` APIs. Registration is process-wide, and the application owns validation of the generated SQL and persistence behavior. A custom registration does not extend this project's certified provider matrix.

## Generated Registration And Materialization

Install the source-generator package:

```bash
dotnet add package FluentMap.Generators
```

Then use the generated registration extension:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddGeneratedMappings();
});
```

The generator discovers eligible maps in the current compilation and can register supported generated materializers.

Generated materialization is normally an optimization: unsupported cases use the runtime fallback.

## Strict Generated Materialization

Applications that require a generated-only path can opt into strict generated materialization:

```csharp
var runtime = new FluentMapConfigurationBuilder()
    .Configure(config => config.AddGeneratedMappings())
    .UseStrictGeneratedMaterialization()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryGeneratedMappedSingle<Customer>(
    connection,
    "SELECT @Id AS customer_id, @Name AS customer_name, 'trace' AS trace_id;",
    new GeneratedParameters()
        .Add("Id", 7, System.Data.DbType.Int32)
        .Add("Name", "Ada", System.Data.DbType.String, size: 100));
```

`GeneratedParameters` binds only explicitly named input values with an explicit `DbType`; it does not inspect anonymous objects. Parameterless commands remain supported. Arbitrary parameter objects fail before the connection is opened, with `QueryMapped*` identified as the non-strict alternative.

Generated materializers accept reordered required columns and additional columns only when the additional names do not resolve to explicitly configured FluentMap members. Missing required columns, duplicate names, explicitly mapped additional columns, incompatible provider values and unsupported nested shapes fail deterministically instead of silently using runtime materialization. Strict generated resolution does not reflect over convention-only entity members; use the non-strict API when Dapper default-member discovery is required.

The strict generated path has a narrower supported contract than normal FluentMap-controlled materialization. Review [COMPATIBILITY.md](COMPATIBILITY.md) before using it for trimming or Native AOT deployments.

## Isolated Configuration

Use immutable configuration and runtime instances when multiple FluentMap-controlled configurations must coexist in the same process:

```csharp
using Dapper.FluentMap.Configuration;

var runtime = new FluentMapConfigurationBuilder()
    .AddMap<CustomerMap>()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryMappedSingle<Customer>(
    connection,
    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");
```

Isolation applies to FluentMap-controlled materialization. It does not isolate normal `Dapper.Query<T>()` type maps or Dommel's process-wide configuration.

## Dependency Injection

Install:

```bash
dotnet add package FluentMap.DependencyInjection
```

Register FluentMap:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
    builder.Configure(config => config.AddGeneratedMappings());
});
```

The DI package registers `ImmutableFluentMapConfiguration` and `FluentMapRuntime` as singletons. It does not register database connections, repositories, Dommel integration or global Dapper type maps.

## Analyzers

Install compile-time mapping diagnostics with:

```bash
dotnet add package FluentMap.Analyzers
```

Analyzers complement runtime validation. They do not execute mapping constructors, access databases or replace `FluentMapper.Validate()`.

## Trimming And Native AOT

FluentMap has trimming-aware APIs and a validated strict generated Native AOT smoke path, but full Native AOT compatibility is not claimed.

Prefer explicit or generated registration over assembly scanning in trimming/AOT scenarios, and review [COMPATIBILITY.md](COMPATIBILITY.md) for the current supported boundary.

## Related Documentation

- [README](README.md)
- [Migration guide](MIGRATION.md)
- [Compatibility](COMPATIBILITY.md)
- [Changelog](CHANGELOG.md)
- [Support](SUPPORT.md)
- [Português (Brasil)](USAGE.pt-BR.md)
