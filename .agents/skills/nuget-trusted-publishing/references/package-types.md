# NuGet Package Type Reference

Structural requirements for each NuGet package type. Use this to validate packaging setup before changing trusted publishing.

## Detection Logic

Inspect `.csproj` files and shared MSBuild properties:

```text
1. Has <PackageType>Template</PackageType>?             → Template package
2. Has <PackageType>McpServer</PackageType>?           → MCP server
3. Has <PackAsTool>true</PackAsTool>?                  → Dotnet tool
4. Has <IsPackable>true</IsPackable> or no OutputType? → NuGet library
5. Has <OutputType>Exe</OutputType> + IsPackable=true? → Application package
6. Executable without PackAsTool/IsPackable?           → Not packable by default
```

## NuGet Library

Common metadata includes `PackageId`, version, authors, description, tags, README, SPDX license, repository URL, Source Link, and symbol-package configuration. Inspect repository-wide metadata before assuming a project is incomplete.

### Including README in Package

`PackageReadmeFile` alone is not enough; the file also needs to be packed:

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>
<ItemGroup>
  <None Include="README.md" Pack="true" PackagePath="/" />
</ItemGroup>
```

## Dapper-FluentMap package family

This repository publishes libraries/analyzer packages. `eng/package-catalog.json` is authoritative for project-to-PackageId mapping. Do not infer PackageId from project name, assembly name, or namespace.

## Common Gotchas

- Class libraries are normally packable; console apps normally are not unless configured.
- Shared package metadata may live in `Directory.Build.props`.
- This repository currently uses explicit `PackageReference` versions in projects; do not assume Central Package Management.
- Multi-project repositories may publish multiple packages from one governed release workflow.
- Prefer explicit pack, metadata validation, release-artifact validation, and consumer-smoke verification before publication.
- Package IDs and published versions are effectively permanent; validate before push.
