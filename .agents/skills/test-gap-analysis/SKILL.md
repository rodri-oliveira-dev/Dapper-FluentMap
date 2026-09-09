---
name: test-gap-analysis
description: >-
  Pseudo-mutation analysis for behavioral blind spots: determine whether existing tests would catch meaningful caller-visible production changes, identify survivors or untested outcomes, and optionally close verified gaps with focused tests. Use for behavioral gaps and missing edge cases. Use coverage-analysis when available for project-wide coverage arithmetic and test-anti-patterns when available for broad test-quality smells.
license: MIT
---

# Test Gap Analysis

Answer one question: **which caller-visible production behaviors could change without an existing test failing?** Mutation reasoning is a probe, not the goal. Inventory public outcomes first, then verify only credible gaps.

> **Dapper-FluentMap integration:** preserve public compatibility and use the repository's existing test layers. Prefer `test/Dapper.FluentMap.Tests` for focused core behavior, provider/integration suites for materialization behavior, Roslyn tests for analyzers/generators, and `eng/consumer-smoke` for package-consumer behavior. Do not mutate global Dapper/FluentMap state without restoring it.

## Decision flow

### 1. Set scope

Discover production and test files from `Dapper.FluentMap.slnx` and the existing test layout. Keep a focused request focused.

| Request | Action |
|---|---|
| One component or named risk | Inventory every high-risk public outcome in scope; do not edit production code unless verification was requested |
| General small-component review | Inventory distinct outcomes and report caller-visible gaps from source/assertion mapping |
| Explicit survivor verification | Execute one representative observable candidate for each distinct high-risk outcome under verification |
| Explicit exhaustive audit | Read [references/mutation-catalog.md](references/mutation-catalog.md) and classify all meaningful candidates |
| Add tests to an existing suite | Analyze first; add tests only for verified survivors or demonstrated no-coverage outcomes |

### 2. Establish one baseline

Run the narrowest existing test command once and confirm tests actually executed. For core behavior, normally start with:

```bash
dotnet test ./test/Dapper.FluentMap.Tests/Dapper.FluentMap.Tests.csproj --configuration Release
```

If the suite cannot run, continue statically and label executable mutation candidates **unverified**.

### 3. Inventory public outcomes

For each public entry point, map:

- input partitions and guard boundaries;
- returns/results/exceptions/state transitions/side effects;
- mapping precedence and fallback behavior;
- case sensitivity, duplicate registration, conventions and caches when relevant;
- Dapper materialization or Dommel behavior only through real caller-visible outcomes;
- analyzer/generator diagnostics and generated output when relevant.

Use:

```text
public input/sequence -> expected outcome -> existing assertion -> gap
```

One asserted field does not cover another. Unit metadata tests do not prove end-to-end Dapper materialization.

### 4. Admit only observable candidates

Before reporting a candidate, replay it against existing asserted inputs. If an assertion observes the changed result, it is **Likely killed** and not a gap.

For survivors, state:

```text
witness -> original observation -> mutant observation
```

Exclude generated code, formatting/logging-only changes unless contractual, non-compiling edits, equivalent mutations, impossible domain values, trivial forwarding members, and private representation changes callers cannot distinguish.

### 5. Rank and classify

Prioritize:

1. public compatibility, mapping correctness, package-consumer behavior, data correctness and error semantics;
2. wholly unasserted public outcomes;
3. exact boundaries/precedence/case behavior reached only by weak assertions;
4. alternate variants of already-protected behavior.

Use these labels:

| Result | Meaning |
|---|---|
| **Likely killed** | An existing assertion observes the changed outcome |
| **Candidate survivor (unverified)** | Observable change appears unasserted; not executed |
| **Survived** | Exact observable mutation executed and tests stayed green |
| **No coverage** | No test reaches the public outcome |
| **Equivalent** | No public observation changes; omit from findings |

Verdict: **Strong**, **Mixed**, or **Weak**.

### 6. Verify safely when requested

1. Apply one temporary candidate and confirm the diff changes exactly one intended expression.
2. Run the narrowest covering tests.
3. Green = **Survived**; red = **Killed**, for that edit only.
4. Revert immediately and confirm the source baseline is clean.
5. Never commit temporary mutations.
6. Restore global FluentMap/Dapper state in tests that alter it.

### 7. Close gaps only when requested

- Add focused behavior tests only for demonstrated gaps.
- Prefer tests that protect a caller-visible contract over tests that merely inflate coverage.
- Use integration/provider tests when the risk concerns actual Dapper materialization.
- Use package/consumer-smoke validation when the risk concerns package identity, analyzers/generators, trimming, or installation.

## Output contract

For focused analysis return one-line verdict, a short strengths sentence, then one compact row per actionable gap:

| Risk | Public outcome | Change | Result/evidence | Smallest test |
|---|---|---|---|---|

Every gap needs a distinguishing witness and a concrete smallest test.

## Reliability rules

- A passing test that does not assert the changed outcome does not kill a mutation.
- Coverage is per behavior partition, not merely per executed line.
- Never recommend a redundant test for behavior already protected.
- `AGENTS.md`, repository compatibility rules, and deterministic validation prevail over this skill.
