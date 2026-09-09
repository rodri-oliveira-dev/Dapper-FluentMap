---
name: dotnet-issue-implementation
description: Use this skill to turn a well-defined issue into a small Dapper-FluentMap library change with requirements, implementation, tests, validation, and Definition of Done traceability. Do not use for open-ended bug investigation or PR review.
license: MIT
---

# Purpose

Implement a defined issue end to end without expanding scope, while preserving Dapper-FluentMap's public contracts, package boundaries, compatibility promises, and release safety.

# Process

1. Read root `AGENTS.md`, then the full issue or user-provided requirements.
2. Extract the problem, expected behavior, constraints, Definition of Done, non-goals, and ambiguities.
3. Search before opening large files. Locate only the relevant contracts, implementation, tests, package metadata, docs, and workflows.
4. Identify the affected package(s): core, Dommel, DependencyInjection, Analyzers, Generators, tests, benchmarks, docs, or CI/release.
5. Check whether the change affects public API, source/binary/behavior compatibility, Dapper integration, global state/cache behavior, dependencies, package output, or release flow.
6. Choose the smallest implementation that satisfies the issue without opportunistic modernization.
7. Add or update tests for behavior changes. For bugs, prefer a regression test that fails before the fix when practical.
8. Update `README.md`, `COMPATIBILITY.md`, `MIGRATION.md`, XML docs, or `CHANGELOG.md` only when the consumer-facing contract changes.
9. Run the nearest validation from `AGENTS.md`; broaden to solution or package validation only when the risk warrants it.
10. Review the complete diff and mark each DoD item as satisfied, blocked, or not applicable.

# Combine With

- `dotnet-library-change` when production code, project files, dependencies, public contracts, or package metadata change.
- `dotnet-refactoring-engineer` when the implementation includes behavior-preserving restructuring.
- `ci-release-governance` when workflows, packaging, versioning, release, or recovery are involved.
- `dotnet-pr-review` only when the task is to review an existing diff rather than implement it.

# Dapper-FluentMap Constraints

- The default branch is `master`; do not implement on `master`.
- Treat this as a public multi-package .NET library, not an application.
- Do not assume project name, assembly name, namespace, path, and NuGet `PackageId` are the same identity.
- Preserve `netstandard2.0` for public packages unless the issue explicitly changes compatibility.
- Keep core focused on mapping; do not add ORM, CRUD, SQL generation, migrations, or connection abstractions as incidental scope.
- Do not publish, tag, release, or push unless explicitly requested by the user.

# Expected Output

Report what changed, main files, validation commands and results, DoD status, compatibility impact, and remaining risks or blockers.

# Quality Bar

A good issue implementation satisfies the stated DoD with the smallest coherent diff, tests the relevant observable behavior, preserves unrelated contracts, and leaves validation evidence proportional to the risk.
