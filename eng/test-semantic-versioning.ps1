[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SemanticVersioning.psm1') -Force

$script:Passed = 0
$script:Failed = 0

function Assert-True([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

function Invoke-Test([string]$Name, [scriptblock]$Test) {
  try {
    & $Test
    $script:Passed++
    Write-Host "PASS: $Name"
  }
  catch {
    $script:Failed++
    Write-Host "FAIL: $Name`n$($_.Exception.Message)"
  }
}

function New-TestPackage([string]$Path, [string]$Id, [string]$Version) {
  $work = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $work | Out-Null
  try {
    $nuspec = @"
<?xml version="1.0"?>
<package><metadata><id>$Id</id><version>$Version</version></metadata></package>
"@
    [System.IO.File]::WriteAllText((Join-Path $work "$Id.nuspec"), $nuspec, [System.Text.UTF8Encoding]::new($false))
    [System.IO.Compression.ZipFile]::CreateFromDirectory($work, $Path)
  }
  finally { Remove-Item -LiteralPath $work -Recurse -Force }
}

Invoke-Test 'SemVer validation and comparison' {
  Assert-True ((Compare-FluentMapSemanticVersion '3.6.1' '3.6.0') -gt 0) '3.6.1 must be greater than 3.6.0.'
  Assert-True ((Compare-FluentMapSemanticVersion '3.6.1-rc.1' '3.6.1') -lt 0) 'A prerelease must be lower than its stable version.'
  $threw = $false
  try { ConvertTo-FluentMapSemanticVersion 'v3.6.1' | Out-Null } catch { $threw = $true }
  Assert-True $threw 'A leading v must be rejected as a package version.'
}

Invoke-Test 'SemVer supports arbitrarily large numeric identifiers' {
  Assert-True ((Compare-FluentMapSemanticVersion '2147483648.0.0' '2147483647.999999999999999999999.0') -gt 0) 'Core numeric identifiers must not be limited to Int32.'
  Assert-True ((Compare-FluentMapSemanticVersion '3.6.1-999999999999' '3.6.1-1000000000000') -lt 0) 'Large numeric prerelease identifiers must compare numerically.'
}

Invoke-Test 'Compatible fix after 3.6.1 recommends 3.6.2' {
  $result = Get-FluentMapVersionRecommendation -ChangeType Patch -PublishedVersion 3.6.1 -PreparedVersion 3.6.1
  Assert-True ($result.RecommendedVersion -eq '3.6.2') 'Expected 3.6.2.'
}

Invoke-Test 'Backward-compatible feature recommends 3.7.0' {
  $result = Get-FluentMapVersionRecommendation -ChangeType Minor -PublishedVersion 3.6.1 -PreparedVersion 3.6.1
  Assert-True ($result.RecommendedVersion -eq '3.7.0') 'Expected 3.7.0.'
}

Invoke-Test 'Internal refactor uses patch policy' {
  $result = Get-FluentMapVersionRecommendation -ChangeType Refactor -PublishedVersion 3.6.1 -PreparedVersion 3.6.1
  Assert-True ($result.RecommendedVersion -eq '3.6.2') 'A package-affecting refactor should recommend a patch.'
}

Invoke-Test 'Documentation or changelog-only change requires no release' {
  foreach ($description in @('documentation', 'changelog')) {
    $result = Get-FluentMapVersionRecommendation -ChangeType None -PublishedVersion 3.6.1 -PreparedVersion 3.6.1
    Assert-True ($result.Status -eq 'NoReleaseRequired') "$description-only changes must not increment the package version."
  }
}

Invoke-Test 'Potential breaking change blocks automatic versioning' {
  $result = Get-FluentMapVersionRecommendation -ChangeType Breaking -PublishedVersion 3.6.1 -PreparedVersion 3.6.1
  Assert-True ($result.Status -eq 'Blocked') 'Breaking change without authorization must be blocked.'
  Assert-True ($null -eq $result.RecommendedVersion) 'No automatic major may be returned.'
}

Invoke-Test 'Explicit major request still requires formal analysis' {
  $withoutAnalysis = Get-FluentMapVersionRecommendation -ChangeType Breaking -PublishedVersion 3.6.1 -PreparedVersion 3.6.1 -AllowMajor
  Assert-True ($withoutAnalysis.Status -eq 'ReviewRequired') 'Authorization alone must not bypass analysis.'

  $analysisPath = Join-Path ([System.IO.Path]::GetTempPath()) "breaking-$([System.Guid]::NewGuid().ToString('N')).md"
  try {
    Set-Content -LiteralPath $analysisPath -Value 'source compatibility; binary compatibility; behavior; target framework; dependency; migration'
    $withAnalysis = Get-FluentMapVersionRecommendation -ChangeType Breaking -PublishedVersion 3.6.1 -PreparedVersion 3.6.1 -AllowMajor -BreakingAnalysisPath $analysisPath
    Assert-True ($withAnalysis.RecommendedVersion -eq '4.0.0') 'Authorized, analyzed major should recommend 4.0.0.'
  }
  finally { Remove-Item -LiteralPath $analysisPath -Force -ErrorAction SilentlyContinue }
}

Invoke-Test 'Version already in preparation is idempotent' {
  $result = Get-FluentMapVersionRecommendation -ChangeType Patch -PublishedVersion 3.6.0 -PreparedVersion 3.6.1
  Assert-True ($result.RecommendedVersion -eq '3.6.1') 'Prepared 3.6.1 must be preserved.'
  Assert-True $result.PreparationPreserved 'The report must identify preparation reuse.'
}

Invoke-Test 'Prepared major cannot be reused without authorization and analysis' {
  $blocked = Get-FluentMapVersionRecommendation -ChangeType Patch -PublishedVersion 3.6.1 -PreparedVersion 4.0.0
  Assert-True ($blocked.Status -eq 'Blocked') 'An unapproved prepared major must be blocked even for a patch change.'

  $review = Get-FluentMapVersionRecommendation -ChangeType Minor -PublishedVersion 3.6.1 -PreparedVersion 4.0.0 -AllowMajor
  Assert-True ($review.Status -eq 'ReviewRequired') 'Major authorization without formal analysis must still require review.'
}

Invoke-Test 'Stable tag discovery ignores tags unreachable from HEAD' {
  $root = Join-Path ([System.IO.Path]::GetTempPath()) "semver-tags-$([System.Guid]::NewGuid().ToString('N'))"
  New-Item -ItemType Directory -Path $root | Out-Null
  try {
    & git -C $root init --initial-branch=main | Out-Null
    & git -C $root config user.email 'semver-tests@example.invalid'
    & git -C $root config user.name 'SemVer Tests'
    Set-Content -LiteralPath (Join-Path $root 'main.txt') -Value 'main'
    & git -C $root add main.txt
    & git -C $root commit -m 'main' | Out-Null
    & git -C $root tag v3.6.0
    & git -C $root switch --orphan unrelated | Out-Null
    Remove-Item -LiteralPath (Join-Path $root 'main.txt') -Force -ErrorAction SilentlyContinue
    Set-Content -LiteralPath (Join-Path $root 'unrelated.txt') -Value 'unrelated'
    & git -C $root add unrelated.txt
    & git -C $root commit -m 'unrelated' | Out-Null
    & git -C $root tag v9.0.0
    & git -C $root switch main | Out-Null

    $versions = @(Get-FluentMapStableTagVersions -SourceRoot $root)
    Assert-True ($versions.Count -eq 1 -and $versions[0] -eq '3.6.0') 'Only stable tags reachable from HEAD should be considered published history.'
  }
  finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}

Invoke-Test 'Downgrade and unapproved major are rejected' {
  $downgrade = Test-FluentMapVersionSelection -SelectedVersion 3.5.9 -PublishedVersion 3.6.0 -PreparedVersion 3.5.9
  Assert-True (-not $downgrade.IsValid) 'Downgrade must fail.'
  $major = Test-FluentMapVersionSelection -SelectedVersion 4.0.0 -PublishedVersion 3.6.1 -PreparedVersion 4.0.0
  Assert-True (-not $major.IsValid) 'Unapproved major must fail.'
}

Invoke-Test 'Version incompatible with classified change type is rejected' {
  $result = Test-FluentMapVersionSelection -SelectedVersion 3.6.2 -PublishedVersion 3.6.1 -PreparedVersion 3.6.2 -ChangeType Minor
  Assert-True (-not $result.IsValid) 'A patch version must not validate for a minor change.'
  Assert-True (@($result.Errors | Where-Object { $_ -like '*incompatible with change type*' }).Count -eq 1) 'The failure must explain the expected semantic version.'
}

Invoke-Test 'Prepared stable version permits a matching prerelease selection' {
  $result = Test-FluentMapVersionSelection -SelectedVersion 3.6.1-rc.1 -PublishedVersion 3.6.0 -PreparedVersion 3.6.1
  Assert-True $result.IsValid 'A prerelease with the prepared stable core must remain supported by the release workflow.'
}

Invoke-Test 'Existing release tag cannot be reused outside governed rerun' {
  $blocked = Test-FluentMapVersionSelection -SelectedVersion 3.6.1 -PublishedVersion 3.6.1 -PreparedVersion 3.6.1
  Assert-True (-not $blocked.IsValid) 'Existing version should fail by default.'
  $switchOnly = Test-FluentMapVersionSelection -SelectedVersion 3.6.1 -PublishedVersion 3.6.1 -PreparedVersion 3.6.1 -AllowExistingPublishedVersion
  Assert-True (-not $switchOnly.IsValid) 'The rerun switch alone must not bypass tag evidence.'
  $rerun = Test-FluentMapVersionSelection -SelectedVersion 3.6.1 -PublishedVersion 3.6.1 -PreparedVersion 3.6.1 -AllowExistingPublishedVersion -SelectedVersionHasTag
  Assert-True $rerun.IsValid 'Governed rerun should be allowed explicitly.'

  $olderRerun = Test-FluentMapVersionSelection -SelectedVersion 3.6.0 -PublishedVersion 3.6.1 -PreparedVersion 3.6.0 -AllowExistingPublishedVersion -SelectedVersionHasTag
  Assert-True $olderRerun.IsValid 'An immutable tagged release may be rerun after a newer release exists.'
}

Invoke-Test 'CLI validation succeeds after an absent release tag probe' {
  $root = Join-Path ([System.IO.Path]::GetTempPath()) "semver-cli-$([System.Guid]::NewGuid().ToString('N'))"
  New-Item -ItemType Directory -Path $root | Out-Null
  try {
    & git -C $root init --initial-branch=main | Out-Null
    $validator = Join-Path $PSScriptRoot 'validate-semantic-version.ps1'
    $runnerScript = Join-Path $root 'run-validator.ps1'
    @"
& '$validator' -Operation Validate -SelectedVersion 3.6.1 -PublishedVersion 3.6.0 -PreparedVersion 3.6.1 -SourceRoot '$root' -AllowExistingPublishedVersion
if (Test-Path -LiteralPath variable:LASTEXITCODE) { exit `$LASTEXITCODE }
"@ | Set-Content -LiteralPath $runnerScript

    # GitHub's pwsh runner propagates LASTEXITCODE from native commands at step end.
    # Probing a missing tag is expected, so successful validation must reset it.
    $output = @(& pwsh -NoProfile -File $runnerScript 2>&1)
    $exitCode = $LASTEXITCODE
    Assert-True ($exitCode -eq 0) "Successful validation must exit zero after the git tag probe. Output: $($output -join ' | ')"
    Assert-True (@($output | Where-Object { $_ -like '*Semantic version validation passed.*' }).Count -gt 0) 'Expected successful validation message.'
  }
  finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}

Invoke-Test 'Divergent package versions fail alignment validation' {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $root = Join-Path ([System.IO.Path]::GetTempPath()) "semver-packages-$([System.Guid]::NewGuid().ToString('N'))"
  $packages = Join-Path $root 'packages'
  New-Item -ItemType Directory -Path $packages | Out-Null
  try {
    $catalog = @{ packages = @(
      @{ packageId = 'A' }, @{ packageId = 'B' }, @{ packageId = 'C' }, @{ packageId = 'D' }, @{ packageId = 'E' }
    ) } | ConvertTo-Json -Depth 4
    $catalogPath = Join-Path $root 'catalog.json'
    Set-Content -LiteralPath $catalogPath -Value $catalog
    foreach ($id in @('A', 'B', 'C', 'D')) { New-TestPackage -Path (Join-Path $packages "$id.3.6.1.nupkg") -Id $id -Version 3.6.1 }
    New-TestPackage -Path (Join-Path $packages 'E.3.6.2.nupkg') -Id E -Version 3.6.2
    $result = Test-FluentMapPackageVersionAlignment -PackageDirectory $packages -ExpectedVersion 3.6.1 -CatalogPath $catalogPath
    Assert-True (-not $result.IsValid) 'Version divergence must fail.'
  }
  finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host "Semantic versioning tests: $script:Passed passed, $script:Failed failed."
if ($script:Failed -gt 0) { exit 1 }
