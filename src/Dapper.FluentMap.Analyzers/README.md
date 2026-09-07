# Dapper.FluentMap.Analyzers

Roslyn analyzers for statically provable `Dapper.FluentMap` configuration errors.

Install it alongside the core package when you want compile-time feedback for invalid map expressions, duplicate member paths, duplicate columns, invalid `IncludeBase<TBase>()` usage, invalid generic map/profile registration, invalid type-based property converters and duplicate converter configuration in a fluent chain.

```bash
dotnet add package Dapper.FluentMap.Analyzers
```

The analyzer package complements runtime validation. It does not execute user mapping constructors, scan assemblies, access databases or replace `FluentMapper.Validate()`.
