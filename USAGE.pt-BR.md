# Guia de Uso do FluentMap

Este guia reúne exemplos práticos para a linha FluentMap 3.x mantida atualmente.

Para uma introdução curta e instruções de instalação, comece pelo [README.pt-BR.md](README.pt-BR.md). Para migração do FluentMap 2.x, consulte [MIGRATION.pt-BR.md](MIGRATION.pt-BR.md). Para faixas suportadas de dependências, providers e limites de AOT, consulte [COMPATIBILITY.md](COMPATIBILITY.md).

## Mapping Básico

Crie maps herdando de `EntityMap<TEntity>`:

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

Registre os maps durante o startup da aplicação:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<ProductMap>();
});

FluentMapper.Validate();
```

Chamadas normais do Dapper continuam usando o bridge global de type map para mappings no nível raiz:

```csharp
var products = connection.Query<Product>(sql);
```

## Convenções e Naming Policies

Regras repetidas de nomenclatura podem ser expressas com conventions:

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

Naming policies oferecem transformações comuns:

```csharp
using Dapper.FluentMap.Naming;

FluentMapper.Initialize(config =>
{
    config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
        .ForEntity<Order>();
});
```

Mappings explícitos têm precedência sobre conventions e naming policies. Membros raiz não mapeados usam o comportamento normal do Dapper.

## Tipos Imutáveis

FluentMap participa do constructor mapping do Dapper para mappings explícitos no nível raiz:

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

Use APIs de query controladas pelo FluentMap quando a construção imutável também envolver objetos aninhados ou value objects.

## Objetos Aninhados

Caminhos aninhados usam a mesma API `Map(...)`:

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

A materialização aninhada é opt-in. `Dapper.Query<T>()` normal continua fazendo materialização no nível raiz.

## Value Objects

Para um value object armazenado como um único valor do banco, prefira um `TypeHandler<T>` do Dapper quando a representação for global para o tipo:

```csharp
Map(customer => customer.Cpf).ToColumn("cpf");
```

Para value objects mapeados por componentes, use materialização controlada pelo FluentMap:

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

Por padrão, o materializador de runtime usa construtores públicos compatíveis. Um mapping pode optar por uma factory explícita quando essa regra não for suficiente:

```csharp
public CustomerMap()
{
    Map(customer => customer.Id).ToColumn("customer_id");
    Map(customer => customer.Name).ToColumn("customer_name");
    ConstructUsing(customer => customer.Id, customer => customer.Name, Customer.Restore);
}
```

`ConstructUsing` aceita de um a quatro valores explicitamente mapeados, valida cada binding e é preservado por runtimes isolados. Factories delegate são estratégias de materialização em runtime: o source generator registra o map, mas reporta `DFM011` e não emite materializador gerado. Portanto, o modo strict generated rejeita esse shape sem fallback silencioso.

## Mapping Profiles

Profiles permitem que a mesma entidade use formatos SQL alternativos sem substituir seu type map global padrão do Dapper:

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

Profiles são selecionados por query controlada pelo FluentMap.

## Queries Controladas pelo FluentMap

Use `QueryMapped*` quando a materialização precisar respeitar nested mappings, value objects, profiles, converters ou materializadores gerados:

```csharp
var customers = connection.QueryMapped<Customer>(sql);
var customer = connection.QueryMappedSingle<Customer>(sql);
var legacy = connection.QueryMappedSingle<Customer, LegacyProfile>(legacySql);
```

Essas APIs complementam queries normais do Dapper; elas não substituem o Dapper para mappings simples no nível raiz.

## Multi-Mapping

Para linhas que contêm duas entidades mapeadas, use `splitOn` explícito e um delegate de composição:

```csharp
var rows = connection.QueryMapped<Customer, Order, CustomerOrder>(
    sql,
    (customer, order) => new CustomerOrder(customer, order),
    splitOn: "order_id");
```

Cada segmento é materializado pelo FluentMap. Existem overloads de profile por segmento quando cada parte precisa de um profile diferente.

Em cenários comuns com `LEFT JOIN`, se todas as colunas do segundo segmento forem `NULL`, o segundo argumento pode representar a ausência do filho sem forçar a criação de um objeto inválido.

Essa API faz split de uma linha; ela não agrega linhas repetidas em grafos um-para-muitos.

Três segmentos de entrada também são suportados pelo mesmo pipeline, inclusive nas variantes assíncronas e de runtime isolado:

```csharp
var rows = connection.QueryMapped<Customer, Order, Shipment, CustomerOrderShipment>(
    sql,
    (customer, order, shipment) => new CustomerOrderShipment(customer, order, shipment),
    splitOn: "order_id,shipment_id");
```

Chamadas com três tipos exigem duas fronteiras únicas, separadas por vírgula e na ordem do resultado. Fronteiras vazias, ausentes, duplicadas, ambíguas ou fora de ordem falham deterministicamente. Cada segmento filho contendo apenas `NULL` é entregue como `null`. A aridade pública é deliberadamente limitada a três tipos de entrada; componha manualmente shapes mais largos.

## Múltiplos Result Sets

Use as APIs de múltiplos resultados mapeados quando cada result set precisar de materialização controlada pelo FluentMap:

```csharp
using var multi = connection.QueryMultipleMapped(sql);

var customers = multi.ReadMapped<Customer>();
var orders = multi.ReadMapped<Order>();
```

Também existem variantes assíncronas:

```csharp
await using var multi = await connection.QueryMultipleMappedAsync(
    sql,
    cancellationToken: cancellationToken);

