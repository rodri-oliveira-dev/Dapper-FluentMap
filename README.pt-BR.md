# FluentMap

<p align="center">
  <img src="website/public/social/fluentmap-social.png" alt="Dapper FluentMap — Mapeie seus dados. Mantenha seus modelos limpos." width="1200">
</p>

<p align="center"><strong>Mapeie seus dados. Mantenha seus modelos limpos.</strong></p>

<p align="center">
  <a href="README.md">English</a> · Português (Brasil)
</p>

<p align="center">
  <a href="https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/ci.yml/badge.svg?branch=main"></a>
  <a href="https://www.nuget.org/packages/Dapper.FluentMap"><img alt="NuGet" src="https://img.shields.io/nuget/v/Dapper.FluentMap?logo=nuget"></a>
  <a href="LICENSE"><img alt="Licença: MIT" src="https://img.shields.io/github/license/rodri-oliveira-dev/Dapper-FluentMap"></a>
  <a href="https://github.com/rodri-oliveira-dev/Dapper-FluentMap/stargazers"><img alt="Estrelas no GitHub" src="https://img.shields.io/github/stars/rodri-oliveira-dev/Dapper-FluentMap?style=flat&amp;logo=github"></a>
</p>
<p align="center">
  <a href="https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://github.com/rodri-oliveira-dev/Dapper-FluentMap/actions/workflows/codeql.yml/badge.svg?branch=main"></a>
  <a href="https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_Dapper-FluentMap"><img alt="Quality Gate" src="https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_Dapper-FluentMap&amp;metric=alert_status"></a>
  <a href="https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_Dapper-FluentMap"><img alt="Cobertura" src="https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_Dapper-FluentMap&amp;metric=coverage"></a>
</p>

<p align="center">
  <a href="website/src/content/docs/pt-br/getting-started/index.md">Primeiros passos</a> ·
  <a href="website/src/content/docs/pt-br/index.mdx">Documentação</a> ·
  <a href="https://www.nuget.org/packages/Dapper.FluentMap">NuGet</a> ·
  <a href="website/src/content/docs/pt-br/examples/index.md">Exemplos</a> ·
  <a href="https://github.com/rodri-oliveira-dev/Dapper-FluentMap/releases">Releases no GitHub</a> ·
  <a href="#como-contribuir">Como contribuir</a>
</p>

