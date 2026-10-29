Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SemVerPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?$'

function ConvertTo-FluentMapSemanticVersion {
  [CmdletBinding()]
  param([Parameter(Mandatory = $true)][string]$Version)

  $match = [regex]::Match($Version, $script:SemVerPattern)
  if (-not $match.Success) {
    throw "Invalid FluentMap package version '$Version'. Expected SemVer without a leading 'v' or build metadata."
  }

  [pscustomobject]@{
    Original = $Version
    Major = [System.Numerics.BigInteger]::Parse($match.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    Minor = [System.Numerics.BigInteger]::Parse($match.Groups[2].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    Patch = [System.Numerics.BigInteger]::Parse($match.Groups[3].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    PreRelease = if ($match.Groups[4].Success) { $match.Groups[4].Value } else { $null }
  }
}

function Compare-FluentMapSemanticVersion {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)][string]$Left,
    [Parameter(Mandatory = $true)][string]$Right
  )

  $leftVersion = ConvertTo-FluentMapSemanticVersion $Left
  $rightVersion = ConvertTo-FluentMapSemanticVersion $Right
  foreach ($property in @('Major', 'Minor', 'Patch')) {
    if ($leftVersion.$property -lt $rightVersion.$property) { return -1 }
    if ($leftVersion.$property -gt $rightVersion.$property) { return 1 }
  }

  if ($null -eq $leftVersion.PreRelease -and $null -eq $rightVersion.PreRelease) { return 0 }
  if ($null -eq $leftVersion.PreRelease) { return 1 }
  if ($null -eq $rightVersion.PreRelease) { return -1 }

  $leftIdentifiers = @($leftVersion.PreRelease -split '\.')
  $rightIdentifiers = @($rightVersion.PreRelease -split '\.')
  $count = [Math]::Max($leftIdentifiers.Count, $rightIdentifiers.Count)
  for ($index = 0; $index -lt $count; $index++) {
    if ($index -ge $leftIdentifiers.Count) { return -1 }
    if ($index -ge $rightIdentifiers.Count) { return 1 }

    $leftIsNumber = $leftIdentifiers[$index] -match '^[0-9]+$'
    $rightIsNumber = $rightIdentifiers[$index] -match '^[0-9]+$'
    if ($leftIsNumber -and $rightIsNumber) {
      if ($leftIdentifiers[$index].Length -lt $rightIdentifiers[$index].Length) { return -1 }
      if ($leftIdentifiers[$index].Length -gt $rightIdentifiers[$index].Length) { return 1 }
      $numericComparison = [string]::CompareOrdinal($leftIdentifiers[$index], $rightIdentifiers[$index])
      if ($numericComparison -lt 0) { return -1 }
      if ($numericComparison -gt 0) { return 1 }
      continue
    }

    if ($leftIsNumber -and -not $rightIsNumber) { return -1 }
    if (-not $leftIsNumber -and $rightIsNumber) { return 1 }
    $comparison = [string]::CompareOrdinal($leftIdentifiers[$index], $rightIdentifiers[$index])
    if ($comparison -lt 0) { return -1 }
    if ($comparison -gt 0) { return 1 }
  }

  return 0
}

function Get-FluentMapConfiguredVersion {
  [CmdletBinding()]
  param([Parameter(Mandatory = $true)][string]$SourceRoot)

  $propsPath = Join-Path $SourceRoot 'Directory.Build.props'
  if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
    throw "Directory.Build.props was not found under '$SourceRoot'."
  }

  [xml]$props = Get-Content -Raw -LiteralPath $propsPath
  $node = $props.SelectSingleNode('//*[local-name()="FluentMapPackageVersionPrefix"]')
  if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
    throw "Directory.Build.props does not define FluentMapPackageVersionPrefix."
  }

  $version = $node.InnerText.Trim()
  $null = ConvertTo-FluentMapSemanticVersion $version
  return $version
}

