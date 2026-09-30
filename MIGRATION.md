# Migration Guide: 2.x to 3.x

This guide is for users migrating from the historical Dapper.FluentMap 2.x line to the maintained 3.x line.

```text
Dapper.FluentMap 2.x
        ↓
Dapper.FluentMap 3.x
```

For most applications that use root-level `EntityMap<TEntity>` mappings with `FluentMapper.Initialize(...)` and normal Dapper queries, the migration is primarily a package upgrade plus validation and testing. The historical API remains supported, and the newer 3.x capabilities are opt-in.

Do not rewrite working historical maps only because newer APIs exist. Adopt the newer APIs when they solve a concrete problem.

## TL;DR

If your application looks like this:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});

var customer = connection.QuerySingle<Customer>(sql);
```

and your maps are root-level mappings such as:

```csharp
public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
        Map(customer => customer.TransientValue).Ignore();
    }
}
```

you normally do not need source changes to migrate to 3.x.

Upgrade the package, validate the configuration, and run your application test suite.

## Before You Upgrade

Before changing FluentMap package versions:

- ensure the application uses a supported Dapper version;
- if using Dommel, ensure it uses a supported Dommel version;
- keep all FluentMap packages on the same release version;
- run the existing test suite before and after the upgrade;
- identify whether the application uses Dommel, `Ignore()` for database-generated columns, assembly scanning, custom Dapper `TypeHandler<T>` implementations, trimming or Native AOT.

Current supported package ranges are documented in [COMPATIBILITY.md](COMPATIBILITY.md). The Dapper values are governed by `eng/compatibility-contract.json` and validated against package metadata and CI. At the time of this guide:

```text
Dapper [2.1.79,3.0.0)
Dommel [3.5.3,4.0.0)
```

Provider certification and exact tested versions are also maintained in [COMPATIBILITY.md](COMPATIBILITY.md).

## Minimal Migration

For applications that only use the historical root-level mapping model, the recommended first migration step is intentionally small.

Keep:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
    config.AddMap<OrderMap>();
});

FluentMapper.Validate();
```

Keep normal Dapper calls:

```csharp
var customer = connection.QuerySingle<Customer>(sql);
var customers = connection.Query<Customer>(sql);
```

Then:

1. update FluentMap packages to the same 3.x release;
2. restore and build the application;
3. call `FluentMapper.Validate()` during startup or validation tests;
4. run unit and integration tests;
5. review the scenario-specific items below only when they apply.

## Migration Decision Table

| If your application uses | Migration action |
| --- | --- |
| `EntityMap<T>` + normal `Dapper.Query<T>()` | Usually no source changes. Keep the historical API. |
| `FluentMapper.Initialize(...)` | Keep it when the process has one effective global configuration. |
| Dommel | Review persistence and generated-column behavior and run real write tests. |
| `Ignore()` only to avoid writing a generated/default column | Replace that workaround with persistence metadata. |
| Nested objects | Use `QueryMapped*` only where FluentMap must materialize nested paths. |
| Value objects stored as one scalar value | Keep using Dapper `TypeHandler<T>` when the representation is global for the type. |
| Value objects mapped through components | Use FluentMap-controlled materialization such as `QueryMapped*`. |
| Assembly scanning | It remains available; prefer explicit or generated registration for trimming/Native AOT. |
| Multiple mapping configurations in one process | Consider `FluentMapRuntime` and immutable configuration. |
| Dependency Injection | Add `FluentMap.DependencyInjection` only when host integration is needed. |
| Trimming or Native AOT | Prefer explicit/generated registration and review the documented AOT boundary. |
| Alternate SQL shapes for one entity | Consider mapping profiles. |
| Per-property database conversion | Consider property converters for FluentMap-controlled materialization. |

## Packages

Install only the packages you use:

| Package | When to install |
| --- | --- |
| `Dapper.FluentMap` | Core mapping and Dapper integration. |
| `Dapper.FluentMap.Dommel` | Dommel table/key/generated-column integration. |
| `FluentMap.DependencyInjection` | DI registration of immutable configuration and runtime. |
| `FluentMap.Analyzers` | Compile-time diagnostics for mapping mistakes. |
| `FluentMap.Generators` | Generated registration and supported generated materializers. |

The historical core PackageIds are unchanged:

```text
Dapper.FluentMap        -> Dapper.FluentMap
Dapper.FluentMap.Dommel -> Dapper.FluentMap.Dommel
```

The optional modern packages use these NuGet PackageIds:

```text
FluentMap.DependencyInjection
FluentMap.Analyzers
FluentMap.Generators
```

