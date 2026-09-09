[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

function Add-TestFailure {
  param(
    [string]$Name,
    [string]$Message
  )

  $failures.Add("$Name`: $Message")
  Write-Error "$Name`: $Message" -ErrorAction Continue
}

function Invoke-Test {
  param(
    [string]$Name,
    [scriptblock]$Body
  )

  try {
    & $Body
    Write-Host "PASS $Name"
  }
  catch {
    Add-TestFailure -Name $Name -Message $_.Exception.Message
  }
}

function Assert-True {
  param(
    [bool]$Condition,
    [string]$Message
  )

  if (-not $Condition) {
    throw $Message
  }
}

function Assert-Contains {
  param(
    [string]$Text,
    [string]$Expected,
    [string]$Message
  )

  if ($Text.IndexOf($Expected, [System.StringComparison]::Ordinal) -lt 0) {
    throw $Message
  }
}

function New-ZipPackage {
  param(
    [string]$Path,
    [string]$EntryName,
    [string]$Content
  )

  Add-Type -AssemblyName System.IO.Compression
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive = [System.IO.Compression.ZipFile]::Open($Path, [System.IO.Compression.ZipArchiveMode]::Create)
  try {
    $entry = $archive.CreateEntry($EntryName)
    $stream = $entry.Open()
    try {
      $writer = [System.IO.StreamWriter]::new($stream)
      try {
        $writer.Write($Content)
      }
      finally {
        $writer.Dispose()
      }
    }
    finally {
      $stream.Dispose()
    }
  }
  finally {
    $archive.Dispose()
  }
}

function Invoke-PublishScenario {
  param(
    [array]$Packages,
    [hashtable]$StatusSequences,
    [string[]]$DifferentRemotePackages = @(),
    [int]$MaxAttempts = 3
  )

  $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "fluentmap-release-test-$([System.Guid]::NewGuid())"
  $oldMockStatusDir = $env:MOCK_STATUS_DIR
  $oldMockRemoteDir = $env:MOCK_REMOTE_DIR
  $oldMockPushLog = $env:MOCK_PUSH_LOG
  $oldMockEventLog = $env:MOCK_EVENT_LOG
  $oldCurlCommand = $env:FLUENTMAP_CURL_COMMAND
  $oldDotnetCommand = $env:FLUENTMAP_DOTNET_COMMAND
  $oldCurlAlias = Get-Alias curl -ErrorAction SilentlyContinue

  try {
    $packageDir = Join-Path $tempRoot 'packages'
    $remoteDir = Join-Path $tempRoot 'remote'
    $statusDir = Join-Path $tempRoot 'status'
    New-Item -ItemType Directory -Force -Path $packageDir, $remoteDir, $statusDir | Out-Null

    $version = '9.9.9-test.1'
    foreach ($package in $Packages) {
      $packageId = [string]$package.packageId
      $localPackage = Join-Path $packageDir "$packageId.$version.nupkg"
      New-ZipPackage -Path $localPackage -EntryName 'content.txt' -Content "local $packageId"

      $remotePackage = Join-Path $remoteDir "$($packageId.ToLowerInvariant()).nupkg"
      $remoteContent = if ($packageId -in $DifferentRemotePackages) { "different $packageId" } else { "local $packageId" }
      New-ZipPackage -Path $remotePackage -EntryName 'content.txt' -Content $remoteContent

      if ([bool]$package.symbols) {
        New-ZipPackage -Path (Join-Path $packageDir "$packageId.$version.snupkg") -EntryName 'symbols.pdb' -Content "symbols $packageId"
      }
    }

    $catalog = [ordered]@{
      schemaVersion = '1.0'
      packages = @($Packages)
    }
    $catalogPath = Join-Path $tempRoot 'package-catalog.json'
    $catalog | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $catalogPath -Encoding UTF8

    foreach ($packageId in $StatusSequences.Keys) {
      [System.IO.File]::WriteAllLines(
        (Join-Path $statusDir "$($packageId.ToLowerInvariant()).status"),
        [string[]]$StatusSequences[$packageId])
    }

    $pushLog = Join-Path $tempRoot 'push.log'
    $eventLog = Join-Path $tempRoot 'events.log'
    New-Item -ItemType File -Force -Path $pushLog, $eventLog | Out-Null

    $env:MOCK_STATUS_DIR = $statusDir
    $env:MOCK_REMOTE_DIR = $remoteDir
    $env:MOCK_PUSH_LOG = $pushLog
    $env:MOCK_EVENT_LOG = $eventLog
    $env:FLUENTMAP_CURL_COMMAND = 'mock-curl'
    $env:FLUENTMAP_DOTNET_COMMAND = 'mock-dotnet'
    Remove-Item alias:\curl -ErrorAction SilentlyContinue

    function global:mock-curl {
      $Arguments = $args
      $url = @($Arguments | Where-Object { $_ -match '^https?://' } | Select-Object -Last 1)
      if ($url.Count -ne 1 -or $url[0] -notmatch '/v3-flatcontainer/([^/]+)/([^/]+)/') {
        Write-Error "mock curl could not determine NuGet package id from arguments: $($Arguments -join ' ')"
        return
      }

      $packageId = $Matches[1].ToLowerInvariant()
      $statusPath = Join-Path $env:MOCK_STATUS_DIR "$packageId.status"
      if ($Arguments -contains '--write-out') {
        if (Test-Path -LiteralPath $statusPath) {
          $statuses = @(Get-Content -LiteralPath $statusPath)
        }
        else {
          $statuses = @('200')
        }

        $status = $statuses[0]
        if ($statuses.Count -gt 1) {
          [System.IO.File]::WriteAllLines($statusPath, [string[]]$statuses[1..($statuses.Count - 1)])
        }

        Add-Content -LiteralPath $env:MOCK_EVENT_LOG -Value "curl-status:$packageId`:$status"
        Write-Output $status
        $global:LASTEXITCODE = 0
        return
      }

      $outputIndex = [Array]::IndexOf($Arguments, '--output')
      if ($outputIndex -lt 0 -or $outputIndex -ge ($Arguments.Count - 1)) {
        Write-Error 'mock curl download was missing --output'
        $global:LASTEXITCODE = 2
        return
      }

      $outputPath = $Arguments[$outputIndex + 1]
      $remotePath = Join-Path $env:MOCK_REMOTE_DIR "$packageId.nupkg"
      if (-not (Test-Path -LiteralPath $remotePath -PathType Leaf)) {
        Write-Error "mock remote package does not exist: $remotePath"
        $global:LASTEXITCODE = 22
        return
      }

      Copy-Item -LiteralPath $remotePath -Destination $outputPath -Force
      Add-Content -LiteralPath $env:MOCK_EVENT_LOG -Value "curl-download:$packageId"
      $global:LASTEXITCODE = 0
    }

    function global:mock-dotnet {
      $Arguments = $args
      if ($Arguments.Count -lt 3 -or $Arguments[0] -ne 'nuget' -or $Arguments[1] -ne 'push') {
        Write-Error "mock dotnet only supports 'nuget push': $($Arguments -join ' ')"
        $global:LASTEXITCODE = 2
        return
      }

      $packagePath = $Arguments[2]
      $fileName = [System.IO.Path]::GetFileName($packagePath)
      Add-Content -LiteralPath $env:MOCK_PUSH_LOG -Value "$fileName|$($Arguments -join ' ')"
      Write-Output "mock accepted $fileName"
      $global:LASTEXITCODE = 0
    }

    $script = Join-Path $repoRoot 'eng/publish-package-set.ps1'
    $exitCode = 0
    $output = @(
      try {
        & $script `
          -PackageDirectory $packageDir `
          -Version $version `
          -Source 'https://api.nuget.org/v3/index.json' `
          -ApiKey 'test-key' `
          -Registry NuGetOrg `
          -CatalogPath $catalogPath `
          -NuGetConvergenceMaxAttempts $MaxAttempts `
          -NuGetConvergenceDelaySeconds 0 2>&1
      }
      catch {
        $exitCode = 1
        $_
      }
    )

    return [pscustomobject]@{
      Succeeded = $exitCode -eq 0
      Output = ($output | Out-String)
      PushLog = if (Test-Path -LiteralPath $pushLog) { @(Get-Content -LiteralPath $pushLog) } else { @() }
      EventLog = if (Test-Path -LiteralPath $eventLog) { @(Get-Content -LiteralPath $eventLog) } else { @() }
    }
  }
  finally {
    $env:MOCK_STATUS_DIR = $oldMockStatusDir
    $env:MOCK_REMOTE_DIR = $oldMockRemoteDir
    $env:MOCK_PUSH_LOG = $oldMockPushLog
    $env:MOCK_EVENT_LOG = $oldMockEventLog
    $env:FLUENTMAP_CURL_COMMAND = $oldCurlCommand
    $env:FLUENTMAP_DOTNET_COMMAND = $oldDotnetCommand
    Remove-Item function:\mock-curl -ErrorAction SilentlyContinue
    Remove-Item function:\mock-dotnet -ErrorAction SilentlyContinue
    if ($null -ne $oldCurlAlias) {
      Set-Alias -Name curl -Value $oldCurlAlias.Definition
    }
    if (Test-Path -LiteralPath $tempRoot) {
      Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
  }
}

function Invoke-ChecksumScenario {
  param([switch]$CorruptPackageHash)

  $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "fluentmap-checksum-test-$([System.Guid]::NewGuid())"
  try {
    $packageDir = Join-Path $tempRoot 'packages'
    $metadataDir = Join-Path $tempRoot 'release-metadata'
    New-Item -ItemType Directory -Force -Path $packageDir, $metadataDir | Out-Null

    $packagePath = Join-Path $packageDir 'A.1.2.3.nupkg'
    $manifestPath = Join-Path $metadataDir 'artifact-manifest.json'
    $dependenciesPath = Join-Path $metadataDir 'dependencies.json'
    New-ZipPackage -Path $packagePath -EntryName 'content.txt' -Content 'package'
    Set-Content -LiteralPath $manifestPath -Value '{"schemaVersion":"1.0"}' -Encoding UTF8
    Set-Content -LiteralPath $dependenciesPath -Value '{}' -Encoding UTF8

    $files = @(
      Get-Item -LiteralPath $packagePath
      Get-Item -LiteralPath $manifestPath
      Get-Item -LiteralPath $dependenciesPath
    )

    $lines = foreach ($file in $files) {
      $relative = $file.FullName.Substring($tempRoot.Length).TrimStart('\', '/').Replace('\', '/')
      $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
      if ($CorruptPackageHash -and $file.Extension -eq '.nupkg') {
        $hash = '0000000000000000000000000000000000000000000000000000000000000000'
      }

      "$hash  $relative"
    }

    Set-Content -LiteralPath (Join-Path $metadataDir 'SHA256SUMS') -Value $lines -Encoding ascii

    $exitCode = 0
    $output = @(
      try {
        & (Join-Path $repoRoot 'eng/verify-release-checksums.ps1') -ArtifactRoot $tempRoot 2>&1
      }
      catch {
        $exitCode = 1
        $_
      }
    )

    return [pscustomobject]@{
      Succeeded = $exitCode -eq 0
      Output = ($output | Out-String)
    }
  }
  finally {
    if (Test-Path -LiteralPath $tempRoot) {
      Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
  }
}

$releaseWorkflow = Get-Content -Raw -LiteralPath (Join-Path $repoRoot '.github/workflows/release.yml')
$recoveryWorkflow = Get-Content -Raw -LiteralPath (Join-Path $repoRoot '.github/workflows/release-recovery-missing-nuget.yml')

Invoke-Test 'release and recovery share a non-cancelling release lock' {
  Assert-Contains $releaseWorkflow 'group: release' 'normal release must use the shared release lock.'
  Assert-Contains $recoveryWorkflow 'group: release' 'recovery must use the shared release lock.'
  Assert-Contains $releaseWorkflow 'cancel-in-progress: false' 'normal release must not cancel in-progress release mutations.'
  Assert-Contains $recoveryWorkflow 'cancel-in-progress: false' 'recovery must not cancel in-progress release mutations.'
}

Invoke-Test 'recovery workflow reconciles governed release state' {
  foreach ($expected in @(
      'original_release_run_id',
      'gh run download',
      'Recover and verify NuGet.org packages',
      'Recover and verify GitHub Packages',
      'Restore or verify release tag',
      'Create or update GitHub Release',
      'Attest recovered release artifacts',
      'Release recovery completed and governed release state reconciled.'
    )) {
    Assert-Contains $recoveryWorkflow $expected "recovery workflow must contain '$expected'."
  }

  Assert-Contains $recoveryWorkflow 'Refusing to move the tag' 'recovery must fail closed when the release tag points to the wrong commit.'
  Assert-Contains $recoveryWorkflow 'source=rebuild' 'recovery must expose deterministic rebuild fallback when original artifacts are unavailable.'
  Assert-Contains $recoveryWorkflow 'headSha' 'recovery must validate that original artifacts came from the requested commit.'
}

Invoke-Test 'release checksum verification accepts a complete artifact set' {
  $result = Invoke-ChecksumScenario
  Assert-True $result.Succeeded "checksum verification failed: $($result.Output)"
}

Invoke-Test 'release checksum verification rejects artifact hash mismatch' {
  $result = Invoke-ChecksumScenario -CorruptPackageHash
  Assert-True (-not $result.Succeeded) 'checksum mismatch should fail.'
  Assert-Contains $result.Output "Checksum mismatch for 'packages/A.1.2.3.nupkg'" 'checksum mismatch diagnostic should identify the package artifact.'
}

$twoPackages = @(
  [ordered]@{ project = 'A'; projectPath = 'src/A/A.csproj'; packageId = 'A'; assetKind = 'library'; symbols = $true },
  [ordered]@{ project = 'B'; projectPath = 'src/B/B.csproj'; packageId = 'B'; assetKind = 'library'; symbols = $false }
)

Invoke-Test 'NuGet.org recovery publishes all missing primary packages and symbol packages' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('404', '200')
    B = @('404', '200')
  }

  Assert-True $result.Succeeded "publish script failed: $($result.Output)"
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'A.9.9.9-test.1.nupkg|*--no-symbols*' }).Count -eq 1) 'A primary package should be pushed with --no-symbols.'
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'B.9.9.9-test.1.nupkg|*--no-symbols*' }).Count -eq 1) 'B primary package should be pushed with --no-symbols.'
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'A.9.9.9-test.1.snupkg|*--skip-duplicate*' }).Count -eq 1) 'A symbol package should be pushed separately with duplicate-safe tooling semantics.'
}

