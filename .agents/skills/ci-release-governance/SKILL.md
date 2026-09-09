---
name: ci-release-governance
description: Use this skill to review or adjust Dapper-FluentMap GitHub Actions, packaging, NuGet publishing, versioning, release, rollback, recovery, provenance, and automation security. Do not use for production code changes without pipeline impact.
license: MIT
---

# Purpose

Guide CI/CD, packaging, versioning, release, and recovery changes according to the automation that actually exists in Dapper-FluentMap.

# Primary Rule

Inspect `.github/workflows/` and relevant `eng/**` scripts before assuming any automation. Do not document or depend on workflows, scripts, package sets, branches, or publishing behavior that are not present in the current tree.

# Current Automation Facts

- The development and release branch is `master`.
- `ci.yml` runs on pushes to `master`, pull requests, and manual dispatch.
- `release.yml` is manual (`workflow_dispatch`) and rejects releases not started from `master`.
- `release.yml` validates a SemVer input without `v`, rejects versions below major `3`, checks existing tags, checks NuGet availability for all published package IDs, builds/tests/packs, validates artifacts, runs consumer smoke tests, creates a tag, publishes to NuGet.org and GitHub Packages, creates/updates a GitHub Release, and runs rollback on partial failure.
- `release.yml` and `release-recovery-missing-nuget.yml` share the `release` concurrency group with `cancel-in-progress: false`; normal release and recovery wait for each other and never cancel an in-flight release mutation.
- `release-recovery-missing-nuget.yml` reconciles future partial release state by running current recovery tooling, checking out the validated commit into a separate source directory, preferring the original `release-package` artifact from an explicit `original_release_run_id`, falling back to a validated deterministic rebuild when that artifact is unavailable, validating package identities and dependency contracts from the validated source, recovering missing NuGet.org and GitHub Packages identities, accepting or restoring the release tag only when it points to the validated commit, creating/updating the GitHub Release, re-attesting recovered artifacts, and producing a final reconciliation summary.
- NuGet.org primary package recovery verifies Flat Container convergence and compares content before accepting existing packages. Symbol packages are submitted separately with NuGet.org V3 tooling; NuGet.org does not expose a public `.snupkg` content-comparison endpoint, so recovery documents that limitation instead of claiming independent symbol-byte verification.
- `sonar-quality-issues.yml` syncs high-impact Sonar findings to GitHub issues when `SONAR_CI_ENABLED` and `SONAR_TOKEN` are configured.
- Package validation scripts live under `eng/`: `validate-slnx-equivalence.ps1`, `validate-package-metadata.ps1`, `validate-release-artifacts.ps1`, `run-sonar-tests.ps1`, `rollback-release.ps1`, and consumer smoke scripts.

# When To Use

- Changes to `.github/workflows/**` or `eng/**` automation.
- Restore/build/test/coverage/pack/publish changes in CI.
- Versioning, SemVer, tag, artifact, Source Link/provenance, package metadata, or package count changes.
- NuGet.org, GitHub Packages, OIDC, `NuGet/login`, permissions, environments, concurrency, credentials, rollback, or recovery work.
- Documentation changes that describe CI, packaging, release, or recovery behavior.

# When Not To Use

- Functional source changes with no pipeline impact: use `dotnet-library-change`.
- Pure behavior-preserving code refactoring: use `dotnet-refactoring-engineer`.
- Ordinary tests with no CI or package behavior change.
- Running a publication/release operation without explicit user request.

# Process

1. Read root `AGENTS.md`, existing workflows, and relevant `eng` scripts.
2. Identify triggers, branch guards, permissions, credentials, version sources, artifacts, commands, package IDs, and rollback/recovery paths.
3. Preserve the real package set unless the task explicitly changes it:
   - `Dapper.FluentMap`
   - `Dapper.FluentMap.Dommel`
   - `FluentMap.DependencyInjection`
   - `FluentMap.Analyzers`
   - `FluentMap.Generators`
4. Preserve one effective release version source per flow. `Directory.Build.props` defines `FluentMapPackageVersionPrefix`; release passes the requested version through MSBuild `Version`.
5. Preserve validation order: restore, audit when applicable, build, test, pack, validate artifacts, smoke test when relevant, then tag/publish/release.
6. Preserve minimum required permissions; avoid broad write permissions.
7. Keep secrets out of files and logs. Prefer the existing OIDC/Trusted Publishing pattern for NuGet.org unless explicit requirements justify a different approach.
8. Ensure failure before package publication prevents later release announcement. Ensure partial release failure has an explicit rollback or recovery story.
9. Update documentation only for behavior that really exists after the change.
10. Review the diff for import-source assumptions, wrong branch names, incorrect package identities, fixed old versions, nonexistent scripts, or one-package assumptions.

# Validation

For workflow-only changes, use the cheapest reliable checks first:

```bash
rg --files .github/workflows eng
```

For package/release pipeline changes, validate with the actual repository commands:

```bash
dotnet restore ./Dapper.FluentMap.slnx
dotnet build ./Dapper.FluentMap.slnx --configuration Release --no-restore
dotnet test ./Dapper.FluentMap.slnx --configuration Release --no-build
dotnet pack ./Dapper.FluentMap.slnx --configuration Release --no-build --output ./artifacts/packages
./eng/validate-package-metadata.ps1 -PackageDirectory './artifacts/packages'
```

Use `./eng/validate-release-artifacts.ps1` and `./eng/consumer-smoke/run-consumer-smoke.ps1` when release artifacts or consumer package behavior are in scope.

# Restrictions

- Do not run `dotnet nuget push`, create tags, create GitHub Releases, trigger release workflows, or publish external artifacts unless explicitly requested.
- Do not replace temporary OIDC-based publishing with persistent API keys without explicit scope and security review.
- Do not remove restore/build/test/pack/package validation to reduce CI time without a documented replacement.
- Do not change package IDs, package count checks, version guards, artifact manifests, checksums, provenance, or rollback behavior incidentally.
- Do not assume administrative settings such as environments, repository variables, rulesets, trusted publishing policies, or secrets exist beyond what workflows reference.

# Quality Bar

A good automation change matches the current repository, uses minimum permissions, keeps versioning and package identity explicit, validates before publication, preserves provenance and recovery paths, fails diagnostically, and documents only real capabilities.
