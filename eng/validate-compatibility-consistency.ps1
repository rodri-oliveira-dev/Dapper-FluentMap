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
if ([string]$contract.schemaVersion -ne '1.0') {
  Fail "Unsupported manifest schemaVersion '$($contract.schemaVersion)'. Expected '1.0'."
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

foreach ($document in @('COMPATIBILITY.md', 'MIGRATION.md')) {
  $text = Read-RequiredText $document
  Assert-Contains $document $text "Dapper $packageRange" 'documented Dapper package range'
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

Write-Host "Compatibility contract is consistent: Dapper minimum $minimum, latest stable $latest, package range $packageRange."