These `FluentMap.*` names are distribution identities only. Assemblies, namespaces and public APIs remain under `Dapper.FluentMap.*`.

The three `FluentMap.*` PackageIds replace the unpublished `Dapper.FluentMap.DependencyInjection`, `Dapper.FluentMap.Analyzers` and `Dapper.FluentMap.Generators` distribution identities.

## What Stays Compatible

The following patterns remain the compatibility path:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});
```

```csharp
public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
        Map(customer => customer.TransientValue).Ignore();
    }
}
```

Normal Dapper calls such as `connection.Query<T>()` continue to use Dapper's global type map bridge for root-level mappings installed by `FluentMapper.Initialize(...)`.

## Initialize And Validation

The historical static initialization remains supported:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
    config.AddMap<OrderMap>();
});

FluentMapper.Validate();
```

Use this when your process has one effective mapping configuration and you want normal `Dapper.Query<T>()` calls to use FluentMap's global Dapper type map bridge.

Version 3.x also publishes `FluentMapper.Configuration` and `FluentMapper.Runtime` after initialization. Existing code does not need to use those properties.

## Registration

Existing explicit registrations remain valid:

```csharp
config.AddMap<CustomerMap>();
config.AddMap(new CustomerMap());
```

Assembly scanning also remains available:

```csharp
config.AddMapsFromAssemblyContaining<CustomerMap>();
```

For trimming and Native AOT deployments, prefer explicit registration or generated registration instead of assembly scanning.

## Conventions

Existing conventions remain supported:

```csharp
config.AddConvention<PrefixConvention>().ForEntity<Customer>();
```

Version 3.x adds naming policies for common transformations:

```csharp
config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
    .ForEntity<Customer>();
```

Precedence remains explicit mapping first, then convention/naming policy, then Dapper default behavior.

## Dommel And Ignore()

If you use Dommel, review every `Ignore()` mapping before upgrading.

`Ignore()` keeps its historical meaning: the property is not mapped for FluentMap materialization and is excluded from generated persistence metadata.

If historical Dommel code used `Ignore()` only to avoid writing a database-generated column while still reading it, migrate that mapping to persistence metadata:

```csharp
Map(entity => entity.CreatedAt)
    .ToColumn("created_at")
    .DatabaseDefaultOnInsert();

Map(entity => entity.UpdatedAt)
    .ToColumn("updated_at")
    .ReadOnly();

Map(entity => entity.Total)
    .ToColumn("total")
    .Computed();
```

Use `Ignore()` only for values that should not be materialized by FluentMap.

After upgrading a Dommel application, test at least:

- inserts;
- updates;
- reads such as `Get`/`GetAll` used by the application;
- identity keys;
- database defaults;
- computed columns;
- read-only columns.

## Persistence Semantics

The core package stores persistence metadata. Dommel consumes this metadata for generated writes:

| Mapping | Read | Insert | Update |
| --- | --- | --- | --- |
| default | yes | yes | yes |
| `Ignore()` | no | no | no |
| `ReadOnly()` | yes | no | no |
| `Computed()` | yes | no | no |
| `DatabaseDefaultOnInsert()` | yes | no | yes |
| `ExcludeFromInsert()` | yes | no | yes |
| `ExcludeFromUpdate()` | yes | yes | no |

The core package still does not generate CRUD SQL.

## Nested Objects

Historical FluentMap mainly helps Dapper map root-level members. Nested object materialization in 3.x is opt-in:

```csharp
public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Address.City).ToColumn("city");
    }
}

var customer = connection.QueryMappedSingle<Customer>(
    "SELECT 'Sao Paulo' AS city;");
```

Use `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped` or streaming helpers when FluentMap must materialize nested paths. Normal `Dapper.Query<T>()` does not become a graph mapper.

## Value Objects

For a value object stored as a single database value, keep using Dapper `TypeHandler<T>` when that representation is global for the type.

For value objects stored through mapped components, use FluentMap-controlled materialization:

```csharp
Map(customer => customer.Cpf.Number).ToColumn("cpf");
```

The current materializer uses compatible public constructors. Factory methods are not used.

## Profiles

Profiles are opt-in mappings for alternate SQL shapes:

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
        Map(customer => customer.Name).ToColumn("legacy_name");
    }
}

config.AddProfile<LegacyCustomerMap>();

