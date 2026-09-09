[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('branch', 'tag')]
  [string]$SourceType,

  [Parameter(Mandatory = $true)]
  [string]$SourceRef,

  [string]$Version,

  [string]$Remote = 'origin',

  [string]$AllowedBranch = 'master',

  [string]$CurrentWorkflowRef,

  [string]$GitHubOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$semverPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$'

function Fail {
  param([string]$Message)
  throw "Release source resolution failed: $Message"
}

function Invoke-Git {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments,

    [switch]$AllowFailure
  )

  $stderrPath = [System.IO.Path]::GetTempFileName()
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    $output = & git @Arguments 2> $stderrPath
    $exitCode = $LASTEXITCODE
    $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
  }

  if ($exitCode -ne 0 -and -not $AllowFailure) {
    Fail "git $($Arguments -join ' ') failed with exit code $exitCode. $stderr"
  }

  return [pscustomobject]@{
    ExitCode = $exitCode
    Output = @($output)
  }
}

function Assert-SemVer {
  param([string]$Value)

  if ($Value -notmatch $semverPattern) {
    Fail "Invalid semantic version '$Value'. Expected a value such as 3.1.2 or 3.2.0-rc.1."
  }

  $major = [int]($Value.Split('.')[0])
  if ($major -lt 3) {
    Fail 'FluentMap releases from this workflow must use major version 3 or later.'
  }

  if ($Value -eq '3.0.0') {
    Fail 'Version 3.0.0 is immutable and must not be recovered from a newer workflow.'
  }
}

function Get-SingleCommit {
  param([string]$Revision)

  $result = Invoke-Git -Arguments @('rev-parse', "$Revision^{commit}")
  $commits = @($result.Output | Where-Object { $_ -match '^[0-9a-fA-F]{40}$' })
  if ($commits.Count -ne 1) {
    Fail "'$Revision' did not peel to exactly one commit."
  }

  return $commits[0].ToLowerInvariant()
}

function Assert-ReachableFromAllowedBranch {
  param([string]$Commit)

  Invoke-Git -Arguments @('fetch', '--no-tags', $Remote, "+refs/heads/$AllowedBranch`:refs/remotes/$Remote/$AllowedBranch") | Out-Null
  $historyCheck = Invoke-Git -Arguments @('merge-base', '--is-ancestor', $Commit, "refs/remotes/$Remote/$AllowedBranch") -AllowFailure
  if ($historyCheck.ExitCode -ne 0) {
    Fail "resolved commit $Commit is not reachable from $Remote/$AllowedBranch."
  }
}

function Write-OutputValue {
  param(
    [string]$Name,
    [AllowNull()]
    [string]$Value
  )

  $normalizedValue = if ($null -eq $Value) { '' } else { $Value }
  if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    "$Name=$normalizedValue" | Out-File -FilePath $GitHubOutputPath -Encoding utf8 -Append
  }
}

$sourceTypeNormalized = $SourceType.ToLowerInvariant()
$sourceRefNormalized = $SourceRef.Trim()
$versionNormalized = if ($null -eq $Version) { '' } else { $Version.Trim() }

if ([string]::IsNullOrWhiteSpace($sourceRefNormalized)) {
  Fail 'source_ref is required.'
}

if (-not [string]::IsNullOrWhiteSpace($CurrentWorkflowRef) -and $CurrentWorkflowRef -ne "refs/heads/$AllowedBranch") {
  Fail "The recovery workflow must be started from $AllowedBranch. Current ref: $CurrentWorkflowRef"
}

$resolvedBranch = ''
$resolvedTag = ''
$resolvedRef = ''
$resolvedVersion = ''
$resolvedCommit = ''
$releaseHistoryRef = "refs/heads/$AllowedBranch"

