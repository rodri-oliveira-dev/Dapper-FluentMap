param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseTag,

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [switch]$DeleteReleaseTag
)

$ErrorActionPreference = 'Stop'

if ($Version -eq '3.0.0' -or $ReleaseTag -eq 'v3.0.0') {
    throw 'Version 3.0.0 and tag v3.0.0 are immutable historical artifacts and must not be modified by rollback automation.'
}

$failures = [System.Collections.Generic.List[string]]::new()

function Add-RollbackFailure {
    param([string]$Message)

    $failures.Add($Message)
    Write-Error $Message -ErrorAction Continue
}

function Invoke-GitHubRequest {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('GET', 'DELETE')]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Uri
    )

    $headers = @{
        Authorization = "Bearer $GitHubToken"
        Accept = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent' = 'Dapper-FluentMap-release-rollback'
    }

    try {
        return Invoke-WebRequest `
            -Uri $Uri `
            -Method $Method `
            -Headers $headers `
            -SkipHttpErrorCheck
    }
    catch {
        Add-RollbackFailure "GitHub rollback request failed for $Method $Uri`: $($_.Exception.Message)"
        return $null
    }
}

Write-Host "Starting compensating rollback for $ReleaseTag ($Version)."
Write-Host 'Package registry artifacts are not deleted, unlisted, or overwritten by rollback.'
Write-Host 'A partial registry publication is recovered by validating existing package identities and publishing only genuinely missing packages.'

# Remove a partially-created GitHub Release, if any.
$escapedTag = [System.Uri]::EscapeDataString($ReleaseTag)
$releaseLookup = Invoke-GitHubRequest -Method GET -Uri "https://api.github.com/repos/$Repository/releases/tags/$escapedTag"
if ($null -ne $releaseLookup) {
    $releaseStatus = [int]$releaseLookup.StatusCode
    if ($releaseStatus -eq 200) {
        $release = $releaseLookup.Content | ConvertFrom-Json
        $releaseDelete = Invoke-GitHubRequest -Method DELETE -Uri "https://api.github.com/repos/$Repository/releases/$($release.id)"
        if ($null -ne $releaseDelete -and [int]$releaseDelete.StatusCode -notin @(204, 404)) {
            Add-RollbackFailure "GitHub Release deletion for $ReleaseTag returned HTTP $([int]$releaseDelete.StatusCode)."
        }
        else {
            Write-Host "GitHub Release: removed $ReleaseTag."
        }
    }
    elseif ($releaseStatus -ne 404) {
        Add-RollbackFailure "GitHub Release lookup for $ReleaseTag returned HTTP $releaseStatus."
    }
}

if ($DeleteReleaseTag) {
    $tagDelete = Invoke-GitHubRequest -Method DELETE -Uri "https://api.github.com/repos/$Repository/git/refs/tags/$escapedTag"
    if ($null -ne $tagDelete) {
        $tagStatus = [int]$tagDelete.StatusCode
        if ($tagStatus -in @(204, 404, 422)) {
            Write-Host "Git tag: removed or already absent: $ReleaseTag."
        }
        else {
            Add-RollbackFailure "Git tag deletion for $ReleaseTag returned HTTP $tagStatus."
        }
    }
}
else {
    Write-Host "Git tag: retained $ReleaseTag because it was not created by this workflow run."
}

if ($failures.Count -gt 0) {
    throw "Release rollback completed with $($failures.Count) compensation error(s). Review the rollback log before starting another release."
}

Write-Host "Compensating rollback completed for $ReleaseTag."
