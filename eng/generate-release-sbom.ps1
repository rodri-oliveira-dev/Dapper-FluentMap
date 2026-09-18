[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$PackageDirectory,

  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$OutputPath,

  [Parameter(Mandatory = $true)]
  [string]$Repository,

  [Parameter(Mandatory = $true)]
  [string]$Commit,

  [string]$CatalogPath,

  [string]$CreatedUtc,

  [switch]$PreserveExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PackageCatalog.psm1') -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Fail {
  param([string]$Message)
  throw "Release SBOM generation failed: $Message"
}

function Get-Sha256 {
  param([string]$Path)
  return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-StableSpdxId {
  param(
    [string]$Prefix,
    [string]$Value
  )

  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $hash = $sha.ComputeHash($bytes)
    $suffix = ([System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()).Substring(0, 16)
    return "$Prefix-$suffix"
  }
  finally {
    $sha.Dispose()
  }
}

function Get-NuspecMetadata {
  param([string]$PackagePath)

  $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
  try {
    $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
    if ($nuspecEntries.Count -ne 1) {
      Fail "Expected exactly one nuspec in '$PackagePath', found $($nuspecEntries.Count)."
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
  }
  finally {
    $archive.Dispose()
  }

  $metadata = $nuspec.SelectSingleNode('//*[local-name()="metadata"]')
  if ($null -eq $metadata) {
    Fail "Package '$PackagePath' does not contain nuspec metadata."
  }

  $idNode = $metadata.ChildNodes | Where-Object { $_.LocalName -eq 'id' } | Select-Object -First 1
  $versionNode = $metadata.ChildNodes | Where-Object { $_.LocalName -eq 'version' } | Select-Object -First 1
  if ($null -eq $idNode -or $null -eq $versionNode) {
    Fail "Package '$PackagePath' is missing nuspec id or version."
  }

  $licenseNode = $metadata.ChildNodes | Where-Object { $_.LocalName -eq 'license' } | Select-Object -First 1
  $licenseExpression = 'NOASSERTION'
  if ($null -ne $licenseNode -and
      $licenseNode.GetAttribute('type') -eq 'expression' -and
      -not [string]::IsNullOrWhiteSpace($licenseNode.InnerText)) {
    $licenseExpression = $licenseNode.InnerText.Trim()
  }

  $dependencies = @(
    $nuspec.SelectNodes('//*[local-name()="dependency"]') |
      ForEach-Object {
        [pscustomobject]@{
          Id = $_.GetAttribute('id')
          Version = $_.GetAttribute('version')
        }
      } |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_.Id) } |
      Sort-Object Id, Version -Unique
  )

  return [pscustomobject]@{
    Id = $idNode.InnerText.Trim()
    Version = $versionNode.InnerText.Trim()
    LicenseExpression = $licenseExpression
    Dependencies = $dependencies
  }
}

function ConvertTo-Purl {
  param(
    [string]$PackageId,
    [string]$PackageVersion
  )

  $encodedId = [System.Uri]::EscapeDataString($PackageId)
  $encodedVersion = [System.Uri]::EscapeDataString($PackageVersion)
  return "pkg:nuget/$encodedId@$encodedVersion"
}

