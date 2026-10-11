[CmdletBinding()]
param(
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
  [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail {
  param([string]$Message)
  throw "Compatibility consistency validation failed: $Message"
}

function Read-RequiredText {
  param([string]$RelativePath)

  $path = Join-Path $RepositoryRoot $RelativePath
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    Fail "Required file '$RelativePath' was not found."
  }

  return Get-Content -LiteralPath $path -Raw
}

function Assert-Contains {
  param(
    [string]$RelativePath,
    [string]$Text,
    [string]$Expected,
    [string]$ContractName
  )

  if ($Text.IndexOf($Expected, [System.StringComparison]::Ordinal) -lt 0) {
    Fail "$RelativePath is inconsistent for $ContractName. Expected to find '$Expected'."
  }
}

$manifestPath = Join-Path $RepositoryRoot 'eng/compatibility-contract.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
  Fail "The source-of-truth manifest 'eng/compatibility-contract.json' was not found."
}

$contract = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]$contract.schemaVersion -ne '2.0') {
  Fail "Unsupported manifest schemaVersion '$($contract.schemaVersion)'. Expected '2.0'."
}

$minimum = [string]$contract.dapper.minimumSupportedVersion
$latest = [string]$contract.dapper.latestStableVersion
$packageRange = [string]$contract.dapper.packageRange
$expectedRange = "[$minimum,3.0.0)"

if ($packageRange -ne $expectedRange) {
  Fail "eng/compatibility-contract.json declares Dapper packageRange '$packageRange', expected '$expectedRange' from minimumSupportedVersion."
}

