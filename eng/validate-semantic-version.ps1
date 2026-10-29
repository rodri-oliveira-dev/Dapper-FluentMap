[CmdletBinding()]
param(
  [ValidateSet('Recommend', 'Validate', 'ValidatePackages')]
  [string]$Operation = 'Validate',

  [ValidateSet('None', 'Patch', 'Minor', 'Refactor', 'Breaking')]
  [string]$ChangeType,

  [string]$SelectedVersion,
  [string]$PublishedVersion,
  [string]$PreparedVersion,
  [string]$SourceRoot,
  [string]$PackageDirectory,
  [string]$CatalogPath,
  [string]$ReleaseMetadataPath,
  [string]$JsonOutputPath,
  [switch]$AllowExistingPublishedVersion,
  [switch]$AllowMajor,
  [string]$BreakingAnalysisPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
  $SourceRoot = Split-Path -Parent $PSScriptRoot
}
$SourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
  $CatalogPath = Join-Path $SourceRoot 'eng/package-catalog.json'
}

Import-Module (Join-Path $PSScriptRoot 'SemanticVersioning.psm1') -Force

if ([string]::IsNullOrWhiteSpace($PreparedVersion)) {
  $PreparedVersion = Get-FluentMapConfiguredVersion -SourceRoot $SourceRoot
}
if ($Operation -ne 'ValidatePackages' -and [string]::IsNullOrWhiteSpace($PublishedVersion)) {
  $stableTags = @(Get-FluentMapStableTagVersions -SourceRoot $SourceRoot)
  $PublishedVersion = Get-FluentMapLatestVersion -Versions $stableTags
  if ([string]::IsNullOrWhiteSpace($PublishedVersion)) {
    throw 'No stable release tag was found. Supply -PublishedVersion when working with incomplete history.'
  }
}

switch ($Operation) {
  'Recommend' {
    if ([string]::IsNullOrWhiteSpace($ChangeType)) { throw '-ChangeType is required for Recommend.' }
    $result = Get-FluentMapVersionRecommendation `
      -ChangeType $ChangeType `
      -PublishedVersion $PublishedVersion `
      -PreparedVersion $PreparedVersion `
      -AllowMajor:$AllowMajor `
      -BreakingAnalysisPath $BreakingAnalysisPath
    $failed = $result.Status -in @('Blocked', 'ReviewRequired')
  }
  'Validate' {
    if ([string]::IsNullOrWhiteSpace($SelectedVersion)) { throw '-SelectedVersion is required for Validate.' }
    $selectionParameters = @{
      SelectedVersion = $SelectedVersion
      PublishedVersion = $PublishedVersion
      PreparedVersion = $PreparedVersion
      AllowExistingPublishedVersion = $AllowExistingPublishedVersion
      AllowMajor = $AllowMajor
      BreakingAnalysisPath = $BreakingAnalysisPath
    }
    if ($AllowExistingPublishedVersion) {
      & git -C $SourceRoot rev-parse -q --verify "refs/tags/v$SelectedVersion" *> $null
      $selectionParameters.SelectedVersionHasTag = $LASTEXITCODE -eq 0
    }
    if ($PSBoundParameters.ContainsKey('ChangeType')) { $selectionParameters.ChangeType = $ChangeType }
    $result = Test-FluentMapVersionSelection @selectionParameters
    $failed = -not $result.IsValid
  }
  'ValidatePackages' {
    if ([string]::IsNullOrWhiteSpace($SelectedVersion)) { throw '-SelectedVersion is required for ValidatePackages.' }
    if ([string]::IsNullOrWhiteSpace($PackageDirectory)) { throw '-PackageDirectory is required for ValidatePackages.' }
    $result = Test-FluentMapPackageVersionAlignment `
      -PackageDirectory $PackageDirectory `
      -ExpectedVersion $SelectedVersion `
      -CatalogPath $CatalogPath `
      -ReleaseMetadataPath $ReleaseMetadataPath
    $failed = -not $result.IsValid
  }
}

$json = $result | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($JsonOutputPath)) {
  $parent = Split-Path -Parent $JsonOutputPath
  if (-not [string]::IsNullOrWhiteSpace($parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
  [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($JsonOutputPath), $json, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Semantic versioning operation: $Operation"
Write-Host "Published version: $(if ([string]::IsNullOrWhiteSpace($PublishedVersion)) { 'Not evaluated for artifact-only validation' } else { $PublishedVersion })"
Write-Host "Prepared version: $PreparedVersion"
if ($Operation -eq 'Recommend') {
  Write-Host "Status: $($result.Status)"
  Write-Host "Recommended version: $(if ($null -eq $result.RecommendedVersion) { 'No release required / manual review' } else { $result.RecommendedVersion })"
  Write-Host "Reason: $($result.Reason)"
}
elseif ($failed) {
  foreach ($message in @($result.Errors)) { Write-Error $message }
}
else {
  Write-Host 'Semantic version validation passed.'
}

if ($failed) { exit 1 }

# Do not leak the exit code from the earlier git tag probe into the pwsh action.
exit 0
