[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate-compatibility-consistency.ps1'

& $validator -RepositoryRoot $repoRoot

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("fluentmap-compatibility-" + [Guid]::NewGuid().ToString('N'))
try {
  foreach ($directory in @(
      'eng',
      '.github/workflows',
      'test/Dapper.FluentMap.ProviderCompatibility.Tests')) {
    [System.IO.Directory]::CreateDirectory((Join-Path $temporaryRoot $directory)) | Out-Null
  }

  foreach ($relativePath in @(
      'eng/compatibility-contract.json',
      'Directory.Build.props',
      '.github/workflows/ci.yml',
      '.github/workflows/compatibility-canary.yml',
      'COMPATIBILITY.md',
      'MIGRATION.md',
      'MIGRATION.pt-BR.md',
      'test/Dapper.FluentMap.ProviderCompatibility.Tests/Dapper.FluentMap.ProviderCompatibility.Tests.csproj')) {
    $destination = Join-Path $temporaryRoot $relativePath
    [System.IO.File]::Copy((Join-Path $repoRoot $relativePath), $destination)
  }

  $compatibilityPath = Join-Path $temporaryRoot 'COMPATIBILITY.md'
  $contract = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/compatibility-contract.json') -Raw | ConvertFrom-Json
  $canaryPath = Join-Path $temporaryRoot '.github/workflows/compatibility-canary.yml'
  $canaryText = [System.IO.File]::ReadAllText($canaryPath)
  $reorderedCanary = [Regex]::Replace(
    $canaryText,
    '(?m)^(?<indent>\s*)- label: (?<label>[^\r\n]+)\r?\n\s+version: (?<version>[^\r\n]+)',
    '${indent}- version: ${version}' + [Environment]::NewLine + '${indent}  future-key: accepted' + [Environment]::NewLine + '${indent}  label: ${label}')
  [System.IO.File]::WriteAllText($canaryPath, $reorderedCanary)
  & $validator -RepositoryRoot $temporaryRoot
  Write-Host 'PASS compatibility consistency validator accepts lane keys in a different order with intervening keys.'

  $documentedRange = "Dapper $([string]$contract.dapper.packageRange)"
  $drifted = [System.IO.File]::ReadAllText($compatibilityPath).Replace(
    $documentedRange,
    'Dapper [0.0.0,3.0.0)')
  [System.IO.File]::WriteAllText($compatibilityPath, $drifted)

  $detected = $false
  try {
    & $validator -RepositoryRoot $temporaryRoot
  }
  catch {
    $detected = $_.Exception.Message -like '*COMPATIBILITY.md is inconsistent*'
  }

  if (-not $detected) {
    throw 'Negative compatibility-consistency proof failed: documentation drift was not detected with an actionable file name.'
  }

  Write-Host 'PASS compatibility consistency validator detects intentional documentation drift.'

  [System.IO.File]::Copy((Join-Path $repoRoot 'COMPATIBILITY.md'), $compatibilityPath, $true)
  $ciPath = Join-Path $temporaryRoot '.github/workflows/ci.yml'
  $driftedCi = [System.IO.File]::ReadAllText($ciPath).Replace(
    "dapper-version: $([string]$contract.dapper.minimumSupportedVersion)",
    'dapper-version: 0.0.0')
  [System.IO.File]::WriteAllText($ciPath, $driftedCi)

  $detected = $false
  try {
    & $validator -RepositoryRoot $temporaryRoot
  }
  catch {
    $detected = $_.Exception.Message -like '*.github/workflows/ci.yml supported Dapper lane matrix*'
  }

  if (-not $detected) {
    throw 'Negative compatibility-consistency proof failed: Dapper CI lane drift was not detected with an actionable file name.'
  }

  Write-Host 'PASS compatibility consistency validator detects intentional Dapper CI lane drift.'

  [System.IO.File]::Copy((Join-Path $repoRoot '.github/workflows/ci.yml'), $ciPath, $true)
  $providerProjectPath = Join-Path $temporaryRoot 'test/Dapper.FluentMap.ProviderCompatibility.Tests/Dapper.FluentMap.ProviderCompatibility.Tests.csproj'
  $sqliteProvider = @($contract.providerSupport.providers | Where-Object { [string]$_.id -eq 'sqlite' })[0]
  $driftedProviderProject = [System.IO.File]::ReadAllText($providerProjectPath).Replace(
    ">$([string]$sqliteProvider.clientVersion)</SqliteClientPackageVersion>",
    '>0.0.0</SqliteClientPackageVersion>')
  [System.IO.File]::WriteAllText($providerProjectPath, $driftedProviderProject)

  $detected = $false
  try {
    & $validator -RepositoryRoot $temporaryRoot
  }
  catch {
    $detected = $_.Exception.Message -like '*provider test project declares Microsoft.Data.Sqlite version*'
  }

  if (-not $detected) {
    throw 'Negative compatibility-consistency proof failed: provider client version drift was not detected with an actionable package name.'
  }

  Write-Host 'PASS compatibility consistency validator detects intentional provider client version drift.'

  [System.IO.File]::Copy(
    (Join-Path $repoRoot 'test/Dapper.FluentMap.ProviderCompatibility.Tests/Dapper.FluentMap.ProviderCompatibility.Tests.csproj'),
    $providerProjectPath,
    $true)
  $driftedProviderReference = [System.IO.File]::ReadAllText($providerProjectPath).Replace(
    'Include="Microsoft.Data.Sqlite" Version="$(SqliteClientPackageVersion)"',
    'Include="Microsoft.Data.Sqlite" Version="0.0.0"')
  [System.IO.File]::WriteAllText($providerProjectPath, $driftedProviderReference)

  $detected = $false
  try {
    & $validator -RepositoryRoot $temporaryRoot
  }
  catch {
    $detected = $_.Exception.Message -like "*must set 'Microsoft.Data.Sqlite' Version*"
  }

  if (-not $detected) {
    throw 'Negative compatibility-consistency proof failed: provider PackageReference drift was not detected.'
  }

  Write-Host 'PASS compatibility consistency validator detects provider PackageReference drift.'

  [System.IO.File]::Copy(
    (Join-Path $repoRoot 'test/Dapper.FluentMap.ProviderCompatibility.Tests/Dapper.FluentMap.ProviderCompatibility.Tests.csproj'),
    $providerProjectPath,
    $true)
  $driftedCi = [System.IO.File]::ReadAllText($ciPath).Replace(
    '"SQLite" "SQL Server" "PostgreSQL" "MySQL" "MariaDB"',
    '"SQLite" "SQL Server" "PostgreSQL" "MySQL"')
  [System.IO.File]::WriteAllText($ciPath, $driftedCi)

  $detected = $false
  try {
    & $validator -RepositoryRoot $temporaryRoot
  }
  catch {
    $detected = $_.Exception.Message -like '*.github/workflows/ci.yml provider loop*'
  }

  if (-not $detected) {
    throw 'Negative compatibility-consistency proof failed: provider CI drift was not detected with an actionable file name.'
  }

  Write-Host 'PASS compatibility consistency validator detects intentional provider CI drift.'
}
finally {
  if (Test-Path -LiteralPath $temporaryRoot) {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
  }
}
