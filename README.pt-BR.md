# FluentMap

[English](README.md) | Português (Brasil)

FluentMap é uma camada avançada de mapeamento para Dapper. Ela permite descrever, com uma API fluente e fortemente tipada, como propriedades .NET se conectam a colunas de banco de dados, mantendo atributos de persistência fora dos POCOs.

FluentMap não é um ORM. Ele não faz tracking de entidades, não gera SQL arbitrário, não gerencia conexões, não executa migrations, não oferece LINQ e não substitui o Dapper. Use FluentMap quando o mapeamento padrão por nome do Dapper não for suficiente e as regras de mapeamento precisarem ficar fora do modelo.

## Estado do Projeto

Dapper.FluentMap está sendo modernizado e mantido ativamente novamente. A versão 3.0 continua a história do projeto original, preservando o modelo central de mapeamento do FluentMap e seu caminho de compatibilidade.

A linha 3.0 moderniza a biblioteca e adiciona novos recursos opt-in sem transformar FluentMap em um ORM e sem exigir que aplicações existentes adotem as novas APIs.

## Voltando do FluentMap 2.x?

Se você já usava FluentMap e está retornando ao projeto, os pontos mais importantes de compatibilidade são:

- mappings existentes com `EntityMap<T>` continuam suportados;
- `FluentMapper.Initialize(...)` continua suportado;
- chamadas normais de `Dapper.Query<T>()` continuam funcionando com mappings raiz instalados pela API estática histórica;
- a maioria dos mappings raiz existentes não deve exigir mudanças de código;
- a maior parte dos recursos da 3.0 é opt-in, então não é necessário reescrever mappings que já funcionam apenas porque existem APIs novas.

Consulte [MIGRATION.md](MIGRATION.md) para o caminho recomendado de migração da 2.x para a 3.0 e para as diferenças de comportamento que merecem revisão.

## O que há de novo na 3.0

FluentMap 3.0 moderniza o projeto original sem mudar sua finalidade principal. Além da API fluente histórica, a linha 3.0 adiciona suporte opt-in para:

- melhorias no constructor mapping de tipos imutáveis;
- materialização de objetos aninhados e value objects;
- mapping profiles para formatos SQL alternativos;
- `QueryMultiple` mapeado, leituras unbuffered e streaming assíncrono;
- metadata de conversão de propriedades e diagnósticos;
- registro gerado de mappings e materializadores suportados;
- analyzers Roslyn para diagnósticos de mapping;
- configuração imutável isolada e dependency injection;
- metadata de persistência mais rica consumida pela integração Dommel;
- registro e diagnósticos conscientes de trimming/AOT;
- testes modernos de compatibilidade, harnesses de providers, benchmarks, CI e validação de pacotes.

Mappings existentes continuam sendo a base de compatibilidade. Adote as APIs novas apenas quando elas resolverem um problema concreto.

## Posicionamento

Use FluentMap para:

- mappings explícitos entre propriedades e colunas;
- convenções e políticas de nomenclatura;
- propriedades ignoradas;
- constructor mapping para tipos imutáveis;
- materialização opt-in de objetos aninhados e value objects;
- profiles para formatos SQL alternativos;
- registro e materialização gerados quando suportados;
- metadata de persistência consumida por integrações como Dommel;
- configuração isolada e DI para materialização controlada pelo FluentMap.

Não use FluentMap como ORM, framework CRUD, query builder, unit of work ou abstração de banco.

## Instalação

Instale o pacote que corresponde ao recurso necessário:

| Pacote | Finalidade |
| --- | --- |
| `Dapper.FluentMap` | API core de mapping e integração com Dapper. |
| `Dapper.FluentMap.Dommel` | Integração opcional com Dommel para tabela, chave e colunas geradas. |
| `Dapper.FluentMap.DependencyInjection` | Integração opcional com `Microsoft.Extensions.DependencyInjection`. |
| `Dapper.FluentMap.Analyzers` | Analyzers Roslyn para erros de configuração prováveis em tempo de compilação. |
| `Dapper.FluentMap.Generators` | Source generator para registro de maps e materializadores gerados. |

