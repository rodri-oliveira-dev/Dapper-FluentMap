---
name: dotnet-pr-review
description: Use esta skill para revisar um Pull Request ou diff de biblioteca .NET com foco em corretude, regressões, compatibilidade, testes, performance, segurança e manutenção. Não use para implementar mudanças no PR sem solicitação explícita.
license: MIT
---

# Objetivo

Revisar alterações com foco em risco real e comportamento observável, produzindo apontamentos acionáveis e evitando comentários cosméticos sem impacto técnico.

# Processo

1. Leia `AGENTS.md`, a descrição do PR e a issue relacionada quando disponível.
2. Entenda o objetivo e o comportamento esperado antes de avaliar a implementação.
3. Revise primeiro o diff; expanda arquivos completos apenas quando o contexto do trecho for insuficiente.
4. Avalie riscos em corretude, API pública/compatibilidade, concorrência/estado, performance/I/O, segurança, dependências, testes, CI/packaging/release e manutenção.
5. Delegue inventário do diff, busca de referências e localização de testes quando houver workers/subagentes; mantenha severidade e conclusão no agente principal.
6. Para cada problema, confirme que ele é introduzido ou materialmente agravado pelo diff e descreva um cenário concreto de falha.
7. Verifique se os testes protegem o novo comportamento e se quality gates foram preservados.
8. Execute validações direcionadas e a baseline de `AGENTS.md` quando o ambiente permitir.
9. Revise o conjunto final para detectar escopo acidental, dependência não explicada ou mudança pública sem documentação/changelog.

# Severidade

- **P0 — Blocker:** perda de dados, falha crítica de segurança, pacote inutilizável ou quebra ampla inevitável.
- **P1 — High:** bug funcional provável, breaking change não intencional, falha de concorrência, vulnerabilidade relevante ou release incorreto.
- **P2 — Medium:** defeito real em cenário limitado, teste insuficiente para comportamento relevante ou risco significativo de manutenção/performance.
- **P3 — Low:** melhoria objetiva que não bloqueia o merge por si só.

Não use severidade alta para preferências de estilo já cobertas por `.editorconfig`, analyzers ou `dotnet format`.

# Formato de finding

Cada finding deve indicar severidade, arquivo/trecho, comportamento problemático, cenário de falha e direção objetiva de correção.

# Restrições específicas

- Não reescreva o PR por preferência pessoal.
- Não exija abstrações/patterns sem problema concreto.
- Não marque como bug algo não afetado pelo diff, salvo aumento material de risco.
- Não aprove um PR apenas porque compila.

As demais restrições e validações globais são definidas em `AGENTS.md`.

# Critério de qualidade

Uma boa revisão prioriza poucos problemas reais e acionáveis, explica impacto e cenário de falha e usa validação determinística como evidência sempre que possível.
