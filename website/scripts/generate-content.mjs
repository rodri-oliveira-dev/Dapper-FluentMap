import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import sharp from 'sharp';

const root = resolve(import.meta.dirname, '../src/content/docs');

const sections = {
  'getting-started': {
    en: 'Getting Started', pt: 'Primeiros passos',
    introEn: 'Build a correct mental model, install only what you need, and create your first verified mapping.',
    introPt: 'Construa um modelo mental correto, instale apenas o necessário e crie seu primeiro mapping verificado.',
    pages: [
      ['introduction', 'Introduction', 'Introdução', 'Understand where FluentMap fits in a Dapper application.', 'Entenda onde o FluentMap se encaixa em uma aplicação com Dapper.'],
      ['why-fluentmap', 'Why FluentMap?', 'Por que FluentMap?', 'Separate database naming from clean, strongly typed domain models.', 'Separe os nomes do banco de modelos de domínio limpos e fortemente tipados.'],
      ['installation', 'Installation', 'Instalação', 'Choose and install only the FluentMap packages your application uses.', 'Escolha e instale apenas os pacotes FluentMap usados pela aplicação.'],
      ['quick-start', 'Quick Start', 'Início rápido', 'Register an explicit map and execute a normal Dapper query.', 'Registre um map explícito e execute uma consulta normal do Dapper.'],
      ['first-mapping', 'First Mapping', 'Primeiro mapeamento', 'Create an EntityMap and connect properties to result-set columns.', 'Crie um EntityMap e associe propriedades às colunas do resultado.'],
      ['configuration', 'Configuration', 'Configuração', 'Initialize, compose, and validate mappings during application startup.', 'Inicialize, componha e valide mappings durante a inicialização da aplicação.'],
      ['common-questions', 'Getting Started FAQ', 'Dúvidas iniciais', 'Resolve the most common setup and scope questions before going deeper.', 'Resolva as dúvidas mais comuns de configuração e escopo antes de avançar.']
    ]
  },
  concepts: {
    en: 'Core Concepts', pt: 'Conceitos fundamentais',
    introEn: 'Learn the contracts that determine how a column becomes a property value.',
    introPt: 'Conheça os contratos que determinam como uma coluna se torna um valor de propriedade.',
    pages: [
      ['entity-mapping', 'Entity Mapping', 'Mapeamento de entidades', 'Describe the EntityMap contract for one entity type.', 'Descreva o contrato EntityMap de mapping para uma entidade.'],
      ['property-mapping', 'Property Mapping', 'Mapeamento de propriedades', 'Map a property expression to the exact column name returned by SQL.', 'Mapeie uma expressão de propriedade para o nome exato retornado pelo SQL.'],
      ['mapping-conventions', 'Mapping Conventions', 'Convenções', 'Apply reusable mapping rules when explicit configuration would repeat.', 'Aplique regras reutilizáveis quando a configuração explícita seria repetitiva.'],
      ['naming-policies', 'Naming Policies', 'Políticas de nomenclatura', 'Translate common naming styles such as snake_case predictably.', 'Traduza estilos comuns, como snake_case, de forma previsível.'],
      ['ignored-properties', 'Ignored Properties', 'Propriedades ignoradas', 'Exclude a property from read and persistence metadata deliberately.', 'Exclua uma propriedade da leitura e dos metadados de persistência conscientemente.'],
      ['configuration-validation', 'Configuration Validation', 'Validação de configuração', 'Detect invalid or ambiguous mappings before handling requests.', 'Detecte mappings inválidos ou ambíguos antes de atender requisições.'],
      ['mapping-precedence', 'Mapping Precedence', 'Precedência de mapeamento', 'Understand explicit maps, conventions, and Dapper fallback order.', 'Entenda a ordem entre maps explícitos, convenções e fallback do Dapper.']
    ]
  },
  advanced: {
    en: 'Advanced Mapping', pt: 'Mapeamento avançado',
    introEn: 'Use FluentMap-controlled materialization only when the result shape requires it.',
    introPt: 'Use a materialização controlada pelo FluentMap quando o formato do resultado exigir.',
    pages: [
      ['immutable-objects', 'Immutable Objects', 'Objetos imutáveis', 'Bind constructor parameters to mapped columns for immutable models.', 'Associe parâmetros de construtor às colunas mapeadas em modelos imutáveis.'],
      ['constructor-mapping', 'Constructor Mapping', 'Mapeamento por construtor', 'Select supported constructors or an explicit root-level factory.', 'Selecione construtores suportados ou uma factory explícita no nível raiz.'],
      ['nested-objects', 'Nested Objects', 'Objetos aninhados', 'Materialize mapped member paths with FluentMap-controlled queries.', 'Materialize caminhos de membros com queries controladas pelo FluentMap.'],
      ['value-objects', 'Value Objects', 'Value objects', 'Build component value objects from a defined group of columns.', 'Construa value objects componentes a partir de um grupo definido de colunas.'],
      ['mapping-profiles', 'Mapping Profiles', 'Profiles de mapeamento', 'Select a mapping for a specific SQL shape without changing the default.', 'Selecione um mapping para um formato SQL específico sem alterar o padrão.'],
      ['property-converters', 'Property Converters', 'Conversores de propriedade', 'Convert database values before assigning them to mapped properties.', 'Converta valores do banco antes de atribuí-los às propriedades mapeadas.'],
      ['multi-mapping', 'Multi-Mapping', 'Multi-mapping', 'Compose two or three mapped row segments through splitOn.', 'Componha dois ou três segmentos mapeados da linha por meio de splitOn.'],
      ['multiple-result-sets', 'Multiple Result Sets', 'Múltiplos result sets', 'Read each result set through a mapping-aware grid reader.', 'Leia cada result set por meio de um grid reader ciente dos mappings.'],
      ['streaming', 'Mapped Streaming', 'Streaming mapeado', 'Enumerate mapped rows without buffering the full result in memory.', 'Percorra linhas mapeadas sem manter todo o resultado em memória.'],
      ['runtime-isolation', 'Runtime Isolation', 'Isolamento em runtime', 'Keep mapping configuration local instead of mutating global Dapper state.', 'Mantenha a configuração local sem alterar o estado global do Dapper.']
    ]
  },
  integrations: {
    en: 'Integrations', pt: 'Integrações',
    introEn: 'See which capability belongs to the core package and which requires an optional package.',
    introPt: 'Veja quais recursos pertencem ao pacote principal e quais exigem pacotes opcionais.',
    pages: [
      ['dapper', 'Dapper Integration', 'Integração com Dapper', 'Use public Dapper type-map and query contracts with FluentMap.', 'Use contratos públicos de type map e consulta do Dapper com FluentMap.'],
      ['dommel', 'Dommel Integration', 'Integração com Dommel', 'Add table, key, generated-column, and mapped write metadata.', 'Adicione metadados de tabela, chave, coluna gerada e escrita mapeada.'],
      ['dependency-injection', 'Dependency Injection Integration', 'Integração com injeção de dependência', 'Register immutable configuration and FluentMapRuntime in a service collection.', 'Registre configuração imutável e FluentMapRuntime em uma coleção de serviços.'],
      ['aspnet-core', 'ASP.NET Core Integration', 'Integração com ASP.NET Core', 'Compose mappings once at startup and inject an isolated runtime.', 'Componha mappings uma vez na inicialização e injete um runtime isolado.'],
      ['roslyn-analyzers', 'Roslyn Analyzers', 'Analyzers Roslyn', 'Catch statically provable configuration errors during compilation.', 'Encontre erros de configuração comprováveis estaticamente durante a compilação.'],
      ['source-generators', 'Source Generators', 'Source generators', 'Generate mapping registration and supported materializers at build time.', 'Gere registro de mappings e materializadores suportados durante o build.']
    ]
  },
  examples: {
    en: 'Examples', pt: 'Exemplos',
    introEn: 'Start from complete, provider-aware patterns built from the documented public API.',
    introPt: 'Parta de padrões completos e conscientes do provider, construídos com a API pública documentada.',
    pages: [
      ['simple-mapping', 'Simple Mapping', 'Mapeamento simples', 'Map two columns into a mutable Customer entity.', 'Mapeie duas colunas para uma entidade Customer mutável.'],
      ['legacy-database', 'Legacy Database Mapping', 'Mapeamento de banco legado', 'Keep legacy column names outside the domain model.', 'Mantenha nomes de colunas legados fora do modelo de domínio.'],
      ['aspnet-core', 'ASP.NET Core Example', 'Exemplo com ASP.NET Core', 'Register an isolated mapping runtime in the application container.', 'Registre um runtime de mapping isolado no contêiner da aplicação.'],
      ['immutable-domain', 'Immutable Domain Model', 'Modelo de domínio imutável', 'Materialize a constructor-based aggregate with mapped columns.', 'Materialize um agregado baseado em construtor com colunas mapeadas.'],
      ['postgresql', 'PostgreSQL Example', 'Exemplo com PostgreSQL', 'Apply FluentMap above an Npgsql connection and explicit SQL aliases.', 'Aplique FluentMap sobre uma conexão Npgsql e aliases SQL explícitos.'],
      ['sql-server', 'SQL Server Example', 'Exemplo com SQL Server', 'Use FluentMap with a SqlConnection while retaining parameterized SQL.', 'Use FluentMap com SqlConnection mantendo SQL parametrizado.'],
      ['sqlite', 'SQLite Example', 'Exemplo com SQLite', 'Use an in-memory SQLite database for a compact integration example.', 'Use um banco SQLite em memória em um exemplo compacto de integração.'],
      ['dependency-injection', 'Dependency Injection Example', 'Exemplo com injeção de dependência', 'Resolve FluentMapRuntime and keep connection ownership explicit.', 'Resolva FluentMapRuntime e mantenha explícita a propriedade da conexão.'],
      ['generated-mapping', 'Generated Mapping', 'Mapping gerado', 'Use AddGeneratedMappings for maps declared in the current compilation.', 'Use AddGeneratedMappings para maps declarados na compilação atual.']
    ]
  },
  migration: {
    en: 'Migration', pt: 'Migração',
    introEn: 'Move from 2.x to 3.x with compatibility decisions grounded in the maintained migration guide.',
    introPt: 'Migre da 2.x para a 3.x com decisões de compatibilidade baseadas no guia mantido.',
    pages: [
      ['migrating-2-to-3', 'Migrating from 2.x to 3.x', 'Migrando da 2.x para a 3.x', 'Preserve the historical mapping path while reviewing opt-in 3.x behavior.', 'Preserve o caminho histórico de mapping enquanto revisa recursos opt-in da 3.x.'],
      ['breaking-changes', 'Breaking Changes', 'Mudanças incompatíveis', 'Review behavior and package differences that can affect an upgrade.', 'Revise diferenças de comportamento e pacote que podem afetar a atualização.'],
      ['compatibility-considerations', 'Compatibility Considerations', 'Considerações de compatibilidade', 'Check target frameworks, dependency ranges, providers, and global state.', 'Verifique targets, faixas de dependências, providers e estado global.'],
      ['checklist', 'Migration Checklist', 'Checklist de migração', 'Run a repeatable upgrade sequence with validation at each boundary.', 'Execute uma sequência de atualização repetível com validação em cada limite.'],
      ['troubleshooting', 'Migration Troubleshooting', 'Solução de problemas de migração', 'Diagnose registration, materialization, Dommel, and generated-path issues.', 'Diagnostique problemas de registro, materialização, Dommel e caminhos gerados.']
    ]
  },
  reference: {
    en: 'Reference', pt: 'Referência',
    introEn: 'Use concise contracts and matrices while implementing or diagnosing an integration.',
    introPt: 'Use contratos e matrizes concisos ao implementar ou diagnosticar uma integração.',
    pages: [
      ['api-overview', 'API Overview', 'Visão geral da API', 'Find the main configuration, mapping, query, and runtime entry points.', 'Encontre os principais pontos de entrada de configuração, mapping, query e runtime.'],
      ['package-overview', 'Package Overview', 'Visão geral dos pacotes', 'Choose among the five separately published FluentMap packages.', 'Escolha entre os cinco pacotes FluentMap publicados separadamente.'],
      ['compatibility-matrix', 'Compatibility Matrix', 'Matriz de compatibilidade', 'Consult the maintained dependency and runtime compatibility contract.', 'Consulte o contrato mantido de compatibilidade de dependências e runtime.'],
      ['supported-providers', 'Supported Providers', 'Providers suportados', 'Distinguish certified providers from general ADO.NET compatibility.', 'Diferencie providers certificados de compatibilidade ADO.NET geral.'],
      ['runtime-requirements', 'Runtime Requirements', 'Requisitos de runtime', 'Understand netstandard2.0 and the application runtime responsibility.', 'Entenda netstandard2.0 e a responsabilidade do runtime da aplicação.'],
      ['trimming-native-aot', 'Trimming and Native AOT Limitations', 'Limitações de trimming e Native AOT', 'Use explicit or generated paths without claiming complete AOT support.', 'Use caminhos explícitos ou gerados sem afirmar suporte completo a AOT.'],
      ['configuration-reference', 'Configuration Reference', 'Referência de configuração', 'Compare global, isolated, profile, convention, and generated configuration.', 'Compare configurações global, isolada, por profile, convenção e gerada.'],
      ['changelog', 'FluentMap Changelog', 'Changelog do FluentMap', 'Read release history from the repository source of truth.', 'Leia o histórico de releases na fonte de verdade do repositório.'],
      ['troubleshooting', 'Troubleshooting', 'Solução de problemas', 'Trace common configuration and materialization failures systematically.', 'Investigue falhas comuns de configuração e materialização de modo sistemático.'],
      ['faq', 'FluentMap FAQ', 'FAQ do FluentMap', 'Get concise answers about scope, packages, state, and support boundaries.', 'Obtenha respostas concisas sobre escopo, pacotes, estado e limites de suporte.']
    ]
  }
};