function Normalize-CreatedTimestamp {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) {
    return [System.DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
  }

  $parsed = [System.DateTimeOffset]::Parse(
    $Value,
    [System.Globalization.CultureInfo]::InvariantCulture,
    [System.Globalization.DateTimeStyles]::AssumeUniversal)

  return $parsed.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

$catalogPackages = @(Get-FluentMapPackages -CatalogPath $CatalogPath)
if ($catalogPackages.Count -eq 0) {
  Fail 'Package catalog is empty.'
}

$packageDirectoryFull = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packageFiles = @(Get-ChildItem -LiteralPath $packageDirectoryFull -File -Filter '*.nupkg')
if ($packageFiles.Count -ne $catalogPackages.Count) {
  Fail "Expected $($catalogPackages.Count) primary .nupkg files, found $($packageFiles.Count)."
}

$releaseIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$releaseSpdxIds = @{}
foreach ($package in $catalogPackages) {
  $packageId = [string]$package.packageId
  [void]$releaseIds.Add($packageId)
  $releaseSpdxIds[$packageId] = Get-StableSpdxId -Prefix 'SPDXRef-ReleasePackage' -Value "$packageId|$Version"
}

$releasePackages = [System.Collections.Generic.List[object]]::new()
$dependencyPackages = @{}
$relationshipMap = @{}
$documentDescribes = [System.Collections.Generic.List[string]]::new()
$expectedHashes = @{}

foreach ($package in $catalogPackages) {
  $packageId = [string]$package.packageId
  $packagePath = Join-Path $packageDirectoryFull "$packageId.$Version.nupkg"
  if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    Fail "Expected package '$packageId.$Version.nupkg' was not found."
  }

  $metadata = Get-NuspecMetadata -PackagePath $packagePath
  if ($metadata.Id -ne $packageId) {
    Fail "Package '$packagePath' has nuspec id '$($metadata.Id)', expected '$packageId'."
  }

  if ($metadata.Version -ne $Version) {
    Fail "Package '$packagePath' has version '$($metadata.Version)', expected '$Version'."
  }

  $packageHash = Get-Sha256 -Path $packagePath
  $expectedHashes[$packageId] = $packageHash
  $spdxId = [string]$releaseSpdxIds[$packageId]
  $documentDescribes.Add($spdxId)

  $releasePackages.Add([ordered]@{
      name = $packageId
      SPDXID = $spdxId
      versionInfo = $Version
      downloadLocation = 'NOASSERTION'
      filesAnalyzed = $false
      licenseConcluded = 'NOASSERTION'
      licenseDeclared = $metadata.LicenseExpression
      copyrightText = 'NOASSERTION'
      checksums = @(
        [ordered]@{
          algorithm = 'SHA256'
          checksumValue = $packageHash
        }
      )
      externalRefs = @(
        [ordered]@{
          referenceCategory = 'PACKAGE-MANAGER'
          referenceType = 'purl'
          referenceLocator = ConvertTo-Purl -PackageId $packageId -PackageVersion $Version
        }
      )
    })

  foreach ($dependency in @($metadata.Dependencies)) {
    $dependencyId = [string]$dependency.Id
    $dependencyVersion = [string]$dependency.Version

    if ($releaseIds.Contains($dependencyId)) {
      $relatedSpdxId = [string]$releaseSpdxIds[$dependencyId]
    }
    else {
      $dependencyKey = "$dependencyId|$dependencyVersion"
      if (-not $dependencyPackages.ContainsKey($dependencyKey)) {
        $dependencySpdxId = Get-StableSpdxId -Prefix 'SPDXRef-Dependency' -Value $dependencyKey
        $dependencyPackage = [ordered]@{
          name = $dependencyId
          SPDXID = $dependencySpdxId
          versionInfo = if ([string]::IsNullOrWhiteSpace($dependencyVersion)) { 'NOASSERTION' } else { $dependencyVersion }
          downloadLocation = 'NOASSERTION'
          filesAnalyzed = $false
          licenseConcluded = 'NOASSERTION'
          licenseDeclared = 'NOASSERTION'
          copyrightText = 'NOASSERTION'
        }

        if ($dependencyVersion -match '^[0-9]+(\.[0-9A-Za-z-]+)+([+-][0-9A-Za-z.-]+)?$') {
          $dependencyPackage['externalRefs'] = @(
            [ordered]@{
              referenceCategory = 'PACKAGE-MANAGER'
              referenceType = 'purl'
              referenceLocator = ConvertTo-Purl -PackageId $dependencyId -PackageVersion $dependencyVersion
            }
          )
        }

        $dependencyPackages[$dependencyKey] = $dependencyPackage
      }

      $relatedSpdxId = [string]$dependencyPackages[$dependencyKey].SPDXID
    }

    $relationshipKey = "$spdxId|DEPENDS_ON|$relatedSpdxId"
    if (-not $relationshipMap.ContainsKey($relationshipKey)) {
      $relationshipMap[$relationshipKey] = [ordered]@{
        spdxElementId = $spdxId
        relationshipType = 'DEPENDS_ON'
        relatedSpdxElement = $relatedSpdxId
      }
    }
  }
}