var customers = await multi.ReadMappedAsync<Customer>(cancellationToken);
var orders = await multi.ReadMappedAsync<Order>(cancellationToken);
```

Os result sets são consumidos em ordem. Leituras concorrentes no mesmo `MappedGridReader` são rejeitadas deterministicamente.

## Streaming

Para processamento incremental síncrono:

```csharp
foreach (var customer in connection.QueryMappedUnbuffered<Customer>(sql))
{
    Process(customer);
}
```

Streaming assíncrono está disponível em `DbConnection`:

```csharp
await foreach (var customer in connection.QueryMappedUnbufferedAsync<Customer>(
    sql,
    cancellationToken))
{
    await ProcessAsync(customer, cancellationToken);
}
```

O streaming mantém o reader subjacente aberto até a enumeração terminar ou o enumerator ser descartado.

## Property Converters

Property converters se aplicam a uma propriedade mapeada específica durante a materialização controlada pelo FluentMap:

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

A precedência de conversão de leitura é:

```text
tratamento de null/DBNull
    -> read converter da propriedade
    -> Dapper TypeHandler<TProperty>
    -> conversão default do FluentMap
```

`Dapper.Query<T>()` normal não executa property converters. Use `TypeHandler<T>` do Dapper para conversão global por tipo.

A conversão de escrita do Dommel é opt-in por `InsertMapped*` e `UpdateMapped*`, do pacote `Dapper.FluentMap.Dommel`:

```csharp
var id = connection.InsertMapped(product);
product.Id = Convert.ToInt32(id);
connection.UpdateMapped(product);
```

Somente propriedades participantes da operação escolhida são convertidas. A saída do conversor é entregue ao Dapper; portanto, um type handler registrado para o tipo de saída é executado depois do conversor da propriedade. Falhas incluem contexto de entidade, propriedade, coluna e operação, sem nova tentativa com o valor original. Os métodos históricos `Insert`/`Update` do Dommel permanecem inalterados porque o Dommel não oferece hook por parâmetro/propriedade.

## Metadata de Persistência e Dommel

Metadata de persistência controla a participação em escrita mantendo os valores disponíveis para leitura:

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

Semântica:

| Mapping | Leitura | Insert | Update |
| --- | --- | --- | --- |
| padrão | sim | sim | sim |
| `Ignore()` | não | não | não |
| `ReadOnly()` | sim | não | não |
| `Computed()` | sim | não | não |
| `DatabaseDefaultOnInsert()` | sim | não | sim |
| `ExcludeFromInsert()` | sim | não | sim |
| `ExcludeFromUpdate()` | sim | sim | não |

O pacote core armazena a metadata. `Dapper.FluentMap.Dommel` a consome para comportamentos suportados de `INSERT` e `UPDATE` gerados.

Configure Dommel pela integração global histórica:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<ProductMap>();
    config.ForDommel();
});
```

`DommelEntityMap<TEntity>`, `IsKey()`, `IsIdentity()` e `SetGeneratedOption(...)` continuam sendo a superfície de mapping específica do Dommel.

## Registro e Materialização Gerados

Instale o pacote de source generator:

```bash
dotnet add package FluentMap.Generators
```

Depois use a extensão de registro gerada:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddGeneratedMappings();
});
```

O generator descobre maps elegíveis na compilação atual e pode registrar materializadores gerados suportados.

Materialização gerada normalmente é uma otimização: casos não suportados usam fallback runtime.

## Materialização Gerada Estrita

Aplicações que exigem um caminho exclusivamente gerado podem habilitar materialização gerada estrita:

```csharp
var runtime = new FluentMapConfigurationBuilder()
    .Configure(config => config.AddGeneratedMappings())
    .UseStrictGeneratedMaterialization()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryGeneratedMappedSingle<Customer>(
    connection,
    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");
```

Shapes não suportados falham deterministicamente em vez de usar silenciosamente materialização runtime.

O caminho gerado estrito possui um contrato suportado mais restrito do que a materialização normal controlada pelo FluentMap. Revise [COMPATIBILITY.md](COMPATIBILITY.md) antes de usá-lo em deployments com trimming ou Native AOT.

## Configuração Isolada

Use configuração imutável e instâncias de runtime quando múltiplas configurações controladas pelo FluentMap precisarem coexistir no mesmo processo:

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

O isolamento se aplica à materialização controlada pelo FluentMap. Ele não isola os type maps de `Dapper.Query<T>()` normal nem a configuração process-wide do Dommel.

## Dependency Injection

Instale:

```bash
dotnet add package FluentMap.DependencyInjection
```

Registre o FluentMap:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
    builder.Configure(config => config.AddGeneratedMappings());
});
```

O pacote de DI registra `ImmutableFluentMapConfiguration` e `FluentMapRuntime` como singletons. Ele não registra conexões de banco, repositories, integração Dommel ou type maps globais do Dapper.

## Analyzers

Instale diagnósticos de mapping em tempo de compilação com:

```bash
dotnet add package FluentMap.Analyzers
```

Analyzers complementam a validação em runtime. Eles não executam construtores de mapping, não acessam bancos e não substituem `FluentMapper.Validate()`.

## Trimming e Native AOT

FluentMap possui APIs conscientes de trimming e um caminho de smoke Native AOT gerado estrito validado, mas compatibilidade Native AOT completa não é afirmada.

Prefira registro explícito ou gerado em vez de assembly scanning em cenários com trimming/AOT e revise [COMPATIBILITY.md](COMPATIBILITY.md) para o limite de suporte atual.

## Documentação Relacionada

- [README](README.pt-BR.md)
- [Guia de migração](MIGRATION.pt-BR.md)
- [Compatibilidade](COMPATIBILITY.md)
- [Changelog](CHANGELOG.md)
- [Suporte](SUPPORT.md)
- [English](USAGE.md)