const code = {
  default: `public sealed class CustomerMap : EntityMap<Customer>\n{\n    public CustomerMap()\n    {\n        Map(x => x.Id).ToColumn("customer_id");\n        Map(x => x.Name).ToColumn("customer_name");\n    }\n}`,
  installation: `dotnet add package Dapper.FluentMap`,
  'quick-start': `using Dapper;\nusing Dapper.FluentMap;\nusing Dapper.FluentMap.Mapping;\nusing Microsoft.Data.Sqlite;\n\nFluentMapper.Initialize(config =>\n{\n    config.AddMap<CustomerMap>();\n});\n\nFluentMapper.Validate();\n\nusing var connection = new SqliteConnection("Data Source=:memory:");\nvar customer = connection.QuerySingle<Customer>(\n    "SELECT 7 AS customer_id, 'Ada' AS customer_name;");\n\npublic sealed class Customer\n{\n    public int Id { get; set; }\n    public string Name { get; set; } = string.Empty;\n}\n\npublic sealed class CustomerMap : EntityMap<Customer>\n{\n    public CustomerMap()\n    {\n        Map(x => x.Id).ToColumn("customer_id");\n        Map(x => x.Name).ToColumn("customer_name");\n    }\n}`,
  configuration: `FluentMapper.Initialize(config =>\n{\n    config.AddMap<CustomerMap>();\n});\n\nFluentMapper.Validate();`,
  'mapping-conventions': `FluentMapper.Initialize(config =>\n{\n    config.UseNamingPolicy(NamingPolicy.SnakeCase, caseSensitive: false);\n});`,
  'ignored-properties': `Map(x => x.TransientValue).Ignore();`,
  'configuration-validation': `FluentMapper.Initialize(config => config.AddMap<CustomerMap>());\nFluentMapper.Validate();`,
  'immutable-objects': `public sealed class CustomerMap : EntityMap<Customer>\n{\n    public CustomerMap()\n    {\n        Map(x => x.Id).ToColumn("customer_id");\n        Map(x => x.Name).ToColumn("customer_name");\n    }\n}\n\nvar customer = connection.QueryMappedSingle<Customer>(sql);`,
  'constructor-mapping': `ConstructUsing(\n    customer => customer.Id,\n    customer => customer.Name,\n    Customer.Restore);`,
  'nested-objects': `Map(x => x.Address.City).ToColumn("city_name");\nvar customer = connection.QueryMappedSingle<Customer>(sql);`,
  'value-objects': `Map(x => x.Name.Value).ToColumn("customer_name");\nvar customer = connection.QueryMappedSingle<Customer>(sql);`,
  'mapping-profiles': `config.AddProfile<LegacyCustomerMap>();\nvar customer = connection.QueryMappedSingle<Customer, LegacyProfile>(sql);`,
  'property-converters': `Map(x => x.Status)\n    .ToColumn("status_code")\n    .ConvertFromDatabaseUsing<ProductStatusConverter, string>();`,
  'multi-mapping': `var rows = connection.QueryMapped<Customer, Order, CustomerOrder>(\n    sql, (customer, order) => new(customer, order), splitOn: "order_id");`,
  'multiple-result-sets': `using var multi = connection.QueryMultipleMapped(sql);\nvar customers = multi.ReadMapped<Customer>();\nvar orders = multi.ReadMapped<Order>();`,
  streaming: `foreach (var customer in connection.QueryMappedUnbuffered<Customer>(sql))\n{\n    Process(customer);\n}`,
  'runtime-isolation': `var runtime = new FluentMapConfigurationBuilder()\n    .AddMap<CustomerMap>()\n    .Build()\n    .CreateRuntime();\n\nvar customer = runtime.QueryMappedSingle<Customer>(connection, sql);`,
  dommel: `FluentMapper.Initialize(config =>\n{\n    config.AddMap<CustomerMap>();\n    config.ForDommel();\n});`,
  'dependency-injection': `services.AddFluentMap(builder =>\n{\n    builder.Configure(config => config.AddMap<CustomerMap>());\n});`,
  'aspnet-core': `builder.Services.AddFluentMap(mapping =>\n    mapping.Configure(config => config.AddMap<CustomerMap>()));`,
  'roslyn-analyzers': `dotnet add package FluentMap.Analyzers`,
  'source-generators': `dotnet add package FluentMap.Generators\n\n// Startup\nconfig.AddGeneratedMappings();`,
  postgresql: `await using var connection = new NpgsqlConnection(connectionString);\nvar rows = connection.Query<Customer>(\n    "SELECT customer_id, customer_name FROM customers");`,
  'sql-server': `await using var connection = new SqlConnection(connectionString);\nvar row = connection.QuerySingle<Customer>(\n    "SELECT customer_id, customer_name FROM dbo.Customers WHERE customer_id = @id",\n    new { id = 7 });`,
  sqlite: `using var connection = new SqliteConnection("Data Source=:memory:");\nconnection.Open();\nvar customer = connection.QuerySingle<Customer>(sql);`,
  'legacy-database': `public sealed class CustomerMap : EntityMap<Customer>\n{\n    public CustomerMap()\n    {\n        Map(x => x.Id).ToColumn("CUST_NO");\n        Map(x => x.Name).ToColumn("LEGAL_NM");\n    }\n}`,
  'immutable-domain': `public sealed class Customer\n{\n    public Customer(int id, string name)\n    {\n        Id = id;\n        Name = name;\n    }\n\n    public int Id { get; }\n    public string Name { get; }\n}\n\nvar customer = connection.QueryMappedSingle<Customer>(sql);`,
  'generated-mapping': `FluentMapper.Initialize(config =>\n{\n    config.AddGeneratedMappings();\n});`
};

