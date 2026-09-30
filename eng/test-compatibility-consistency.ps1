[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate-compatibility-consistency.ps1'

& $validator -RepositoryRoot $repoRoot

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("fluentmap-compatibility-" + [Guid]::NewGuid().ToString('N'))
try {
  foreach ($directory in @('eng', '.github/workflows')) {
    [System.IO.Directory]::CreateDirectory((Join-Path $temporaryRoot $directory)) | Out-Null
  }

  foreach ($relativePath in @(
      'eng/compatibility-contract.json',
      'Directory.Build.props',
      '.github/workflows/ci.yml',
      'COMPATIBILITY.md',
      'MIGRATION.md')) {
    $destination = Join-Path $temporaryRoot $relativePath
    [System.IO.File]::Copy((Join-Path $repoRoot $relativePath), $destination)
  }

  $compatibilityPath = Join-Path $temporaryRoot 'COMPATIBILITY.md'
  $drifted = [System.IO.File]::ReadAllText($compatibilityPath).Replace(
    'Dapper [2.1.79,3.0.0)',
    'Dapper [2.1.89,3.0.0)')
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
}
finally {
  if (Test-Path -LiteralPath $temporaryRoot) {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
  }
}
