---
name: directory-build-organization
description: "Guide for organizing MSBuild infrastructure with Directory.Build.props, Directory.Build.targets, Directory.Packages.props when present, and related repository build files. USE FOR: structuring multi-project repos, consolidating duplicated properties, understanding MSBuild evaluation order, or evaluating whether Central Package Management would be appropriate. Critical pitfall: TargetFramework-dependent properties in .props may evaluate too early."
license: MIT
---

# Organizing Build Infrastructure with Directory.Build Files

> **Dapper-FluentMap integration:** this repository already uses root `Directory.Build.props` and `Directory.Build.targets`, plus a separate `eng/consumer-smoke/Directory.Build.props`. It does **not** currently use `Directory.Packages.props`/Central Package Management. Preserve that actual structure unless CPM migration is explicitly requested and justified. Package identity also lives in `eng/package-catalog.json`; do not move PackageIds casually into generic build defaults.

## Evaluation order

```text
Directory.Build.props → SDK .props → .csproj → SDK .targets → Directory.Build.targets
```

| Use `.props` for | Use `.targets` for |
|---|---|
| property defaults | custom build targets |
| common items | late-bound property overrides |
| shared package/assembly metadata | logic depending on final SDK properties |
| shared dependency-version properties | post-build/pack validation |

### TargetFramework pitfall

Property conditions on `$(TargetFramework)` in `.props` files can silently fail for single-target projects because the project may set the TFM after `.props` import. Move such property logic to `.targets` or the project file. See [references/targetframework-props-pitfall.md](references/targetframework-props-pitfall.md).

## Dependency-version organization

The repository currently uses a mixed explicit model:

- shared ranges/properties such as Dapper/Dommel versions live in `Directory.Build.props`;
- many package versions remain explicit in individual `.csproj` files;
- consumer-smoke package versioning is isolated under `eng/consumer-smoke/Directory.Build.props`.

Do not describe this repository as using CPM unless a `Directory.Packages.props` with `ManagePackageVersionsCentrally` actually exists. A future CPM migration is a dedicated dependency-governance change, not incidental cleanup.

## Multi-level Directory.Build files

MSBuild auto-imports the first `Directory.Build.props`/`.targets` it finds walking upward. If a subtree intentionally uses another file, verify whether it needs to import the parent or intentionally isolates itself. See [references/multi-level-examples.md](references/multi-level-examples.md).

## Workflow

1. Audit relevant `.csproj`, root `Directory.Build.props`, `Directory.Build.targets`, and any subtree build files.
2. Identify duplicated vs project-specific settings.
3. Preserve ownership of package identity, versioning, compatibility, analyzers, warnings, pack metadata and consumer-smoke behavior.
4. Move only clearly shared defaults into `.props`.
5. Keep custom targets/late SDK-dependent logic in `.targets`.
6. Validate SLNX equivalence if project structure changes.
7. Validate restore/build/test and pack/consumer-smoke when packaging semantics could be affected.

Useful diagnosis:

```bash
dotnet msbuild -pp:output.xml path/to/Project.csproj
```

## Validation

- [ ] No `TargetFramework`-dependent property was moved to an early `.props` evaluation point
- [ ] Existing explicit dependency-version model remains consistent unless migration was in scope
- [ ] Project-specific settings were not generalized without evidence
- [ ] `eng/package-catalog.json` remains authoritative for package identities
- [ ] Restore/build/test still pass
- [ ] Packaging metadata/output remains unchanged unless intentionally modified

See [references/common-patterns.md](references/common-patterns.md) for common layouts and validation examples.
