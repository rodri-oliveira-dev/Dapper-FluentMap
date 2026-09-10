[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$PackageDirectory,

  [Parameter(Mandatory = $true)]
  [string]$Version,

  [string]$ManifestPath,

  [string]$Repository,

  [string]$RepositoryUrl,

  [string]$Commit,

  [string]$Branch,

  [string]$CatalogPath,

  [string]$SourceRoot,

  [switch]$VerifyExistingManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PackageCatalog.psm1') -Force

$expectedRepositoryUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap'

function Fail {
  param([string]$Message)
  throw "Release artifact validation failed: $Message"
}

function Resolve-MSBuildValue {
  param(
    [string]$Value,
    [hashtable]$Properties
  )

  $resolved = $Value
  for ($attempt = 0; $attempt -lt 10; $attempt++) {
    $changed = $false
    $resolved = [regex]::Replace(
      $resolved,
      '\$\(([^)]+)\)',
      {
        param($match)
        $propertyName = $match.Groups[1].Value
        if ($Properties.ContainsKey($propertyName)) {
          $changed = $true
          return [string]$Properties[$propertyName]
        }

        return $match.Value
      })

    if (-not $changed) {
      break
    }
  }

  return $resolved
}

function Test-PropertyConditionAllowsSet {
  param(
    [string]$Condition,
    [string]$PropertyName,
    [hashtable]$Properties
  )

  if ([string]::IsNullOrWhiteSpace($Condition)) {
    return $true
  }

  if ($Condition -match "^\s*'\$\(([A-Za-z0-9_.-]+)\)'\s*==\s*''\s*$") {
    return [string]::IsNullOrWhiteSpace([string]$Properties[$Matches[1]])
  }

  if ($Condition -match "^\s*'\$\(([A-Za-z0-9_.-]+)\)'\s*!=\s*''\s*$") {
    return -not [string]::IsNullOrWhiteSpace([string]$Properties[$Matches[1]])
  }

  if ($Condition -match "^\s*'\$\(([A-Za-z0-9_.-]+)\)'\s*==\s*'([^']*)'\s*$") {
    return [string]$Properties[$Matches[1]] -eq $Matches[2]
  }

  if ($Condition -match "^\s*'\$\(([A-Za-z0-9_.-]+)\)'\s*!=\s*'([^']*)'\s*$") {
    return [string]$Properties[$Matches[1]] -ne $Matches[2]
  }

  if ($Condition -match [regex]::Escape("'$(" + $PropertyName + ")' == ''")) {
    return [string]::IsNullOrWhiteSpace([string]$Properties[$PropertyName])
  }

  return $true
}

function Add-MSBuildProperties {
  param(
    [string]$ProjectPath,
    [hashtable]$Properties
  )

  [xml]$projectXml = Get-Content -LiteralPath $ProjectPath
  foreach ($property in @($projectXml.SelectNodes('//*[local-name()="PropertyGroup"]/*'))) {
    if ($property.ChildNodes.Count -gt 1) {
      continue
    }

    $propertyName = $property.LocalName
    if (Test-PropertyConditionAllowsSet -Condition $property.GetAttribute('Condition') -PropertyName $propertyName -Properties $Properties) {
      $Properties[$propertyName] = Resolve-MSBuildValue -Value $property.InnerText -Properties $Properties
    }
  }
}

function Get-MSBuildProperties {
  param(
    [string]$SourceRootPath,
    [string]$ProjectPath
  )

  $properties = @{}
  $directoryBuildProps = Join-Path $SourceRootPath 'Directory.Build.props'
  if (Test-Path -LiteralPath $directoryBuildProps -PathType Leaf) {
    Add-MSBuildProperties -ProjectPath $directoryBuildProps -Properties $properties
  }

  Add-MSBuildProperties -ProjectPath $ProjectPath -Properties $properties
  return $properties
}

function Get-XmlChildText {
  param(
    [System.Xml.XmlNode]$Node,
    [string]$Name
  )

  $child = $Node.ChildNodes |
    Where-Object { $_.LocalName -eq $Name } |
    Select-Object -First 1

  if ($null -eq $child) {
    return $null
  }

  return $child.InnerText
}

function Test-SuppressDependenciesWhenPacking {
  param(
    [xml]$ProjectXml,
    [hashtable]$Properties
  )

  $value = Get-XmlChildText -Node $ProjectXml.Project -Name 'SuppressDependenciesWhenPacking'
  if ([string]::IsNullOrWhiteSpace($value)) {
    $value = [string]$Properties['SuppressDependenciesWhenPacking']
  }

  return $value -eq 'true'
}

function Test-PrivatePackageReference {
  param([System.Xml.XmlNode]$PackageReference)

  $privateAssets = $PackageReference.GetAttribute('PrivateAssets')
  if ([string]::IsNullOrWhiteSpace($privateAssets)) {
    $privateAssets = Get-XmlChildText -Node $PackageReference -Name 'PrivateAssets'
  }

  return @($privateAssets -split '[;, ]+' | Where-Object { $_ -eq 'all' }).Count -gt 0
}

function Normalize-PathKey {
  param([string]$Path)
  return [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).ToUpperInvariant()
}

function Normalize-DependencyVersion {
  param([string]$DependencyVersion)

  if ($null -eq $DependencyVersion) {
    return $null
  }

  return ($DependencyVersion -replace '\s+', '')
}

function Get-ExpectedDependencies {
  param(
    [string]$SourceRootPath,
    [array]$Packages
  )

  $packagesByProjectPath = @{}
  foreach ($package in $Packages) {
    $projectPath = Join-Path $SourceRootPath ([string]$package.projectPath)
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
      Fail "Package catalog references missing project '$($package.projectPath)' under '$SourceRootPath'."
    }

    $packagesByProjectPath[(Normalize-PathKey -Path $projectPath)] = $package
  }

  $expected = @{}
  foreach ($package in $Packages) {
    $packageId = [string]$package.packageId
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $SourceRootPath ([string]$package.projectPath)))
    [xml]$projectXml = Get-Content -LiteralPath $projectPath
    $properties = Get-MSBuildProperties -SourceRootPath $SourceRootPath -ProjectPath $projectPath
    $dependencies = @{}

    foreach ($projectReference in @($projectXml.SelectNodes('//*[local-name()="ProjectReference"]'))) {
      $include = $projectReference.GetAttribute('Include')
      if ([string]::IsNullOrWhiteSpace($include)) {
        continue
      }

      $referencedProjectPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $projectPath) $include))
      $referencedKey = Normalize-PathKey -Path $referencedProjectPath
      if ($packagesByProjectPath.ContainsKey($referencedKey)) {
        $referencedPackage = $packagesByProjectPath[$referencedKey]
        $dependencies[[string]$referencedPackage.packageId] = $Version
      }
    }

    if (-not (Test-SuppressDependenciesWhenPacking -ProjectXml $projectXml -Properties $properties)) {
      foreach ($packageReference in @($projectXml.SelectNodes('//*[local-name()="PackageReference"]'))) {
        if (Test-PrivatePackageReference -PackageReference $packageReference) {
          continue
        }

        $dependencyId = $packageReference.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($dependencyId)) {
          $dependencyId = $packageReference.GetAttribute('Update')
        }

        if ([string]::IsNullOrWhiteSpace($dependencyId)) {
          continue
        }

        $dependencyVersion = $packageReference.GetAttribute('Version')
        if ([string]::IsNullOrWhiteSpace($dependencyVersion)) {
          $dependencyVersion = Get-XmlChildText -Node $packageReference -Name 'Version'
        }

        if ([string]::IsNullOrWhiteSpace($dependencyVersion)) {
          Fail "PackageReference '$dependencyId' in '$projectPath' does not specify a Version."
        }

        $dependencies[$dependencyId] = Resolve-MSBuildValue -Value $dependencyVersion -Properties $properties
      }
    }

    $expected[$packageId] = $dependencies
  }

  return $expected
}

