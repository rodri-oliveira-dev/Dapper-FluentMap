# Multi-level Directory.Build Examples

Use multi-level `Directory.Build.props` only when subtrees genuinely need distinct policy. MSBuild automatically stops at the first matching file it finds while walking upward, so an inner file can intentionally isolate a subtree or explicitly import the parent when inheritance is required.

## Root

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

## Child props importing parent

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))"
          Condition="Exists('$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))')" />

  <PropertyGroup>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
</Project>
```

## Dapper-FluentMap note

The repository currently has a separate `eng/consumer-smoke/Directory.Build.props`. Before adding an import to the root file, determine whether that subtree is intentionally isolated. Do not change its package-version behavior incidentally.

## Central Package Management example

If CPM is intentionally adopted in a future dedicated change, package versions can move to `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Example.Package" Version="1.2.3" />
  </ItemGroup>
</Project>
```

```xml
<ItemGroup>
  <PackageReference Include="Example.Package" />
</ItemGroup>
```

Do not treat this example as the repository's current state; Dapper-FluentMap currently uses explicit project package versions.
