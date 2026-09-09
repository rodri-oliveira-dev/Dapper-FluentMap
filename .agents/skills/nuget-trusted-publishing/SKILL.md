---
name: nuget-trusted-publishing
description: >
  Review, maintain, or set up NuGet trusted publishing (OIDC) for GitHub Actions. USE FOR: NuGet OIDC, keyless NuGet publish, NuGet/login, trusted-publishing policy changes, release authentication, and diagnosing NuGet trusted-publishing failures. DO NOT USE FOR: private feeds that do not support nuget.org trusted publishing.
license: MIT
---

# NuGet Trusted Publishing

Use NuGet trusted publishing to exchange GitHub OIDC identity for a short-lived NuGet API key instead of storing a long-lived publishing key.

> **Dapper-FluentMap baseline:** trusted publishing is already implemented in `.github/workflows/release.yml` through `NuGet/login`, a protected `release` environment, and job-scoped `id-token: write`. Treat that implementation as the baseline to preserve and review, not something to recreate from a generic sample. `AGENTS.md`, `eng/package-catalog.json`, `eng/publish-package-set.ps1`, release recovery, and package immutability rules are authoritative.

## When to Use

- Reviewing or changing the NuGet.org publishing job.
- Diagnosing `NuGet/login`, OIDC, policy, environment, or authorization failures.
- Adding a new package to the governed package family.
- Changing the release workflow filename or GitHub Environment in a way that may affect the nuget.org trusted-publishing policy.
- Verifying that release changes preserve short-lived credentials and least privilege.

## Safety Rules

- Never replace OIDC with a long-lived NuGet API key merely to make a release pass.
- Never publish, tag, create a GitHub Release, or invoke recovery unless the user explicitly requests it.
- Package IDs and published versions are immutable release identities; validate before publishing.
- A NuGet.org flat-container `404` proves only that a package/version is not currently present. It does not prove that the publisher is authorized to create that PackageId.
- Preserve partial-release recovery: validate existing registry artifacts and publish only missing artifacts rather than deleting or overwriting package versions.

## Repository Assessment

Before changing trusted publishing, inspect:

1. `.github/workflows/release.yml` and `release-recovery-missing-nuget.yml`.
2. `eng/package-catalog.json` for the exact five governed PackageIds.
3. `eng/publish-package-set.ps1` for publication/idempotency behavior.
4. `eng/validate-release-artifacts.ps1` and consumer-smoke validation.
5. The `release` environment and the exact nuget.org trusted-publishing policy values when external configuration is involved.

Current governed package family:

- `Dapper.FluentMap`
- `Dapper.FluentMap.Dommel`
- `FluentMap.DependencyInjection`
- `FluentMap.Analyzers`
- `FluentMap.Generators`

Project identity, assembly identity, namespace identity, and NuGet `PackageId` are independent. Do not rename projects/assemblies/namespaces as a side effect of a publishing change.

## Trusted Publishing Pattern

The NuGet publishing job should retain the equivalent of:

```yaml
permissions:
  contents: read
  id-token: write

- name: Exchange GitHub OIDC token for temporary NuGet API key
  id: nuget-login
  uses: NuGet/login@<approved-pinned-sha>
  with:
    user: "${{ vars.NUGET_USER }}"
```

The temporary key is then passed only to the governed publication step. Keep `NuGet/login` SHA-pinned according to repository policy.

## Policy Coupling

The nuget.org trusted-publishing policy is coupled to GitHub identity. Changes to these values may require external policy updates:

- repository owner/name;
- publishing workflow filename;
- GitHub Environment name;
- nuget.org account/package ownership.

Do not claim external policy changes are complete unless they are verified.

## Validation

For code-only release changes, validate the existing deterministic release pipeline rather than performing a real publish:

- workflow schema/actionlint where applicable;
- restore/build/test/pack;
- package metadata and artifact-manifest validation;
- consumer-smoke tests;
- package catalog consistency;
- least-privilege permissions and OIDC path.

A real publish is required only when the user explicitly asks for release execution.

## Troubleshooting

| Problem | Likely cause | Check |
|---|---|---|
| `NuGet/login` 403 | OIDC permission/policy mismatch | `id-token: write`, policy repo/workflow/environment |
| No matching policy | Workflow/environment identity mismatch | Exact nuget.org policy values |
| Push unauthorized | Package ownership/policy authorization | Package owner/publisher configuration |
| Temporary key expired | Login too early | Move token exchange close to publication |
| Package already exists | Re-run/partial release | Validate existing artifact and publish only missing packages |

## References

- [references/package-types.md](references/package-types.md)
- [references/publish-workflow.md](references/publish-workflow.md)
- [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
