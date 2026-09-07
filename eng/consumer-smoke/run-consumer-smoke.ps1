[CmdletBinding()]
param(
  [string]$RepositoryRoot,
  [string]$RemoteArtifactDirectory,
  [string]$PackageDirectory,
  [switch]$SkipTrimPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageVersion = '3.0.0-rc.1'
$expectedPackageIds = @(
  'Dapper.FluentMap',
  'Dapper.FluentMap.Dommel',
  'Dapper.FluentMap.DependencyInjection',
  'Dapper.FluentMap.Analyzers',
  'Dapper.FluentMap.Generators'
)

function Fail {
  param([string]$Message)
  throw "Consumer smoke failed: $Message"
}

function Get-RepoRoot {
  if (-not [string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    return (Resolve-Path -LiteralPath $RepositoryRoot).Path
  }

  return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
}

function Invoke-External {
  param(
    [string]$FilePath,
    [string[]]$Arguments,
    [string]$LogPath,
    [switch]$AllowFailure
  )

  Write-Host "> $FilePath $($Arguments -join ' ')"
  $output = & $FilePath @Arguments 2>&1
  $exitCode = $LASTEXITCODE

  if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
    $logDirectory = Split-Path -Parent $LogPath
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    [System.IO.File]::WriteAllText(
      $LogPath,
      ($output -join [Environment]::NewLine) + [Environment]::NewLine,
      [System.Text.UTF8Encoding]::new($false))
  }

  if ($exitCode -ne 0 -and -not $AllowFailure) {
    $tail = ($output | Select-Object -Last 60) -join [Environment]::NewLine
    Fail "Command failed with exit code $exitCode. Tail:$([Environment]::NewLine)$tail"
  }

  return [pscustomobject]@{
    ExitCode = $exitCode
    Output = @($output)
  }
}

function Get-CurrentRid {
  if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
    $architecture = 'arm64'
  }
  else {
    $architecture = 'x64'
  }

  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    return "win-$architecture"
  }

  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
    return "linux-$architecture"
  }

  if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
    return "osx-$architecture"
  }

  Fail 'Unsupported OS for trimming consumer smoke.'
}

