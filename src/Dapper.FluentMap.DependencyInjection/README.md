# Dapper.FluentMap.DependencyInjection

Dependency injection integration for Dapper.FluentMap.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
    builder.Configure(config => config.AddGeneratedMappings());
});
```

`AddFluentMap(...)` builds and validates the configuration during service
composition, then registers `ImmutableFluentMapConfiguration` and
`FluentMapRuntime` as singleton services.

The package does not register database connections, repositories, Dommel
bridges or global Dapper type maps. Use explicit or generated map registration
for trimmed and Native AOT applications.
