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

[English](README.md) | Português (Brasil)

FluentMap é uma camada avançada de mapeamento para Dapper. Ela permite descrever, com uma API fluente e fortemente tipada, como propriedades .NET se conectam a colunas de banco de dados, mantendo atributos de persistência fora dos POCOs.

FluentMap não é um ORM. Ele não faz tracking de entidades, não gera SQL arbitrário, não gerencia conexões, não executa migrations, não oferece LINQ e não substitui o Dapper.

## Estado do Projeto

Dapper.FluentMap é mantido ativamente. A linha 3.x atual continua a história do projeto original, preservando o modelo histórico de mapping e adicionando recursos mais novos de forma opt-in.

Mappings existentes com `EntityMap<T>` e `FluentMapper.Initialize(...)` continuam sendo a base de compatibilidade. Aplicações que usam mappings raiz normais do Dapper geralmente não precisam reescrever maps que já funcionam ao migrar da 2.x.

Consulte [MIGRATION.pt-BR.md](MIGRATION.pt-BR.md) ao migrar do FluentMap 2.x.

## Principais Recursos

| Recurso | API principal |
| --- | --- |
| Mapping explícito de propriedade para coluna | `EntityMap<T>`, `Map(...).ToColumn(...)` |
| Convenções e naming policies | `AddConvention(...)`, `UseNamingPolicy(...)` |
| Construção imutável/factory | mapping de construtor, `ConstructUsing(...)` |
| Objetos aninhados e value objects por componentes | `QueryMapped*` |
| Formatos SQL alternativos | mapping profiles |
| Multi-mapping de dois/três tipos | `QueryMapped<...>(..., splitOn: ...)` |
| Múltiplos result sets | `QueryMultipleMapped*`, `ReadMapped*` |
| Streaming síncrono/assíncrono | `QueryMappedUnbuffered*` |
| Conversão por propriedade | property converters |
| Registro/materialização gerados | `AddGeneratedMappings()` |
| Caminho gerado estrito | `UseStrictGeneratedMaterialization()`, `QueryGeneratedMapped*` |
| Configuração isolada | `FluentMapRuntime` |
| Dependency Injection | `AddFluentMap(...)` |
| Persistência/conversão de escrita Dommel | `InsertMapped*`, `UpdateMapped*` |
| Diagnósticos em compilação | `FluentMap.Analyzers` |

Exemplos detalhados estão em [USAGE.pt-BR.md](USAGE.pt-BR.md).

## Instalação

Instale somente os pacotes necessários para a aplicação:

| Finalidade | NuGet PackageId |
| --- | --- |
| Core | `Dapper.FluentMap` |
| Integração Dommel | `Dapper.FluentMap.Dommel` |
| Dependency Injection | `FluentMap.DependencyInjection` |
| Analyzers Roslyn | `FluentMap.Analyzers` |
| Source generators | `FluentMap.Generators` |

Pacote principal:

```bash
dotnet add package Dapper.FluentMap
```

Os PackageIds `FluentMap.*` são apenas identidades de distribuição. Assemblies, namespaces e APIs públicas continuam sob `Dapper.FluentMap.*`.

Os pacotes públicos targetam `netstandard2.0`. Faixas suportadas de dependências e providers certificados estão documentados em [COMPATIBILITY.md](COMPATIBILITY.md).

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

Chame `FluentMapper.Initialize(...)` durante o startup da aplicação e trate a configuração global efetiva como somente leitura depois que as queries começarem.

Para validar a configuração:

```csharp
FluentMapper.Validate();
```

## Uso Avançado

Use as APIs de query controladas pelo FluentMap quando o mapping precisar de comportamento além do type map histórico no nível raiz do Dapper.

Isso inclui:

- objetos aninhados e value objects por componentes;
- profiles;
- multi-mapping de dois/três tipos com `splitOn`;
- múltiplos result sets mapeados;
- streaming síncrono/assíncrono;
- property converters;
- materialização gerada e gerada estrita;
- runtimes isolados e DI;
- metadata de persistência Dommel.

Consulte [USAGE.pt-BR.md](USAGE.pt-BR.md) para exemplos completos e orientação de API.

## Migrando da 2.x

A linha 3.x preserva o principal caminho histórico de mapping compatível em código-fonte.

Se a aplicação usa `EntityMap<T>`, `FluentMapper.Initialize(...)` e chamadas normais de `Dapper.Query<T>()` para mappings raiz, a migração normalmente consiste em atualizar os pacotes, validar a configuração e executar os testes da aplicação.

Consulte [MIGRATION.pt-BR.md](MIGRATION.pt-BR.md) para:

- o caminho mínimo de migração;
- os pré-requisitos de Dapper e Dommel;
- a revisão de persistência envolvendo `Ignore()`/Dommel;
- decisões de migração por cenário;
- o checklist final.

## Compatibilidade

As afirmações de compatibilidade ficam intencionalmente fora do README para poderem evoluir sem duplicar informações sensíveis a cada release.

Consulte [COMPATIBILITY.md](COMPATIBILITY.md) para:

- faixas suportadas de Dapper e Dommel;
- certificação de providers;
- limites de trimming e Native AOT;
- limitações de estado global;
- ambientes não suportados e fronteiras de API.

## Documentação

- [Guia de uso](USAGE.pt-BR.md)
- [Migração da 2.x](MIGRATION.pt-BR.md)
- [Compatibilidade](COMPATIBILITY.md)
- [Changelog](CHANGELOG.md)
- [Suporte](SUPPORT.md)
- [Governança para mantenedores](MAINTAINING.md)
- [English](README.md)

## Contribuição

Mantenha mudanças pequenas, compatíveis com a API pública e cobertas por testes focados. `Dapper.FluentMap.slnx` é a solução preferencial para SDKs .NET atuais; `Dapper.FluentMap.sln` permanece disponível como fallback de compatibilidade.

Validação local típica:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./Dapper.FluentMap.slnx --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.slnx --configuration Release --no-build
```

Quando habilitado, SonarQube Cloud participa do quality gate da CI. A configuração específica do repositório fica intencionalmente fora deste README.

## Licença

FluentMap é licenciado sob a [MIT License](LICENSE).