const restrictions = {
  'nested-objects': ['Use `QueryMapped*`; ordinary Dapper `Query<T>()` only covers the historical root mapping path.', 'Use `QueryMapped*`; o `Query<T>()` comum do Dapper cobre apenas o caminho histórico de mapping raiz.'],
  'value-objects': ['Support depends on an explicit, validated component shape; do not infer it from property names.', 'O suporte depende de um formato componente explícito e validado; não o deduza apenas por nomes.'],
  'constructor-mapping': ['ConstructUsing binds one to four explicitly mapped root properties and is runtime-only.', 'ConstructUsing associa de uma a quatro propriedades raiz explicitamente mapeadas e funciona em runtime.'],
  'source-generators': ['The generator scans the current compilation, does not parse SQL, and does not replace FluentMapper.Validate().', 'O generator examina a compilação atual, não interpreta SQL e não substitui FluentMapper.Validate().'],
  'roslyn-analyzers': ['Analyzers detect statically provable errors; they do not execute mapping constructors or access a database.', 'Analyzers detectam erros comprováveis estaticamente; não executam construtores de maps nem acessam o banco.'],
  dommel: ['Dommel is optional. Historical Dommel Insert/Update cannot apply per-property write converters; use InsertMapped*/UpdateMapped* when conversion is required.', 'Dommel é opcional. Insert/Update históricos não aplicam conversores por propriedade; use InsertMapped*/UpdateMapped* quando houver conversão.'],
  'trimming-native-aot': ['Native AOT support is bounded and scenario-specific. Consult COMPATIBILITY.md and run the repository smoke tests for your path.', 'O suporte a Native AOT é limitado e depende do cenário. Consulte COMPATIBILITY.md e execute os smoke tests do repositório.'],
  'supported-providers': ['A provider implementing ADO.NET contracts may work without being part of the certified matrix.', 'Um provider que implemente os contratos ADO.NET pode funcionar sem fazer parte da matriz certificada.'],
  default: ['Initialize before concurrent queries and treat the effective configuration as read-only afterward.', 'Inicialize antes de consultas concorrentes e trate a configuração efetiva como somente leitura depois disso.']
};

