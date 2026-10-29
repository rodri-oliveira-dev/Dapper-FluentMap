---
name: semantic-versioning
description: Determine and validate the next Dapper-FluentMap package version for API, feature, fix, dependency, compatibility, packaging, changelog, or release-preparation work. Never select a major version without explicit user authorization and formal breaking-change analysis.
license: MIT
---

# Purpose

Provide conservative, reproducible Semantic Versioning analysis for the five-package FluentMap release family. This skill calculates and validates versions; it never publishes packages, creates tags, creates releases, or triggers workflows.

# Sources and boundaries

Read `AGENTS.md` first. Use `ci-release-governance` for release/publishing mechanics; this skill owns the version decision and alignment checks.

Establish facts in this order:

1. Stable Git tags reachable in the available history, excluding prereleases.
2. GitHub Releases and NuGet.org versions when network access is available.
3. `FluentMapPackageVersionPrefix` in `Directory.Build.props` as the version in preparation, not proof of publication.
4. `eng/package-catalog.json`, package `.csproj` files, generated `.nuspec` metadata, changelog and documentation.
5. The relevant diff from the latest stable release tag to the intended release commit.

If tags or history are missing, state the limitation and accept an explicitly supplied verified published version. Never infer publication only from the configured MSBuild version or a commit message.

# Required analysis

1. Identify the last stable version actually published and the version currently in preparation.
2. Separate already-published changes from the accumulated unpublished diff. Ignore version-only, changelog-only and administrative commits when deciding whether another increment is needed.
3. Classify caller-visible impact across public APIs, binary contracts, behavior, dependencies and minimum versions, target frameworks, generators/analyzers, package metadata, exceptions, serialization/materialization semantics, and consumer migration.
4. Choose one outcome:
   - `No release required` for documentation, comments, tests-only, or administrative changes with no package/consumer effect.
   - `PATCH` for compatible fixes, security fixes, internal improvements, behavior-preserving refactors, performance fixes, or compatibility improvements.
   - `MINOR` for backward-compatible public APIs, optional features, mapping capabilities, or generator extensions.
   - `Breaking change review required` when an existing contract appears incompatible.
5. Run the repository validator when the version decision or package artifacts are in scope.
6. Explain the evidence, uncertainties and recommendation. Only update version files when the user authorized preparation or alignment.

# Idempotent calculation

Use the latest published stable version as the increment base. If a greater version is already configured, not published, and satisfies the accumulated change type, preserve it. Do not turn a prepared `3.6.1` into `3.6.2` merely because another compatible fix, changelog entry, or versioning commit joins the same release.

After `3.6.1` is published, a new compatible package change may recommend `3.6.2`; a backward-compatible feature may recommend `3.7.0`. A documentation-only change still requires no release. The decision must remain the same when rerun against unchanged tags, configuration and diff.

# Major-version guard

Never automatically recommend or apply a major increment. `BREAKING CHANGE` text, diff size, or new methods are not authorization.

When a breaking change is suspected, identify the exact contract and assess source compatibility, binary compatibility, observable behavior, target frameworks, NuGet dependency requirements, API removals/renames, exceptions, serialization/materialization semantics, affected consumers, migration and backward-compatible alternatives.

Without an explicit user request for a major version, do not hide the break in PATCH or MINOR. Report it, recommend a compatible design or deferral, and stop only incompatible versioning/publication work. With explicit authorization, require a written formal analysis before recommending the next major version.

# Validator

The validator is read-only and has separate recommendation, selection-validation and artifact-validation operations:

```powershell
./eng/validate-semantic-version.ps1 -Operation Recommend -ChangeType Patch
./eng/validate-semantic-version.ps1 -Operation Validate -SelectedVersion 3.6.1
./eng/validate-semantic-version.ps1 -Operation ValidatePackages -SelectedVersion 3.6.1 -PackageDirectory ./artifacts/packages -ReleaseMetadataPath ./artifacts/release-metadata/artifact-manifest.json
```

Supply `-PublishedVersion` only when verified history is incomplete. `-AllowExistingPublishedVersion` is reserved for governed immutable rerun/recovery validation. `-AllowMajor` is valid only after explicit user authorization and must be paired with `-BreakingAnalysisPath`; it is not a general override.

Run `./eng/test-semantic-versioning.ps1` after changing the validator or this policy. Pair artifact checks with `validate-package-metadata.ps1` and `validate-release-artifacts.ps1`; those scripts remain authoritative for the full package contract.

# Required report

For release-affecting implementation tasks, report:

- last published stable version and evidence;
- version in preparation;
- classified change type;
- detected breaking changes or confirmation that none were found;
- recommended version or `No release required`;
- decision rationale and any incomplete-history/network limitation;
- alignment status for version, changelog, documentation and artifacts when produced.
