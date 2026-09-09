[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PackageCatalog.psm1') -Force

$expectedRepositoryUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap'
$expectedReleaseNotesUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap/releases'
$expectedAuthors = @('Henk Mollema', 'Rodrigo de Oliveira')

function Fail {
  param([string]$Message)
  throw "NuGet package metadata validation failed: $Message"
}

$catalogPackages = @(Get-FluentMapPackages)
$expectedPackages = @{}
foreach ($package in $catalogPackages) {
  $expectedPackages[[string]$package.packageId] = switch ([string]$package.project) {
    'Dapper.FluentMap' {
      @{
        Title = 'Dapper.FluentMap'
        RequiredTags = @('dapper', 'fluent-mapping', 'mapping', 'micro-orm', 'database', 'sql', 'poco', 'fluentmap')
      }
    }
    'Dapper.FluentMap.Dommel' {
      @{
        Title = 'Dapper.FluentMap - Dommel Integration'
        RequiredTags = @('dapper', 'dommel', 'fluent-mapping', 'mapping', 'crud', 'database', 'sql', 'fluentmap')
      }
    }
    'Dapper.FluentMap.DependencyInjection' {
      @{
        Title = 'Dapper.FluentMap - Dependency Injection'
        RequiredTags = @('dapper', 'dependency-injection', 'di', 'fluent-mapping', 'mapping', 'database', 'fluentmap')
      }
    }
    'Dapper.FluentMap.Analyzers' {
      @{
        Title = 'Dapper.FluentMap Analyzers'
        RequiredTags = @('dapper', 'roslyn', 'analyzers', 'mapping', 'diagnostics', 'fluent-mapping', 'fluentmap')
      }
    }
    'Dapper.FluentMap.Generators' {
      @{
        Title = 'Dapper.FluentMap Source Generators'
        RequiredTags = @('dapper', 'roslyn', 'source-generator', 'mapping', 'code-generation', 'fluent-mapping', 'fluentmap')
      }
    }
    default {
      Fail "Package catalog contains unexpected project identity '$($package.project)'."
    }
  }
}

function Get-ChildText {
  param(
    [System.Xml.XmlNode]$Node,
    [string]$Name
  )

  $child = $Node.ChildNodes | Where-Object { $_.LocalName -eq $Name } | Select-Object -First 1
  if ($null -eq $child) {
    return $null
  }

  return $child.InnerText
}

function Split-Authors {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) {
    return @()
  }

  return @(
    $Value -split '[;,]' |
      ForEach-Object { $_.Trim() } |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
  )
}

function Split-Tags {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) {
    return @()
  }

  return @(
    $Value -split '[;,\s]+' |
      ForEach-Object { $_.Trim() } |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
  )
}

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
  Fail "Package directory '$PackageDirectory' does not exist."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$nupkgs = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter '*.nupkg' | Sort-Object Name)

if ($nupkgs.Count -ne $expectedPackages.Count) {
  Fail "Expected $($expectedPackages.Count) .nupkg files, found $($nupkgs.Count)."
}

foreach ($file in $nupkgs) {
  $archive = [System.IO.Compression.ZipFile]::OpenRead($file.FullName)
  try {
    $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
    if ($nuspecEntries.Count -ne 1) {
      Fail "$($file.Name) must contain exactly one nuspec, found $($nuspecEntries.Count)."
    }

    $stream = $nuspecEntries[0].Open()
    try {
      $reader = [System.IO.StreamReader]::new($stream)
      try {
        [xml]$nuspec = $reader.ReadToEnd()
      }
      finally {
        $reader.Dispose()
      }
    }
    finally {
      $stream.Dispose()
    }

    $metadata = $nuspec.SelectSingleNode('//*[local-name()="metadata"]')
    if ($null -eq $metadata) {
      Fail "$($file.Name) is missing nuspec metadata."
    }

    $id = Get-ChildText -Node $metadata -Name 'id'
    if (-not $expectedPackages.ContainsKey($id)) {
      Fail "$($file.Name) has unexpected package ID '$id'."
    }

    $expected = $expectedPackages[$id]
    $title = Get-ChildText -Node $metadata -Name 'title'
    if ($title -ne $expected.Title) {
      Fail "$id title is '$title', expected '$($expected.Title)'."
    }

    $description = Get-ChildText -Node $metadata -Name 'description'
    if ([string]::IsNullOrWhiteSpace($description) -or $description.Length -lt 40) {
      Fail "$id must have a meaningful package description."
    }

    $authors = Split-Authors -Value (Get-ChildText -Node $metadata -Name 'authors')
    foreach ($expectedAuthor in $expectedAuthors) {
      if ($expectedAuthor -notin $authors) {
        Fail "$id authors '$($authors -join ', ')' must include '$expectedAuthor'."
      }
    }

    $tags = Split-Tags -Value (Get-ChildText -Node $metadata -Name 'tags')
    foreach ($requiredTag in $expected.RequiredTags) {
      if ($requiredTag -notin $tags) {
        Fail "$id tags are missing '$requiredTag'."
      }
    }

    $projectUrl = Get-ChildText -Node $metadata -Name 'projectUrl'
    if ($projectUrl -ne $expectedRepositoryUrl) {
      Fail "$id projectUrl is '$projectUrl', expected '$expectedRepositoryUrl'."
    }

    $releaseNotes = Get-ChildText -Node $metadata -Name 'releaseNotes'
    if ($releaseNotes -ne $expectedReleaseNotesUrl) {
      Fail "$id releaseNotes is '$releaseNotes', expected '$expectedReleaseNotesUrl'."
    }

    $requireLicenseAcceptance = Get-ChildText -Node $metadata -Name 'requireLicenseAcceptance'
    if ($requireLicenseAcceptance -eq 'true') {
      Fail "$id must not require license acceptance."
    }

    $readme = Get-ChildText -Node $metadata -Name 'readme'
    if ($readme -ne 'README.md' -or 'README.md' -notin @($archive.Entries.FullName)) {
      Fail "$id must reference and include README.md."
    }

    $icon = Get-ChildText -Node $metadata -Name 'icon'
    if ($icon -ne 'package-icon.png' -or 'package-icon.png' -notin @($archive.Entries.FullName)) {
      Fail "$id must reference and include package-icon.png."
    }

    $licenseNode = $metadata.ChildNodes | Where-Object { $_.LocalName -eq 'license' } | Select-Object -First 1
    if ($null -eq $licenseNode -or $licenseNode.GetAttribute('type') -ne 'expression' -or $licenseNode.InnerText -ne 'MIT') {
      Fail "$id must use the MIT license expression."
    }

    $repositoryNode = $metadata.ChildNodes | Where-Object { $_.LocalName -eq 'repository' } | Select-Object -First 1
    if ($null -eq $repositoryNode -or $repositoryNode.GetAttribute('url') -ne $expectedRepositoryUrl -or $repositoryNode.GetAttribute('type') -ne 'git') {
      Fail "$id must point repository metadata to '$expectedRepositoryUrl'."
    }
  }
  finally {
    $archive.Dispose()
  }
}

Write-Host "Validated NuGet presentation metadata for $($nupkgs.Count) packages."
