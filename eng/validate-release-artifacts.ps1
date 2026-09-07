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

  [string]$Branch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$candidateVersion = '3.0.0-rc.1'
$candidateBranch = 'refs/heads/release/3.0.0-rc.1'
$expectedRepositoryUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap'
$expectedNupkgIds = @(
  'Dapper.FluentMap',
  'Dapper.FluentMap.Dommel',
  'Dapper.FluentMap.DependencyInjection',
  'Dapper.FluentMap.Analyzers',
  'Dapper.FluentMap.Generators'
)
$expectedSnupkgIds = @(
  'Dapper.FluentMap',
  'Dapper.FluentMap.Dommel',
  'Dapper.FluentMap.DependencyInjection'
)
$expectedDependencies = @{
  'Dapper.FluentMap' = @{
    'Dapper' = '[2.1.79, 3.0.0)'
    'Microsoft.Bcl.AsyncInterfaces' = '10.0.8'
  }
  'Dapper.FluentMap.Dommel' = @{
    'Dapper.FluentMap' = $Version
    'Dapper' = '[2.1.79, 3.0.0)'
    'Dommel' = '[3.5.3, 4.0.0)'
  }
  'Dapper.FluentMap.DependencyInjection' = @{
    'Dapper.FluentMap' = $Version
    'Microsoft.Extensions.DependencyInjection.Abstractions' = '10.0.10'
  }
  'Dapper.FluentMap.Analyzers' = @{}
  'Dapper.FluentMap.Generators' = @{}
}

function Fail {
  param([string]$Message)
  throw "Release artifact validation failed: $Message"
}

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

  $child = $Node.ChildNodes | Where-Object { $_.LocalName -eq $Name } | Select-Object -First 1
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
      ProjectUrl = Get-ChildText -Node $metadata -Name 'projectUrl'
      RepositoryUrl = if ($null -eq $repositoryNode) { $null } else { $repositoryNode.GetAttribute('url') }
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
    if ($Actual[$dependencyId] -ne $Expected[$dependencyId]) {
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

  if ($PackageInfo.Id -ne $ExpectedId) {
    Fail "$($PackageInfo.File.Name) has package ID '$($PackageInfo.Id)', expected '$ExpectedId'."
  }

  if ($PackageInfo.Version -ne $Version) {
    Fail "$ExpectedId has version '$($PackageInfo.Version)', expected '$Version'."
  }

  if ($RequireReadmeAndLicense) {
    if ($PackageInfo.LicenseType -ne 'expression' -or $PackageInfo.License -ne 'MIT') {
      Fail "$ExpectedId must use MIT license expression."
    }

    if ($PackageInfo.Readme -ne 'README.md' -or 'README.md' -notin $PackageInfo.Entries) {
      Fail "$ExpectedId must include README.md and reference it from the nuspec."
    }
  }

  if ($PackageInfo.ProjectUrl -ne $expectedRepositoryUrl) {
    Fail "$ExpectedId projectUrl is '$($PackageInfo.ProjectUrl)', expected '$expectedRepositoryUrl'."
  }

  if ($PackageInfo.RepositoryUrl -ne $RepositoryUrl) {
    Fail "$ExpectedId repository URL is '$($PackageInfo.RepositoryUrl)', expected '$RepositoryUrl'."
  }

  if ($PackageInfo.RepositoryCommit -ne $Commit) {
    Fail "$ExpectedId repository commit is '$($PackageInfo.RepositoryCommit)', expected '$Commit'."
  }

  if ($Branch -ne '' -and $PackageInfo.RepositoryBranch -ne $Branch) {
    Fail "$ExpectedId repository branch is '$($PackageInfo.RepositoryBranch)', expected '$Branch'."
  }
}

if ($Version -ne $candidateVersion) {
  Fail "This release gate only accepts version $candidateVersion; received '$Version'."
}

