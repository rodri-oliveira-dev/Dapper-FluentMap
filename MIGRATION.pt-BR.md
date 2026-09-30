# Guia de Migração: 2.x para 3.x

Este guia é destinado a usuários que estão migrando da linha histórica Dapper.FluentMap 2.x para a linha 3.x mantida atualmente.

```text
Dapper.FluentMap 2.x
        ↓
Dapper.FluentMap 3.x
```

Para a maioria das aplicações que usam mappings raiz com `EntityMap<TEntity>`, `FluentMapper.Initialize(...)` e queries normais do Dapper, a migração é principalmente uma atualização de pacote seguida de validação e testes. A API histórica continua suportada e os recursos mais novos da linha 3.x são opt-in.

Não reescreva mappings históricos que já funcionam apenas porque APIs mais novas existem. Adote essas APIs quando elas resolverem um problema concreto.

## Resumo Rápido

Se a aplicação usa algo como:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
});

var customer = connection.QuerySingle<Customer>(sql);
```

com mappings raiz como:

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

normalmente não são necessárias mudanças de código-fonte para migrar para a 3.x.

Atualize o pacote, valide a configuração e execute a suíte de testes da aplicação.

## Antes de Atualizar

Antes de alterar as versões dos pacotes FluentMap:

- confirme que a aplicação usa uma versão suportada do Dapper;
- se usar Dommel, confirme que está em uma versão suportada;
- mantenha todos os pacotes FluentMap na mesma versão de release;
- execute a suíte de testes existente antes e depois da atualização;
- identifique se a aplicação usa Dommel, `Ignore()` para colunas geradas pelo banco, assembly scanning, implementações customizadas de `TypeHandler<T>` do Dapper, trimming ou Native AOT.

As faixas suportadas atualmente ficam documentadas em [COMPATIBILITY.md](COMPATIBILITY.md). Os valores do Dapper são governados por `eng/compatibility-contract.json` e validados contra metadata de pacote e CI. No momento deste guia:

```text
Dapper [2.1.79,3.0.0)
Dommel [3.5.3,4.0.0)
```

A certificação de providers e as versões exatas testadas também são mantidas em [COMPATIBILITY.md](COMPATIBILITY.md).

## Migração Mínima

Para aplicações que usam apenas o modelo histórico de mapping no nível raiz, o primeiro passo recomendado é intencionalmente pequeno.

Mantenha:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
    config.AddMap<OrderMap>();
});

FluentMapper.Validate();
```

Mantenha as chamadas normais do Dapper:

```csharp
var customer = connection.QuerySingle<Customer>(sql);
var customers = connection.Query<Customer>(sql);
```

Depois:

1. atualize todos os pacotes FluentMap para a mesma release 3.x;
2. restaure e compile a aplicação;
3. execute `FluentMapper.Validate()` no startup ou em testes de validação;
4. execute os testes unitários e de integração;
5. revise os itens específicos de cenário abaixo somente quando se aplicarem.

## Tabela de Decisão da Migração

| Se a aplicação usa | Ação de migração |
| --- | --- |
| `EntityMap<T>` + `Dapper.Query<T>()` normal | Normalmente nenhuma mudança de código. Mantenha a API histórica. |
| `FluentMapper.Initialize(...)` | Mantenha quando o processo possui uma única configuração global efetiva. |
| Dommel | Revise o comportamento de persistência e colunas geradas e execute testes reais de escrita. |
| `Ignore()` apenas para evitar escrever uma coluna gerada/default | Substitua esse workaround por metadata de persistência. |
| Objetos aninhados | Use `QueryMapped*` somente onde FluentMap precisar materializar caminhos aninhados. |
| Value objects armazenados como um único valor escalar | Continue usando `TypeHandler<T>` do Dapper quando a representação for global para o tipo. |
| Value objects mapeados por componentes | Use materialização controlada pelo FluentMap, como `QueryMapped*`. |
| Assembly scanning | Continua disponível; prefira registro explícito ou gerado para trimming/Native AOT. |
| Múltiplas configurações de mapping no mesmo processo | Considere `FluentMapRuntime` e configuração imutável. |
| Dependency Injection | Adicione `FluentMap.DependencyInjection` somente quando precisar de integração com o host. |
| Trimming ou Native AOT | Prefira registro explícito/gerado e revise os limites documentados de AOT. |
| Formatos SQL alternativos para a mesma entidade | Considere mapping profiles. |
| Conversão de banco específica por propriedade | Considere property converters na materialização controlada pelo FluentMap. |

