# Dapper.FluentMap.Generators

Build-time source generator for Dapper.FluentMap mapping registration and supported row materializers.

The generator discovers eligible `IEntityMap<TEntity>` implementations declared in the current compilation and emits an `AddGeneratedMappings()` extension method that registers them through the existing `AddMap<TMap>()` / `AddProfile<TMap>()` APIs. For explicit maps with literal columns and supported deterministic construction, it also registers generated `IDataRecord -> entity` materializers for the matching ordered column shape, including flat properties, nested object paths and constructor-built Value Objects.

```bash
dotnet add package Dapper.FluentMap.Generators
```

```csharp
using Dapper.FluentMap;

FluentMapper.Initialize(config =>
{
    config.AddGeneratedMappings();
});
```

Generated registration avoids reflection-based assembly scanning for maps in the current compilation. It does not scan referenced assemblies, execute map constructors during generation, parse SQL or replace `FluentMapper.Validate()`. Unsupported maps and unexpected column shapes continue to use the runtime fallback.
