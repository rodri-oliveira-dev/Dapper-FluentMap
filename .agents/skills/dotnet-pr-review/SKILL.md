---
name: dotnet-pr-review
description: Use this skill to review a Dapper-FluentMap pull request or diff for correctness, regressions, compatibility, tests, performance, security, packaging, release, and maintainability. Do not use to implement PR changes unless explicitly asked.
license: MIT
---

# Purpose

Review changes for real risk and observable behavior, producing actionable findings instead of cosmetic comments.

# Review Process

1. Read root `AGENTS.md`, the PR description, linked issue, and the diff.
2. Understand the intended behavior before evaluating the implementation.
3. Review the diff first. Open full files only when the surrounding contract or call path is unclear.
4. Check correctness, public API/source/binary/behavior compatibility, SemVer impact, Dapper behavior, global state/cache, concurrency, dependencies, performance, security, tests, docs, CI, package output, and release safety.
5. Confirm every finding is introduced or materially worsened by the diff.
6. Describe a concrete failure scenario and a practical correction direction.
7. Verify tests protect the changed behavior and that validation evidence matches the risk.
8. Look for accidental scope expansion, unrelated modernization, PackageId/namespace/path confusion, or workflow assumptions that do not match this repository.

# Severity

- **P0 - Blocker:** data loss, critical security failure, unusable package, or unavoidable broad breakage.
- **P1 - High:** likely functional bug, unintended breaking change, concurrency/global-state failure, meaningful vulnerability, or incorrect release/package behavior.
- **P2 - Medium:** real defect in a limited scenario, missing tests for important behavior, or significant maintainability/performance risk.
- **P3 - Low:** objective improvement that should not block merge by itself.

Do not assign high severity to style preferences already handled by `.editorconfig`, analyzers, or formatting tools.

# Finding Format

Lead with findings. For each one include severity, file/line, problematic behavior, failure scenario, and correction direction. If no issues are found, say so clearly and mention any residual validation gaps.

# Dapper-FluentMap Review Focus

- Does the diff preserve explicit mapping > convention > Dapper default precedence?
- Does it keep normal Dapper queries distinct from FluentMap-controlled materialization paths?
- Are global static configuration and `SqlMapper.SetTypeMap` effects isolated in tests?
- Are nested/value-object claims proven end to end, not just by metadata?
- Do package changes account for all five NuGet packages and independent identities?
- Do release/workflow changes preserve `master`, version validation, artifact checks, provenance, and recovery behavior?

# Restrictions

- Do not rewrite the PR from preference.
- Do not require a new abstraction without a concrete failure or maintenance risk.
- Do not mark pre-existing unrelated behavior as a regression unless the diff materially increases risk.
- Do not approve a risky PR merely because it builds.

# Quality Bar

A good review prioritizes a small number of real, reproducible issues, explains impact clearly, and uses deterministic validation as evidence when possible.