$catalogPackages = @(Get-FluentMapPackages -CatalogPath $CatalogPath)
$symbolCatalogPackages = @(Get-FluentMapPackages -CatalogPath $CatalogPath -WithSymbols)
$expectedNupkgIds = @($catalogPackages | ForEach-Object { [string]$_.packageId })
$expectedSnupkgIds = @($symbolCatalogPackages | ForEach-Object { [string]$_.packageId })

function Get-GitOutput {
  param([string[]]$Arguments)

  $output = & git @Arguments 2>$null
  if ($LASTEXITCODE -ne 0) {
    return $null
  }

  return ($output | Select-Object -First 1)
}

function Get-RequiredXmlNode {
  param(
    [xml]$Document,
    [string]$XPath,
    [string]$Description
  )

  $node = $Document.SelectSingleNode($XPath)
  if ($null -eq $node) {
    Fail "Missing $Description."
  }

  return $node
}

function Get-ChildText {
  param(
    [System.Xml.XmlNode]$Node,
    [string]$Name
  )

  $child = $Node.ChildNodes |
    Where-Object { $_.LocalName -eq $Name } |
    Select-Object -First 1

  if ($null -eq $child) {
    return $null
  }

  return $child.InnerText
}

function Read-Nuspec {
  param([System.IO.Compression.ZipArchive]$Archive)

  $nuspecEntries = @($Archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
  if ($nuspecEntries.Count -ne 1) {
    Fail "Expected exactly one nuspec in package archive, found $($nuspecEntries.Count)."
  }

  $stream = $nuspecEntries[0].Open()
  try {
    $reader = [System.IO.StreamReader]::new($stream)
    try {
      return [xml]$reader.ReadToEnd()
    }
    finally {
      $reader.Dispose()
    }
  }
  finally {
    $stream.Dispose()
  }
}

function Get-ZipPackageInfo {
  param([System.IO.FileInfo]$File)

  $archive = [System.IO.Compression.ZipFile]::OpenRead($File.FullName)
  try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    $nuspec = Read-Nuspec -Archive $archive
    $metadata = Get-RequiredXmlNode `
      -Document $nuspec `
      -XPath '//*[local-name()="metadata"]' `
      -Description "nuspec metadata in $($File.Name)"

    $dependencies = @{}
    foreach ($dependency in @($nuspec.SelectNodes('//*[local-name()="dependency"]'))) {
      $dependencies[$dependency.GetAttribute('id')] = $dependency.GetAttribute('version')
    }

    $repositoryNode = $metadata.ChildNodes |
      Where-Object { $_.LocalName -eq 'repository' } |
      Select-Object -First 1
    $licenseNode = $metadata.ChildNodes |
      Where-Object { $_.LocalName -eq 'license' } |
      Select-Object -First 1

    return [pscustomobject]@{
      File = $File
      Entries = $entries
      Id = Get-ChildText -Node $metadata -Name 'id'
      Version = Get-ChildText -Node $metadata -Name 'version'
      LicenseType = if ($null -eq $licenseNode) { $null } else { $licenseNode.GetAttribute('type') }
      License = Get-ChildText -Node $metadata -Name 'license'
      Readme = Get-ChildText -Node $metadata -Name 'readme'
      Icon = Get-ChildText -Node $metadata -Name 'icon'
      ProjectUrl = Get-ChildText -Node $metadata -Name 'projectUrl'
      RequireLicenseAcceptance = Get-ChildText -Node $metadata -Name 'requireLicenseAcceptance'
      RepositoryUrl = if ($null -eq $repositoryNode) { $null } else { $repositoryNode.GetAttribute('url') }
      RepositoryType = if ($null -eq $repositoryNode) { $null } else { $repositoryNode.GetAttribute('type') }
      RepositoryCommit = if ($null -eq $repositoryNode) { $null } else { $repositoryNode.GetAttribute('commit') }
      RepositoryBranch = if ($null -eq $repositoryNode) { $null } else { $repositoryNode.GetAttribute('branch') }
      Dependencies = $dependencies
    }
  }
  finally {
    $archive.Dispose()
  }
}

function Assert-SetEquals {
  param(
    [string[]]$Expected,
    [string[]]$Actual,
    [string]$Description
  )

  $missing = @($Expected | Where-Object { $_ -notin $Actual })
  $unexpected = @($Actual | Where-Object { $_ -notin $Expected })
  if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
    Fail "$Description mismatch. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
  }
}

function Assert-Dependencies {
  param(
    [string]$PackageId,
    [hashtable]$Actual,
    [hashtable]$Expected
  )

  Assert-SetEquals `
    -Expected ([string[]]$Expected.Keys) `
    -Actual ([string[]]$Actual.Keys) `
    -Description "Dependency IDs for $PackageId"

  foreach ($dependencyId in $Expected.Keys) {
    if ((Normalize-DependencyVersion -DependencyVersion $Actual[$dependencyId]) -ne (Normalize-DependencyVersion -DependencyVersion $Expected[$dependencyId])) {
      Fail "Dependency $dependencyId in $PackageId has version '$($Actual[$dependencyId])', expected '$($Expected[$dependencyId])'."
    }
  }
}

function Assert-CommonPackageMetadata {
  param(
    [pscustomobject]$PackageInfo,
    [string]$ExpectedId,
    [bool]$RequireReadmeAndLicense = $true
  )

  Assert-PackageIdentity -PackageInfo $PackageInfo -ExpectedId $ExpectedId

  if ($RequireReadmeAndLicense) {
    Assert-PackageAssets -PackageInfo $PackageInfo -ExpectedId $ExpectedId
  }

  Assert-RepositoryMetadata -PackageInfo $PackageInfo -ExpectedId $ExpectedId
}

function Assert-PackageIdentity {
  param(
    [pscustomobject]$PackageInfo,
    [string]$ExpectedId
  )

  if ($PackageInfo.Id -ne $ExpectedId) {
    Fail "$($PackageInfo.File.Name) has package ID '$($PackageInfo.Id)', expected '$ExpectedId'."
  }

  if ($PackageInfo.Version -ne $Version) {
    Fail "$ExpectedId has version '$($PackageInfo.Version)', expected '$Version'."
  }
}

function Assert-PackageAssets {
  param(
    [pscustomobject]$PackageInfo,
    [string]$ExpectedId
  )

  if ($PackageInfo.LicenseType -ne 'expression' -or $PackageInfo.License -ne 'MIT') {
    Fail "$ExpectedId must use MIT license expression."
  }

  if ($PackageInfo.Readme -ne 'README.md' -or 'README.md' -notin $PackageInfo.Entries) {
    Fail "$ExpectedId must include README.md and reference it from the nuspec."
  }

  if ($PackageInfo.Icon -ne 'package-icon.png' -or 'package-icon.png' -notin $PackageInfo.Entries) {
    Fail "$ExpectedId must include package-icon.png and reference it from the nuspec."
  }
}

function Assert-RepositoryMetadata {
  param(
    [pscustomobject]$PackageInfo,
    [string]$ExpectedId
  )

  if ($PackageInfo.RequireLicenseAcceptance -eq 'true') {
    Fail "$ExpectedId must not require license acceptance."
  }

  if ($PackageInfo.ProjectUrl -ne $expectedRepositoryUrl) {
    Fail "$ExpectedId projectUrl is '$($PackageInfo.ProjectUrl)', expected '$expectedRepositoryUrl'."
  }

  if ($PackageInfo.RepositoryUrl -ne $RepositoryUrl) {
    Fail "$ExpectedId repository URL is '$($PackageInfo.RepositoryUrl)', expected '$RepositoryUrl'."
  }

  if ($PackageInfo.RepositoryType -ne 'git') {
    Fail "$ExpectedId repository type is '$($PackageInfo.RepositoryType)', expected 'git'."
  }

  if ($PackageInfo.RepositoryCommit -ne $Commit) {
    Fail "$ExpectedId repository commit is '$($PackageInfo.RepositoryCommit)', expected '$Commit'."
  }

  if (-not [string]::IsNullOrWhiteSpace($Branch) -and $PackageInfo.RepositoryBranch -ne $Branch) {
    Fail "$ExpectedId repository branch is '$($PackageInfo.RepositoryBranch)', expected '$Branch'."
  }
}

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
  Fail "Version '$Version' is not a supported Semantic Version. Build metadata (+...) is intentionally not accepted."
}

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
  Fail "Package directory '$PackageDirectory' does not exist."
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
  $Repository = $env:GITHUB_REPOSITORY
}