## Pacotes

Instale somente os pacotes que utiliza:

| Pacote | Quando instalar |
| --- | --- |
| `Dapper.FluentMap` | Mapping principal e integração com Dapper. |
| `Dapper.FluentMap.Dommel` | Integração Dommel para tabela, chave e colunas geradas. |
| `FluentMap.DependencyInjection` | Registro em DI da configuração imutável e runtime. |
| `FluentMap.Analyzers` | Diagnósticos em tempo de compilação para erros de mapping. |
| `FluentMap.Generators` | Registro gerado e materializadores gerados suportados. |

Os PackageIds históricos dos pacotes principais não mudaram:

```text
Dapper.FluentMap        -> Dapper.FluentMap
Dapper.FluentMap.Dommel -> Dapper.FluentMap.Dommel
```

Os pacotes modernos opcionais usam estes NuGet PackageIds:

```text
FluentMap.DependencyInjection
FluentMap.Analyzers
FluentMap.Generators
```

Os nomes `FluentMap.*` são apenas identidades de distribuição. Assemblies, namespaces e APIs públicas continuam sob `Dapper.FluentMap.*`.

Os três PackageIds `FluentMap.*` substituem as identidades de distribuição não publicadas `Dapper.FluentMap.DependencyInjection`, `Dapper.FluentMap.Analyzers` e `Dapper.FluentMap.Generators`.

## O Que Continua Compatível

Os padrões abaixo continuam sendo o caminho de compatibilidade:

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

Chamadas normais do Dapper como `connection.Query<T>()` continuam usando o bridge global de type map do Dapper para mappings raiz instalados por `FluentMapper.Initialize(...)`.

## Inicialização e Validação

A inicialização estática histórica continua suportada:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<CustomerMap>();
    config.AddMap<OrderMap>();
});

FluentMapper.Validate();
```

Use esse modelo quando o processo possui uma única configuração efetiva e você quer que chamadas normais de `Dapper.Query<T>()` usem o bridge global de type map do FluentMap.

A linha 3.x também publica `FluentMapper.Configuration` e `FluentMapper.Runtime` após a inicialização. Código existente não precisa usar essas propriedades.

## Registro

Registros explícitos existentes continuam válidos:

```csharp
config.AddMap<CustomerMap>();
config.AddMap(new CustomerMap());
```

Assembly scanning também continua disponível:

```csharp
config.AddMapsFromAssemblyContaining<CustomerMap>();
```

Para deployments com trimming e Native AOT, prefira registro explícito ou gerado em vez de assembly scanning.

## Convenções

Convenções existentes continuam suportadas:

```csharp
config.AddConvention<PrefixConvention>().ForEntity<Customer>();
```

A linha 3.x adiciona naming policies para transformações comuns:

```csharp
config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false)
    .ForEntity<Customer>();