```bash
dotnet add package Dapper.FluentMap
```

Os pacotes públicos targetam `netstandard2.0`. Consulte [COMPATIBILITY.md](COMPATIBILITY.md) antes de adotar um release candidate.

## Início Rápido

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

Chame `FluentMapper.Initialize(...)` no startup e trate a configuração global efetiva como somente leitura depois que as queries começarem.

## Mapeamento

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

Mappings explícitos têm precedência sobre convenções. Membros raiz não mapeados usam o comportamento normal do Dapper.

Convenções e políticas de nomenclatura cobrem padrões repetidos:

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

As políticas disponíveis incluem `Identity`, `SnakeCase`, `Prefix(...)`, `Suffix(...)`, `Custom(...)`, `Then(...)`, `WithPrefix(...)` e `WithSuffix(...)`.

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

Use `QueryMapped*` quando o FluentMap precisar construir objetos aninhados imutáveis ou value objects.

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

Materialização aninhada é opt-in via `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped` e helpers de streaming. `Dapper.Query<T>()` normal continua usando materialização raiz do Dapper.

## Value Objects

Para value objects escalares mapeados como um único valor de banco, prefira um `TypeHandler<T>` do Dapper:

```csharp
Map(customer => customer.Cpf).ToColumn("cpf");
```

Para value objects mapeados por componentes, a materialização controlada pelo FluentMap pode chamar construtores públicos compatíveis:

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

Factory methods não são usadas pelo materializador atual.

## Profiles

Profiles são mappings opt-in para a mesma entidade em formatos SQL diferentes:

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

Profiles são selecionados por query controlada pelo FluentMap. Eles não substituem o type map global do Dapper para a entidade.

## Materialização Gerada

Instale `Dapper.FluentMap.Generators` para registro gerado de maps da compilação atual:

```bash
dotnet add package Dapper.FluentMap.Generators
```

Depois chame a extensão gerada:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddGeneratedMappings();
});
```

O generator emite chamadas `AddMap<TMap>()` e `AddProfile<TMap>()` para maps elegíveis. Para mappings explícitos suportados, ele também pode registrar materializadores de linha gerados para o shape ordenado de colunas, incluindo propriedades simples, caminhos aninhados, value objects construídos por construtor e read converters suportados estaticamente.

Materialização gerada é otimização. Maps não suportados, shapes dinâmicos, divergências de shape, converters por instância/delegate e alguns padrões avançados usam fallback runtime.

## Semântica de Persistência

Metadata de persistência descreve participação em escrita sem mudar materialização de leitura:

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

`Ignore()` mantém o significado histórico: a propriedade não é materializada pelo FluentMap e não participa da metadata de persistência gerada. Para valores de banco que ainda devem ser selecionados, mas não escritos, use `ReadOnly()`, `Computed()`, `DatabaseDefaultOnInsert()`, `ExcludeFromInsert()` ou `ExcludeFromUpdate()`.

O pacote core armazena metadata. Dommel é o pacote atual que a consome para comportamento de `INSERT` e `UPDATE` gerados.

## QueryMultiple / Streaming

Use os helpers de query do FluentMap quando a materialização precisa honrar nested mappings, value objects, profiles, converters ou materializers gerados:

```csharp
var customers = connection.QueryMapped<Customer>(sql);
var customer = connection.QueryMappedSingle<Customer>(sql);
var legacy = connection.QueryMappedSingle<Customer, LegacyProfile>(legacySql);
```

Para múltiplos result sets:

```csharp
using var multi = connection.QueryMultipleMapped(sql);

var customers = multi.ReadMapped<Customer>();
var orders = multi.ReadMapped<Order>();
```

`ReadMapped*` consome result sets em sequência e bufferiza o result set atual.

Para processamento incremental:

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

Streaming mantém o reader subjacente aberto até a enumeração terminar ou o enumerator ser descartado.

## Conversores de Propriedade

Conversores de propriedade são configurados por propriedade mapeada e executam somente na materialização controlada pelo FluentMap:

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

A precedência de conversão de leitura na materialização controlada pelo FluentMap é:

```text
tratamento de null/DBNull
    -> read converter da propriedade
    -> Dapper TypeHandler<TProperty>
    -> conversão default do FluentMap