const pageDetails = {
  installation: [
    `## Version status

- Latest published stable release: \`3.6.0\`.
- Version configured in the current source: \`3.6.1\`.
- Next package prepared for publication: \`3.6.1\` (not published yet).

The unversioned installation command resolves the latest version available from the selected package source. Pin \`--version 3.6.0\` when the application must remain on the current published stable release. Use \`3.6.1\` only after it is published or when consuming an explicitly built, trusted artifact from this source. Keep all five FluentMap packages on the same release version.

## Dapper dependency

For FluentMap 3.6.1, the supported Dapper range is \`[2.1.89,3.0.0)\`, with \`2.1.89\` as both the minimum and current validated stable baseline. FluentMap 3.6.0 supports \`[2.1.79,3.0.0)\`. The 3.6.1 NuGet range allows compatible Dapper 2.x versions at or above its minimum; it is not the exact pin \`[2.1.89]\`. Upgrade applications still resolving Dapper 2.1.79 before adopting the FluentMap 3.6.1 baseline.`,
    `## Estado da versão

- Última release estável publicada: \`3.6.0\`.
- Versão configurada no código-fonte atual: \`3.6.1\`.
- Próximo pacote preparado para publicação: \`3.6.1\` (ainda não publicado).

O comando de instalação sem versão resolve a versão mais recente disponível na fonte de pacotes selecionada. Fixe \`--version 3.6.0\` quando a aplicação precisar permanecer na release estável atualmente publicada. Use \`3.6.1\` somente após sua publicação ou ao consumir um artefato confiável, explicitamente compilado a partir deste código-fonte. Mantenha os cinco pacotes FluentMap na mesma versão de release.

## Dependência do Dapper

Para o FluentMap 3.6.1, a faixa suportada do Dapper é \`[2.1.89,3.0.0)\`, tendo \`2.1.89\` como mínimo e baseline estável validado atual. O FluentMap 3.6.0 suporta \`[2.1.79,3.0.0)\`. A faixa NuGet da versão 3.6.1 permite versões 2.x compatíveis iguais ou superiores ao seu mínimo; ela não é o pin exato \`[2.1.89]\`. Atualize aplicações que ainda resolvem Dapper 2.1.79 antes de adotar o baseline do FluentMap 3.6.1.`
  ],
  changelog: [
    `## Current release state

Version \`3.6.0\` is the latest published stable release. Version \`3.6.1\` is configured and prepared in the repository but has not been published. The authoritative changelog linked below records the 3.6.1 preparation separately from the historical 3.6.0 release.`,
    `## Estado atual das releases

A versão \`3.6.0\` é a última release estável publicada. A versão \`3.6.1\` está configurada e preparada no repositório, mas ainda não foi publicada. O changelog autoritativo indicado abaixo registra a preparação da 3.6.1 separadamente da release histórica 3.6.0.`
  ],
  dapper: [
    `## Dapper 2.1.89 boundary

- Dapper owns ordinary \`Query<T>()\` materialization, \`DynamicParameters\`, its IL-diagnostic suppressions and its separate StrongName packages.
- FluentMap owns mapping metadata and the value assignment performed by \`QueryMapped*\`, \`ReadMapped*\` and eligible generated materializers.
- FluentMap therefore implements matching verified conversions for \`DateTime\` to \`DateOnly\`, \`DateOnly\` to midnight \`DateTime\`, \`DateTime\`/\`TimeSpan\` to \`TimeOnly\`, and \`TimeOnly\` to \`TimeSpan\`.
- Nullable values, immutable constructors, nested members and property converters are covered in runtime and generated tests. No other implicit date/time conversion is claimed.

The public \`DynamicParameters.AddParameters(IDbCommand)\` change is upstream Dapper API. FluentMap passes \`DynamicParameters\` through non-strict mapped queries but does not wrap that method. Strict-generated queries use explicit \`GeneratedParameters\` instead.`,
    `## Limite do Dapper 2.1.89

- O Dapper é responsável pela materialização de \`Query<T>()\` comum, por \`DynamicParameters\`, pelos suppressions de diagnósticos IL e pelos pacotes StrongName separados.
- O FluentMap é responsável pelos metadados de mapping e pela atribuição de valores realizada por \`QueryMapped*\`, \`ReadMapped*\` e materializadores gerados elegíveis.
- Por isso, o FluentMap implementa as conversões verificadas correspondentes: \`DateTime\` para \`DateOnly\`, \`DateOnly\` para \`DateTime\` à meia-noite, \`DateTime\`/\`TimeSpan\` para \`TimeOnly\` e \`TimeOnly\` para \`TimeSpan\`.
- Valores nullable, construtores imutáveis, membros aninhados e property converters possuem cobertura runtime e gerada. Nenhuma outra conversão implícita de data/hora é afirmada.

A mudança pública em \`DynamicParameters.AddParameters(IDbCommand)\` pertence à API upstream do Dapper. O FluentMap repassa \`DynamicParameters\` em queries mapeadas não estritas, mas não encapsula esse método. Queries strict-generated usam \`GeneratedParameters\` explícitos.`
  ],
  'property-converters': [
    `## Provider-specific date/time values

Property converters run before Dapper type handlers and FluentMap default conversion. They are the verified solution when a provider exposes date/time data in another CLR representation, such as SQLite ISO text:

\`\`\`csharp
Map(x => x.BusinessDate)
    .ToColumn("business_date")
    .ConvertFromDatabaseUsing<string, DateOnly>(value =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture));
\`\`\`

Database \`NULL\` remains null for nullable targets and does not invoke the converter.`,
    `## Valores de data/hora específicos do provider

Property converters executam antes de type handlers do Dapper e da conversão default do FluentMap. Eles são a solução verificada quando um provider expõe data/hora em outra representação CLR, como texto ISO no SQLite:

\`\`\`csharp
Map(x => x.BusinessDate)
    .ToColumn("business_date")
    .ConvertFromDatabaseUsing<string, DateOnly>(value =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture));
\`\`\`

\`NULL\` do banco permanece null em targets nullable e não chama o converter.`
  ],
  'source-generators': [
    `## Date/time and strict-generated behavior

For consumer targets with \`NET6_0_OR_GREATER\`, eligible generated materializers emit the same verified date/time conversions as the runtime path. Older target frameworks receive no invalid \`DateOnly\` or \`TimeOnly\` references. Strict-generated Native AOT evidence covers the SQLite generated path, nullable date/time properties and explicit property converters; unsupported shapes fail instead of entering runtime fallback.`,
    `## Data/hora e comportamento strict-generated

Para targets consumidores com \`NET6_0_OR_GREATER\`, materializadores gerados elegíveis emitem as mesmas conversões verificadas de data/hora do caminho runtime. Targets mais antigos não recebem referências inválidas a \`DateOnly\` ou \`TimeOnly\`. A evidência strict-generated em Native AOT cobre o caminho gerado com SQLite, propriedades nullable de data/hora e property converters explícitos; formatos não suportados falham em vez de entrar no fallback runtime.`
  ],
  'compatibility-considerations': [
    `## Dapper baseline migration

The minimum supported Dapper version is now \`2.1.89\`; \`2.1.79\` is no longer supported. Check direct and transitive package resolution, refresh lock files, and rerun provider-backed tests. The range \`[2.1.89,3.0.0)\` is a minimum constraint, not an exact pin. General provider certification does not automatically certify every provider-specific \`DateOnly\`/\`TimeOnly\` CLR shape.`,
    `## Migração do baseline do Dapper

A versão mínima suportada do Dapper agora é \`2.1.89\`; \`2.1.79\` deixou de ser suportada. Verifique a resolução direta e transitiva de pacotes, renove lock files e repita testes com os providers reais. A faixa \`[2.1.89,3.0.0)\` é uma restrição mínima, não um pin exato. A certificação geral de um provider não certifica automaticamente todos os seus formatos CLR específicos de \`DateOnly\`/\`TimeOnly\`.`
  ],
  'compatibility-matrix': [
    `## Current dependency contract

| Dependency | Supported range | Validated stable baseline |
| --- | --- | --- |
| Dapper | \`[2.1.89,3.0.0)\` | \`2.1.89\` |
| Dommel | \`[3.5.3,4.0.0)\` | \`3.5.3\` |

The Dapper range accepts compatible 2.x versions from the minimum onward; it does not pin exactly 2.1.89. Package metadata and this portal are checked against \`eng/compatibility-contract.json\`.`,
    `## Contrato de dependências atual

| Dependência | Faixa suportada | Baseline estável validado |
| --- | --- | --- |
| Dapper | \`[2.1.89,3.0.0)\` | \`2.1.89\` |
| Dommel | \`[3.5.3,4.0.0)\` | \`3.5.3\` |

A faixa do Dapper aceita versões 2.x compatíveis a partir do mínimo; ela não fixa exatamente a 2.1.89. A metadata dos pacotes e este portal são verificados contra \`eng/compatibility-contract.json\`.`
  ],
  'trimming-native-aot': [
    `## Validated boundary

Ordinary runtime materialization may use reflection, expression compilation and Dapper fallback; it is not the certified Native AOT path. Generated materialization normally may also fall back. The validated Native AOT boundary is strict-generated SQLite with generated registration/materializers, explicit \`GeneratedParameters\`, safe column reordering/additions, property converters, nullable \`DateOnly\`/\`TimeOnly\` and deterministic rejection of unsupported shapes.

Dapper 2.1.89 suppresses selected IL diagnostics in upstream runtime fallback code. Suppression alone does not establish universal Native AOT compatibility for Dapper or FluentMap. Assembly scanning, convention-only discovery, arbitrary parameter objects, reflective TypeHandler interop and delegate construction strategies remain outside the strict-generated claim.`,
    `## Limite validado

A materialização runtime comum pode usar reflection, compilação de expressões e fallback do Dapper; ela não é o caminho Native AOT certificado. A materialização gerada comum também pode usar fallback. O limite Native AOT validado é SQLite strict-generated com registro/materializadores gerados, \`GeneratedParameters\` explícitos, reordenação/adição segura de colunas, property converters, \`DateOnly\`/\`TimeOnly\` nullable e rejeição determinística de formatos não suportados.

O Dapper 2.1.89 suprime alguns diagnósticos IL em seu código upstream de fallback runtime. O suppression isolado não estabelece compatibilidade Native AOT universal para Dapper ou FluentMap. Assembly scanning, descoberta somente por convenção, objetos de parâmetros arbitrários, interoperabilidade reflexiva de TypeHandler e estratégias de construção por delegate ficam fora da afirmação strict-generated.`
  ],
  dommel: [
    `## Provider boundary

The Dommel package has a narrower provider boundary than FluentMap Core:

| Provider path | Status |
| --- | --- |
| SQLite, SQL Server, PostgreSQL | Dommel persistence certified |
| MySQL, MariaDB | Certified through the shared \`MySqlConnection\` / \`MySqlSqlBuilder\` path |
| Oracle, Firebird | Core-only; no built-in Dommel 3.5.3 SQL builder |
| SQL Server CE | Builder registration retained; legacy/upstream-limited and not modernly certified |

Advanced consumers may use Dommel's public \`DommelMapper.AddSqlBuilder(Type, ISqlBuilder)\` or \`AddSqlBuilder(string, ISqlBuilder)\` APIs. Registration is process-wide, and a custom builder remains the application's SQL-generation and persistence-testing responsibility; it does not extend this project's certification.`,
    `## Limite de providers

O pacote Dommel possui um limite de providers mais restrito que o FluentMap Core:

| Caminho do provider | Status |
| --- | --- |
| SQLite, SQL Server, PostgreSQL | Persistência Dommel certificada |
| MySQL, MariaDB | Certificados pelo caminho compartilhado \`MySqlConnection\` / \`MySqlSqlBuilder\` |
| Oracle, Firebird | Somente Core; sem SQL builder integrado no Dommel 3.5.3 |
| SQL Server CE | Registro do builder preservado; legado/limitado pelo upstream e sem certificação moderna |

Consumidores avançados podem usar as APIs públicas \`DommelMapper.AddSqlBuilder(Type, ISqlBuilder)\` ou \`AddSqlBuilder(string, ISqlBuilder)\` do Dommel. O registro é global ao processo, e um builder customizado continua sob responsabilidade da aplicação quanto à geração de SQL e aos testes de persistência; ele não amplia a certificação deste projeto.`
  ],
  'supported-providers': [
    `## Support levels

- **Dapper-compatible:** expected to work through Dapper/ADO.NET, without dedicated real-database FluentMap certification.
- **FluentMap Core certified:** required real-database tests cover FluentMap mapping and materialization, independently of Dommel.
- **FluentMap + Dommel certified:** required real-database tests also cover the supported Dommel persistence path.
- **Legacy/upstream-limited:** retained for compatibility without a modern certification lane.

| Provider | Core | Dommel | Tested server/runtime | Tested client |
| --- | --- | --- | --- | --- |
| SQLite | Certified | Certified | In-memory | \`Microsoft.Data.Sqlite\` 10.0.14 |
| SQL Server | Certified | Certified | 2022 CU23 | \`Microsoft.Data.SqlClient\` 7.1.1 |
| PostgreSQL | Certified | Certified | 18.6 | \`Npgsql\` 10.0.3 |
| MySQL | Certified | Certified | 8.4.11 | \`MySqlConnector\` 2.6.2 |
| MariaDB | Certified | Certified | 11.8.9 | \`MySqlConnector\` 2.6.2 |
| Oracle | Certified | Not certified | 23.26.0 Free | \`Oracle.ManagedDataAccess.Core\` 23.26.400 |
| Firebird | Certified | Not certified | 5.0.4 | \`FirebirdSql.Data.FirebirdClient\` 10.3.4 |
| SQL Server CE | Legacy/upstream-limited | Legacy/upstream-limited | Not certified | Not certified |

These pinned versions and evidence are also maintained in the machine-readable compatibility contract.

The date/time feature has a narrower evidence set: SQLite is verified with ISO text plus property converters, SQL Server with \`DateTime\`/\`TimeSpan\` provider values, and PostgreSQL with native \`DateOnly\`/\`TimeOnly\` values. MySQL, MariaDB, Oracle and Firebird remain Core-certified generally, but their DateOnly/TimeOnly shapes are not certified.`,
    `## Níveis de suporte

- **Compatível com Dapper:** funcionamento esperado via Dapper/ADO.NET, sem certificação dedicada do FluentMap em banco real.
- **FluentMap Core certificado:** testes obrigatórios em banco real cobrem mapping e materialização do FluentMap, independentemente do Dommel.
- **FluentMap + Dommel certificado:** testes obrigatórios em banco real também cobrem o caminho de persistência suportado pelo Dommel.
- **Legado/limitado pelo upstream:** mantido por compatibilidade, sem uma lane moderna de certificação.

| Provider | Core | Dommel | Servidor/runtime testado | Cliente testado |
| --- | --- | --- | --- | --- |
| SQLite | Certificado | Certificado | Em memória | \`Microsoft.Data.Sqlite\` 10.0.14 |
| SQL Server | Certificado | Certificado | 2022 CU23 | \`Microsoft.Data.SqlClient\` 7.1.1 |
| PostgreSQL | Certificado | Certificado | 18.6 | \`Npgsql\` 10.0.3 |
| MySQL | Certificado | Certificado | 8.4.11 | \`MySqlConnector\` 2.6.2 |
| MariaDB | Certificado | Certificado | 11.8.9 | \`MySqlConnector\` 2.6.2 |
| Oracle | Certificado | Não certificado | 23.26.0 Free | \`Oracle.ManagedDataAccess.Core\` 23.26.400 |
| Firebird | Certificado | Não certificado | 5.0.4 | \`FirebirdSql.Data.FirebirdClient\` 10.3.4 |
| SQL Server CE | Legado/limitado pelo upstream | Legado/limitado pelo upstream | Não certificado | Não certificado |

Essas versões fixadas e suas evidências também são mantidas no contrato de compatibilidade legível por máquina.

O recurso de data/hora possui evidência mais restrita: SQLite é verificado com texto ISO e property converters, SQL Server com valores de provider \`DateTime\`/\`TimeSpan\` e PostgreSQL com valores nativos \`DateOnly\`/\`TimeOnly\`. MySQL, MariaDB, Oracle e Firebird continuam certificados no Core em geral, mas seus formatos DateOnly/TimeOnly não são certificados.`
  ]
};