if ([string]::IsNullOrWhiteSpace($RepositoryUrl)) {
  $RepositoryUrl = $env:GITHUB_SERVER_URL
  if (-not [string]::IsNullOrWhiteSpace($RepositoryUrl) -and -not [string]::IsNullOrWhiteSpace($Repository)) {
    $RepositoryUrl = "$RepositoryUrl/$Repository"
  }
}

if ([string]::IsNullOrWhiteSpace($RepositoryUrl)) {
  $RepositoryUrl = Get-GitOutput -Arguments @('config', '--get', 'remote.origin.url')
  if ($RepositoryUrl -match '^git@github\.com:(.+)$') {
    $RepositoryUrl = "https://github.com/$($Matches[1])"
  }

  if ($RepositoryUrl -like '*.git') {
    $RepositoryUrl = $RepositoryUrl.Substring(0, $RepositoryUrl.Length - 4)
  }
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
  $Repository = $RepositoryUrl
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
  $Commit = $env:GITHUB_SHA
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
  $Commit = Get-GitOutput -Arguments @('rev-parse', 'HEAD')
}

if ([string]::IsNullOrWhiteSpace($Branch)) {
  $Branch = $env:GITHUB_REF
}

if ([string]::IsNullOrWhiteSpace($Branch)) {
  $gitBranch = Get-GitOutput -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
  if (-not [string]::IsNullOrWhiteSpace($gitBranch) -and $gitBranch -ne 'HEAD') {
    $Branch = "refs/heads/$gitBranch"
  }
}