if ($Version -eq '2.0.0' -or $Version -eq '3.0.0' -or $Version -notmatch '-') {
  Fail "Version '$Version' is not allowed for this release candidate."
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

if ($Branch -ne $candidateBranch) {
  Fail "This release gate only accepts branch $candidateBranch; received '$Branch'."
}

if ($RepositoryUrl -ne $expectedRepositoryUrl) {
  Fail "Repository URL '$RepositoryUrl' is not the expected release repository '$expectedRepositoryUrl'."
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
  $ManifestPath = Join-Path $PackageDirectory 'release-artifact-manifest.json'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$nupkgs = @(Get-ChildItem -LiteralPath $packageRoot -Filter '*.nupkg' -File | Sort-Object Name)
$snupkgs = @(Get-ChildItem -LiteralPath $packageRoot -Filter '*.snupkg' -File | Sort-Object Name)
$allArtifacts = @($nupkgs + $snupkgs)

if ($nupkgs.Count -ne 5) {
  Fail "Expected 5 .nupkg files, found $($nupkgs.Count)."
}

if ($snupkgs.Count -ne 3) {
  Fail "Expected 3 .snupkg files, found $($snupkgs.Count)."
}

$expectedNupkgNames = @($expectedNupkgIds | ForEach-Object { "$_.$Version.nupkg" })
$expectedSnupkgNames = @($expectedSnupkgIds | ForEach-Object { "$_.$Version.snupkg" })
Assert-SetEquals -Expected $expectedNupkgNames -Actual ([string[]]@($nupkgs.Name)) -Description '.nupkg file set'
Assert-SetEquals -Expected $expectedSnupkgNames -Actual ([string[]]@($snupkgs.Name)) -Description '.snupkg file set'

$forbiddenArtifacts = @($allArtifacts | Where-Object { $_.Name -match '(?i)(Tests|Benchmarks|AotSmoke)' })
if ($forbiddenArtifacts.Count -gt 0) {
  Fail "Unexpected test/benchmark/smoke artifacts: $($forbiddenArtifacts.Name -join ', ')."
}

$packageInfos = @{}
foreach ($file in $nupkgs) {
  $info = Get-ZipPackageInfo -File $file
  Assert-CommonPackageMetadata -PackageInfo $info -ExpectedId ($file.Name.Substring(0, $file.Name.Length - ".$Version.nupkg".Length))
  Assert-Dependencies -PackageId $info.Id -Actual $info.Dependencies -Expected $expectedDependencies[$info.Id]

  if ($packageInfos.ContainsKey($info.Id)) {
    Fail "Duplicate .nupkg package ID '$($info.Id)'."
  }

  $packageInfos[$info.Id] = $info
}

Assert-SetEquals -Expected $expectedNupkgIds -Actual ([string[]]$packageInfos.Keys) -Description '.nupkg package IDs'

foreach ($id in @('Dapper.FluentMap', 'Dapper.FluentMap.Dommel', 'Dapper.FluentMap.DependencyInjection')) {
  $expectedDll = "lib/netstandard2.0/$id.dll"
  $expectedXml = "lib/netstandard2.0/$id.xml"
  $info = $packageInfos[$id]
  if ($expectedDll -notin $info.Entries -or $expectedXml -notin $info.Entries) {
    Fail "$id must include $expectedDll and $expectedXml."
  }
}

foreach ($id in @('Dapper.FluentMap.Analyzers', 'Dapper.FluentMap.Generators')) {
  $info = $packageInfos[$id]
  $expectedDll = "analyzers/dotnet/cs/$id.dll"
  $expectedPdb = "analyzers/dotnet/cs/$id.pdb"
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

  $expectedPdb = "lib/netstandard2.0/$expectedId.pdb"
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

$manifestDirectory = Split-Path -Parent $ManifestPath
if (-not [string]::IsNullOrWhiteSpace($manifestDirectory)) {
  New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null
}

$manifestJson = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
  (Resolve-Path -LiteralPath (Split-Path -Parent $ManifestPath)).Path + [System.IO.Path]::DirectorySeparatorChar + (Split-Path -Leaf $ManifestPath),
  $manifestJson + [Environment]::NewLine,
  [System.Text.UTF8Encoding]::new($false))

$validatedManifest = Get-Content -Raw -Path $ManifestPath | ConvertFrom-Json
if ($validatedManifest.version -ne $Version -or @($validatedManifest.packages).Count -ne 8) {
  Fail "Generated manifest '$ManifestPath' did not round-trip with the expected version and package count."
}

Write-Host "Validated 5 .nupkg files, 3 .snupkg files and wrote manifest '$ManifestPath' for $Version."