Invoke-Test 'NuGet.org recovery validates and skips identical existing packages' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('200')
    B = @('404', '200')
  }

  Assert-True $result.Succeeded "publish script failed: $($result.Output)"
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'A.9.9.9-test.1.nupkg|*' }).Count -eq 0) 'Existing identical A primary package should not be pushed.'
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'B.9.9.9-test.1.nupkg|*' }).Count -eq 1) 'Missing B primary package should be pushed.'
}

Invoke-Test 'NuGet.org recovery fails closed on existing artifact mismatch' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('200')
    B = @('404', '200')
  } -DifferentRemotePackages @('A')

  Assert-True (-not $result.Succeeded) 'artifact mismatch should fail.'
  Assert-Contains $result.Output 'does not match the local artifact' 'artifact mismatch diagnostic should be precise.'
}

Invoke-Test 'NuGet.org async indexing does not block submission of remaining primary packages' {
  $fivePackages = @(
    [ordered]@{ project = 'A'; projectPath = 'src/A/A.csproj'; packageId = 'A'; assetKind = 'library'; symbols = $true },
    [ordered]@{ project = 'B'; projectPath = 'src/B/B.csproj'; packageId = 'B'; assetKind = 'library'; symbols = $false },
    [ordered]@{ project = 'C'; projectPath = 'src/C/C.csproj'; packageId = 'C'; assetKind = 'library'; symbols = $false },
    [ordered]@{ project = 'D'; projectPath = 'src/D/D.csproj'; packageId = 'D'; assetKind = 'library'; symbols = $false },
    [ordered]@{ project = 'E'; projectPath = 'src/E/E.csproj'; packageId = 'E'; assetKind = 'library'; symbols = $false }
  )

  $result = Invoke-PublishScenario -Packages $fivePackages -StatusSequences @{
    A = @('404', '404', '200')
    B = @('404', '200')
    C = @('404', '200')
    D = @('404', '200')
    E = @('404', '200')
  }

  Assert-True $result.Succeeded "publish script failed: $($result.Output)"
  foreach ($id in @('A', 'B', 'C', 'D', 'E')) {
    Assert-True (@($result.PushLog | Where-Object { $_ -like "$id.9.9.9-test.1.nupkg|*" }).Count -eq 1) "$id should be submitted before final convergence can fail or wait."
  }

  $firstDownloadIndex = [Array]::IndexOf([string[]]$result.EventLog, 'curl-download:a')
  Assert-True ($firstDownloadIndex -gt 0) 'Convergence download should occur only after publication status checks.'
  Assert-Contains $result.Output 'indexing still pending for A' 'pending indexing diagnostic should identify the package that has not converged.'
}