const packagesFor = (_section, slug) => {
  if (slug === 'dommel') return ['Dapper.FluentMap.Dommel'];
  if (slug === 'dependency-injection' || slug === 'aspnet-core') return ['FluentMap.DependencyInjection'];
  if (slug === 'roslyn-analyzers') return ['FluentMap.Analyzers'];
  if (slug === 'source-generators' || slug === 'generated-mapping') return ['FluentMap.Generators'];
  if (slug === 'quick-start') return ['Dapper.FluentMap', 'Microsoft.Data.Sqlite'];
  if (slug === 'postgresql') return ['Dapper.FluentMap', 'Npgsql'];
  if (slug === 'sql-server') return ['Dapper.FluentMap', 'Microsoft.Data.SqlClient'];
  if (slug === 'sqlite') return ['Dapper.FluentMap', 'Microsoft.Data.Sqlite'];
  return ['Dapper.FluentMap'];
};

const yaml = (value) => JSON.stringify(value);
const sourceLinks = (section, pt) => {
  const base = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap/blob/main/';
  const links = section === 'migration'
    ? [[pt ? 'Guia de migração autoritativo' : 'Authoritative migration guide', `${base}${pt ? 'MIGRATION.pt-BR.md' : 'MIGRATION.md'}`]]
    : section === 'reference'
      ? [[pt ? 'Contrato de compatibilidade' : 'Compatibility contract', `${base}COMPATIBILITY.md`], ['Changelog', `${base}CHANGELOG.md`]]
      : [[pt ? 'Guia de uso completo' : 'Complete usage guide', `${base}${pt ? 'USAGE.pt-BR.md' : 'USAGE.md'}`]];
  return links.map(([label, href]) => `- [${label}](${href})`).join('\n');
};

