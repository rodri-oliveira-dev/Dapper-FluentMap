# TargetFramework conditions in .props files

**Smell:** property conditions such as `<PropertyGroup Condition="'$(TargetFramework)' == '...'">` inside `Directory.Build.props` or another `.props` imported before the project body.

## Why it matters

`$(TargetFramework)` is not reliably available during early `.props` evaluation for single-target projects. A condition can silently evaluate against an empty value.

```xml
<!-- BAD in Directory.Build.props -->
<PropertyGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <DefineConstants>$(DefineConstants);MY_FEATURE</DefineConstants>
</PropertyGroup>
```

Prefer late evaluation:

```xml
<!-- GOOD in Directory.Build.targets -->
<PropertyGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <DefineConstants>$(DefineConstants);MY_FEATURE</DefineConstants>
</PropertyGroup>
```

or keep the condition in the project file when it is project-specific.

## Important distinction

The warning is about **property** conditions evaluated too early. Item and Target conditions are evaluated differently and can validly depend on `TargetFramework` in scenarios where an early property assignment cannot.

Do not mechanically move safe item conditions merely because they mention `TargetFramework`. In Dapper-FluentMap, public packages preserve `netstandard2.0`, so any TFM-sensitive reorganization is compatibility-sensitive and must be validated deliberately.