Invoke-Test 'NuGet.org final convergence times out with precise missing identities' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('404')
    B = @('404')
  } -MaxAttempts 2

  Assert-True (-not $result.Succeeded) 'convergence timeout should fail.'
  Assert-Contains $result.Output 'Still missing from the Flat Container: A, B' 'timeout should list the exact missing package IDs.'
}

Invoke-Test 'NuGet.org unexpected HTTP status fails closed' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('503')
    B = @('404', '200')
  }

  Assert-True (-not $result.Succeeded) 'unexpected HTTP status should fail.'
  Assert-Contains $result.Output 'Unexpected NuGet.org response HTTP 503 for A' 'unexpected status diagnostic should include package and status.'
}

Invoke-Test 'symbol-enabled package is not considered recovered solely by primary visibility' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('200')
    B = @('200')
  }

  Assert-True $result.Succeeded "publish script failed: $($result.Output)"
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'A.9.9.9-test.1.snupkg|*--skip-duplicate*' }).Count -eq 1) 'Visible primary package must still trigger independent symbol package recovery.'
  Assert-Contains $result.Output 'does not expose a public content-comparison endpoint for .snupkg artifacts' 'symbol limitation should be logged.'
}

if ($failures.Count -gt 0) {
  throw "Release governance tests failed:`n$($failures -join [Environment]::NewLine)"
}

Write-Host "Release governance tests passed."