function Get-FluentMapStableTagVersions {
  [CmdletBinding()]
  param([Parameter(Mandatory = $true)][string]$SourceRoot)

  $tagOutput = & git -C $SourceRoot tag --merged HEAD --list 'v*' 2>$null
  if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Git tags from '$SourceRoot'."
  }

  $versions = @()
  foreach ($tag in @($tagOutput)) {
    if ($tag -notmatch '^v(.+)$') { continue }
    try {
      $parsed = ConvertTo-FluentMapSemanticVersion $Matches[1]
      if ($null -eq $parsed.PreRelease) { $versions += $parsed.Original }
    }
    catch {
      continue
    }
  }

  return @($versions)
}

function Get-FluentMapLatestVersion {
  [CmdletBinding()]
  param([Parameter(Mandatory = $true)][string[]]$Versions)

  if ($Versions.Count -eq 0) { return $null }
  $latest = $Versions[0]
  foreach ($candidate in $Versions | Select-Object -Skip 1) {
    if ((Compare-FluentMapSemanticVersion $candidate $latest) -gt 0) { $latest = $candidate }
  }

  return $latest
}

function Get-FluentMapVersionRecommendation {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)][ValidateSet('None', 'Patch', 'Minor', 'Refactor', 'Breaking')][string]$ChangeType,
    [Parameter(Mandatory = $true)][string]$PublishedVersion,
    [Parameter(Mandatory = $true)][string]$PreparedVersion,
    [switch]$AllowMajor,
    [string]$BreakingAnalysisPath
  )

  $published = ConvertTo-FluentMapSemanticVersion $PublishedVersion
  $prepared = ConvertTo-FluentMapSemanticVersion $PreparedVersion
  $preparedComparison = Compare-FluentMapSemanticVersion $PreparedVersion $PublishedVersion
  $isPreparation = $preparedComparison -gt 0

  if ($ChangeType -eq 'None') {
    return [pscustomobject]@{
      Status = 'NoReleaseRequired'; RecommendedVersion = $null; ChangeType = $ChangeType
      PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
      Reason = 'Documentation, changelog-only, test-only, comment-only, or administrative changes do not require a package release by themselves.'
    }
  }

  if ($prepared.Major -gt $published.Major) {
    if (-not $AllowMajor) {
      return [pscustomobject]@{
        Status = 'Blocked'; RecommendedVersion = $null; ChangeType = $ChangeType
        PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
        Reason = "Prepared version $PreparedVersion crosses the current major boundary and requires explicit user authorization."
      }
    }

    if (-not (Test-FluentMapBreakingAnalysis $BreakingAnalysisPath)) {
      return [pscustomobject]@{
        Status = 'ReviewRequired'; RecommendedVersion = $null; ChangeType = $ChangeType
        PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
        Reason = "Prepared version $PreparedVersion crosses the current major boundary and requires a complete formal breaking-change analysis."
      }
    }
  }

  if ($ChangeType -eq 'Breaking') {
    if (-not $AllowMajor) {
      return [pscustomobject]@{
        Status = 'Blocked'; RecommendedVersion = $null; ChangeType = $ChangeType
        PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
        Reason = 'A potential breaking change requires explicit user authorization; a major version is never selected automatically.'
      }
    }

    if ([string]::IsNullOrWhiteSpace($BreakingAnalysisPath) -or -not (Test-Path -LiteralPath $BreakingAnalysisPath -PathType Leaf)) {
      return [pscustomobject]@{
        Status = 'ReviewRequired'; RecommendedVersion = $null; ChangeType = $ChangeType
        PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
        Reason = 'Major authorization was supplied, but a formal breaking-change analysis file is required before recommending a major version.'
      }
    }

    $analysis = Get-Content -Raw -LiteralPath $BreakingAnalysisPath
    $requiredTopics = @('source compatibility', 'binary compatibility', 'behavior', 'target framework', 'dependency', 'migration')
    $missingTopics = @($requiredTopics | Where-Object { $analysis.IndexOf($_, [System.StringComparison]::OrdinalIgnoreCase) -lt 0 })
    if ($missingTopics.Count -gt 0) {
      return [pscustomobject]@{
        Status = 'ReviewRequired'; RecommendedVersion = $null; ChangeType = $ChangeType
        PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
        Reason = "The formal breaking-change analysis is incomplete. Missing topics: $($missingTopics -join ', ')."
      }
    }

    return [pscustomobject]@{
      Status = 'Recommended'; RecommendedVersion = "$($published.Major + 1).0.0"; ChangeType = $ChangeType
      PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
      Reason = 'The user explicitly authorized major-version evaluation and the required compatibility analysis is present.'
    }
  }

  $candidate = if ($ChangeType -eq 'Minor') {
    "$($published.Major).$($published.Minor + 1).0"
  }
  else {
    "$($published.Major).$($published.Minor).$($published.Patch + 1)"
  }

  if ($isPreparation -and (Compare-FluentMapSemanticVersion $PreparedVersion $candidate) -ge 0) {
    return [pscustomobject]@{
      Status = 'Recommended'; RecommendedVersion = $PreparedVersion; ChangeType = $ChangeType
      PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $true
      Reason = "Version $PreparedVersion is already in preparation and satisfies the $ChangeType change; no repeated increment is needed."
    }
  }

  return [pscustomobject]@{
    Status = 'Recommended'; RecommendedVersion = $candidate; ChangeType = $ChangeType
    PublishedVersion = $PublishedVersion; PreparedVersion = $PreparedVersion; PreparationPreserved = $false
    Reason = if ($ChangeType -eq 'Minor') { 'A backward-compatible feature requires the next minor version.' } else { 'A compatible package-affecting change requires the next patch version.' }
  }
}