$allPackages = @(
  @($releasePackages)
  @($dependencyPackages.Values | Sort-Object @{ Expression = { [string]$_.name } }, @{ Expression = { [string]$_.versionInfo } })
)

$relationships = @(
  $relationshipMap.Values | Sort-Object @{ Expression = { [string]$_.spdxElementId } }, @{ Expression = { [string]$_.relatedSpdxElement } }
)

$created = Normalize-CreatedTimestamp -Value $CreatedUtc
$namespaceVersion = [System.Uri]::EscapeDataString($Version)
$documentNamespace = "https://github.com/$Repository/releases/sbom/$namespaceVersion/$Commit"

$sbom = [ordered]@{
  spdxVersion = 'SPDX-2.3'
  dataLicense = 'CC0-1.0'
  SPDXID = 'SPDXRef-DOCUMENT'
  name = "Dapper-FluentMap-$Version-release-sbom"
  documentNamespace = $documentNamespace
  creationInfo = [ordered]@{
    created = $created
    creators = @('Tool: Dapper-FluentMap eng/generate-release-sbom.ps1')
  }
  documentDescribes = @($documentDescribes)
  packages = $allPackages
  relationships = $relationships
}

function Assert-SbomMatchesRelease {
  param([object]$Document)

  if ([string]$Document.spdxVersion -ne 'SPDX-2.3') {
    Fail "SBOM '$OutputPath' must use SPDX-2.3."
  }

  if ([string]$Document.documentNamespace -ne $documentNamespace) {
    Fail "SBOM '$OutputPath' document namespace does not match release identity."
  }

  $described = @($Document.documentDescribes)
  foreach ($package in $catalogPackages) {
    $packageId = [string]$package.packageId
    $matches = @(
      $Document.packages |
        Where-Object {
          [string]$_.name -eq $packageId -and
          [string]$_.versionInfo -eq $Version
        }
    )

    if ($matches.Count -ne 1) {
      Fail "SBOM '$OutputPath' must describe exactly one '$packageId' package at version '$Version'."
    }

    $rootPackage = $matches[0]
    if ([string]$rootPackage.SPDXID -notin $described) {
      Fail "SBOM '$OutputPath' does not list '$packageId' in documentDescribes."
    }

    $sha256 = @(
      $rootPackage.checksums |
        Where-Object { [string]$_.algorithm -eq 'SHA256' } |
        ForEach-Object { [string]$_.checksumValue }
    )

    if ($sha256.Count -ne 1 -or $sha256[0].ToLowerInvariant() -ne [string]$expectedHashes[$packageId]) {
      Fail "SBOM '$OutputPath' SHA256 for '$packageId' does not match the final .nupkg."
    }
  }
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
if ($PreserveExisting -and (Test-Path -LiteralPath $outputFullPath -PathType Leaf)) {
  $existing = Get-Content -Raw -LiteralPath $outputFullPath | ConvertFrom-Json
  Assert-SbomMatchesRelease -Document $existing
  Write-Host "Preserved and validated existing release SBOM '$outputFullPath'."
  return
}

$outputDirectory = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
  New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$json = $sbom | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText(
  $outputFullPath,
  $json + [Environment]::NewLine,
  [System.Text.UTF8Encoding]::new($false))

$roundTrip = Get-Content -Raw -LiteralPath $outputFullPath | ConvertFrom-Json
Assert-SbomMatchesRelease -Document $roundTrip

Write-Host "Generated SPDX 2.3 release SBOM '$outputFullPath' for $($catalogPackages.Count) NuGet packages."