if ([string]::IsNullOrWhiteSpace($RepositoryUrl)) {
  Fail 'Repository URL could not be determined.'
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
  Fail 'Repository commit could not be determined.'
}

if ($RepositoryUrl -ne $expectedRepositoryUrl) {
  Fail "Repository URL '$RepositoryUrl' is not the expected release repository '$expectedRepositoryUrl'."
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
  $ManifestPath = Join-Path $PackageDirectory 'release-artifact-manifest.json'
}

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
  $SourceRoot = Join-Path $PSScriptRoot '..'
}

$sourceRootFullPath = (Resolve-Path -LiteralPath $SourceRoot).Path
$expectedDependencies = Get-ExpectedDependencies -SourceRootPath $sourceRootFullPath -Packages $catalogPackages

Add-Type -AssemblyName System.IO.Compression.FileSystem

$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$nupkgs = @(Get-ChildItem -LiteralPath $packageRoot -Filter '*.nupkg' -File | Sort-Object Name)
$snupkgs = @(Get-ChildItem -LiteralPath $packageRoot -Filter '*.snupkg' -File | Sort-Object Name)
$allArtifacts = @($nupkgs + $snupkgs)

if ($nupkgs.Count -ne $expectedNupkgIds.Count) {
  Fail "Expected $($expectedNupkgIds.Count) .nupkg files, found $($nupkgs.Count)."
}

