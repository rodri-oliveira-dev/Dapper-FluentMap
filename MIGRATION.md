# Migration Guide

This guide is for users moving from FluentMap 2.x to the 3.0 line in the original `Dapper.FluentMap` repository.

```text
Dapper.FluentMap 2.x
        ↓
Dapper.FluentMap 3.0
```

Most existing root-level maps should not need source changes. The historical API remains supported while 3.0 adds opt-in capabilities for advanced materialization, generated registration, persistence metadata, property converters, profiles and isolated configuration.

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

Do not migrate working historical maps just because newer APIs exist. Prefer the newer APIs when they solve a concrete problem.

## Packages

Install only the packages you use:

| Package | When to install |
| --- | --- |
| `Dapper.FluentMap` | Core mapping and Dapper integration. |
| `Dapper.FluentMap.Dommel` | Dommel table/key/generated-column integration. |
| `Dapper.FluentMap.DependencyInjection` | DI registration of immutable configuration and runtime. |
| `Dapper.FluentMap.Analyzers` | Compile-time diagnostics for mapping mistakes. |
| `Dapper.FluentMap.Generators` | Generated registration and supported generated materializers. |

## Initialize

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

Version 3.0 also publishes `FluentMapper.Configuration` and `FluentMapper.Runtime` after initialization. Existing code does not need to use those properties.

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

Version 3.0 adds naming policies for common transformations:

```csharp
config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
    .ForEntity<Customer>();
```

Precedence remains explicit mapping first, then convention/naming policy, then Dapper default behavior.

## Nested Objects

Historical FluentMap mainly helps Dapper map root-level members. Nested object materialization in 3.0 is opt-in:

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

Profiles are new opt-in mappings for alternate SQL shapes:

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

## Ignore

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

## Property Converters

Property converters are new. They run only in FluentMap-controlled materialization:

```csharp
Map(product => product.Status)
    .ToColumn("status_code")
    .ConvertFromDatabaseUsing<ProductStatusConverter, string>();
```

Normal `Dapper.Query<T>()` does not execute property converters. Use Dapper `TypeHandler<T>` for type-wide conversion.

Write converter metadata exists, but Dapper/Dommel writes do not execute it yet.

## Generated Registration

Install `Dapper.FluentMap.Generators` and call:

```csharp
config.AddGeneratedMappings();
```

This can replace manual registration for eligible maps in the current compilation. It does not scan referenced assemblies and does not remove the need for runtime validation.

Generated materializers are an optimization. Unsupported cases fall back to runtime materialization.

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

## DI

Install `Dapper.FluentMap.DependencyInjection` and register:

```csharp
services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
});
```

The DI package registers `ImmutableFluentMapConfiguration` and `FluentMapRuntime` as singletons. It does not register database connections, repositories, Dommel integration or global Dapper type maps.

## Dommel

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

- Dommel persistence metadata has new behavior for read-only, computed, insert-excluded and update-excluded properties.
- Some contradictory configurations that were previously accepted by accident now fail validation.
- `DommelPropertyMap.GeneratedOption` has changed from non-nullable to nullable in the 3.0 line; treat binary compatibility with historical Dommel `2.0.0` as not guaranteed.
- Generated materialization and isolated runtime APIs are additive, but the stable release still requires a defined 3.0 API baseline.

## Recommended Migration Path

1. Keep existing `EntityMap<TEntity>` maps and `FluentMapper.Initialize(...)`.
2. Run the full test suite of your application against the 3.0 package.
3. Replace historical `Ignore()` write-workarounds with persistence metadata where needed.
4. Move nested/value-object reads to `QueryMapped*` only where required.
5. Add profiles only for alternate SQL shapes.
6. Add isolated runtime/DI only when you need multiple configurations or host integration.
7. Add analyzers and generators after the runtime behavior is already understood.