function pageBody(section, entry, pt) {
  const [slug, enTitle, ptTitle, enDescription, ptDescription] = entry;
  const title = pt ? ptTitle : enTitle;
  const description = pt ? ptDescription : enDescription;
  const packageNames = packagesFor(section, slug);
  const packageLinks = packageNames
    .map((packageName) => `[\`${packageName}\`](https://www.nuget.org/packages/${packageName})`)
    .join(', ');
  const sample = code[slug] ?? code.default;
  const constraint = restrictions[slug]?.[pt ? 1 : 0] ?? restrictions.default[pt ? 1 : 0];
  const detail = pageDetails[slug]?.[pt ? 1 : 0] ?? '';
  const detailBlock = detail ? `\n\n${detail}` : '';
  const labels = pt ? {
    package: packageNames.length > 1 ? 'Pacotes necessários' : 'Pacote necessário', problem: 'Problema resolvido', when: 'Quando usar', example: 'Exemplo', explanation: 'Como funciona', limits: 'Restrições', related: 'Referências relacionadas',
    whenText: 'Use este recurso quando o formato das colunas e o modelo .NET precisarem permanecer separados por uma configuração explícita e revisável.',
    explainText: 'O FluentMap registra metadados tipados e os aplica no caminho de materialização correspondente. O SQL, a conexão, os parâmetros e o ciclo de vida continuam sob responsabilidade da aplicação e do Dapper.'
  } : {
    package: packageNames.length > 1 ? 'Required packages' : 'Required package', problem: 'Problem solved', when: 'When to use it', example: 'Example', explanation: 'How it works', limits: 'Constraints', related: 'Related references',
    whenText: 'Use this capability when the column shape and .NET model should remain separated by explicit, reviewable configuration.',
    explainText: 'FluentMap registers typed metadata and applies it through the matching materialization path. SQL, connections, parameters, and lifecycle remain responsibilities of the application and Dapper.'
  };
  const prefix = pt ? '/Dapper-FluentMap/pt-br' : '/Dapper-FluentMap';
  const sectionTitle = pt ? sections[section].pt : sections[section].en;
  const breadcrumb = `<nav class="portal-breadcrumbs" aria-label="${pt ? 'Trilha de navegação' : 'Breadcrumb'}"><a href="${prefix}/">${pt ? 'Início' : 'Home'}</a><span aria-hidden="true">/</span><a href="${prefix}/${section}/">${sectionTitle}</a><span aria-hidden="true">/</span><span aria-current="page">${title}</span></nav>`;
  return `---\ntitle: ${yaml(title)}\ndescription: ${yaml(description)}\n---\n\n<!-- Generated by scripts/generate-content.mjs. Edit the generator, not this file. -->\n\n${breadcrumb}\n\n> **${labels.package}:** ${packageLinks}\n\n## ${labels.problem}\n\n${description}\n\n## ${labels.when}\n\n${labels.whenText}\n\n## ${labels.example}\n\n\`\`\`${sample.startsWith('dotnet ') ? 'bash' : 'csharp'}\n${sample}\n\`\`\`\n\n## ${labels.explanation}\n\n${labels.explainText}${detailBlock}\n\n## ${labels.limits}\n\n:::caution\n${constraint}\n:::\n\n## ${labels.related}\n\n${sourceLinks(section, pt)}\n`;
}