if ($snupkgs.Count -ne $expectedSnupkgIds.Count) {
  Fail "Expected $($expectedSnupkgIds.Count) .snupkg files, found $($snupkgs.Count)."
}

$expectedNupkgNames = @($catalogPackages | ForEach-Object { Get-FluentMapPackageFileName -Package $_ -Version $Version })
$expectedSnupkgNames = @($symbolCatalogPackages | ForEach-Object { Get-FluentMapPackageFileName -Package $_ -Version $Version -Kind symbols })
Assert-SetEquals -Expected $expectedNupkgNames -Actual ([string[]]@($nupkgs | ForEach-Object { $_.Name })) -Description '.nupkg file set'
Assert-SetEquals -Expected $expectedSnupkgNames -Actual ([string[]]@($snupkgs | ForEach-Object { $_.Name })) -Description '.snupkg file set'

$forbiddenArtifacts = @($allArtifacts | Where-Object { $_.Name -match '(?i)(Tests|Benchmarks|AotSmoke)' })
if ($forbiddenArtifacts.Count -gt 0) {
  $forbiddenArtifactNames = @($forbiddenArtifacts | ForEach-Object { $_.Name })
  Fail "Unexpected test/benchmark/smoke artifacts: $($forbiddenArtifactNames -join ', ')."
}

