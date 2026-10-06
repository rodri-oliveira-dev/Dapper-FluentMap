---
name: dotnet-issue-implementation
description: Use esta skill para transformar uma issue bem definida em uma alteração pequena de biblioteca .NET, com requisitos explícitos, implementação, testes, validação e conferência do Definition of Done. Não use para investigação de bug sem causa conhecida ou revisão de PR existente.
license: MIT
---

# Objetivo

Executar uma issue de ponta a ponta sem ampliar escopo, preservando contrato público, baseline técnica e rastreabilidade entre requisito, código, testes e validação.

# Processo

1. Leia `AGENTS.md` e a issue completa.
2. Extraia problema, comportamento esperado, restrições, DoD e ambiguidades.
3. Pesquise antes de abrir arquivos grandes e localize implementação, contratos, testes, configuração e documentação relacionados.
4. Identifique impacto em API pública, dependências, packaging, segurança, compatibilidade e changelog.
5. Defina a menor estratégia capaz de satisfazer o DoD.
6. Delegue somente tarefas mecânicas quando workers/subagentes estiverem disponíveis; mantenha decisões de design, comportamento e risco no agente principal.
7. Implemente com diff focado e adicione ou atualize testes que comprovem o comportamento pedido.
8. Atualize `CHANGELOG.md` quando houver impacto relevante para consumidores.
9. Execute a baseline definida em `AGENTS.md` e validações adicionais exigidas pelo tipo de mudança.
10. Revise o diff completo e confira o DoD item a item como comprovado, bloqueado ou não aplicável.

# Combinação com outras skills

Combine quando necessário com:

- `dotnet-library-change` para mudança funcional/técnica;
- `dotnet-refactoring-engineer` para refatoração preservando comportamento;
- `coverage-analysis` ou `test-anti-patterns` para riscos de teste;
- `ci-release-governance` para CI, packaging ou release;
- `dotnet-security-review` para superfície de segurança relevante.

# Restrições específicas

- Não invente requisitos ausentes na issue.
- Não faça refatoração oportunista fora da área necessária.
- Não faça breaking change incidental.

As demais restrições e validações globais são definidas em `AGENTS.md`.

# Saída esperada

Informe de forma objetiva o que mudou, arquivos principais, testes/validações executados, estado do DoD e riscos ou bloqueios restantes.

# Critério de qualidade

Uma boa implementação satisfaz o DoD com o menor diff coerente, possui evidência proporcional ao risco e passa pelos gates determinísticos aplicáveis.