var customer = connection.QueryMappedSingle<Customer, LegacyProfile>(sql);
```

Profiles do not replace the default global Dapper type map. Select them per FluentMap-controlled query.

## Property Converters

Property converters run only in FluentMap-controlled materialization:

```csharp
Map(product => product.Status)
    .ToColumn("status_code")
    .ConvertFromDatabaseUsing<ProductStatusConverter, string>();
```

Normal `Dapper.Query<T>()` does not execute property converters. Use Dapper `TypeHandler<T>` for type-wide conversion.

Write converter metadata exists, but Dapper/Dommel writes do not execute it yet.

## Generated Registration

Install `FluentMap.Generators` and call:

```csharp
config.AddGeneratedMappings();
```

This can replace manual registration for eligible maps in the current compilation. It does not scan referenced assemblies and does not remove the need for runtime validation.

Generated materializers are an optimization. Unsupported cases fall back to runtime materialization unless strict generated materialization is explicitly enabled.

Strict generated queries can use `GeneratedParameters` for parameterized commands. Each value has an explicit `DbType`; anonymous objects and other arbitrary parameter bags remain unsupported so strict mode does not introduce reflection-based parameter discovery. Safe reordered and unmapped additional result columns can stay on the generated path, while missing, duplicate/ambiguous or mapped additional columns fail deterministically.

## Configuration Isolation

If your application needs multiple FluentMap configurations in the same process, use immutable configuration and runtime instances:

```csharp
var runtime = new FluentMapConfigurationBuilder()
    .AddMap<CustomerMap>()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryMappedSingle<Customer>(connection, sql);
```

This isolates FluentMap-controlled materialization. It does not isolate normal `Dapper.Query<T>()` because Dapper type maps are global per entity type.

## Dependency Injection

Install `FluentMap.DependencyInjection` and register:

```csharp
services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
});
```

The DI package registers `ImmutableFluentMapConfiguration` and `FluentMapRuntime` as singletons. It does not register database connections, repositories, Dommel integration or global Dapper type maps.

## Dommel Configuration

Dommel remains optional and process-wide:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<ProductMap>();
    config.ForDommel();
});
```

`DommelEntityMap<TEntity>`, `IsKey()`, `IsIdentity()` and `SetGeneratedOption(...)` remain the Dommel-specific mapping surface. Isolated FluentMap runtimes do not configure Dommel.

## Breaking Or Risky Differences To Review

- Dommel persistence metadata has behavior for read-only, computed, insert-excluded and update-excluded properties; verify real write scenarios after upgrading.
- Some contradictory configurations that were previously accepted by accident now fail validation.
- `DommelPropertyMap.GeneratedOption` changed from non-nullable to nullable in the 3.x line; treat binary compatibility with historical Dommel `2.0.0` as not guaranteed.
- Generated materialization and isolated runtime APIs are additive and do not require existing applications to change their historical mapping model.
- Normal `Dapper.Query<T>()`, `FluentMapper.Initialize(...)` and Dommel still involve process-wide global state where documented.

## Recommended Migration Path

1. Confirm Dapper and, when applicable, Dommel versions are within the supported ranges.
2. Keep existing `EntityMap<TEntity>` maps and `FluentMapper.Initialize(...)`.
3. Upgrade all FluentMap packages to the same 3.x release.
4. Run `FluentMapper.Validate()` and the full application test suite.
5. If using Dommel, review every `Ignore()` workaround and test generated/default/computed/read-only columns.
6. Move nested/value-object reads to `QueryMapped*` only where required.
7. Add profiles only for alternate SQL shapes.
8. Add isolated runtime/DI only when you need multiple configurations or host integration.
9. Add analyzers and generators after the runtime behavior is already understood.
10. For trimming or Native AOT, review [COMPATIBILITY.md](COMPATIBILITY.md) and prefer explicit/generated registration.

## Migration Checklist

Your migration is ready when:

- [ ] all FluentMap packages use the same 3.x release version;
- [ ] Dapper is inside the supported version range;
- [ ] Dommel is inside the supported version range, when used;
- [ ] the application builds successfully;
- [ ] `FluentMapper.Validate()` completes without validation errors;
- [ ] existing root-level queries behave as before;
- [ ] Dommel insert/update behavior has been tested, when applicable;
- [ ] identity, database-default, computed and read-only columns have been verified, when applicable;
- [ ] custom Dapper `TypeHandler<T>` behavior has been verified, when used;
- [ ] integration tests pass against the application's actual database provider;
- [ ] trimming/Native AOT scenarios have been validated against the documented support boundary, when applicable.

For current compatibility claims, provider certification and unsupported environments, see [COMPATIBILITY.md](COMPATIBILITY.md).