function indexBody(data, pt) {
  const title = pt ? data.pt : data.en;
  const description = pt ? data.introPt : data.introEn;
  const list = data.pages.map(([slug, enTitle, ptTitle,,]) => `- [${pt ? ptTitle : enTitle}](./${slug}/)`).join('\n');
  const prefix = pt ? '/Dapper-FluentMap/pt-br' : '/Dapper-FluentMap';
  const breadcrumb = `<nav class="portal-breadcrumbs" aria-label="${pt ? 'Trilha de navegação' : 'Breadcrumb'}"><a href="${prefix}/">${pt ? 'Início' : 'Home'}</a><span aria-hidden="true">/</span><span aria-current="page">${title}</span></nav>`;
  return `---\ntitle: ${yaml(title)}\ndescription: ${yaml(description)}\n---\n\n<!-- Generated by scripts/generate-content.mjs. Edit the generator, not this file. -->\n\n${breadcrumb}\n\n${description}\n\n${list}\n`;
}

export const routeManifest = [];
for (const [section, data] of Object.entries(sections)) {
  for (const pt of [false, true]) {
    const prefix = pt ? 'pt-br/' : '';
    const indexPath = resolve(root, prefix, section, 'index.md');
    await mkdir(dirname(indexPath), { recursive: true });
    await writeFile(indexPath, indexBody(data, pt), 'utf8');
    for (const entry of data.pages) {
      const target = resolve(root, prefix, section, `${entry[0]}.md`);
      await writeFile(target, pageBody(section, entry, pt), 'utf8');
      if (!pt) routeManifest.push(`${section}/${entry[0]}`);
    }
  }
}

