# AGENTS.md

## Objetivo

Este repositório mantém o **Dapper.FluentMap**, uma biblioteca .NET que fornece uma API fluente para mapear propriedades de POCOs para colunas de banco de dados usadas pelo Dapper, mantendo os modelos livres de atributos de persistência.

O trabalho realizado por agentes deve ser pequeno, correto, reproduzível e compatível com o comportamento público existente. Responda em português, salvo pedido explícito em outro idioma.

A prioridade inicial é a evolução segura do projeto principal `Dapper.FluentMap`. O módulo `Dapper.FluentMap.Dommel` só deve ser alterado quando a tarefa o mencionar explicitamente ou quando uma mudança no core exigir uma adaptação comprovada.

## Princípios do projeto

- Trate o repositório como uma **biblioteca pública**, não como uma aplicação interna.
- Preserve compatibilidade de código-fonte, binária e comportamental sempre que possível.
- Não introduza breaking changes sem solicitação explícita, justificativa técnica, testes e estratégia de versionamento.
- Prefira alterações pequenas, coesas, revisáveis e cobertas por testes.
- Não transforme o FluentMap em ORM, query builder, gerador de SQL ou camada de CRUD.
- Mantenha o diferencial principal: mapeamento fluente, fortemente tipado e sem atributos no modelo.
- Não implemente recomendações apenas por serem boas práticas genéricas. Exija benefício observável, correção, redução de risco ou requisito claro.
- Não misture modernização, refatoração estrutural, atualização de dependências e mudança funcional no mesmo trabalho sem necessidade explícita.
- Não altere testes apenas para fazê-los passar.
- Não publique pacotes, tags, releases ou versões sem solicitação explícita.

## Fontes de verdade

Consulte apenas os arquivos relevantes para a tarefa atual, priorizando:

1. `AGENTS.md`
2. `README.md`
3. `Dapper.FluentMap.sln`
4. `src/Dapper.FluentMap/Dapper.FluentMap.csproj`
5. `src/Dapper.FluentMap/`
6. `test/Dapper.FluentMap.Tests/Dapper.FluentMap.Tests.csproj`
7. `test/Dapper.FluentMap.Tests/`
8. `src/Dapper.FluentMap.Dommel/` e seus testes, somente quando o escopo incluir Dommel
9. `NuGet.Config`
10. `LICENSE`
11. `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` e workflows, quando existirem e forem relevantes

Não carregue indiscriminadamente todo o repositório. Localize primeiro o contrato, tipo, teste, issue ou comportamento diretamente relacionado à tarefa.

## Estrutura do repositório

- `src/Dapper.FluentMap/`: biblioteca principal e escopo padrão das mudanças.
- `test/Dapper.FluentMap.Tests/`: testes da biblioteca principal.
- `src/Dapper.FluentMap.Dommel/`: integração opcional com Dommel.
- `test/Dapper.FluentMap.Dommel.Tests/`: testes da integração Dommel.
- `Dapper.FluentMap.sln`: solution agregadora.

Mudanças no core não devem incluir Dommel automaticamente. Quando uma alteração pública no core puder afetar Dommel, avalie o impacto, mas não amplie o escopo sem evidência ou pedido explícito.

## Regras obrigatórias

- Faça a menor mudança possível para resolver o problema.
- Preserve a API pública existente, salvo quando a tarefa pedir explicitamente uma mudança de contrato.
- Antes de alterar comportamento público, identifique usos, testes e compatibilidade esperada.
- Para bugs, crie primeiro um teste de regressão que reproduza a falha quando isso for viável.
- Para refatorações, use testes de caracterização quando o comportamento não estiver suficientemente protegido.
- Não torne membros públicos apenas para facilitar testes.
- Não silencie warnings, exceções ou testes sem justificativa técnica.
- Não use `Skip`, remova asserts ou reduza verificações para obter uma suíte verde.
- Não introduza `Task.Delay`, sleeps, dependência de horário, rede externa ou ordem entre testes.
- Não adicione dependências sem necessidade clara.
- Não adote Central Package Management, novo framework de testes, source generator, analyzer, NativeAOT ou nullable em uma tarefa não relacionada.
- Não altere `PackageId`, versão, autores, licença, URLs do pacote ou metadados de publicação sem solicitação explícita.
- Não faça publish para NuGet.
- Não introduza segredos, tokens, chaves ou credenciais.
- Não faça alterações diretamente na branch `master`.
- Não abra pull request, publique branch, crie tag ou release sem solicitação explícita.