```

Metadata de write converter pode ser configurada, mas não é executada atualmente por escritas Dapper ou Dommel.

## Configuração Isolada / DI

A API estática histórica continua suportada:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});
```

Para múltiplas configurações controladas pelo FluentMap no mesmo processo, crie configurações imutáveis e use seus runtimes:

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

Instale `Dapper.FluentMap.DependencyInjection` para registro em DI:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
    builder.Configure(config => config.AddGeneratedMappings());
});
```

O pacote de DI registra `ImmutableFluentMapConfiguration` e `FluentMapRuntime` como singletons. Ele não registra conexões de banco, repositories, bridges Dommel ou type maps globais do Dapper.

## AOT / Trimming

FluentMap tem prontidão parcial para trimming/AOT, não compatibilidade Native AOT completa:

| Área | Status |
| --- | --- |
| Registro explícito com `AddMap<TMap>()` | Preferencial para cenários com trimming e Native AOT. |
| Registro gerado com `AddGeneratedMappings()` | Alternativa preferencial ao assembly scanning para maps da compilação atual. |
| Assembly scanning | Baseado em reflection e anotado como sensível a trimming. |
| `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped`, streaming | Anotados como sensíveis a trimming/dynamic code porque fallback runtime pode ocorrer. |

Não trate o pacote como totalmente seguro para Native AOT sem validar o caminho de query e o modo de publicação exatos da sua aplicação.

## Compatibilidade

A documentação atual de compatibilidade está em [COMPATIBILITY.md](COMPATIBILITY.md).

Resumo:

- pacotes públicos targetam `netstandard2.0`;
- testes rodam atualmente em `net10.0`;
- a faixa de Dapper é `[2.1.79,3.0.0)`, com `2.1.79` validado na matriz atual;
- a faixa de Dommel é `[3.5.3,4.0.0)` no pacote opcional Dommel;
- SQLite é validado por testes automatizados de provider;
- SQL Server e PostgreSQL têm harness condicional, mas ainda não são certificados em CI;
- MySQL/MariaDB não está validado;
- SQL Server CE permanece legado/limitado por upstream.

Para migrar do FluentMap 2.x, consulte [MIGRATION.md](MIGRATION.md).

## Limitações Atuais

- `FluentMapper.Initialize(...)`, `Dapper.Query<T>()` normal e integrações Dommel usam estado global process-wide.
- Runtimes isolados se aplicam à materialização controlada pelo FluentMap, não a queries Dapper normais nem Dommel.
- Dommel usa resolvers/builders globais do `DommelMapper`.
- `QueryMultipleMapped` é sequencial e bufferizado por result set; não há `QueryMultipleMappedAsync`.
- `QueryMultipleMapped` não é multi-mapping do Dapper com `splitOn`.
- FluentMap não agrega linhas de joins em grafos e não mantém identity map.
- Write converters são apenas metadata no caminho atual de escrita Dapper/Dommel.
- Materializers gerados cobrem um subconjunto suportado e podem cair para materialização runtime.
- Assembly scanning e fallback runtime são sensíveis a trimming/AOT.
- Construção de value objects usa construtores públicos compatíveis, não factory methods.

## Mais Documentação

- [Migração da 2.x](MIGRATION.md)
- [Compatibilidade](COMPATIBILITY.md)
- [Suporte](SUPPORT.md)
- [Changelog](CHANGELOG.md)
- [English](README.md)

## Contribuição

Mantenha mudanças pequenas, compatíveis com a API pública e cobertas por testes focados. Validação local típica:

```bash
dotnet restore ./Dapper.FluentMap.sln
dotnet build ./Dapper.FluentMap.sln --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.sln --configuration Release --no-build
```

## Licença

FluentMap é licenciado sob a [MIT License](LICENSE).