```

A precedência continua sendo mapping explícito primeiro, depois convenção/naming policy e por fim o comportamento padrão do Dapper.

## Dommel e Ignore()

Se você usa Dommel, revise todo mapping com `Ignore()` antes de atualizar.

`Ignore()` mantém seu significado histórico: a propriedade não é mapeada para materialização do FluentMap e fica fora da metadata de persistência gerada.

Se código Dommel histórico usava `Ignore()` apenas para evitar escrever uma coluna gerada pelo banco, mas ainda precisava lê-la, migre esse mapping para metadata de persistência:

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

Use `Ignore()` somente para valores que não devem ser materializados pelo FluentMap.

Após atualizar uma aplicação Dommel, teste pelo menos:

- inserts;
- updates;
- leituras como `Get`/`GetAll` usadas pela aplicação;
- chaves identity;
- defaults do banco;
- colunas computadas;
- colunas somente leitura.

## Semântica de Persistência

O pacote core armazena metadata de persistência. Dommel consome essa metadata para escritas geradas:

| Mapping | Leitura | Insert | Update |
| --- | --- | --- | --- |
| padrão | sim | sim | sim |
| `Ignore()` | não | não | não |
| `ReadOnly()` | sim | não | não |
| `Computed()` | sim | não | não |
| `DatabaseDefaultOnInsert()` | sim | não | sim |
| `ExcludeFromInsert()` | sim | não | sim |
| `ExcludeFromUpdate()` | sim | sim | não |

O pacote core continua sem gerar SQL CRUD.

## Objetos Aninhados

Historicamente, FluentMap ajuda principalmente o Dapper a mapear membros no nível raiz. A materialização de objetos aninhados na linha 3.x é opt-in:

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

Use `QueryMapped*`, `ReadMapped*`, `QueryMultipleMapped` ou helpers de streaming quando FluentMap precisar materializar caminhos aninhados. `Dapper.Query<T>()` normal não se transforma em um graph mapper.

## Value Objects

Para um value object armazenado como um único valor do banco, continue usando `TypeHandler<T>` do Dapper quando essa representação for global para o tipo.

Para value objects armazenados por componentes mapeados, use materialização controlada pelo FluentMap:

```csharp
Map(customer => customer.Cpf.Number).ToColumn("cpf");
```

O materializador de runtime continua selecionando construtores públicos compatíveis por padrão. Maps que antes não podiam ser materializados agora podem optar por `ConstructUsing(...)`, com um a quatro valores de propriedades raiz explicitamente mapeadas; caminhos aninhados são rejeitados durante a configuração. Esse caminho de factory é suportado pela materialização em runtime e por runtimes isolados; materializadores gerados reportam `DFM011`, e o modo strict generated rejeita o shape sem fallback.

## Profiles

Profiles são mappings opt-in para formatos SQL alternativos:

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

Profiles não substituem o type map global padrão do Dapper. Selecione-os por query controlada pelo FluentMap.

## Conversores de Propriedade

Property converters são executados somente na materialização controlada pelo FluentMap:

```csharp
Map(product => product.Status)
    .ToColumn("status_code")
    .ConvertFromDatabaseUsing<ProductStatusConverter, string>();
```

`Dapper.Query<T>()` normal não executa property converters. Use `TypeHandler<T>` do Dapper para conversões globais por tipo.

Write converters configurados são executados apenas pelos métodos opt-in `InsertMapped*` e `UpdateMapped*` do adapter Dommel. O comportamento histórico de `Insert`/`Update` do Dommel permanece inalterado. O conversor da propriedade executa primeiro; depois, o Dapper aplica eventual type handler registrado para o tipo de saída do conversor.

Overloads `QueryMapped<TFirst,TSecond,TThird,TReturn>` com três entradas agora complementam a API histórica de duas entradas. Informe duas fronteiras `splitOn` únicas, separadas por vírgula. Essas APIs apenas compõem linhas individuais e não agregam grafos um-para-muitos.

## Anotações Nullable

Os assemblies públicos agora carregam anotações de nullable reference types. As assinaturas CLR e a compatibilidade binária não mudam, mas consumidores com nullable habilitado podem receber warnings mais precisos: parâmetros/transações opcionais e metadados opcionais são nullable, e argumentos filhos de multi-mapping são nullable porque um segmento contendo apenas `NULL` é entregue como `null`. Trate os novos warnings como orientação de contrato; não desabilite a análise nullable globalmente para ocultá-los.

## Registro Gerado

Instale `FluentMap.Generators` e chame:

```csharp
config.AddGeneratedMappings();
```

Isso pode substituir registro manual para maps elegíveis da compilação atual. Não faz scan de assemblies referenciados e não elimina a necessidade de validação em runtime.

Materializadores gerados são uma otimização. Casos não suportados usam materialização runtime como fallback, exceto quando o modo estrito de materialização gerada é habilitado explicitamente.

Queries strict generated podem usar `GeneratedParameters` para comandos parametrizados. Cada valor possui um `DbType` explícito; objetos anônimos e outros bags de parâmetros arbitrários continuam não suportados, para que o modo estrito não introduza descoberta de parâmetros por reflection. Colunas reordenadas e colunas adicionais que não resolvem para membros configurados explicitamente no FluentMap podem permanecer no caminho gerado; colunas obrigatórias ausentes, nomes duplicados/ambíguos ou colunas adicionais explicitamente mapeadas falham deterministicamente. A descoberta de membros apenas por convenção do Dapper permanece exclusiva do caminho não estrito.

Falhas de conversão de valores gerados agora lançam `FluentMapConfigurationException` com contexto da entidade, membro, coluna, tipo do provider e tipo de destino. Código que antes capturava `FormatException` ou `InvalidCastException`, dependentes do provider, deve capturar `FluentMapConfigurationException` e inspecionar a exceção interna.

## Isolamento de Configuração

Se a aplicação precisa de múltiplas configurações FluentMap no mesmo processo, use configuração imutável e instâncias de runtime:

```csharp
var runtime = new FluentMapConfigurationBuilder()
    .AddMap<CustomerMap>()
    .Build()
    .CreateRuntime();