$packageInfos = @{}
foreach ($file in $nupkgs) {
  $info = Get-ZipPackageInfo -File $file
  $expectedId = $file.Name.Substring(0, $file.Name.Length - ".$Version.nupkg".Length)
  Assert-CommonPackageMetadata -PackageInfo $info -ExpectedId $expectedId

  if (-not $expectedDependencies.ContainsKey($info.Id)) {
    Fail "Unexpected package ID '$($info.Id)' has no dependency contract."
  }

  Assert-Dependencies -PackageId $info.Id -Actual $info.Dependencies -Expected $expectedDependencies[$info.Id]

  if ($packageInfos.ContainsKey($info.Id)) {
    Fail "Duplicate .nupkg package ID '$($info.Id)'."
  }

  $packageInfos[$info.Id] = $info
}

Assert-SetEquals -Expected $expectedNupkgIds -Actual ([string[]]$packageInfos.Keys) -Description '.nupkg package IDs'

foreach ($package in @($catalogPackages | Where-Object { $_.assetKind -eq 'library' })) {
  $id = [string]$package.packageId
  $assemblyName = [string]$package.project
  $expectedDll = "lib/netstandard2.0/$assemblyName.dll"
  $expectedXml = "lib/netstandard2.0/$assemblyName.xml"
  $info = $packageInfos[$id]
  if ($expectedDll -notin $info.Entries -or $expectedXml -notin $info.Entries) {
    Fail "$id must include $expectedDll and $expectedXml."
  }
}

foreach ($package in @($catalogPackages | Where-Object { $_.assetKind -eq 'analyzer' })) {
  $id = [string]$package.packageId
  $assemblyName = [string]$package.project
  $info = $packageInfos[$id]
  $expectedDll = "analyzers/dotnet/cs/$assemblyName.dll"
  $expectedPdb = "analyzers/dotnet/cs/$assemblyName.pdb"
  if ($expectedDll -notin $info.Entries -or $expectedPdb -notin $info.Entries) {
    Fail "$id must use analyzer package layout under analyzers/dotnet/cs."
  }

  $libEntries = @($info.Entries | Where-Object { $_ -like 'lib/*' })
  if ($libEntries.Count -gt 0) {
    Fail "$id must not include lib assets: $($libEntries -join ', ')."
  }
}