function Test-FluentMapBreakingAnalysis {
  param([string]$BreakingAnalysisPath)
  if ([string]::IsNullOrWhiteSpace($BreakingAnalysisPath) -or -not (Test-Path -LiteralPath $BreakingAnalysisPath -PathType Leaf)) { return $false }
  $content = Get-Content -Raw -LiteralPath $BreakingAnalysisPath
  foreach ($topic in @('source compatibility', 'binary compatibility', 'behavior', 'target framework', 'dependency', 'migration')) {
    if ($content.IndexOf($topic, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { return $false }
  }
  return $true
}

function Test-FluentMapVersionSelection {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)][string]$SelectedVersion,
    [Parameter(Mandatory = $true)][string]$PublishedVersion,
    [Parameter(Mandatory = $true)][string]$PreparedVersion,
    [ValidateSet('None', 'Patch', 'Minor', 'Refactor', 'Breaking')][string]$ChangeType,
    [switch]$AllowExistingPublishedVersion,
    [switch]$SelectedVersionHasTag,
    [switch]$AllowMajor,
    [string]$BreakingAnalysisPath
  )

  $selected = ConvertTo-FluentMapSemanticVersion $SelectedVersion
  $published = ConvertTo-FluentMapSemanticVersion $PublishedVersion
  $prepared = ConvertTo-FluentMapSemanticVersion $PreparedVersion
  $errors = [System.Collections.Generic.List[string]]::new()

  $selectedMatchesPrepared = $SelectedVersion -eq $PreparedVersion -or (
    $null -ne $selected.PreRelease -and
    $null -eq $prepared.PreRelease -and
    $selected.Major -eq $prepared.Major -and
    $selected.Minor -eq $prepared.Minor -and
    $selected.Patch -eq $prepared.Patch)
  if (-not $selectedMatchesPrepared) {
    $errors.Add("Selected version $SelectedVersion diverges from the prepared repository version $PreparedVersion.")
  }

  $comparison = Compare-FluentMapSemanticVersion $SelectedVersion $PublishedVersion
  if ($comparison -lt 0 -and -not ($AllowExistingPublishedVersion -and $SelectedVersionHasTag)) {
    $errors.Add("Selected version $SelectedVersion is a downgrade from published version $PublishedVersion.")
  }
  elseif ($comparison -eq 0 -and -not ($AllowExistingPublishedVersion -and $SelectedVersionHasTag)) {
    $errors.Add("Version $SelectedVersion is already represented by the latest stable release tag; reuse requires an explicit governed rerun/recovery path.")
  }

  if ($selected.Major -gt $published.Major) {
    if (-not $AllowMajor) {
      $errors.Add("Major version $($selected.Major) was not explicitly authorized.")
    }
    elseif (-not (Test-FluentMapBreakingAnalysis $BreakingAnalysisPath)) {
      $errors.Add('A complete formal breaking-change analysis is required for an authorized major version.')
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($ChangeType)) {
    $recommendation = Get-FluentMapVersionRecommendation `
      -ChangeType $ChangeType `
      -PublishedVersion $PublishedVersion `
      -PreparedVersion $PreparedVersion `
      -AllowMajor:$AllowMajor `
      -BreakingAnalysisPath $BreakingAnalysisPath
    if ($recommendation.Status -ne 'Recommended') {
      $errors.Add("Change type $ChangeType cannot be assigned a package version automatically: $($recommendation.Reason)")
    }
    elseif ($SelectedVersion -ne $recommendation.RecommendedVersion) {
      $errors.Add("Selected version $SelectedVersion is incompatible with change type $ChangeType; expected $($recommendation.RecommendedVersion).")
    }
  }

  [pscustomobject]@{
    IsValid = $errors.Count -eq 0
    SelectedVersion = $SelectedVersion
    PublishedVersion = $PublishedVersion
    PreparedVersion = $PreparedVersion
    Errors = @($errors)
  }
}

function Test-FluentMapPackageVersionAlignment {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedVersion,
    [Parameter(Mandatory = $true)][string]$CatalogPath,
    [string]$ReleaseMetadataPath
  )

  $null = ConvertTo-FluentMapSemanticVersion $ExpectedVersion
  if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) { throw "Package directory '$PackageDirectory' does not exist." }
  if (-not (Test-Path -LiteralPath $CatalogPath -PathType Leaf)) { throw "Package catalog '$CatalogPath' does not exist." }

  $catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json
  $expectedIds = @($catalog.packages | ForEach-Object { [string]$_.packageId })
  $errors = [System.Collections.Generic.List[string]]::new()
  $actual = @{}
  Add-Type -AssemblyName System.IO.Compression.FileSystem

  foreach ($packagePath in @(Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg')) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath.FullName)
    try {
      $nuspecs = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
      if ($nuspecs.Count -ne 1) { $errors.Add("$($packagePath.Name) must contain exactly one nuspec."); continue }
      $reader = [System.IO.StreamReader]::new($nuspecs[0].Open())
      try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
      $metadata = $nuspec.SelectSingleNode('//*[local-name()="metadata"]')
      $id = $metadata.SelectSingleNode('./*[local-name()="id"]').InnerText
      $version = $metadata.SelectSingleNode('./*[local-name()="version"]').InnerText
      if ($actual.ContainsKey($id)) { $errors.Add("Package ID $id appears more than once.") } else { $actual[$id] = $version }
      if ($version -ne $ExpectedVersion) { $errors.Add("Package $id has version $version; expected $ExpectedVersion.") }
    }
    finally { $archive.Dispose() }
  }

  foreach ($id in $expectedIds) {
    if (-not $actual.ContainsKey($id)) { $errors.Add("Expected package $id is missing.") }
  }
  foreach ($id in $actual.Keys) {
    if ($id -notin $expectedIds) { $errors.Add("Unexpected package $id is present.") }
  }

  if (-not [string]::IsNullOrWhiteSpace($ReleaseMetadataPath)) {
    if (-not (Test-Path -LiteralPath $ReleaseMetadataPath -PathType Leaf)) {
      $errors.Add("Release metadata '$ReleaseMetadataPath' does not exist.")
    }
    else {
      $metadata = Get-Content -Raw -LiteralPath $ReleaseMetadataPath | ConvertFrom-Json
      if ([string]$metadata.version -ne $ExpectedVersion) {
        $errors.Add("Release metadata version '$($metadata.version)' diverges from $ExpectedVersion.")
      }
    }
  }

  [pscustomobject]@{
    IsValid = $errors.Count -eq 0
    ExpectedVersion = $ExpectedVersion
    PackageVersions = [pscustomobject]$actual
    Errors = @($errors)
  }
}

Export-ModuleMember -Function @(
  'ConvertTo-FluentMapSemanticVersion',
  'Compare-FluentMapSemanticVersion',
  'Get-FluentMapConfiguredVersion',
  'Get-FluentMapStableTagVersions',
  'Get-FluentMapLatestVersion',
  'Get-FluentMapVersionRecommendation',
  'Test-FluentMapVersionSelection',
  'Test-FluentMapPackageVersionAlignment'
)