FluentMap fornece mapeamentos fluentes e fortemente tipados para aplicações que usam [Dapper](https://github.com/DapperLib/Dapper). Ele conecta propriedades .NET a colunas do banco de dados sem adicionar atributos de persistência às entidades de domínio.

FluentMap complementa o Dapper; não o substitui e não é um ORM completo. SQL, conexões, transações, migrations e tracking de entidades permanecem fora do seu escopo.

## Por que FluentMap?

Imagine uma aplicação .NET com uma entidade `Customer` cujas propriedades são `Id` e `Name`, enquanto o banco retorna `customer_id` e `customer_name`. O FluentMap mantém esses nomes físicos em um map dedicado e fortemente tipado, sem acoplar a entidade aos detalhes de persistência.

Com isso, você obtém:

- modelos de domínio mais limpos;
- configuração fortemente tipada;
- mappings explícitos e reutilizáveis;
- integração direta com Dapper;
- suporte a convenções reutilizáveis;
- melhor organização do código de persistência.

## Início rápido

Instale o pacote principal e, para este exemplo autocontido com SQLite, o provider de banco de dados:

```bash
dotnet add package Dapper.FluentMap
dotnet add package Microsoft.Data.Sqlite
```

Defina a entidade e seu map, registre-o uma vez durante a inicialização e consulte normalmente com Dapper:

```csharp
using Dapper;
using Dapper.FluentMap;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;

FluentMapper.Initialize(config => config.AddMap<CustomerMap>());
FluentMapper.Validate();

using var connection = new SqliteConnection("Data Source=:memory:");
var customer = connection.QuerySingle<Customer>(
    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(customer => customer.Id).ToColumn("customer_id");
        Map(customer => customer.Name).ToColumn("customer_name");
    }
}
```

Inicialize a configuração global antes de consultas concorrentes e trate-a como somente leitura depois disso. Consulte o [código-fonte do Início rápido](website/src/content/docs/pt-br/getting-started/quick-start.md) se o portal planejado ainda não estiver publicado.

## Principais funcionalidades

| Funcionalidade | Escopo | Saiba mais |
| --- | --- | --- |
| Mapeamento explícito de propriedades | Principal | [`EntityMap<T>` e mapeamento de propriedades](website/src/content/docs/pt-br/concepts/property-mapping.md) |
| Convenções de nomenclatura | Principal | [Convenções](website/src/content/docs/pt-br/concepts/mapping-conventions.md) |
| Objetos imutáveis | Principal | [Objetos imutáveis](website/src/content/docs/pt-br/advanced/immutable-objects.md) |
| Profiles de mapeamento | Principal | [Profiles de mapeamento](website/src/content/docs/pt-br/advanced/mapping-profiles.md) |
| Isolamento em runtime | Principal | [`FluentMapRuntime`](website/src/content/docs/pt-br/advanced/runtime-isolation.md) |
| Dependency Injection | Pacote opcional | [Integração com DI](website/src/content/docs/pt-br/integrations/dependency-injection.md) |
| Integração com Dommel | Pacote opcional | [Integração com Dommel](website/src/content/docs/pt-br/integrations/dommel.md) |
| Source generators | Pacote opcional | [Source generators](website/src/content/docs/pt-br/integrations/source-generators.md) |
| Analyzers Roslyn | Pacote opcional | [Analyzers Roslyn](website/src/content/docs/pt-br/integrations/roslyn-analyzers.md) |

O [guia de uso](USAGE.pt-BR.md) no repositório permanece disponível como alternativa para exemplos detalhados da API.

## Pacotes

| Pacote | Finalidade |
| --- | --- |
| [`Dapper.FluentMap`](https://www.nuget.org/packages/Dapper.FluentMap) | APIs principais de mapping, convenções, profiles, materialização e runtime. |
| [`Dapper.FluentMap.Dommel`](https://www.nuget.org/packages/Dapper.FluentMap.Dommel) | Integração opcional com metadados e persistência do Dommel. |
| [`FluentMap.DependencyInjection`](https://www.nuget.org/packages/FluentMap.DependencyInjection) | Registro da configuração imutável e de runtimes isolados no Microsoft DI. |
| [`FluentMap.Analyzers`](https://www.nuget.org/packages/FluentMap.Analyzers) | Diagnósticos Roslyn para problemas de mapping detectáveis estaticamente. |
| [`FluentMap.Generators`](https://www.nuget.org/packages/FluentMap.Generators) | Registro gerado de maps e materializadores compatíveis. |

Os PackageIds são identidades de distribuição. Assemblies, namespaces e APIs públicas permanecem sob `Dapper.FluentMap.*` onde documentado.

## Documentação

O portal bilíngue de documentação está planejado para `https://rodri-oliveira-dev.github.io/Dapper-FluentMap/pt-br/`. Enquanto a publicação não estiver confirmada, use como alternativa os arquivos locais indicados.

| Assunto | Código-fonte do portal | Guia no repositório |
| --- | --- | --- |
| Primeiros passos | [Comece aqui](website/src/content/docs/pt-br/getting-started/index.md) | [Início rápido](website/src/content/docs/pt-br/getting-started/quick-start.md) |
| Mapeamento básico | [Mapeamento de propriedades](website/src/content/docs/pt-br/concepts/property-mapping.md) | [Guia de uso](USAGE.pt-BR.md) |
| Mapeamento avançado | [Mapeamento avançado](website/src/content/docs/pt-br/advanced/index.md) | [Guia de uso](USAGE.pt-BR.md) |
| Integrações | [Integrações](website/src/content/docs/pt-br/integrations/index.md) | [Visão geral dos pacotes](website/src/content/docs/pt-br/reference/package-overview.md) |
| Exemplos | [Exemplos](website/src/content/docs/pt-br/examples/index.md) | [Exemplo com SQLite](website/src/content/docs/pt-br/examples/sqlite.md) |
| Guia de migração | [Migração](website/src/content/docs/pt-br/migration/index.md) | [MIGRATION.pt-BR.md](MIGRATION.pt-BR.md) |
| Compatibilidade | [Matriz de compatibilidade](website/src/content/docs/pt-br/reference/compatibility-matrix.md) | [COMPATIBILITY.md](COMPATIBILITY.md) |
| Referência da API | [Visão geral da API](website/src/content/docs/pt-br/reference/api-overview.md) | [Referência de configuração](website/src/content/docs/pt-br/reference/configuration-reference.md) |

## Estado do projeto

FluentMap está em manutenção ativa. A linha 3.x preserva os principais contratos históricos — incluindo `EntityMap<T>`, `FluentMapper.Initialize(...)` e a ponte de type map do Dapper — enquanto as funcionalidades mais recentes permanecem opt-in. Consulte a [documentação de compatibilidade](COMPATIBILITY.md) antes de atualizar.

Para versões publicadas e histórico de releases, consulte as [Releases no GitHub](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/releases) e o [changelog](CHANGELOG.md).

## Como contribuir

Contribuições são bem-vindas. Você pode [reportar um bug ou sugerir uma melhoria](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/issues/new/choose), ajudar a melhorar a [documentação](website/README.md) ou enviar um [pull request](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/pulls) focado.

Se o FluentMap for útil no seu projeto, considere deixar uma [estrela no GitHub](https://github.com/rodri-oliveira-dev/Dapper-FluentMap/stargazers).

## Licença

FluentMap é licenciado sob a [Licença MIT](LICENSE).
