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
    [int]$MaxAttempts = 3,
    [ValidateSet('NuGetOrg', 'GitHubPackages')]
    [string]$Registry = 'NuGetOrg'
  )

  $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "fluentmap-release-test-$([System.Guid]::NewGuid())"
  $oldMockStatusDir = $env:MOCK_STATUS_DIR
  $oldMockRemoteDir = $env:MOCK_REMOTE_DIR
  $oldMockPushLog = $env:MOCK_PUSH_LOG
  $oldMockEventLog = $env:MOCK_EVENT_LOG
  $oldMockPackageVersion = $env:MOCK_PACKAGE_VERSION
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
    $env:MOCK_PACKAGE_VERSION = $version
    $env:FLUENTMAP_CURL_COMMAND = 'mock-curl'
    $env:FLUENTMAP_DOTNET_COMMAND = 'mock-dotnet'
    Remove-Item alias:\curl -ErrorAction SilentlyContinue

    function global:mock-curl {
      $Arguments = $args
      $url = @($Arguments | Where-Object { $_ -match '^https?://' } | Select-Object -Last 1)
      if ($url.Count -ne 1) {
        Write-Error "mock curl could not determine URL from arguments: $($Arguments -join ' ')"
        return
      }

      if ($Arguments -contains '--write-out') {
        if ($url[0] -notmatch '/v3-flatcontainer/([^/]+)/([^/]+)/') {
          Write-Error "mock curl could not determine NuGet package id from status URL: $($Arguments -join ' ')"
          return
        }

        $packageId = $Matches[1].ToLowerInvariant()
        $statusPath = Join-Path $env:MOCK_STATUS_DIR "$packageId.status"
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
      if ($url[0] -match '/index\.json$') {
        $serviceIndex = @{
          version = '3.0.0'
          resources = @(
            @{
              '@id' = 'https://nuget.pkg.github.com/test-owner/download'
              '@type' = 'PackageBaseAddress/3.0.0'
            }
          )
        } | ConvertTo-Json -Depth 5
        Set-Content -LiteralPath $outputPath -Value $serviceIndex -Encoding UTF8
        Add-Content -LiteralPath $env:MOCK_EVENT_LOG -Value 'curl-index'
        $global:LASTEXITCODE = 0
        return
      }

      if ($url[0] -notmatch '/(?:v3-flatcontainer|download)/([^/]+)/([^/]+)/') {
        Write-Error "mock curl could not determine NuGet package id from download URL: $($Arguments -join ' ')"
        $global:LASTEXITCODE = 2
        return
      }

      $packageId = $Matches[1].ToLowerInvariant()
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

    function global:Invoke-WebRequest {
      param(
        [string]$Uri,
        [string]$Method,
        [hashtable]$Headers,
        [switch]$SkipHttpErrorCheck
      )

      if ($Uri -notmatch '/packages/nuget/([^/]+)/versions') {
        throw "mock Invoke-WebRequest only supports GitHub package version lookup: $Uri"
      }

      $packageId = [System.Uri]::UnescapeDataString($Matches[1]).ToLowerInvariant()
      $statusPath = Join-Path $env:MOCK_STATUS_DIR "$packageId.status"
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

      Add-Content -LiteralPath $env:MOCK_EVENT_LOG -Value "github-api:$packageId`:$status"
      switch ($status) {
        '200' {
          return [pscustomobject]@{
            StatusCode = 200
            Content = (@(@{ name = $env:MOCK_PACKAGE_VERSION }) | ConvertTo-Json -Depth 3)
          }
        }
        '404' {
          return [pscustomobject]@{
            StatusCode = 404
            Content = ''
          }
        }
        'empty' {
          return [pscustomobject]@{
            StatusCode = 200
            Content = '[]'
          }
        }
        default {
          return [pscustomobject]@{
            StatusCode = [int]$status
            Content = ''
          }
        }
      }
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
    $source = if ($Registry -eq 'GitHubPackages') { 'https://nuget.pkg.github.com/test-owner/index.json' } else { 'https://api.nuget.org/v3/index.json' }
    $publishArguments = @{
      PackageDirectory = $packageDir
      Version = $version
      Source = $source
      ApiKey = 'test-key'
      Registry = $Registry
      CatalogPath = $catalogPath
      NuGetConvergenceMaxAttempts = $MaxAttempts
      NuGetConvergenceDelaySeconds = 0
    }

    if ($Registry -eq 'GitHubPackages') {
      $publishArguments.RepositoryOwner = 'test-owner'
      $publishArguments.GitHubToken = 'test-token'
    }

    $exitCode = 0
    $output = @(
      try {
        & $script @publishArguments 2>&1
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
    $env:MOCK_PACKAGE_VERSION = $oldMockPackageVersion
    $env:FLUENTMAP_CURL_COMMAND = $oldCurlCommand
    $env:FLUENTMAP_DOTNET_COMMAND = $oldDotnetCommand
    Remove-Item function:\mock-curl -ErrorAction SilentlyContinue
    Remove-Item function:\mock-dotnet -ErrorAction SilentlyContinue
    Remove-Item function:\Invoke-WebRequest -ErrorAction SilentlyContinue
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

function Invoke-GitTestCommand {
  param(
    [string]$WorkingDirectory,
    [string[]]$Arguments
  )

  $stderrPath = [System.IO.Path]::GetTempFileName()
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    $output = & git -C $WorkingDirectory @Arguments 2> $stderrPath
    $exitCode = $LASTEXITCODE
    $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
  }

  if ($exitCode -ne 0) {
    throw "git -C $WorkingDirectory $($Arguments -join ' ') failed: $stderr"
  }

  return @($output)
}

function Invoke-ResolveSourceScenario {
  param(
    [ValidateSet('branch', 'tag')]
    [string]$SourceType,
    [string]$SourceRef,
    [string]$Version,
    [hashtable]$Tags = @{},
    [switch]$AddDevelopBranch,
    [switch]$SkipPushMaster,
    [string]$CurrentWorkflowRef = 'refs/heads/master'
  )

  $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "fluentmap-source-test-$([System.Guid]::NewGuid())"
  try {
    $remote = Join-Path $tempRoot 'remote.git'
    $work = Join-Path $tempRoot 'work'
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

    & git init --bare $remote *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize bare remote repository.' }

    & git init $work *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize working repository.' }

    Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('config', 'user.email', 'release-tests@example.invalid') | Out-Null
    Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('config', 'user.name', 'Release Tests') | Out-Null
    Set-Content -LiteralPath (Join-Path $work 'README.md') -Value 'release source test' -Encoding utf8
    Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('add', 'README.md') | Out-Null
    Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('commit', '-m', 'initial') | Out-Null
    Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('branch', '-M', 'master') | Out-Null
    Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('remote', 'add', 'origin', $remote) | Out-Null
    $masterCommit = (Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1).Trim()

    if (-not $SkipPushMaster) {
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('push', '-u', 'origin', 'master') | Out-Null
    }

    if ($AddDevelopBranch) {
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('checkout', '-b', 'develop') | Out-Null
      Set-Content -LiteralPath (Join-Path $work 'develop.txt') -Value 'develop' -Encoding utf8
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('add', 'develop.txt') | Out-Null
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('commit', '-m', 'develop') | Out-Null
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('push', '-u', 'origin', 'develop') | Out-Null
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('checkout', 'master') | Out-Null
    }

    foreach ($tagName in $Tags.Keys) {
      if ($Tags[$tagName] -eq 'annotated') {
        Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('tag', '-a', [string]$tagName, '-m', "Release $tagName", $masterCommit) | Out-Null
      }
      else {
        Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('tag', [string]$tagName, $masterCommit) | Out-Null
      }
    }

    if ($Tags.Count -gt 0) {
      Invoke-GitTestCommand -WorkingDirectory $work -Arguments @('push', 'origin', '--tags') | Out-Null
    }

    $outputPath = Join-Path $tempRoot 'github-output.txt'
    $script = Join-Path $repoRoot 'eng/resolve-release-source.ps1'
    $exitCode = 0
    $output = @(
      try {
        Push-Location $work
        try {
          & $script `
            -SourceType $SourceType `
            -SourceRef $SourceRef `
            -Version $Version `
            -CurrentWorkflowRef $CurrentWorkflowRef `
            -GitHubOutputPath $outputPath 2>&1
        }
        finally {
          Pop-Location
        }
      }
      catch {
        $exitCode = 1
        $_
      }
    )

    $outputs = @{}
    if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
      foreach ($line in @(Get-Content -LiteralPath $outputPath)) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
          $outputs[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
      }
    }

    return [pscustomobject]@{
      Succeeded = $exitCode -eq 0
      Output = ($output | Out-String)
      Outputs = $outputs
      MasterCommit = $masterCommit
    }
  }
  finally {
    if (Test-Path -LiteralPath $tempRoot) {
      Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
  }
}

function New-TestNuGetPackage {
  param(
    [string]$Path,
    [string]$PackageId,
    [string]$Version,
    [string]$Commit,
    [string]$Branch
  )

  Add-Type -AssemblyName System.IO.Compression
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $entries = [ordered]@{
    "$PackageId.nuspec" = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$PackageId</id>
    <version>$Version</version>
    <authors>Release Tests</authors>
    <description>Release test package.</description>
    <projectUrl>https://github.com/rodri-oliveira-dev/Dapper-FluentMap</projectUrl>
    <license type="expression">MIT</license>
    <readme>README.md</readme>
    <icon>package-icon.png</icon>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <repository type="git" url="https://github.com/rodri-oliveira-dev/Dapper-FluentMap" commit="$Commit" branch="$Branch" />
  </metadata>
</package>
"@
    'README.md' = 'readme'
    'package-icon.png' = 'icon'
    "lib/netstandard2.0/$PackageId.dll" = 'dll'
    "lib/netstandard2.0/$PackageId.xml" = '<doc />'
  }

  $archive = [System.IO.Compression.ZipFile]::Open($Path, [System.IO.Compression.ZipArchiveMode]::Create)
  try {
    foreach ($entryName in $entries.Keys) {
      $entry = $archive.CreateEntry($entryName)
      $stream = $entry.Open()
      try {
        $writer = [System.IO.StreamWriter]::new($stream)
        try {
          $writer.Write([string]$entries[$entryName])
        }
        finally {
          $writer.Dispose()
        }
      }
      finally {
        $stream.Dispose()
      }
    }
  }
  finally {
    $archive.Dispose()
  }
}

function Invoke-ReleaseArtifactValidationScenario {
  param(
    [string]$ExpectedVersion = '3.1.2',
    [string]$ArtifactVersion = '3.1.2',
    [string]$ExpectedCommit = '1111111111111111111111111111111111111111',
    [string]$ArtifactCommit = '1111111111111111111111111111111111111111',
    [string]$Branch = 'refs/heads/master'
  )

  $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "fluentmap-artifact-test-$([System.Guid]::NewGuid())"
  try {
    $packageDir = Join-Path $tempRoot 'packages'
    $metadataDir = Join-Path $tempRoot 'release-metadata'
    $sourceRoot = Join-Path $tempRoot 'source'
    $projectDir = Join-Path $sourceRoot 'src/A'
    New-Item -ItemType Directory -Force -Path $packageDir, $metadataDir, $projectDir | Out-Null

    $catalogPath = Join-Path $sourceRoot 'eng/package-catalog.json'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $catalogPath) | Out-Null
    [ordered]@{
      schemaVersion = '1.0'
      packages = @(
        [ordered]@{ project = 'A'; projectPath = 'src/A/A.csproj'; packageId = 'A'; assetKind = 'library'; symbols = $false }
      )
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $catalogPath -Encoding UTF8

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectDir 'A.csproj') -Encoding UTF8

    $packagePath = Join-Path $packageDir "A.$ArtifactVersion.nupkg"
    New-TestNuGetPackage -Path $packagePath -PackageId 'A' -Version $ArtifactVersion -Commit $ArtifactCommit -Branch $Branch
    $packageFile = Get-Item -LiteralPath $packagePath
    $manifestPath = Join-Path $metadataDir 'artifact-manifest.json'
    [ordered]@{
      schemaVersion = '1.0'
      version = $ArtifactVersion
      repository = 'rodri-oliveira-dev/Dapper-FluentMap'
      repositoryUrl = 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap'
      commit = $ArtifactCommit
      branch = $Branch
      packages = @(
        [ordered]@{
          file = $packageFile.Name
          packageId = 'A'
          version = $ArtifactVersion
          sha256 = (Get-FileHash -LiteralPath $packageFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
          kind = 'package'
          size = $packageFile.Length
        }
      )
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $exitCode = 0
    $output = @(
      try {
        & (Join-Path $repoRoot 'eng/validate-release-artifacts.ps1') `
          -PackageDirectory $packageDir `
          -Version $ExpectedVersion `
          -ManifestPath $manifestPath `
          -Repository 'rodri-oliveira-dev/Dapper-FluentMap' `
          -RepositoryUrl 'https://github.com/rodri-oliveira-dev/Dapper-FluentMap' `
          -Commit $ExpectedCommit `
          -Branch $Branch `
          -CatalogPath $catalogPath `
          -SourceRoot $sourceRoot `
          -VerifyExistingManifest 2>&1
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
      'source_type',
      'source_ref',
      'Resolve release source',
      'steps.source.outputs.resolved_version',
      'steps.source.outputs.resolved_commit',
      'original_release_run_id',
      'gh run download',
      'Recover and verify NuGet.org packages',
      'Recover and verify GitHub Packages',
      'Restore or verify release tag',
      'Create or update GitHub Release',
      'gh release delete-asset',
      'gh release edit',
      '--draft=false',
      '--prerelease=false',
      'recovery-attestation.json',
      'validatedCommit',
      'GitHub Release metadata and assets: exact governed state',
      'Attest recovered release artifacts',
      'Release recovery completed and governed release state reconciled.'
    )) {
    Assert-Contains $recoveryWorkflow $expected "recovery workflow must contain '$expected'."
  }

  Assert-Contains $recoveryWorkflow 'Refusing to move the tag' 'recovery must fail closed when the release tag points to the wrong commit.'
  Assert-Contains $recoveryWorkflow 'asset set does not match governed artifacts' 'recovery must fail closed when GitHub Release assets differ from governed artifacts.'
  Assert-Contains $recoveryWorkflow 'metadata does not match governed state' 'recovery must fail closed when GitHub Release metadata differs from governed state.'
  Assert-Contains $recoveryWorkflow 'source=rebuild' 'recovery must expose deterministic rebuild fallback when original artifacts are unavailable.'
  Assert-Contains $recoveryWorkflow 'headSha' 'recovery must validate that original artifacts came from the requested commit.'
  Assert-True ($recoveryWorkflow.IndexOf('validated_commit:', [System.StringComparison]::Ordinal) -lt 0) 'validated_commit must not remain an operator-facing workflow input.'
}

Invoke-Test 'branch source resolves valid master version' {
  $result = Invoke-ResolveSourceScenario -SourceType branch -SourceRef master -Version '3.1.2'
  Assert-True $result.Succeeded "source resolution failed: $($result.Output)"
  Assert-True ($result.Outputs['source_type'] -eq 'branch') 'source_type output should be branch.'
  Assert-True ($result.Outputs['resolved_version'] -eq '3.1.2') 'branch mode should use the explicit version.'
  Assert-True ($result.Outputs['resolved_commit'] -eq $result.MasterCommit) 'branch mode should resolve origin/master HEAD.'
  Assert-True ($result.Outputs['resolved_ref'] -eq 'refs/heads/master') 'branch mode should normalize the branch ref.'
  Assert-True ($result.Outputs['resolved_branch'] -eq 'master') 'branch mode should expose the resolved branch.'
}

Invoke-Test 'branch source requires version' {
  $result = Invoke-ResolveSourceScenario -SourceType branch -SourceRef master -Version ''
  Assert-True (-not $result.Succeeded) 'branch mode without version should fail.'
  Assert-Contains $result.Output 'Release source resolution failed' 'missing branch version diagnostic should come from source resolution.'
}

Invoke-Test 'branch source rejects invalid semantic version' {
  $result = Invoke-ResolveSourceScenario -SourceType branch -SourceRef master -Version '3.1'
  Assert-True (-not $result.Succeeded) 'invalid branch SemVer should fail.'
  Assert-Contains $result.Output 'Invalid semantic version' 'invalid SemVer diagnostic should be clear.'
}

Invoke-Test 'branch source rejects disallowed branch' {
  $result = Invoke-ResolveSourceScenario -SourceType branch -SourceRef develop -Version '3.1.2' -AddDevelopBranch
  Assert-True (-not $result.Succeeded) 'non-master branch source should fail.'
  Assert-Contains $result.Output 'Release source resolution failed' 'disallowed branch diagnostic should come from source resolution.'
}

Invoke-Test 'branch source rejects missing branch' {
  $result = Invoke-ResolveSourceScenario -SourceType branch -SourceRef master -Version '3.1.2' -SkipPushMaster
  Assert-True (-not $result.Succeeded) 'recovery started outside master should fail closed.'
  Assert-Contains $result.Output "Branch 'master' does not exist" 'missing branch diagnostic should be clear.'
}

Invoke-Test 'tag source derives version from release tag' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.1.2' -Tags @{ 'v3.1.2' = 'lightweight' }
  Assert-True $result.Succeeded "tag source resolution failed: $($result.Output)"
  Assert-True ($result.Outputs['source_type'] -eq 'tag') 'source_type output should be tag.'
  Assert-True ($result.Outputs['resolved_version'] -eq '3.1.2') 'tag mode should derive the package version by removing only the leading v.'
  Assert-True ($result.Outputs['resolved_commit'] -eq $result.MasterCommit) 'tag mode should peel the tag to the target commit.'
  Assert-True ($result.Outputs['resolved_ref'] -eq 'refs/tags/v3.1.2') 'tag mode should normalize the tag ref.'
  Assert-True ($result.Outputs['resolved_tag'] -eq 'v3.1.2') 'tag mode should expose the resolved tag.'
}

Invoke-Test 'tag source supports prerelease tags' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.2.0-rc.1' -Tags @{ 'v3.2.0-rc.1' = 'lightweight' }
  Assert-True $result.Succeeded "prerelease tag source resolution failed: $($result.Output)"
  Assert-True ($result.Outputs['resolved_version'] -eq '3.2.0-rc.1') 'prerelease tag should derive the prerelease package version.'
}

Invoke-Test 'tag source peels annotated tag to commit' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.2.0-rc.1' -Tags @{ 'v3.2.0-rc.1' = 'annotated' }
  Assert-True $result.Succeeded "annotated tag source resolution failed: $($result.Output)"
  Assert-True ($result.Outputs['resolved_commit'] -eq $result.MasterCommit) 'annotated tag should peel to the underlying commit, not the tag object.'
}

Invoke-Test 'tag source peels lightweight tag to commit' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.1.2' -Tags @{ 'v3.1.2' = 'lightweight' }
  Assert-True $result.Succeeded "lightweight tag source resolution failed: $($result.Output)"
  Assert-True ($result.Outputs['resolved_commit'] -eq $result.MasterCommit) 'lightweight tag should resolve to its target commit.'
}

Invoke-Test 'tag source rejects missing tag' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.1.2'
  Assert-True (-not $result.Succeeded) 'missing tag should fail.'
  Assert-Contains $result.Output "does not exist on remote" 'missing tag diagnostic should be clear.'
}

Invoke-Test 'tag source rejects malformed tag' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.1' -Tags @{ 'v3.1' = 'lightweight' }
  Assert-True (-not $result.Succeeded) 'malformed tag should fail.'
  Assert-Contains $result.Output 'Invalid semantic version' 'malformed tag diagnostic should validate the derived version.'
}

Invoke-Test 'tag source requires v prefix' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef '3.1.2' -Tags @{ '3.1.2' = 'lightweight' }
  Assert-True (-not $result.Succeeded) 'tag without v prefix should fail.'
  Assert-Contains $result.Output 'v<SemVer>' 'missing v prefix diagnostic should be clear.'
}

Invoke-Test 'tag source rejects invalid release version' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v2.9.9' -Tags @{ 'v2.9.9' = 'lightweight' }
  Assert-True (-not $result.Succeeded) 'tag below the release major floor should fail.'
  Assert-Contains $result.Output 'major version 3 or later' 'major-version diagnostic should be preserved.'
}

Invoke-Test 'tag source keeps v3.0.0 immutable' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.0.0' -Tags @{ 'v3.0.0' = 'lightweight' }
  Assert-True (-not $result.Succeeded) 'v3.0.0 tag recovery should fail.'
  Assert-Contains $result.Output '3.0.0 is immutable' 'immutable 3.0.0 diagnostic should be preserved.'
}

Invoke-Test 'tag source rejects explicit version input' {
  $result = Invoke-ResolveSourceScenario -SourceType tag -SourceRef 'v3.1.2' -Version '3.1.3' -Tags @{ 'v3.1.2' = 'lightweight' }
  Assert-True (-not $result.Succeeded) 'tag mode with explicit version should fail.'
  Assert-Contains $result.Output 'Release source resolution failed' 'tag-mode version diagnostic should come from source resolution.'
}

Invoke-Test 'release artifact validation rejects tag-derived version mismatch' {
  $result = Invoke-ReleaseArtifactValidationScenario -ExpectedVersion '3.1.2' -ArtifactVersion '3.2.0'
  Assert-True (-not $result.Succeeded) 'artifact version mismatch should fail.'
  Assert-Contains $result.Output 'A.3.1.2.nupkg' 'artifact version mismatch diagnostic should include the expected versioned package file.'
}

Invoke-Test 'release artifact validation rejects resolved commit mismatch' {
  $result = Invoke-ReleaseArtifactValidationScenario `
    -ExpectedCommit '1111111111111111111111111111111111111111' `
    -ArtifactCommit '2222222222222222222222222222222222222222'
  Assert-True (-not $result.Succeeded) 'artifact commit mismatch should fail.'
}

Invoke-Test 'original release artifact can match resolved tag identity' {
  $result = Invoke-ReleaseArtifactValidationScenario `
    -ExpectedVersion '3.1.2' `
    -ArtifactVersion '3.1.2' `
    -ExpectedCommit '1111111111111111111111111111111111111111' `
    -ArtifactCommit '1111111111111111111111111111111111111111'
  Assert-True $result.Succeeded "matching tag-derived artifact identity should pass: $($result.Output)"
}

Invoke-Test 'original release artifact can match resolved branch identity' {
  $result = Invoke-ReleaseArtifactValidationScenario `
    -ExpectedVersion '3.1.3' `
    -ArtifactVersion '3.1.3' `
    -ExpectedCommit '3333333333333333333333333333333333333333' `
    -ArtifactCommit '3333333333333333333333333333333333333333'
  Assert-True $result.Succeeded "matching branch-derived artifact identity should pass: $($result.Output)"
}

Invoke-Test 'release checksum verification accepts a complete artifact set' {
  $result = Invoke-ChecksumScenario
  Assert-True $result.Succeeded "checksum verification failed: $($result.Output)"
}

Invoke-Test 'release checksum verification rejects artifact hash mismatch' {
  $result = Invoke-ChecksumScenario -CorruptPackageHash
  Assert-True (-not $result.Succeeded) 'checksum mismatch should fail.'
  Assert-Contains $result.Output 'Checksum mismatch for' 'checksum mismatch diagnostic should explain the failure.'
  Assert-Contains $result.Output 'A.1.2.3.nupkg' 'checksum mismatch diagnostic should identify the package artifact.'
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

Invoke-Test 'NuGet.org recovery validates all existing packages before first push' {
  $result = Invoke-PublishScenario -Packages $twoPackages -StatusSequences @{
    A = @('404')
    B = @('200')
  } -DifferentRemotePackages @('B')

  Assert-True (-not $result.Succeeded) 'later existing package mismatch should fail before publication.'
  Assert-Contains $result.Output 'does not match the local artifact' 'artifact mismatch diagnostic should be precise.'
  Assert-True (@($result.PushLog).Count -eq 0) 'No package should be pushed before every existing package has passed content preflight.'
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

Invoke-Test 'GitHub Packages recovery validates and skips identical existing packages' {
  $result = Invoke-PublishScenario -Packages $twoPackages -Registry GitHubPackages -StatusSequences @{
    A = @('200')
    B = @('404', '200')
  }

  Assert-True $result.Succeeded "GitHub Packages recovery failed: $($result.Output)"
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'A.9.9.9-test.1.nupkg|*' }).Count -eq 0) 'Existing identical GitHub Packages artifact should not be pushed.'
  Assert-True (@($result.PushLog | Where-Object { $_ -like 'B.9.9.9-test.1.nupkg|*' }).Count -eq 1) 'Missing GitHub Packages artifact should be pushed.'
  Assert-Contains $result.Output 'GitHub Packages: validated existing A 9.9.9-test.1 against local artifact content' 'existing GitHub Packages artifact should be content-compared.'
}

Invoke-Test 'GitHub Packages recovery fails closed on existing artifact mismatch' {
  $result = Invoke-PublishScenario -Packages $twoPackages -Registry GitHubPackages -StatusSequences @{
    A = @('200')
    B = @('404', '200')
  } -DifferentRemotePackages @('A')

  Assert-True (-not $result.Succeeded) 'GitHub Packages artifact mismatch should fail.'
  Assert-Contains $result.Output 'does not match the local artifact' 'GitHub Packages mismatch diagnostic should be precise.'
  Assert-True (@($result.PushLog).Count -eq 0) 'GitHub Packages recovery should not publish missing packages after an existing package mismatch.'
}

if ($failures.Count -gt 0) {
  throw "Release governance tests failed:`n$($failures -join [Environment]::NewLine)"
}

Write-Host "Release governance tests passed."
