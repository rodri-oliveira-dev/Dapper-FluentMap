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

  [string]$GitHubToken,

  [string]$CatalogPath,

  [int]$NuGetConvergenceMaxAttempts = 60,

  [int]$NuGetConvergenceDelaySeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PackageCatalog.psm1') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem

$curlCommand = if ([string]::IsNullOrWhiteSpace($env:FLUENTMAP_CURL_COMMAND)) { 'curl' } else { $env:FLUENTMAP_CURL_COMMAND }
$dotnetCommand = if ([string]::IsNullOrWhiteSpace($env:FLUENTMAP_DOTNET_COMMAND)) { 'dotnet' } else { $env:FLUENTMAP_DOTNET_COMMAND }

function Fail {
  param([string]$Message)
  throw "Package publication failed: $Message"
}

function Convert-BytesToLowerHex {
  param([byte[]]$Bytes)

  return ([System.BitConverter]::ToString($Bytes) -replace '-', '').ToLowerInvariant()
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

function Get-NuGetPackageContentUrl {
  param(
    [string]$PackageBaseAddress,
    [string]$PackageId,
    [string]$PackageVersion
  )

  $baseAddress = $PackageBaseAddress.TrimEnd('/')
  $idLower = $PackageId.ToLowerInvariant()
  $versionLower = $PackageVersion.ToLowerInvariant()
  return "$baseAddress/$idLower/$versionLower/$idLower.$versionLower.nupkg"
}

function Get-HttpStatus {
  param([string]$Uri)

  $status = (& $curlCommand `
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

function Invoke-CurlDownload {
  param(
    [string]$Uri,
    [string]$OutputPath,
    [switch]$Authenticated
  )

  $arguments = @(
    '--fail-with-body'
    '--silent'
    '--show-error'
    '--location'
    '--proto'
    '=https'
    '--proto-redir'
    '=https'
    '--retry'
    '3'
    '--retry-delay'
    '2'
    '--retry-all-errors'
    '--connect-timeout'
    '10'
    '--max-time'
    '60'
    '--output'
    $OutputPath
  )

  if ($Authenticated) {
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
      Fail 'GitHubToken is required to download existing GitHub Packages artifacts for content comparison.'
    }

    $arguments += @('--header', "Authorization: Bearer $GitHubToken")
  }

  $arguments += $Uri
  & $curlCommand @arguments
}

function Get-AuthenticatedNuGetPackageBaseAddress {
  if ([string]::IsNullOrWhiteSpace($Source)) {
    Fail 'Package source is required to resolve the authenticated NuGet package content endpoint.'
  }

  $serviceIndexPath = Join-Path ([System.IO.Path]::GetTempPath()) "$([System.Guid]::NewGuid()).json"
  try {
    Invoke-CurlDownload -Uri $Source -OutputPath $serviceIndexPath -Authenticated
    if ($LASTEXITCODE -ne 0) {
      Fail "Unable to download NuGet service index '$Source'."
    }

    $serviceIndex = Get-Content -LiteralPath $serviceIndexPath -Raw | ConvertFrom-Json
    $resource = @($serviceIndex.resources | Where-Object { [string]$_.'@type' -eq 'PackageBaseAddress/3.0.0' } | Select-Object -First 1)
    if ($resource.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$resource[0].'@id')) {
      Fail "NuGet service index '$Source' does not expose a PackageBaseAddress/3.0.0 resource for content comparison."
    }

    return [string]$resource[0].'@id'
  }
  finally {
    if (Test-Path -LiteralPath $serviceIndexPath) {
      Remove-Item -LiteralPath $serviceIndexPath -Force
    }
  }
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
        $entries[$entry.FullName] = Convert-BytesToLowerHex -Bytes $sha256.ComputeHash($stream)
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
    [string]$DownloadedPackagePath,
    [string]$RegistryLabel = $Registry
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

    Fail "$RegistryLabel already has $PackageId $Version, but the downloaded package content does not match the local artifact ($($details -join '; ')). Refusing to mask an artifact mismatch."
  }
}

function Assert-RemotePackageMatches {
  param(
    [string]$PackageId,
    [string]$LocalPackagePath,
    [string]$PackageUrl,
    [string]$RegistryLabel,
    [switch]$Authenticated
  )

  $downloadPath = Join-Path ([System.IO.Path]::GetTempPath()) "$([System.Guid]::NewGuid()).nupkg"

  try {
    Invoke-CurlDownload -Uri $PackageUrl -OutputPath $downloadPath -Authenticated:$Authenticated

    if ($LASTEXITCODE -ne 0) {
      Fail "Unable to download existing $RegistryLabel package $PackageId $Version for artifact comparison."
    }

    Assert-NuGetPackagesHaveSameContent `
      -PackageId $PackageId `
      -LocalPackagePath $LocalPackagePath `
      -DownloadedPackagePath $downloadPath `
      -RegistryLabel $RegistryLabel

    Write-Output "$RegistryLabel`: validated existing $PackageId $Version against local artifact content while accounting for repository signatures; skipping push."
  }
  finally {
    if (Test-Path -LiteralPath $downloadPath) {
      Remove-Item -LiteralPath $downloadPath -Force
    }
  }
}

function Assert-NuGetOrgPackageMatches {
  param(
    [string]$PackageId,
    [string]$LocalPackagePath
  )

  $url = Get-NuGetFlatContainerUrl -PackageId $PackageId -PackageVersion $Version
  Assert-RemotePackageMatches -PackageId $PackageId -LocalPackagePath $LocalPackagePath -PackageUrl $url -RegistryLabel 'NuGet.org'
}

function Assert-GitHubPackageMatches {
  param(
    [string]$PackageId,
    [string]$LocalPackagePath,
    [string]$PackageBaseAddress
  )

  $url = Get-NuGetPackageContentUrl -PackageBaseAddress $PackageBaseAddress -PackageId $PackageId -PackageVersion $Version
  Assert-RemotePackageMatches -PackageId $PackageId -LocalPackagePath $LocalPackagePath -PackageUrl $url -RegistryLabel 'GitHub Packages' -Authenticated
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
    [string]$PackagePath,
    [string[]]$AdditionalArguments = @()
  )

  $maskedArguments = if ($AdditionalArguments.Count -gt 0) { " $($AdditionalArguments -join ' ')" } else { '' }
  Write-Output "> dotnet nuget push $PackagePath --source $Source --api-key ***$maskedArguments"
  $arguments = @(
    'nuget'
    'push'
    $PackagePath
    '--source'
    $Source
    '--api-key'
    $ApiKey
  ) + $AdditionalArguments
  $output = & $dotnetCommand @arguments 2>&1
  $exitCode = $LASTEXITCODE

  $output | ForEach-Object { Write-Output $_ }

  if ($exitCode -ne 0) {
    Fail "$Registry publication failed for $PackageId $Version from '$PackagePath' with exit code $exitCode. The original dotnet nuget push output is shown above."
  }
}