## Escopo funcional do core

O projeto principal deve permanecer focado em:

- registro de mapas de entidades;
- mapeamento entre propriedades e nomes de colunas;
- convenções de nomes;
- resolução de membros pelo Dapper;
- composição previsível entre mapeamentos explícitos, convenções e comportamento padrão;
- validação e diagnóstico de configuração;
- compatibilidade com tipos, construtores e modelos suportados pelo contrato definido.

Não adicionar ao core sem decisão explícita:

- mapeamento de tabela;
- chave primária, identidade ou geração de identificador;
- CRUD;
- geração de SQL;
- tracking de entidades;
- unit of work;
- migrations;
- abstração de conexão;
- query builder;
- recursos específicos de Dommel;
- atributos de persistência como abordagem principal.

Quando houver composição entre estratégias de resolução, preserve uma precedência explícita e testável. A direção preferencial é:

1. mapeamento explícito;
2. convenção configurada;
3. comportamento padrão do Dapper.

Não assuma que esse comportamento já existe em todos os caminhos atuais. Ao implementá-lo ou alterá-lo, proteja-o com testes.

## Compatibilidade pública e SemVer

Considere compatíveis ou incompatíveis, conforme o caso:

- assinaturas públicas;
- nomes de tipos e namespaces;
- construtores públicos;
- interfaces públicas;
- comportamento de `FluentMapper.Initialize`;
- comportamento de `EntityMap`, `PropertyMap`, convenções e type maps;
- tipos de exceção observáveis;
- mensagens de erro usadas apenas para diagnóstico, sem prometer texto exato salvo teste ou documentação explícita;
- targets suportados;
- dependências transitivas;
- comportamento de resolução case-sensitive e case-insensitive.

Antes de realizar breaking change:

1. descreva o contrato atual;
2. explique por que não é possível preservar compatibilidade;
3. identifique impacto para consumidores;
4. crie ou atualize testes;
5. proponha versão major;
6. atualize documentação;
7. não implemente sem pedido explícito.

Correções de bugs podem alterar comportamento incorreto, mas devem ter teste de regressão e explicação clara.

## Frameworks e arquivos de projeto

- O projeto principal deve preservar `netstandard2.0` enquanto não houver decisão explícita de compatibilidade diferente.
- Projetos de teste podem usar runtimes modernos, como .NET 8 e .NET 10, sem obrigar a biblioteca publicada a abandonar `netstandard2.0`.
- Multi-targeting deve existir apenas quando houver finalidade de compatibilidade e validação clara.
- Não use `TargetFrameworks` quando houver somente um target.
- Atualizações de SDK, Test SDK, xUnit, Dapper ou Dommel devem ser separadas de correções funcionais sempre que possível.
- Não migre para xUnit v3 junto com a primeira migração de runtime, salvo pedido explícito.
- Não introduza nullable e corrija centenas de warnings junto com uma tarefa não relacionada.
- Não adote Central Package Management sem avaliar custo, benefício e impacto no repositório pequeno.
- Respeite o formato e as convenções atuais dos arquivos `.csproj`, salvo modernização intencional.

## Reflexão e expressões

Código que interpreta expressões deve:

- trabalhar diretamente com os membros representados pela expressão quando possível;
- aceitar conversões legítimas produzidas por `Expression<Func<TEntity, object>>`;
- validar se o membro final é uma propriedade suportada;
- rejeitar expressões inválidas com exceção específica e mensagem útil;
- não selecionar membros apenas por nome usando o primeiro resultado de reflection;
- não confundir propriedades com métodos, campos ou membros homônimos;
- preservar suporte a propriedades herdadas quando compatível;
- ter testes para propriedades com nomes que colidem com membros de tipos como `string`, `DateTime` e `TimeSpan`.

Mapeamento aninhado e Value Objects não devem ser tratados como simples correção de reflection. Antes de declarar suporte, valide materialização completa, criação de objetos intermediários, construtores, nulabilidade, cache e integração real com o Dapper.

## Estado global, registro e cache

O projeto possui configuração e integração global com o Dapper. Qualquer alteração nessa área deve considerar:

- thread safety;
- inicialização repetida;
- isolamento entre testes;
- invalidação de cache;
- concorrência entre registros;
- múltiplas entidades;
- múltiplos processos de resolução;
- efeitos de `SqlMapper.SetTypeMap`;
- compatibilidade com consumidores que inicializam a biblioteca uma vez no startup.

Regras:

- Não introduza novo estado global mutável sem justificativa.
- Prefira descritores imutáveis depois da configuração.
- Use chaves de cache estruturadas; evite concatenação de strings sujeita a colisões.
- Inclua no cache todas as opções que alteram o resultado, como tipo, nome de coluna e comparação.
- Defina e teste quando o cache deve ser invalidado.
- Não dependa de ordem de execução dos testes.
- Quando necessário, forneça mecanismo interno ou público bem definido para reset controlado em testes, sem comprometer consumidores.
- Ao refatorar configuração estática, preserve uma fachada compatível antes de remover contratos antigos.

## Convenções e mapeamentos explícitos

Ao alterar convenções:

- preserve filtros, transformações, prefixos e configuração por entidade;
- detecte ambiguidades de forma determinística;
- não permita que uma convenção sobrescreva silenciosamente um mapeamento explícito;
- não compartilhe estado mutável acidentalmente entre tipos;
- teste mais de uma entidade usando a mesma convenção;
- teste configuração case-sensitive e case-insensitive;
- evite scanning amplo sem filtros quando uma alternativa explícita for viável.

Ao alterar mapeamentos explícitos:

- identifique propriedades por membro ou caminho completo, não apenas pelo nome terminal quando houver suporte a caminhos;
- detecte duplicidades reais;
- diferencie `Rank.Level` de `Seniority.Level` em qualquer modelo futuro de caminho;
- não declare suporte a propriedades aninhadas antes de a materialização estar implementada de ponta a ponta.

## Dapper

- Use apenas contratos públicos do Dapper.
- Não copie implementação interna do Dapper sem justificativa e avaliação de licença.
- Atualize Dapper em tarefa separada quando possível.
- Ao atualizar Dapper, revise mudanças em `ITypeMap`, `IMemberMap`, `DefaultTypeMap`, constructor mapping, type handlers e comportamento de cache.
- Teste o comportamento com uma consulta real quando a mudança depender da materialização do Dapper.
- Não reimplemente `TypeHandler` sem necessidade; prefira integrar-se ao mecanismo do Dapper.
- Não suponha que uma correção unitária de metadata garante materialização correta.

## Testes

### Estratégia

Use a menor camada capaz de proteger o risco real:

- teste unitário para expression parsing, metadata, duplicidade, convenções, cache e validação;
- teste de integração simples com Dapper para materialização, construtores, type maps e consultas;
- banco em memória ou efêmero quando suficiente;
- nenhuma dependência de serviço externo para testes padrão.

### Regras

- Teste comportamento observável, não detalhes privados.
- Use Arrange, Act e Assert de forma clara.
- Dê nomes que descrevam cenário e resultado esperado.
- Cubra sucesso, falha e ambiguidade relevantes.
- Toda correção de bug deve ter teste de regressão.
- Não aceite teste sem assert significativo.
- Evite `NotNull` como única validação de objeto complexo.
- Evite over-mocking do próprio mecanismo que está sendo testado.
- Não replique a implementação no assert.
- Não compartilhe estado mutável entre testes.
- Restaure type maps e configuração global quando o teste os alterar.
- Se a suíte desabilitar paralelismo por causa do estado global, não reabilite sem eliminar ou isolar a causa.
- Não use cobertura como substituto de qualidade.

## Empacotamento NuGet

Quando uma tarefa alterar embalagem, metadata ou compatibilidade do pacote:

1. execute build em `Release`;
2. execute `dotnet pack`;
3. inspecione o `.nupkg`;
4. verifique assemblies, XML documentation, dependências e targets;
5. confirme que arquivos de teste ou artefatos indevidos não foram incluídos;
6. revise impacto de SemVer;
7. não publique.

Preserve licença e atribuição do projeto original. Não remova créditos históricos sem decisão explícita.

## Documentação

Atualize documentação quando houver:

- nova API pública;
- alteração de inicialização;
- nova convenção;
- alteração de comportamento;
- mudança de compatibilidade;
- novo target suportado;
- mudança de pacote ou dependência relevante;
- depreciação;
- breaking change.

Regras:

- `README.md` deve continuar sendo a porta de entrada.
- Exemplos devem compilar ou refletir a API real.
- Não documente recurso ainda não implementado.
- Preserve documentação XML em APIs públicas.
- Para decisões arquiteturais importantes, registre uma issue de design ou documento de decisão, sem criar burocracia para correções pequenas.
- Issues arquivadas do projeto original são evidência de demanda ou defeito histórico, não requisitos automáticos.

## Skills

Antes de executar tarefa especializada, verifique `.agents/skills/` e selecione somente as skills relacionadas ao pedido. Ter todas as skills disponíveis não significa carregar todas em toda tarefa.

As regras deste `AGENTS.md` prevalecem quando uma skill genérica conflitar com o contexto do FluentMap.

### Governança e processo

#### `repository-governance-sdd`

Use para:

- criar ou revisar skills;
- alterar `AGENTS.md`;
- estruturar prompts;
- organizar documentação de processo;
- decidir entre issue, documentação e decisão arquitetural.

Não use para implementar código de produção ou testes.

### Refatoração de código

#### `dotnet-refactoring-engineer`

Use para:

- correções e refatorações em C#;
- melhoria de legibilidade, coesão, testabilidade e performance;
- redução de estado global;
- revisão de design;
- preservação de comportamento;
- code review.

Combine com as regras de compatibilidade pública deste arquivo.

### MSBuild e projetos

#### `msbuild-modernization`

Use para:

- modernizar `.csproj`;
- atualizar `TargetFramework`;
- configurar multi-targeting;
- revisar propriedades de build;
- modernizar SDK e estrutura MSBuild.

#### `msbuild-antipatterns`

Use para:

- revisar duplicação e condições desnecessárias;
- detectar propriedades redundantes;
- evitar targets frágeis;
- revisar uso incorreto de itens e referências.

Use após ou durante mudanças em arquivos de projeto, sem ampliar escopo para modernização não solicitada.

### Execução e diagnóstico de testes

#### `run-tests`

Use para:

- descobrir e executar os testes;
- aplicar filtros;
- executar por projeto ou target;
- diagnosticar falhas de descoberta e execução.

Esta é a skill padrão para validação da suíte.

### Qualidade dos testes

Use estas skills somente depois de a suíte compilar e, quando aplicável, possuir uma baseline conhecida.

#### `test-anti-patterns`

Use para auditar:

- ausência de asserts;
- asserts fracos;
- sleeps;
- flakiness;
- dependência de ordem;
- over-mocking;
- acoplamento a implementação;
- cobertura artificial.

#### `assertion-quality`

Use para revisar força, precisão e relevância dos asserts.

#### `test-gap-analysis`

Use para identificar cenários importantes sem cobertura, especialmente contratos públicos, falhas, ambiguidades, concorrência e regressões históricas.

#### `coverage-analysis`

Use para analisar cobertura em conjunto com risco e comportamento. Não persiga percentual isolado.

#### `detect-static-dependencies`

Use ao investigar:

- estado global;
- membros estáticos;
- caches estáticos;
- isolamento entre testes;
- dependência de ordem;
- flakiness relacionada a `FluentMapper` e `SqlMapper`.

### Migrações específicas

Estas skills devem ser usadas em tarefas próprias e isoladas.

#### `migrate-xunit-to-xunit-v3`

Use apenas quando a tarefa pedir explicitamente migração para xUnit v3. Não combine por padrão com migração inicial de runtime ou atualização geral de pacotes.

#### `migrate-nullable-references`

Use apenas em trabalho dedicado para habilitar nullable, classificar warnings e corrigir contratos de nulabilidade. Preserve compatibilidade pública e evite mudanças indiscriminadas em assinaturas.

#### `dotnet-aot-compat`

Use para avaliação ou implementação dedicada de:

- trimming;
- NativeAOT;
- reflection;
- assembly scanning;
- `Activator.CreateInstance`;
- source generation;
- anotações de preservação.

Não declare compatibilidade AOT apenas porque o projeto compila. Exija validação real.

## Roteamento recomendado de skills

- Alterar `AGENTS.md`, skills ou processo:
  - `repository-governance-sdd`