$symbolInfos = @{}
foreach ($file in $snupkgs) {
  $expectedId = $file.Name.Substring(0, $file.Name.Length - ".$Version.snupkg".Length)
  $info = Get-ZipPackageInfo -File $file
  Assert-CommonPackageMetadata -PackageInfo $info -ExpectedId $expectedId -RequireReadmeAndLicense $false

  $symbolPackage = $symbolCatalogPackages | Where-Object { $_.packageId -eq $expectedId } | Select-Object -First 1
  if ($null -eq $symbolPackage) {
    Fail "Unexpected symbol package ID '$expectedId'."
  }

  $expectedPdb = "lib/netstandard2.0/$($symbolPackage.project).pdb"
  if ($expectedPdb -notin $info.Entries) {
    Fail "$expectedId symbol package must include $expectedPdb."
  }

  if ($symbolInfos.ContainsKey($info.Id)) {
    Fail "Duplicate .snupkg package ID '$($info.Id)'."
  }

  $symbolInfos[$info.Id] = $info
}

Assert-SetEquals -Expected $expectedSnupkgIds -Actual ([string[]]$symbolInfos.Keys) -Description '.snupkg package IDs'

$manifestPackages = @(
  foreach ($file in $allArtifacts | Sort-Object Name) {
    $id = if ($file.Name.EndsWith('.snupkg', [System.StringComparison]::OrdinalIgnoreCase)) {
      $file.Name.Substring(0, $file.Name.Length - ".$Version.snupkg".Length)
    }
    else {
      $file.Name.Substring(0, $file.Name.Length - ".$Version.nupkg".Length)
    }

    [ordered]@{
      file = $file.Name
      packageId = $id
      version = $Version
      sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
      kind = if ($file.Extension -eq '.snupkg') { 'symbols' } else { 'package' }
      size = $file.Length
    }
  }
)

$manifest = [ordered]@{
  schemaVersion = '1.0'
  version = $Version
  repository = $Repository
  repositoryUrl = $RepositoryUrl
  commit = $Commit
  branch = $Branch
  packages = @($manifestPackages)
}

$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)

$manifestJson = $manifest | ConvertTo-Json -Depth 8

if ($VerifyExistingManifest) {
  if (-not (Test-Path -LiteralPath $manifestFullPath -PathType Leaf)) {
    Fail "Expected existing manifest '$manifestFullPath' was not found."
  }

  $existingManifest = Get-Content -Raw -LiteralPath $manifestFullPath
  $normalizedExistingManifest = ($existingManifest.TrimEnd() | ConvertFrom-Json | ConvertTo-Json -Depth 8).TrimEnd()
  $normalizedExpectedManifest = $manifestJson.TrimEnd()

  if ($normalizedExistingManifest -ne $normalizedExpectedManifest) {
    Fail "Existing manifest '$manifestFullPath' does not match the validated release artifact set for $Version, repository '$Repository', commit '$Commit', and branch '$Branch'."
  }
}
else {
  $manifestDirectory = Split-Path -Parent $manifestFullPath
  if (-not [string]::IsNullOrWhiteSpace($manifestDirectory)) {
    New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null
  }

  [System.IO.File]::WriteAllText(
    $manifestFullPath,
    $manifestJson + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
}

$expectedArtifactCount = $expectedNupkgIds.Count + $expectedSnupkgIds.Count
$validatedManifest = Get-Content -Raw -Path $manifestFullPath | ConvertFrom-Json
if ($validatedManifest.version -ne $Version -or @($validatedManifest.packages).Count -ne $expectedArtifactCount) {
  Fail "Generated manifest '$manifestFullPath' did not round-trip with the expected version and package count."
}

if ($VerifyExistingManifest) {
  Write-Host "Validated $($expectedNupkgIds.Count) .nupkg files, $($expectedSnupkgIds.Count) .snupkg files and verified existing manifest '$manifestFullPath' for $Version."
}
else {
  Write-Host "Validated $($expectedNupkgIds.Count) .nupkg files, $($expectedSnupkgIds.Count) .snupkg files and wrote manifest '$manifestFullPath' for $Version."
}