function Wait-NuGetOrgPackageSetConverged {
  param(
    [array]$ExpectedPackages,
    [hashtable]$PackagePaths
  )

  $validated = @{}

  for ($attempt = 1; $attempt -le $NuGetConvergenceMaxAttempts; $attempt++) {
    $missing = @()

    foreach ($package in $ExpectedPackages) {
      $packageId = [string]$package.packageId
      if ($validated.ContainsKey($packageId)) {
        continue
      }

      $status = Get-HttpStatus -Uri (Get-NuGetFlatContainerUrl -PackageId $packageId -PackageVersion $Version)
      switch ($status) {
        '200' {
          Assert-NuGetOrgPackageMatches -PackageId $packageId -LocalPackagePath $PackagePaths[$packageId]
          $validated[$packageId] = $true
        }
        '404' {
          $missing += $packageId
        }
        default {
          Fail "Unexpected NuGet.org response HTTP $status while verifying convergence for $packageId $Version."
        }
      }
    }

    if ($validated.Count -eq $ExpectedPackages.Count) {
      Write-Output "NuGet.org: all $($ExpectedPackages.Count) primary package identities converged and match the expected artifacts."
      return
    }

    $missingList = $missing -join ', '
    if ($attempt -lt $NuGetConvergenceMaxAttempts) {
      Write-Output "NuGet.org: indexing still pending for $missingList ($attempt/$NuGetConvergenceMaxAttempts); retrying in $NuGetConvergenceDelaySeconds seconds."
      Start-Sleep -Seconds $NuGetConvergenceDelaySeconds
    }
  }

  $notConverged = @(
    foreach ($package in $ExpectedPackages) {
      $packageId = [string]$package.packageId
      if (-not $validated.ContainsKey($packageId)) {
        $packageId
      }
    }
  )

  Fail "NuGet.org primary package convergence timed out for $Version. Still missing from the Flat Container: $($notConverged -join ', ')."
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
$packages = @(Get-FluentMapPackages -CatalogPath $CatalogPath)
$symbolPackages = @(Get-FluentMapPackages -CatalogPath $CatalogPath -WithSymbols)

if ($Registry -eq 'NuGetOrg') {
  $packagePaths = @{}
  $missingPackages = @()
  $existingPackageCount = 0

  foreach ($package in $packages) {
    $packageId = [string]$package.packageId
    $packageName = Get-FluentMapPackageFileName -Package $package -Version $Version
    $packagePath = Join-Path $packageRoot $packageName
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
      Fail "Expected package artifact '$packageName' was not found in '$packageRoot'."
    }

    $packagePaths[$packageId] = $packagePath
    $status = Get-HttpStatus -Uri (Get-NuGetFlatContainerUrl -PackageId $packageId -PackageVersion $Version)
    switch ($status) {
      '200' {
        Assert-NuGetOrgPackageMatches -PackageId $packageId -LocalPackagePath $packagePath
        $existingPackageCount++
      }
      '404' {
        Write-Output "NuGet.org: $packageId $Version is not currently published; local artifact will be submitted after existing package content preflight completes."
        $missingPackages += $package
      }
      default {
        Fail "Unexpected NuGet.org response HTTP $status for $packageId $Version. Failing closed."
      }
    }
  }

  Write-Output "NuGet.org: preflight validated $existingPackageCount existing primary package identity or identities before publishing $($missingPackages.Count) missing identity or identities."

  foreach ($package in $missingPackages) {
    $packageId = [string]$package.packageId
    $packagePath = $packagePaths[$packageId]
    Write-Output "NuGet.org: submitting missing primary package $packageId $Version without waiting for indexing."
    Invoke-DotNetNuGetPush -PackageId $packageId -PackagePath $packagePath -AdditionalArguments @('--no-symbols')
    Write-Output "NuGet.org: accepted primary package $packageId $Version; validation and indexing continue asynchronously."
  }

  Wait-NuGetOrgPackageSetConverged -ExpectedPackages $packages -PackagePaths $packagePaths

  foreach ($package in $symbolPackages) {
    $packageId = [string]$package.packageId
    $symbolName = Get-FluentMapPackageFileName -Package $package -Version $Version -Kind symbols
    $symbolPath = Join-Path $packageRoot $symbolName
    if (-not (Test-Path -LiteralPath $symbolPath -PathType Leaf)) {
      Fail "Expected symbol package artifact '$symbolName' was not found in '$packageRoot'."
    }

    Write-Output "NuGet.org: submitting symbol package $symbolName. NuGet.org does not expose a public content-comparison endpoint for .snupkg artifacts; duplicate symbol submissions are accepted only through NuGet tooling semantics."
    Invoke-DotNetNuGetPush -PackageId "$packageId symbols" -PackagePath $symbolPath -AdditionalArguments @('--skip-duplicate')
  }

  Write-Output "NuGet.org publication completed for $($packages.Count) primary package identities and $($symbolPackages.Count) symbol package identities. Primary packages were converged and content-validated; symbol packages were submitted through NuGet.org V3 tooling."
  return
}

$githubPackageBaseAddress = Get-AuthenticatedNuGetPackageBaseAddress

foreach ($package in $packages) {
  $packageId = [string]$package.packageId
  $packageName = Get-FluentMapPackageFileName -Package $package -Version $Version
  $packagePath = Join-Path $packageRoot $packageName
  if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    Fail "Expected package artifact '$packageName' was not found in '$packageRoot'."
  }

  if (Test-GitHubPackageVersionExists -PackageId $packageId) {
    Assert-GitHubPackageMatches -PackageId $packageId -LocalPackagePath $packagePath -PackageBaseAddress $githubPackageBaseAddress
    continue
  }
  else {
    Write-Output "GitHub Packages: $packageId $Version is not currently published; publishing local artifact."
  }

  Invoke-DotNetNuGetPush -PackageId $packageId -PackagePath $packagePath
  Wait-GitHubPackageVisible -PackageId $packageId
}

Write-Output "$Registry publication completed for $($packages.Count) package identities."
