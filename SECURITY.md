# Security Policy

## Supported versions

Security fixes are prioritized for the latest supported release line and the current `master` branch. Older versions may receive fixes when the impact is high and a safe backport is practical, but long-term support is not implied unless explicitly documented.

## Reporting a vulnerability

Please report suspected vulnerabilities privately instead of opening a public issue containing exploit details, secrets, or sensitive reproduction data.

Prefer GitHub private vulnerability reporting or Security Advisories when the repository exposes the **Report a vulnerability** option. Include the affected package/version or commit, reproduction steps, expected impact, relevant prerequisites, and any known mitigation.

If private GitHub reporting is unavailable, contact the maintainer through the contact methods exposed on the GitHub profile and request a private reporting channel before sharing sensitive details.

## Automated repository security controls

The repository uses complementary controls across source code, dependencies, quality, and release automation:

- CodeQL performs semantic C# security analysis on pull requests targeting `master`, pushes to `master`, and a weekly schedule;
- Dependency Review evaluates dependency changes introduced by pull requests and blocks additions with vulnerabilities of `high` severity or above;
- Dependabot maintains NuGet packages, the .NET SDK, and GitHub Actions references;
- SonarQube Cloud and the main CI workflow provide complementary static analysis, compatibility, build, test, package, and governance validation.

The CodeQL workflow uses the SDK selected by `global.json`, the preferred `Dapper.FluentMap.slnx` solution, manual build mode, SHA-pinned actions, and least-privilege workflow permissions. This keeps security analysis aligned with the repository's normal build contract instead of introducing an independent autobuild path.

Dependency Review is intentionally blocking for relevant findings. Do not add `continue-on-error` to security gates solely to keep a pull request green; findings should be triaged and resolved or explicitly justified.


## Release provenance and SBOM

The governed release flow publishes one SPDX 2.3 SBOM for the coherent FluentMap package family. The SBOM is generated from the exact final `.nupkg` artifacts, records the SHA-256 digest of every primary package, and captures the dependency relationships declared by their NuGet metadata.

The SBOM is stored as `release.sbom.spdx.json` under the release metadata, included in `SHA256SUMS`, attached to the GitHub Release, and covered by the normal build-provenance attestation. A separate SBOM attestation binds the same SPDX document to every primary `.nupkg` in the release.

Recovery preserves an existing SBOM when it matches the resolved package set and package digests. Older release artifacts that predate SBOM support receive a newly generated SBOM from the validated package bytes, after which governed checksums are regenerated and verified before reconciliation continues.

For byte-for-byte attestation verification, use the `.nupkg` downloaded from the GitHub Release. Package registries may add repository-signing metadata after publication, which can legitimately change the package bytes.

Verify build provenance for a normal release:

```bash
gh attestation verify Dapper.FluentMap.<version>.nupkg \
  --repo rodri-oliveira-dev/Dapper-FluentMap \
  --signer-workflow rodri-oliveira-dev/Dapper-FluentMap/.github/workflows/release.yml
```

If the release was reconciled by the recovery workflow, use `rodri-oliveira-dev/Dapper-FluentMap/.github/workflows/release-recovery-missing-nuget.yml` as the `--signer-workflow` value instead.

Verify the SPDX 2.3 SBOM attestation for a normal release:

```bash
gh attestation verify Dapper.FluentMap.<version>.nupkg \
  --repo rodri-oliveira-dev/Dapper-FluentMap \
  --signer-workflow rodri-oliveira-dev/Dapper-FluentMap/.github/workflows/release.yml \
  --predicate-type https://spdx.dev/Document/v2.3
```

For a release reconciled by recovery, use the recovery workflow path above for `--signer-workflow` in the SBOM verification command as well.

Artifact attestations and SBOMs establish provenance and component inventory; they complement rather than replace tests, package validation, Dependency Review, CodeQL, NuGet auditing, Trusted Publishing, and human review.