for (const pt of [false, true]) {
  const target = resolve(root, pt ? 'pt-br/404.md' : '404.md');
  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, `---\ntitle: ${yaml(pt ? 'Página não encontrada' : 'Page not found')}\ndescription: ${yaml(pt ? 'A página solicitada não existe.' : 'The requested page does not exist.')}\ntemplate: splash\neditUrl: false\n---\n\n${pt ? 'Use a navegação ou a pesquisa para continuar.' : 'Use the navigation or search to continue.'}\n`, 'utf8');
}

const socialSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="630" viewBox="0 0 1200 630">
  <defs><linearGradient id="background" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#041f24"/><stop offset="1" stop-color="#0b4b50"/></linearGradient><linearGradient id="mark" x1="0" x2="1"><stop stop-color="#2dd4bf"/><stop offset="1" stop-color="#22d3ee"/></linearGradient></defs>
  <rect width="1200" height="630" fill="url(#background)"/><circle cx="1040" cy="80" r="260" fill="#0f766e" opacity=".3"/><circle cx="1100" cy="560" r="210" fill="#f59e0b" opacity=".12"/>
  <rect x="86" y="94" width="116" height="116" rx="30" fill="#062f35" stroke="#2dd4bf" stroke-width="3"/><path d="M116 130h42c31 0 49 15 49 37s-18 37-49 37h-42v-74Zm29 23v28h15c13 0 20-5 20-14s-7-14-20-14h-15Z" fill="url(#mark)"/><circle cx="202" cy="120" r="13" fill="#f59e0b"/>
  <text x="86" y="300" fill="#ccfbf1" font-family="Inter,Arial,sans-serif" font-size="72" font-weight="760">Dapper FluentMap</text>
  <text x="86" y="384" fill="#ffffff" font-family="Inter,Arial,sans-serif" font-size="44" font-weight="650">Map your data. Keep your models clean.</text>
  <text x="86" y="448" fill="#a7d8d2" font-family="Inter,Arial,sans-serif" font-size="27">Fluent, strongly typed column mapping for Dapper.</text>
  <text x="86" y="545" fill="#fbbf24" font-family="ui-monospace,Consolas,monospace" font-size="22">Dapper.FluentMap · .NET Standard 2.0</text>
</svg>`;
const socialTarget = resolve(import.meta.dirname, '../public/social/fluentmap-social.png');
await mkdir(dirname(socialTarget), { recursive: true });
await sharp(Buffer.from(socialSvg)).png().toFile(socialTarget);

console.log(`Generated ${routeManifest.length * 2 + Object.keys(sections).length * 2 + 2} localized documentation pages.`);