var customer = runtime.QueryMappedSingle<Customer>(connection, sql);
```

Isso isola a materialização controlada pelo FluentMap. Não isola `Dapper.Query<T>()` normal, porque os type maps do Dapper são globais por tipo de entidade.

## Dependency Injection

Instale `FluentMap.DependencyInjection` e registre:

```csharp
services.AddFluentMap(builder =>
{
    builder.AddMap<CustomerMap>();
});
```

O pacote de DI registra `ImmutableFluentMapConfiguration` e `FluentMapRuntime` como singletons. Ele não registra conexões de banco, repositories, integração Dommel ou type maps globais do Dapper.

## Configuração do Dommel

Dommel continua opcional e process-wide:

```csharp
FluentMapper.Initialize(config =>
{
    config.AddMap<ProductMap>();
    config.ForDommel();
});
```

`DommelEntityMap<TEntity>`, `IsKey()`, `IsIdentity()` e `SetGeneratedOption(...)` continuam sendo a superfície de mapping específica do Dommel. Runtimes FluentMap isolados não configuram Dommel.

## Diferenças Quebráveis ou de Risco a Revisar

- A metadata de persistência Dommel possui comportamento para propriedades read-only, computed, excluídas de insert e excluídas de update; valide cenários reais de escrita após a atualização.
- Algumas configurações contraditórias que antes eram aceitas acidentalmente agora falham na validação.
- `DommelPropertyMap.GeneratedOption` mudou de não anulável para anulável na linha 3.x; considere que compatibilidade binária com o Dommel histórico `2.0.0` não é garantida.
- Materialização gerada e APIs de runtime isolado são aditivas e não exigem que aplicações existentes alterem seu modelo histórico de mapping.
- `Dapper.Query<T>()` normal, `FluentMapper.Initialize(...)` e Dommel ainda envolvem estado global process-wide onde documentado.

## Caminho de Migração Recomendado

1. Confirme que as versões do Dapper e, quando aplicável, Dommel estão dentro das faixas suportadas.
2. Mantenha os mappings `EntityMap<TEntity>` existentes e `FluentMapper.Initialize(...)`.
3. Atualize todos os pacotes FluentMap para a mesma release 3.x.
4. Execute `FluentMapper.Validate()` e a suíte completa de testes da aplicação.
5. Se usar Dommel, revise cada workaround com `Ignore()` e teste colunas geradas/default/computed/read-only.
6. Mova leituras de objetos aninhados/value objects para `QueryMapped*` somente onde necessário.
7. Adicione profiles apenas para formatos SQL alternativos.
8. Adicione runtime isolado/DI apenas quando precisar de múltiplas configurações ou integração com host.
9. Adicione analyzers e generators depois que o comportamento de runtime já estiver compreendido.
10. Para trimming ou Native AOT, revise [COMPATIBILITY.md](COMPATIBILITY.md) e prefira registro explícito/gerado.

## Checklist da Migração

A migração está pronta quando:

- [ ] todos os pacotes FluentMap usam a mesma versão de release 3.x;
- [ ] o Dapper está dentro da faixa de versões suportada;
- [ ] o Dommel está dentro da faixa suportada, quando utilizado;
- [ ] a aplicação compila com sucesso;
- [ ] `FluentMapper.Validate()` termina sem erros de validação;
- [ ] as queries raiz existentes se comportam como antes;
- [ ] o comportamento de insert/update do Dommel foi testado, quando aplicável;
- [ ] colunas identity, database-default, computed e read-only foram verificadas, quando aplicável;
- [ ] o comportamento de `TypeHandler<T>` customizado do Dapper foi verificado, quando utilizado;
- [ ] os testes de integração passam contra o provider de banco realmente usado pela aplicação;
- [ ] cenários com trimming/Native AOT foram validados contra os limites de suporte documentados, quando aplicável.

Para as afirmações atuais de compatibilidade, certificação de providers e ambientes não suportados, consulte [COMPATIBILITY.md](COMPATIBILITY.md).