$props = Read-RequiredText 'Directory.Build.props'
Assert-Contains 'Directory.Build.props' $props "<DapperMinimumSupportedVersion>$minimum</DapperMinimumSupportedVersion>" 'Dapper minimum supported version'
Assert-Contains 'Directory.Build.props' $props "<DapperLatestStableVersion>$latest</DapperLatestStableVersion>" 'Dapper latest stable version'
Assert-Contains 'Directory.Build.props' $props '<DapperPackageVersion Condition="''$(DapperPackageVersion)'' == ''''">[$(DapperMinimumSupportedVersion),3.0.0)</DapperPackageVersion>' 'Dapper package range derivation'

$ci = Read-RequiredText '.github/workflows/ci.yml'
Assert-Contains '.github/workflows/ci.yml' $ci "dapper-version: $minimum" 'Dapper minimum compatibility lane'
Assert-Contains '.github/workflows/ci.yml' $ci "dapper-version: $latest" 'Dapper latest-stable compatibility lane'

$requiredProviderIds = @('sqlite', 'sql-server', 'postgresql', 'mysql', 'mariadb', 'oracle', 'firebird', 'sql-server-ce')
$supportLevels = @('dapper-compatible', 'core-certified', 'core-dommel-certified', 'legacy-upstream-limited')
$coreEvidenceLevels = @('certified', 'not-certified', 'legacy')
$dommelEvidenceLevels = @('certified', 'not-certified', 'legacy')
$providers = @($contract.providerSupport.providers)

$actualProviderIds = @($providers | ForEach-Object { [string]$_.id })
if (@($actualProviderIds | Sort-Object -Unique).Count -ne $actualProviderIds.Count) {
  Fail 'eng/compatibility-contract.json contains duplicate provider ids.'
}

$missingProviderIds = @($requiredProviderIds | Where-Object { $actualProviderIds -notcontains $_ })
$unexpectedProviderIds = @($actualProviderIds | Where-Object { $requiredProviderIds -notcontains $_ })
if ($missingProviderIds.Count -gt 0 -or $unexpectedProviderIds.Count -gt 0) {
  Fail "provider inventory drifted. Missing: $($missingProviderIds -join ', '); unexpected: $($unexpectedProviderIds -join ', ')."
}

$declaredLevels = @($contract.providerSupport.levels | ForEach-Object { [string]$_ })
if ((($declaredLevels | Sort-Object) -join ',') -ne (($supportLevels | Sort-Object) -join ',')) {
  Fail "provider support levels must be exactly: $($supportLevels -join ', ')."
}

$documentationLabels = @{
  certified = 'FluentMap Core certified'
  'not-certified' = 'Not certified'
  legacy = 'Legacy/upstream-limited'
}
$dommelDocumentationLabels = @{
  certified = 'FluentMap + Dommel certified'
  'not-certified' = 'Not certified'
  legacy = 'Legacy/upstream-limited'
}

foreach ($provider in $providers) {
  $id = [string]$provider.id
  $supportLevel = [string]$provider.supportLevel
  $coreEvidence = [string]$provider.coreEvidence
  $dommelEvidence = [string]$provider.dommelEvidence

  if ($supportLevels -notcontains $supportLevel) {
    Fail "provider '$id' declares unknown supportLevel '$supportLevel'."
  }
  if ($coreEvidenceLevels -notcontains $coreEvidence) {
    Fail "provider '$id' declares unknown coreEvidence '$coreEvidence'."
  }
  if ($dommelEvidenceLevels -notcontains $dommelEvidence) {
    Fail "provider '$id' declares unknown dommelEvidence '$dommelEvidence'."
  }

  $expectedSupportLevel = if ($coreEvidence -eq 'legacy' -or $dommelEvidence -eq 'legacy') {
    'legacy-upstream-limited'
  } elseif ($dommelEvidence -eq 'certified') {
    'core-dommel-certified'
  } elseif ($coreEvidence -eq 'certified') {
    'core-certified'
  } else {
    'dapper-compatible'
  }
  if ($supportLevel -ne $expectedSupportLevel) {
    Fail "provider '$id' supportLevel '$supportLevel' conflicts with Core '$coreEvidence' and Dommel '$dommelEvidence' evidence; expected '$expectedSupportLevel'."
  }

  if ([bool]$provider.requiredCi -and [string]::IsNullOrWhiteSpace([string]$provider.ciFilter)) {
    Fail "provider '$id' is required in CI but has no ciFilter."
  }
  if ($coreEvidence -eq 'certified' -and (
      [string]::IsNullOrWhiteSpace([string]$provider.serverVersion) -or
      [string]::IsNullOrWhiteSpace([string]$provider.clientPackage) -or
      [string]::IsNullOrWhiteSpace([string]$provider.clientVersion))) {
    Fail "provider '$id' is Core certified but does not pin serverVersion, clientPackage and clientVersion."
  }
}

$providerLoops = [Regex]::Matches($ci, 'for provider in (?<providers>[^\r\n]+); do')
if ($providerLoops.Count -eq 0) {
  Fail '.github/workflows/ci.yml does not expose the required provider execution loop.'
}
$ciProviders = @(
  $providerLoops |
    ForEach-Object { [Regex]::Matches($_.Groups['providers'].Value, '"(?<name>[^"]+)"') } |
    ForEach-Object { $_.Groups['name'].Value }
)
$contractCiProviders = @(
  $providers |
    Where-Object { [bool]$_.requiredCi } |
    ForEach-Object { [string]$_.ciFilter }
)
if ((($ciProviders | Sort-Object) -join ',') -ne (($contractCiProviders | Sort-Object) -join ',')) {
  Fail ".github/workflows/ci.yml provider loop '$($ciProviders -join ', ')' differs from required contract '$($contractCiProviders -join ', ')'."
}

foreach ($document in @('COMPATIBILITY.md', 'MIGRATION.md')) {
  $text = Read-RequiredText $document
  Assert-Contains $document $text "Dapper $packageRange" 'documented Dapper package range'
}

$compatibility = Read-RequiredText 'COMPATIBILITY.md'
foreach ($provider in $providers) {
  $dapperLabel = if ([string]$provider.supportLevel -eq 'legacy-upstream-limited') { 'Legacy/upstream-limited' } else { 'Dapper-compatible' }
  $expectedRow = "| $($provider.displayName) | $dapperLabel | $($documentationLabels[[string]$provider.coreEvidence]) | $($dommelDocumentationLabels[[string]$provider.dommelEvidence]) |"
  Assert-Contains 'COMPATIBILITY.md' $compatibility $expectedRow "provider '$($provider.id)' support row"
}

if (-not [string]::IsNullOrWhiteSpace($PackageDirectory)) {
  if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    Fail "Package directory '$PackageDirectory' does not exist."
  }

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $packagesToValidate = @('Dapper.FluentMap', 'Dapper.FluentMap.Dommel')
  foreach ($packageId in $packagesToValidate) {
    $packagePattern = '^' + [Regex]::Escape($packageId) + '\.\d.*\.nupkg$'
    $matches = @(
      Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg' |
        Where-Object { $_.Name -match $packagePattern }
    )
    if ($matches.Count -ne 1) {
      Fail "Expected exactly one package for '$packageId' in '$PackageDirectory', found $($matches.Count)."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($matches[0].FullName)
    try {
      $nuspecEntry = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
      if ($nuspecEntry.Count -ne 1) {
        Fail "$($matches[0].Name) must contain exactly one nuspec, found $($nuspecEntry.Count)."
      }

      $stream = $nuspecEntry[0].Open()
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

      $dependency = $nuspec.SelectSingleNode("//*[local-name()='dependency' and @id='Dapper']")
      if ($null -eq $dependency) {
        Fail "$($matches[0].Name) does not declare its required Dapper dependency."
      }

      $actualRange = [string]$dependency.GetAttribute('version')
      if ($actualRange.Replace(' ', '') -ne $packageRange) {
        Fail "$($matches[0].Name) declares Dapper range '$actualRange', expected '$packageRange'."
      }
    }
    finally {
      $archive.Dispose()
    }
  }
}

Write-Host "Compatibility contract is consistent: Dapper minimum $minimum, latest stable $latest, package range $packageRange, providers $($actualProviderIds -join ', ')."
