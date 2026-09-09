[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$PackageDirectory,

  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$Source,

  [Parameter(Mandatory = $true)]
  [string]$ApiKey,

  [ValidateSet('NuGetOrg', 'GitHubPackages')]
  [string]$Registry = 'NuGetOrg',

  [string]$RepositoryOwner,

  [string]$GitHubToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PackageCatalog.psm1') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Fail {
  param([string]$Message)
  throw "Package publication failed: $Message"
}

function Get-NuGetFlatContainerUrl {
  param(
    [string]$PackageId,
    [string]$PackageVersion
  )

  $idLower = $PackageId.ToLowerInvariant()
  $versionLower = $PackageVersion.ToLowerInvariant()
  return "https://api.nuget.org/v3-flatcontainer/$idLower/$versionLower/$idLower.$versionLower.nupkg"
}

function Get-HttpStatus {
  param([string]$Uri)

  $status = (& curl `
    --silent `
    --show-error `
    --location `
    --proto '=https' `
    --proto-redir '=https' `
    --retry 3 `
    --retry-delay 2 `
    --retry-all-errors `
    --connect-timeout 10 `
    --max-time 30 `
    --output /dev/null `
    --write-out '%{http_code}' `
    $Uri).Trim()

  if ($LASTEXITCODE -ne 0) {
    Fail "Unable to query '$Uri'."
  }

  return $status
}

function Test-IsRepositorySignatureEntry {
  param([string]$EntryName)

  $normalized = $EntryName.Replace('\', '/')
  return $normalized.Equals('[Content_Types].xml', [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalized.Equals('_rels/.rels', [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalized.Equals('.signature.p7s', [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalized.StartsWith('package/services/digital-signature/', [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-NuGetPackageEntryHashes {
  param([string]$PackagePath)

  $entries = [ordered]@{}
  $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
  try {
    foreach ($entry in @($archive.Entries | Sort-Object -Property FullName)) {
      if ([string]::IsNullOrEmpty($entry.Name) -or (Test-IsRepositorySignatureEntry -EntryName $entry.FullName)) {
        continue
      }

      if ($entries.Contains($entry.FullName)) {
        Fail "Package '$PackagePath' contains duplicate entry '$($entry.FullName)'."
      }

      $stream = $entry.Open()
      $sha256 = [System.Security.Cryptography.SHA256]::Create()
      try {
        $entries[$entry.FullName] = [System.Convert]::ToHexString($sha256.ComputeHash($stream)).ToLowerInvariant()
      }
      finally {
        $sha256.Dispose()
        $stream.Dispose()
      }
    }
  }
  finally {
    $archive.Dispose()
  }

  return $entries
}

function Assert-NuGetPackagesHaveSameContent {
  param(
    [string]$PackageId,
    [string]$LocalPackagePath,
    [string]$DownloadedPackagePath
  )

  $localEntries = Get-NuGetPackageEntryHashes -PackagePath $LocalPackagePath
  $remoteEntries = Get-NuGetPackageEntryHashes -PackagePath $DownloadedPackagePath

  $missingEntries = @($localEntries.Keys | Where-Object { -not $remoteEntries.Contains($_) })
  $unexpectedEntries = @($remoteEntries.Keys | Where-Object { -not $localEntries.Contains($_) })
  $changedEntries = @(
    foreach ($entryName in $localEntries.Keys) {
      if ($remoteEntries.Contains($entryName) -and $remoteEntries[$entryName] -ne $localEntries[$entryName]) {
        $entryName
      }
    }
  )

  if ($missingEntries.Count -gt 0 -or $unexpectedEntries.Count -gt 0 -or $changedEntries.Count -gt 0) {
    $details = @()
    if ($missingEntries.Count -gt 0) {
      $details += "missing entries: $($missingEntries -join ', ')"
    }

    if ($unexpectedEntries.Count -gt 0) {
      $details += "unexpected entries: $($unexpectedEntries -join ', ')"
    }

    if ($changedEntries.Count -gt 0) {
      $details += "changed entries: $($changedEntries -join ', ')"
    }

    Fail "NuGet.org already has $PackageId $Version, but the downloaded package content does not match the local artifact ($($details -join '; ')). Refusing to mask an artifact mismatch."
  }
}

function Assert-NuGetOrgPackageMatches {
  param(
    [string]$PackageId,
    [string]$LocalPackagePath
  )

  $url = Get-NuGetFlatContainerUrl -PackageId $PackageId -PackageVersion $Version
  $downloadPath = Join-Path ([System.IO.Path]::GetTempPath()) "$([System.Guid]::NewGuid()).nupkg"

  try {
    & curl `
      --fail-with-body `
      --silent `
      --show-error `
      --location `
      --proto '=https' `
      --proto-redir '=https' `
      --retry 3 `
      --retry-delay 2 `
      --retry-all-errors `
      --connect-timeout 10 `
      --max-time 60 `
      --output $downloadPath `
      $url

    if ($LASTEXITCODE -ne 0) {
      Fail "Unable to download existing NuGet.org package $PackageId $Version for artifact comparison."
    }

    Assert-NuGetPackagesHaveSameContent `
      -PackageId $PackageId `
      -LocalPackagePath $LocalPackagePath `
      -DownloadedPackagePath $downloadPath

    Write-Output "NuGet.org: validated existing $PackageId $Version against local artifact content while accounting for repository signatures; skipping push."
  }
  finally {
    if (Test-Path -LiteralPath $downloadPath) {
      Remove-Item -LiteralPath $downloadPath -Force
    }
  }
}

function Test-GitHubPackageVersionExists {
  param([string]$PackageId)

  if ([string]::IsNullOrWhiteSpace($RepositoryOwner) -or [string]::IsNullOrWhiteSpace($GitHubToken)) {
    Fail 'RepositoryOwner and GitHubToken are required to preflight GitHub Packages publication.'
  }

  $escapedPackageId = [System.Uri]::EscapeDataString($PackageId)
  $headers = @{
    Authorization = "Bearer $GitHubToken"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'Dapper-FluentMap-release-publish'
  }
  $page = 1

  while ($true) {
    $uri = "https://api.github.com/users/$RepositoryOwner/packages/nuget/$escapedPackageId/versions?per_page=100&page=$page"
    $response = Invoke-WebRequest -Uri $uri -Method Get -Headers $headers -SkipHttpErrorCheck
    $status = [int]$response.StatusCode

    if ($status -eq 404) {
      return $false
    }

    if ($status -ne 200) {
      Fail "GitHub Packages lookup for $PackageId returned HTTP $status."
    }

    $versions = @($response.Content | ConvertFrom-Json)
    if (@($versions | Where-Object { [string]$_.name -eq $Version }).Count -gt 0) {
      return $true
    }

    if ($versions.Count -lt 100) {
      return $false
    }

    $page++
  }
}

function Invoke-DotNetNuGetPush {
  param(
    [string]$PackageId,
    [string]$PackagePath
  )

  Write-Output "> dotnet nuget push $PackagePath --source $Source --api-key ***"
  $output = & dotnet nuget push $PackagePath `
    --source $Source `
    --api-key $ApiKey 2>&1
  $exitCode = $LASTEXITCODE

  $output | ForEach-Object { Write-Output $_ }

  if ($exitCode -ne 0) {
    Fail "$Registry publication failed for $PackageId $Version from '$PackagePath' with exit code $exitCode. The original dotnet nuget push output is shown above."
  }
}

function Wait-NuGetOrgPackageVisible {
  param(
    [string]$PackageId,
    [string]$PackagePath
  )

  $url = Get-NuGetFlatContainerUrl -PackageId $PackageId -PackageVersion $Version
  for ($attempt = 1; $attempt -le 12; $attempt++) {
    $status = Get-HttpStatus -Uri $url
    if ($status -eq '200') {
      Assert-NuGetOrgPackageMatches -PackageId $PackageId -LocalPackagePath $PackagePath
      return
    }

    if ($status -ne '404') {
      Fail "Unexpected NuGet.org response HTTP $status while waiting for $PackageId $Version."
    }

    Start-Sleep -Seconds 10
  }

  Fail "NuGet.org did not expose $PackageId $Version after publication within the retry window."
}

function Wait-GitHubPackageVisible {
  param([string]$PackageId)

  for ($attempt = 1; $attempt -le 12; $attempt++) {
    if (Test-GitHubPackageVersionExists -PackageId $PackageId) {
      Write-Output "GitHub Packages: verified $PackageId $Version after publication."
      return
    }

    Start-Sleep -Seconds 10
  }

  Fail "GitHub Packages did not expose $PackageId $Version after publication within the retry window."
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
  Fail "$Registry API key is empty."
}

$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packages = @(Get-FluentMapPackages)

foreach ($package in $packages) {
  $packageId = [string]$package.packageId
  $packageName = Get-FluentMapPackageFileName -Package $package -Version $Version
  $packagePath = Join-Path $packageRoot $packageName
  if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    Fail "Expected package artifact '$packageName' was not found in '$packageRoot'."
  }

  if ($Registry -eq 'NuGetOrg') {
    $status = Get-HttpStatus -Uri (Get-NuGetFlatContainerUrl -PackageId $packageId -PackageVersion $Version)
    switch ($status) {
      '200' {
        Assert-NuGetOrgPackageMatches -PackageId $packageId -LocalPackagePath $packagePath
        continue
      }
      '404' {
        Write-Output "NuGet.org: $packageId $Version is not currently published; publishing local artifact."
      }
      default {
        Fail "Unexpected NuGet.org response HTTP $status for $packageId $Version. Failing closed."
      }
    }
  }
  elseif (Test-GitHubPackageVersionExists -PackageId $packageId) {
    Write-Output "GitHub Packages: validated that $packageId $Version already exists; skipping push."
    continue
  }
  else {
    Write-Output "GitHub Packages: $packageId $Version is not currently published; publishing local artifact."
  }

  Invoke-DotNetNuGetPush -PackageId $packageId -PackagePath $packagePath
  if ($Registry -eq 'NuGetOrg') {
    Wait-NuGetOrgPackageVisible -PackageId $packageId -PackagePath $packagePath
  }
  else {
    Wait-GitHubPackageVisible -PackageId $packageId
  }
}

Write-Output "$Registry publication completed for $($packages.Count) package identities."
