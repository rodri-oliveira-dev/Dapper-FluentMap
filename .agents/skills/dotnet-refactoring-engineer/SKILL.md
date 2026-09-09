---
name: dotnet-refactoring-engineer
description: Use this skill to review or refactor C#/.NET code in Dapper-FluentMap for clarity, cohesion, testability, performance, or maintainability while preserving observable behavior and public API. Do not use for purely cosmetic rewrites.
license: MIT
---

# Purpose

Guide safe, incremental refactoring in Dapper-FluentMap without accidental functional changes, public API breaks, or package/release side effects.

# When To Use

- Reducing real duplication or coupling.
- Clarifying names, responsibilities, control flow, or tests.
- Improving performance or allocation behavior without changing semantics.
- Preparing a later change while keeping current behavior stable.
- Isolating global state, cache, reflection, expression parsing, or Dapper integration concerns.

# When Not To Use

- A functional production change is the main task: use `dotnet-library-change`.
- CI/CD, packaging, versioning, or release is the main task: use `ci-release-governance`.
- A PR/diff review is requested: use `dotnet-pr-review`.
- The change is only documentation or agent governance.

# Process

1. Read root `AGENTS.md`, target code, and related tests.
2. State the concrete design or maintenance problem being solved.
3. Identify observable behavior and public contracts that must remain stable.
4. Check existing test coverage before touching high-risk areas.
5. Add characterization tests first when behavior is not sufficiently protected.
6. Apply the smallest refactor with clear benefit.
7. Avoid public signature changes, project/PackageId changes, dependency changes, broad formatting, or unrelated renames.
8. Review the diff for unintended behavior change, compatibility impact, global-state changes, cache-key changes, or packaging side effects.
9. Run the nearest validation from `AGENTS.md`.

# Dapper-FluentMap Hot Spots

- `FluentMapper.Initialize`, static configuration, and Dapper `SqlMapper.SetTypeMap`.
- Type maps, member maps, constructor binding, naming policies, conventions, profiles, converters, and generated materializer fallback.
- Expression parsing for converted, inherited, nested, or ambiguous members.
- Cache keys and invalidation involving entity type, profile, column name, naming policy, and comparison rules.
- Dommel integration only when the refactor explicitly touches shared core contracts it consumes.

# Restrictions

- Do not make methods, properties, or types public just to test them.
- Do not simplify by changing documented or observable behavior.
- Do not introduce abstractions, patterns, dependencies, or frameworks without a concrete local payoff.
- Do not remove or weaken tests, warnings, analyzers, or diagnostics.
- Do not use broad formatting or solution-wide cleanup unless that is the explicit task.

# Quality Bar

A good refactor reduces complexity or improves clarity in the requested area, preserves source/binary/behavior compatibility, keeps the diff focused, and remains covered by meaningful tests.