function Ensure-RemoteArtifacts {
  param(
    [string]$RepoRoot,
    [pscustomobject]$Manifest,
    [string]$RemoteRoot
  )

  $missing = @(
    foreach ($artifact in $Manifest.artifacts) {
      if (@(Get-ChildItem -Path $RemoteRoot -Recurse -File -Filter $artifact.name -ErrorAction SilentlyContinue).Count -eq 0) {
        $artifact.name
      }
    }
  )

  if ($missing.Count -eq 0) {
    return
  }

  $gh = Get-Command gh -ErrorAction SilentlyContinue
  if ($null -eq $gh) {
    Fail "Missing remote artifacts and GitHub CLI is not available. Missing: $($missing -join ', ')."
  }

  New-Item -ItemType Directory -Force -Path $RemoteRoot | Out-Null
  Invoke-External `
    -FilePath $gh.Source `
    -Arguments @(
      'run',
      'download',
      [string]$Manifest.workflow.workflowRunId,
      '--repo',
      [string]$Manifest.repository,
      '--name',
      [string]$Manifest.workflow.artifactName,
      '--dir',
      $RemoteRoot) `
    -LogPath (Join-Path $RepoRoot '.tmp/consumer-smoke/logs/gh-run-download.log') | Out-Null
}

function Get-ValidatedArtifacts {
  param(
    [pscustomobject]$Manifest,
    [string]$RemoteRoot
  )

  $files = @{}
  foreach ($artifact in $Manifest.artifacts) {
    $matches = @(Get-ChildItem -Path $RemoteRoot -Recurse -File -Filter $artifact.name)
    if ($matches.Count -ne 1) {
      Fail "Expected exactly one downloaded artifact named '$($artifact.name)', found $($matches.Count)."
    }

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $matches[0].FullName).Hash.ToLowerInvariant()
    if ($hash -ne $artifact.sha256) {
      Fail "SHA-256 mismatch for '$($artifact.name)'. Expected $($artifact.sha256), got $hash."
    }

    $files[$artifact.name] = $matches[0]
  }

  return $files
}

function New-CleanDirectory {
  param(
    [string]$Path,
    [string]$ExpectedParent
  )

  $parent = (Resolve-Path -LiteralPath $ExpectedParent).Path
  if (Test-Path -LiteralPath $Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if (-not $resolved.StartsWith($parent, [System.StringComparison]::OrdinalIgnoreCase)) {
      Fail "Refusing to clean '$resolved' because it is outside '$parent'."
    }

    Remove-Item -LiteralPath $resolved -Recurse -Force
  }

  New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Write-NuGetConfig {
  param(
    [string]$Path,
    [string]$FeedDirectory
  )

  $feed = [System.Security.SecurityElement]::Escape($FeedDirectory)
  $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="rc-artifacts" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="rc-artifacts">
      <package pattern="Dapper.FluentMap" />
      <package pattern="Dapper.FluentMap.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="Dapper" />
      <package pattern="Dommel" />
      <package pattern="Microsoft.*" />
      <package pattern="SQLitePCLRaw.*" />
      <package pattern="SQLitePCLRawLib.*" />
      <package pattern="runtime.*" />
      <package pattern="System.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@

  [System.IO.File]::WriteAllText($Path, $nugetConfig, [System.Text.UTF8Encoding]::new($false))
}

function Assert-NoSourceReferences {
  param([string]$ProjectPath)

  [xml]$projectXml = Get-Content -LiteralPath $ProjectPath
  $projectReferences = @($projectXml.SelectNodes('//*[local-name()="ProjectReference"]'))
  if ($projectReferences.Count -gt 0) {
    Fail "$ProjectPath contains ProjectReference, which is forbidden for package consumer smoke."
  }

  $text = Get-Content -LiteralPath $ProjectPath -Raw
  if ($text -match 'src[\\/]+Dapper\.FluentMap' -or $text -match '<Reference\s') {
    Fail "$ProjectPath contains a direct source or assembly reference."
  }
}

function Assert-RestoredPackages {
  param(
    [string]$ProjectPath,
    [string[]]$ExpectedFluentMapPackages,
    [string]$PackagesDirectory
  )

  Assert-NoSourceReferences -ProjectPath $ProjectPath

  $assetsPath = Join-Path (Split-Path -Parent $ProjectPath) 'obj/project.assets.json'
  if (-not (Test-Path -LiteralPath $assetsPath)) {
    Fail "Missing restore assets file for $ProjectPath."
  }

  $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
  $libraries = @($assets.libraries.PSObject.Properties)
  $libraryNames = @($libraries.Name)

  foreach ($expected in $ExpectedFluentMapPackages) {
    $library = "$expected/$packageVersion"
    if ($library -notin $libraryNames) {
      Fail "$ProjectPath did not restore $library."
    }
  }

  $wrongFluentMapVersions = @($libraryNames | Where-Object {
      $_ -match '^Dapper\.FluentMap(\..*)?/' -and $_ -notmatch "/$([regex]::Escape($packageVersion))$"
    })
  if ($wrongFluentMapVersions.Count -gt 0) {
    Fail "$ProjectPath restored unexpected FluentMap versions: $($wrongFluentMapVersions -join ', ')."
  }

  $versionTwoPackages = @($libraryNames | Where-Object { $_ -match '/2\.0\.0$' })
  if ($versionTwoPackages.Count -gt 0) {
    Fail "$ProjectPath restored forbidden 2.0.0 packages: $($versionTwoPackages -join ', ')."
  }

  $projectLibraries = @($libraries | Where-Object { $_.Value.type -eq 'project' })
  if ($projectLibraries.Count -gt 0) {
    Fail "$ProjectPath restored project libraries: $($projectLibraries.Name -join ', ')."
  }

  foreach ($analyzerId in @('Dapper.FluentMap.Analyzers', 'Dapper.FluentMap.Generators')) {
    $targetLibraryNames = @(
      foreach ($target in $assets.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
          if ($library.Name -eq "$analyzerId/$packageVersion") {
            $library.Name
            if ($library.Value.PSObject.Properties.Name -contains 'runtime') {
              Fail "$ProjectPath restored $analyzerId as a runtime asset."
            }
          }
        }
      }
    )

    if ("$analyzerId/$packageVersion" -in $libraryNames -and $targetLibraryNames.Count -eq 0) {
      Fail "$ProjectPath restored $analyzerId but it was not visible in target assets."
    }
  }

  foreach ($expected in $ExpectedFluentMapPackages) {
    $packageFolder = Join-Path $PackagesDirectory ($expected.ToLowerInvariant())
    $packageVersionFolder = Join-Path $packageFolder $packageVersion
    if (-not (Test-Path -LiteralPath $packageVersionFolder -PathType Container)) {
      Fail "$expected was not restored into the temporary packages path."
    }
  }
}

function Invoke-DotNetForProject {
  param(
    [string]$Verb,
    [string]$ProjectPath,
    [string]$ConfigPath,
    [string]$PackagesDirectory,
    [string]$LogPath,
    [string[]]$ExtraArguments = @(),
    [switch]$AllowFailure
  )

  switch ($Verb) {
    'restore' {
      $arguments = @(
        'restore',
        $ProjectPath,
        '--configfile',
        $ConfigPath,
        '-p:RestoreNoCache=true',
        "-p:RestorePackagesPath=$PackagesDirectory",
        '-p:DisableImplicitNuGetFallbackFolder=true'
      ) + $ExtraArguments
    }
    'build' {
      $arguments = @(
        'build',
        $ProjectPath,
        "-p:RestorePackagesPath=$PackagesDirectory",
        '-p:DisableImplicitNuGetFallbackFolder=true'
      ) + $ExtraArguments
    }
    'run' {
      $arguments = @(
        'run',
        '--project',
        $ProjectPath,
        "--property:RestorePackagesPath=$PackagesDirectory",
        '--property:DisableImplicitNuGetFallbackFolder=true'
      ) + $ExtraArguments
    }
    'publish' {
      $arguments = @(
        'publish',
        $ProjectPath,
        "-p:RestorePackagesPath=$PackagesDirectory",
        '-p:DisableImplicitNuGetFallbackFolder=true'
      ) + $ExtraArguments
    }
    default {
      Fail "Unsupported dotnet verb '$Verb'."
    }
  }

  Invoke-External -FilePath 'dotnet' -Arguments $arguments -LogPath $LogPath -AllowFailure:$AllowFailure
}

$repoRoot = Get-RepoRoot
$artifactFiles = @{}
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
  $releaseDirectory = Join-Path $repoRoot '.sdd/release-3.0.0-rc.1'
  $manifestPath = Join-Path $releaseDirectory 'artifacts.json'
  if (-not (Test-Path -LiteralPath $manifestPath)) {
    Fail "Manifest not found at '$manifestPath'."
  }

  $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
  if ($manifest.release -ne $packageVersion) {
    Fail "Manifest release '$($manifest.release)' does not match expected $packageVersion."
  }

  $remoteRoot = if ([string]::IsNullOrWhiteSpace($RemoteArtifactDirectory)) {
    Join-Path $repoRoot 'artifacts/release-3.0.0-rc.1/remote'
  }
  else {
    $RemoteArtifactDirectory
  }

  Ensure-RemoteArtifacts -RepoRoot $repoRoot -Manifest $manifest -RemoteRoot $remoteRoot
  $artifactFiles = Get-ValidatedArtifacts -Manifest $manifest -RemoteRoot $remoteRoot
}
else {
  $packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
  foreach ($packageId in $expectedPackageIds) {
    $fileName = "$packageId.$packageVersion.nupkg"
    $matches = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter $fileName)
    if ($matches.Count -ne 1) {
      Fail "Expected exactly one package '$fileName' in '$packageRoot', found $($matches.Count)."
    }

    $artifactFiles[$fileName] = $matches[0]
  }
}

$tmpRoot = Join-Path $repoRoot '.tmp'
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
$workRoot = Join-Path $tmpRoot 'consumer-smoke'
New-CleanDirectory -Path $workRoot -ExpectedParent $tmpRoot

$feedDirectory = Join-Path $workRoot 'feed'
$packagesDirectory = Join-Path $workRoot 'packages'
$logsDirectory = Join-Path $workRoot 'logs'
New-Item -ItemType Directory -Force -Path $feedDirectory, $packagesDirectory, $logsDirectory | Out-Null

foreach ($packageId in $expectedPackageIds) {
  $fileName = "$packageId.$packageVersion.nupkg"
  if (-not $artifactFiles.ContainsKey($fileName)) {
    Fail "Manifest did not provide package artifact '$fileName'."
  }

  Copy-Item -LiteralPath $artifactFiles[$fileName].FullName -Destination $feedDirectory
}

$nugetConfig = Join-Path $workRoot 'NuGet.Config'
Write-NuGetConfig -Path $nugetConfig -FeedDirectory $feedDirectory

$projects = @(
  [pscustomobject]@{
    Name = 'CoreConsumer'
    Path = Join-Path $PSScriptRoot 'CoreConsumer/CoreConsumer.csproj'
    Packages = @('Dapper.FluentMap')
  },
  [pscustomobject]@{
    Name = 'GeneratorAnalyzerConsumer'
    Path = Join-Path $PSScriptRoot 'GeneratorAnalyzerConsumer/GeneratorAnalyzerConsumer.csproj'
    Packages = @('Dapper.FluentMap', 'Dapper.FluentMap.Analyzers', 'Dapper.FluentMap.Generators')
  },
  [pscustomobject]@{
    Name = 'DIConsumer'
    Path = Join-Path $PSScriptRoot 'DIConsumer/DIConsumer.csproj'
    Packages = @('Dapper.FluentMap', 'Dapper.FluentMap.DependencyInjection', 'Dapper.FluentMap.Generators')
  },
  [pscustomobject]@{
    Name = 'DommelConsumer'
    Path = Join-Path $PSScriptRoot 'DommelConsumer/DommelConsumer.csproj'
    Packages = @('Dapper.FluentMap', 'Dapper.FluentMap.Dommel')
  }
)

foreach ($project in $projects) {
  Invoke-DotNetForProject `
    -Verb 'restore' `
    -ProjectPath $project.Path `
    -ConfigPath $nugetConfig `
    -PackagesDirectory $packagesDirectory `
    -LogPath (Join-Path $logsDirectory "$($project.Name).restore.log") | Out-Null

  Assert-RestoredPackages `
    -ProjectPath $project.Path `
    -ExpectedFluentMapPackages $project.Packages `
    -PackagesDirectory $packagesDirectory

  Invoke-DotNetForProject `
    -Verb 'build' `
    -ProjectPath $project.Path `
    -ConfigPath $nugetConfig `
    -PackagesDirectory $packagesDirectory `
    -LogPath (Join-Path $logsDirectory "$($project.Name).build.log") `
    -ExtraArguments @('--no-restore', '--configuration', 'Release') | Out-Null

  if ($project.Name -eq 'GeneratorAnalyzerConsumer') {
    $generatedFiles = @(Get-ChildItem -Path (Join-Path (Split-Path -Parent $project.Path) 'obj') -Recurse -File -Filter 'DapperFluentMapGeneratedRegistration.g.cs')
    if ($generatedFiles.Count -eq 0) {
      Fail 'Source generator did not emit DapperFluentMapGeneratedRegistration.g.cs.'
    }
  }

  Invoke-DotNetForProject `
    -Verb 'run' `
    -ProjectPath $project.Path `
    -ConfigPath $nugetConfig `
    -PackagesDirectory $packagesDirectory `
    -LogPath (Join-Path $logsDirectory "$($project.Name).run.log") `
    -ExtraArguments @('--no-restore', '--no-build', '--configuration', 'Release') | Out-Null
}

$analyzerDiagnosticProject = Join-Path $PSScriptRoot 'AnalyzerDiagnosticConsumer/AnalyzerDiagnosticConsumer.csproj'
Invoke-DotNetForProject `
  -Verb 'restore' `
  -ProjectPath $analyzerDiagnosticProject `
  -ConfigPath $nugetConfig `
  -PackagesDirectory $packagesDirectory `
  -LogPath (Join-Path $logsDirectory 'AnalyzerDiagnosticConsumer.restore.log') | Out-Null
Assert-RestoredPackages `
  -ProjectPath $analyzerDiagnosticProject `
  -ExpectedFluentMapPackages @('Dapper.FluentMap', 'Dapper.FluentMap.Analyzers') `
  -PackagesDirectory $packagesDirectory

$diagnosticResult = Invoke-DotNetForProject `
  -Verb 'build' `
  -ProjectPath $analyzerDiagnosticProject `
  -ConfigPath $nugetConfig `
  -PackagesDirectory $packagesDirectory `
  -LogPath (Join-Path $logsDirectory 'AnalyzerDiagnosticConsumer.build.log') `
  -ExtraArguments @('--no-restore', '--configuration', 'Release') `
  -AllowFailure

if ($diagnosticResult.ExitCode -eq 0) {
  Fail 'Analyzer diagnostic consumer build succeeded, but DFM001 was expected.'
}

if (-not (($diagnosticResult.Output -join [Environment]::NewLine) -match 'DFM001')) {
  Fail 'Analyzer diagnostic consumer did not emit DFM001.'
}

$trimWarnings = @()
if (-not $SkipTrimPublish) {
  $rid = Get-CurrentRid
  foreach ($trimProjectName in @('TrimExplicitConsumer', 'TrimGeneratedConsumer')) {
    $trimProject = Join-Path $PSScriptRoot "$trimProjectName/$trimProjectName.csproj"
    Invoke-DotNetForProject `
      -Verb 'restore' `
      -ProjectPath $trimProject `
      -ConfigPath $nugetConfig `
      -PackagesDirectory $packagesDirectory `
      -LogPath (Join-Path $logsDirectory "$trimProjectName.restore.log") `
      -ExtraArguments @('--runtime', $rid) | Out-Null

    $trimExpectedPackages = if ($trimProjectName -eq 'TrimGeneratedConsumer') {
      @('Dapper.FluentMap', 'Dapper.FluentMap.Generators')
    }
    else {
      @('Dapper.FluentMap')
    }

    Assert-RestoredPackages `
      -ProjectPath $trimProject `
      -ExpectedFluentMapPackages $trimExpectedPackages `
      -PackagesDirectory $packagesDirectory

    $publishLog = Join-Path $logsDirectory "$trimProjectName.publish.log"
    $publishResult = Invoke-DotNetForProject `
      -Verb 'publish' `
      -ProjectPath $trimProject `
      -ConfigPath $nugetConfig `
      -PackagesDirectory $packagesDirectory `
      -LogPath $publishLog `
      -ExtraArguments @(
        '--no-restore',
        '--configuration',
        'Release',
        '--runtime',
        $rid,
        '--self-contained',
        'true',
        '-p:PublishTrimmed=true')

    $trimWarnings += @($publishResult.Output | Where-Object { $_ -match '\b(IL|NETSDK)\d{4}\b' })

    $publishDirectory = Join-Path (Split-Path -Parent $trimProject) "bin/Release/net10.0/$rid/publish"
    $executable = if ($rid.StartsWith('win-', [System.StringComparison]::OrdinalIgnoreCase)) {
      Join-Path $publishDirectory "$trimProjectName.exe"
    }
    else {
      Join-Path $publishDirectory $trimProjectName
    }

    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
      Fail "Published executable not found at '$executable'."
    }

    Invoke-External `
      -FilePath $executable `
      -Arguments @() `
      -LogPath (Join-Path $logsDirectory "$trimProjectName.published-run.log") | Out-Null
  }
}

Write-Host "Consumer smoke package source: $feedDirectory"
Write-Host "Consumer smoke package cache: $packagesDirectory"
if ($trimWarnings.Count -gt 0) {
  Write-Host 'Trimming warnings observed:'
  $trimWarnings | ForEach-Object { Write-Host $_ }
}
else {
  Write-Host 'Trimming warnings observed: none'
}

Write-Host 'Consumer smoke completed successfully.'
