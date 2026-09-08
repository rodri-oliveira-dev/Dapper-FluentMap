[CmdletBinding()]
param(
  [string]$SlnPath = './Dapper.FluentMap.sln',
  [string]$SlnxPath = './Dapper.FluentMap.slnx'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail {
  param([string]$Message)
  throw "SLNX equivalence validation failed: $Message"
}

function Get-SolutionProjects {
  param([string]$Path)

  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    Fail "Solution '$Path' does not exist."
  }

  $output = & dotnet sln $Path list 2>&1
  if ($LASTEXITCODE -ne 0) {
    Fail "Unable to list projects for '$Path'. Output:$([Environment]::NewLine)$($output -join [Environment]::NewLine)"
  }

  return @(
    $output |
      ForEach-Object { [string]$_ } |
      Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and
        $_ -notmatch '^(Projects|Projetos)$' -and
        $_ -notmatch '^-+$'
      } |
      ForEach-Object { $_.Trim().Replace('\', '/') } |
      Sort-Object -Unique
  )
}

function Assert-SetEquals {
  param(
    [string[]]$Expected,
    [string[]]$Actual
  )

  $missing = @($Expected | Where-Object { $_ -notin $Actual })
  $unexpected = @($Actual | Where-Object { $_ -notin $Expected })

  if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
    Fail "Project set mismatch. Missing from SLNX: $($missing -join ', '); unexpected in SLNX: $($unexpected -join ', ')."
  }
}

$slnProjects = Get-SolutionProjects -Path $SlnPath
$slnxProjects = Get-SolutionProjects -Path $SlnxPath

Assert-SetEquals -Expected $slnProjects -Actual $slnxProjects

Write-Host "Validated SLNX equivalence: $($slnxProjects.Count) projects match '$SlnPath'."