switch ($sourceTypeNormalized) {
  'branch' {
    if ($sourceRefNormalized -ne $AllowedBranch) {
      Fail "Branch-mode recovery is restricted to '$AllowedBranch'. Provided source_ref: '$sourceRefNormalized'."
    }

    if ([string]::IsNullOrWhiteSpace($versionNormalized)) {
      Fail 'version is required when source_type=branch.'
    }

    if ($versionNormalized.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
      Fail "Enter the semantic version without the 'v' prefix when source_type=branch."
    }

    Assert-SemVer -Value $versionNormalized
    Invoke-Git -Arguments @('check-ref-format', '--branch', $sourceRefNormalized) | Out-Null

    $remoteBranch = Invoke-Git -Arguments @('ls-remote', '--exit-code', '--heads', $Remote, $sourceRefNormalized) -AllowFailure
    if ($remoteBranch.ExitCode -ne 0) {
      Fail "Branch '$sourceRefNormalized' does not exist on remote '$Remote'."
    }

    Invoke-Git -Arguments @('fetch', '--no-tags', $Remote, "+refs/heads/$sourceRefNormalized`:refs/remotes/$Remote/$sourceRefNormalized") | Out-Null
    $resolvedCommit = Get-SingleCommit -Revision "refs/remotes/$Remote/$sourceRefNormalized"
    Assert-ReachableFromAllowedBranch -Commit $resolvedCommit

    $resolvedVersion = $versionNormalized
    $resolvedBranch = $sourceRefNormalized
    $resolvedRef = "refs/heads/$sourceRefNormalized"
  }

  'tag' {
    if (-not [string]::IsNullOrWhiteSpace($versionNormalized)) {
      Fail 'Do not provide version when source_type=tag; the version is derived from the tag.'
    }

    if ($sourceRefNormalized -notmatch '^v(.+)$') {
      Fail "Tag source_ref must use the release tag format v<SemVer>."
    }

    $resolvedVersion = $Matches[1]
    Assert-SemVer -Value $resolvedVersion
    Invoke-Git -Arguments @('check-ref-format', "refs/tags/$sourceRefNormalized") | Out-Null

    $remoteTag = Invoke-Git -Arguments @('ls-remote', '--exit-code', '--tags', $Remote, "refs/tags/$sourceRefNormalized") -AllowFailure
    if ($remoteTag.ExitCode -ne 0) {
      Fail "Tag '$sourceRefNormalized' does not exist on remote '$Remote'."
    }

    Invoke-Git -Arguments @('fetch', '--force', '--no-tags', $Remote, "+refs/tags/$sourceRefNormalized`:refs/tags/$sourceRefNormalized") | Out-Null
    $resolvedCommit = Get-SingleCommit -Revision "refs/tags/$sourceRefNormalized"
    Assert-ReachableFromAllowedBranch -Commit $resolvedCommit

    $resolvedTag = $sourceRefNormalized
    $resolvedRef = "refs/tags/$sourceRefNormalized"
  }
}

$releaseTag = "v$resolvedVersion"

Write-Host 'Release source'
Write-Host '--------------'
Write-Host "Type: $sourceTypeNormalized"
Write-Host "Ref: $sourceRefNormalized"
Write-Host "Resolved ref: $resolvedRef"
Write-Host "Version: $resolvedVersion"
Write-Host "Commit: $resolvedCommit"

Write-OutputValue -Name 'source_type' -Value $sourceTypeNormalized
Write-OutputValue -Name 'source_ref' -Value $sourceRefNormalized
Write-OutputValue -Name 'resolved_ref' -Value $resolvedRef
Write-OutputValue -Name 'resolved_version' -Value $resolvedVersion
Write-OutputValue -Name 'resolved_commit' -Value $resolvedCommit
Write-OutputValue -Name 'resolved_branch' -Value $resolvedBranch
Write-OutputValue -Name 'resolved_tag' -Value $resolvedTag
Write-OutputValue -Name 'release_tag' -Value $releaseTag
Write-OutputValue -Name 'release_history_ref' -Value $releaseHistoryRef

[pscustomobject]@{
  source_type = $sourceTypeNormalized
  source_ref = $sourceRefNormalized
  resolved_ref = $resolvedRef
  resolved_version = $resolvedVersion
  resolved_commit = $resolvedCommit
  resolved_branch = $resolvedBranch
  resolved_tag = $resolvedTag
  release_tag = $releaseTag
  release_history_ref = $releaseHistoryRef
}
