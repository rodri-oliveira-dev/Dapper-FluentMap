# Common Directory.Build Patterns

## Conditional Settings by Project Type

Project naming can help with repo-owned conventions, but do not infer behavior solely from naming when explicit `IsTestProject`, `IsPackable`, or SDK metadata is available.

Use `Directory.Build.targets` for conditions on late SDK-defined properties such as `OutputType`:

```xml
<PropertyGroup Condition="'$(OutputType)' == 'Exe'">
  <SelfContained>false</SelfContained>
</PropertyGroup>
```

## Post-Pack Validation

Custom targets can validate expected package output after `Pack` when that belongs to repository policy:

```xml
<Target Name="ValidatePackageOutput" AfterTargets="Pack"
        Condition="'$(IsPackable)' == 'true'">
  <Error Text="Package was not created at the expected path"
         Condition="!Exists('$(PackageOutputPath)$(PackageId).$(PackageVersion).nupkg')" />
</Target>
```

In Dapper-FluentMap, prefer the existing `eng/validate-package-metadata.ps1`, `eng/validate-release-artifacts.ps1`, package catalog, and consumer-smoke validation over adding duplicate enforcement.

## Artifact Output Layout

`ArtifactsPath` can centralize `bin`, `obj`, and publish artifacts under a repository-level `artifacts/` directory. Adopt it only when compatible with existing scripts/workflows; changing artifact layout can break CI, release packaging, recovery, and consumer-smoke tooling.
