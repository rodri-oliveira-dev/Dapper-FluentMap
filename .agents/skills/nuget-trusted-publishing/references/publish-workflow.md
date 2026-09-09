# Publish Workflow Reference

Reference pattern for NuGet trusted publishing. Adapt it to the repository's existing release workflow instead of replacing governance wholesale.

## Core Pattern

```yaml
jobs:
  publish:
    runs-on: ubuntu-latest
    environment: release
    permissions:
      id-token: write
      contents: read

    steps:
      - name: Checkout
        uses: actions/checkout@<approved-pinned-sha>

      - name: Setup .NET
        uses: actions/setup-dotnet@<approved-pinned-sha>

      - name: NuGet login (OIDC)
        id: login
        uses: NuGet/login@<approved-pinned-sha>
        with:
          user: ${{ vars.NUGET_USER }}

      - name: Push to NuGet
        run: >
          dotnet nuget push ./artifacts/*.nupkg
          --api-key ${{ steps.login.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
```

## Dapper-FluentMap Adaptation Rules

- Preserve the existing `.github/workflows/release.yml` flow rather than replacing it with this sample.
- Preserve version derivation, package catalog, artifact checksums, release metadata, Source Link/symbol package checks, consumer smoke tests, GitHub Packages publication, GitHub Release behavior, and recovery semantics.
- Preserve `NuGet/login` OIDC, the protected `release` environment, least-privilege permissions, and SHA-pinned actions.
- Use the exact workflow filename/environment in the nuget.org trusted-publishing policy.
- Normal PR CI does not need `id-token: write`.
- Do not re-tag or reuse a package version after publication errors.
- On partial releases, validate existing package contents and publish only missing packages; do not delete registry artifacts as rollback.