- Refatorar ou corrigir código C#:
  - `dotnet-refactoring-engineer`
  - `run-tests`
- Alterar `.csproj`, target ou SDK:
  - `msbuild-modernization`
  - `msbuild-antipatterns`
  - `run-tests`
- Corrigir bug relacionado a estado estático ou cache:
  - `dotnet-refactoring-engineer`
  - `detect-static-dependencies`
  - `run-tests`
- Auditar suíte já funcional:
  - `test-anti-patterns`
  - `assertion-quality`
  - `test-gap-analysis`
  - `coverage-analysis`, quando houver dados de cobertura
- Migrar xUnit:
  - `migrate-xunit-to-xunit-v3`
  - `run-tests`
- Habilitar nullable:
  - `migrate-nullable-references`
  - `run-tests`
- Avaliar AOT/trimming:
  - `dotnet-aot-compat`
  - `run-tests`

Não combine skills sem relação direta. Uma skill complementar não pode ampliar o escopo definido pelo pedido.

## Processo de implementação

1. Identifique o escopo: core, testes, Dommel, build, documentação ou empacotamento.
2. Leia os arquivos diretamente relacionados.
3. Selecione as skills necessárias.
4. Descreva o comportamento atual e o risco.
5. Localize testes existentes.
6. Crie teste de regressão ou caracterização quando necessário.
7. Faça a menor alteração coesa.
8. Execute validação localizada.
9. Execute build e testes mais amplos quando o impacto justificar.
10. Execute pack quando houver impacto de pacote.
11. Revise o diff para remover churn, renomeações ou formatação fora do escopo.
12. Registre validações executadas, limitações e riscos restantes.
13. Faça commit semântico apenas quando a alteração estiver coerente e validada.

## Validação

Comece pela validação mais próxima da mudança.

### Biblioteca principal

```bash
dotnet restore ./Dapper.FluentMap.sln
dotnet build ./src/Dapper.FluentMap/Dapper.FluentMap.csproj --configuration Release --no-restore
dotnet test ./test/Dapper.FluentMap.Tests/Dapper.FluentMap.Tests.csproj --configuration Release
```

### Solution completa

Use quando a alteração for transversal ou afetar contratos usados por Dommel:

```bash
dotnet restore ./Dapper.FluentMap.sln
dotnet build ./Dapper.FluentMap.sln --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.sln --configuration Release --no-build
```

### Pacote

Use quando a alteração afetar o pacote principal:

```bash
dotnet pack ./src/Dapper.FluentMap/Dapper.FluentMap.csproj \
  --configuration Release \
  --no-build \
  --output ./artifacts/packages
```

Quando houver multi-targeting, valide cada target relevante. Quando a máquina não possuir o runtime necessário, não instale silenciosamente versões adicionais e não altere targets apenas para contornar o ambiente; registre a limitação ou use a matriz de CI quando disponível.

Se a baseline original estiver quebrada antes da mudança, registre:

- comando executado;
- falha observada;
- se a falha é anterior à alteração;
- o que foi ou não validado.

## Git e commits

- A branch padrão é `master`; nunca aplique alterações diretamente nela.
- Crie ou use branch de trabalho relacionada ao objetivo.
- Use Conventional Commits:
  - `fix:` para correção funcional;
  - `feat:` para nova capacidade;
  - `refactor:` para mudança estrutural sem mudança observável;
  - `test:` para testes;
  - `docs:` para documentação;
  - `chore:` para manutenção;
  - `ci:` para automação.
- Não misture assuntos independentes no mesmo commit.
- Revise o diff antes de commitar.
- Não faça commit com falha de build ou teste sem registrar claramente o motivo.
- Não faça push, abra pull request, crie tag ou release sem pedido explícito.
- Não reescreva histórico compartilhado.
- Não use `--force` sem autorização explícita.

## Formato da resposta final

Ao concluir uma tarefa, informe:

1. objetivo atendido;
2. arquivos alterados;
3. comportamento preservado ou alterado;
4. testes e comandos executados;
5. resultado do build, testes e pack;
6. riscos ou limitações restantes;
7. commit criado, quando aplicável.

Não declare sucesso quando uma validação necessária não tiver sido executada. Explique objetivamente o que ficou sem validar e por quê.
