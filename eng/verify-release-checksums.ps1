[CmdletBinding()]
param(
  [string]$ArtifactRoot = './artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail {
  param([string]$Message)
  throw "Release checksum verification failed: $Message"
}

function Get-ArtifactRelativePath {
  param(
    [string]$RootPath,
    [string]$FullPath
  )

  $root = $RootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
  $rootUri = [System.Uri]::new($root)
  $fileUri = [System.Uri]::new($FullPath)
  return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fileUri).ToString()).Replace('\', '/')
}

if (-not (Test-Path -LiteralPath $ArtifactRoot -PathType Container)) {
  Fail "Artifact root '$ArtifactRoot' does not exist."
}

$artifactRootFullPath = (Resolve-Path -LiteralPath $ArtifactRoot).Path
$packageDirectory = Join-Path $artifactRootFullPath 'packages'
if (-not (Test-Path -LiteralPath $packageDirectory -PathType Container)) {
  Fail "Package directory '$packageDirectory' does not exist."
}

$checksumPath = Join-Path $artifactRootFullPath 'release-metadata/SHA256SUMS'
if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
  Fail "Checksum file '$checksumPath' does not exist."
}

$requiredMetadataFiles = @(
  Join-Path $artifactRootFullPath 'release-metadata/artifact-manifest.json'
  Join-Path $artifactRootFullPath 'release-metadata/dependencies.json'
)

foreach ($requiredMetadataFile in $requiredMetadataFiles) {
  if (-not (Test-Path -LiteralPath $requiredMetadataFile -PathType Leaf)) {
    Fail "Required release metadata file '$requiredMetadataFile' does not exist."
  }
}

$expectedFiles = @(
  Get-ChildItem -LiteralPath $packageDirectory -File |
    Where-Object { $_.Extension -in @('.nupkg', '.snupkg') }
  $requiredMetadataFiles | ForEach-Object { Get-Item -LiteralPath $_ }
)

$expectedRelativePaths = @(
  $expectedFiles |
    ForEach-Object { Get-ArtifactRelativePath -RootPath $artifactRootFullPath -FullPath $_.FullName }
)

$seen = @{}
$lineNumber = 0
foreach ($line in Get-Content -LiteralPath $checksumPath) {
  $lineNumber++
  if ([string]::IsNullOrWhiteSpace($line)) {
    continue
  }

  if ($line -notmatch '^([0-9a-f]{64})  ([^\\].*)$') {
    Fail "Invalid SHA256SUMS entry at line $lineNumber."
  }

  $expectedHash = $Matches[1]
  $relativePath = $Matches[2]
  if ([System.IO.Path]::IsPathRooted($relativePath) -or $relativePath.Contains('..')) {
    Fail "Checksum entry '$relativePath' is not a safe artifact-relative path."
  }

  $fullPath = [System.IO.Path]::GetFullPath((Join-Path $artifactRootFullPath $relativePath))
  if (-not $fullPath.StartsWith($artifactRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Fail "Checksum entry '$relativePath' resolves outside the artifact root."
  }

  if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
    Fail "Checksum entry '$relativePath' references a missing file."
  }

  $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($actualHash -ne $expectedHash) {
    Fail "Checksum mismatch for '$relativePath'."
  }

  $seen[$relativePath] = $true
}

$missingChecksumEntries = @($expectedRelativePaths | Where-Object { -not $seen.ContainsKey($_) })
if ($missingChecksumEntries.Count -gt 0) {
  Fail "Expected release artifacts are missing from SHA256SUMS: $($missingChecksumEntries -join ', ')."
}

Write-Host "Verified SHA256SUMS for $($seen.Count) release artifact file(s) under '$artifactRootFullPath'."
