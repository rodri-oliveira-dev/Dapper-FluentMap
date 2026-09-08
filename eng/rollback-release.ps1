param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseTag,

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$RepositoryOwner,

    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [string]$NuGetApiKey
)

$ErrorActionPreference = 'Stop'

$packageIds = @(
    'Dapper.FluentMap',
    'Dapper.FluentMap.Dommel',
    'Dapper.FluentMap.DependencyInjection',
    'Dapper.FluentMap.Analyzers',
    'Dapper.FluentMap.Generators'
)

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
Write-Host 'NuGet.org cannot permanently delete a published version; rollback unlists versions that were created before the release failed.'

# NuGet.org compensation: unlist any target package version that exists.
if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    Add-RollbackFailure 'No temporary NuGet API key was available for rollback. Ensure the Trusted Publishing policy grants Unlist/relist scope.'
}
else {
    foreach ($packageId in $packageIds) {
        $escapedId = [System.Uri]::EscapeDataString($packageId)
        $escapedVersion = [System.Uri]::EscapeDataString($Version)
        $uri = "https://www.nuget.org/api/v2/package/$escapedId/$escapedVersion"

        try {
            $response = Invoke-WebRequest `
                -Uri $uri `
                -Method Delete `
                -Headers @{ 'X-NuGet-ApiKey' = $NuGetApiKey } `
                -SkipHttpErrorCheck

            switch ([int]$response.StatusCode) {
                204 { Write-Host "NuGet.org: unlisted $packageId $Version." }
                404 { Write-Host "NuGet.org: $packageId $Version was not published; nothing to unlist." }
                default {
                    Add-RollbackFailure "NuGet.org rollback for $packageId $Version returned HTTP $([int]$response.StatusCode)."
                }
            }
        }
        catch {
            Add-RollbackFailure "NuGet.org rollback failed for $packageId $Version`: $($_.Exception.Message)"
        }
    }
}

# GitHub Packages compensation: delete this version from each NuGet package.
foreach ($packageId in $packageIds) {
    $escapedPackageId = [System.Uri]::EscapeDataString($packageId)
    $versionIds = [System.Collections.Generic.List[long]]::new()
    $page = 1

    while ($true) {
        $listUri = "https://api.github.com/users/$RepositoryOwner/packages/nuget/$escapedPackageId/versions?per_page=100&page=$page"
        $response = Invoke-GitHubRequest -Method GET -Uri $listUri
        if ($null -eq $response) {
            break
        }

        $status = [int]$response.StatusCode
        if ($status -eq 404) {
            Write-Host "GitHub Packages: $packageId does not exist; nothing to delete."
            break
        }

        if ($status -ne 200) {
            Add-RollbackFailure "GitHub Packages lookup for $packageId returned HTTP $status."
            break
        }

        $versions = @($response.Content | ConvertFrom-Json)
        foreach ($packageVersion in $versions) {
            if ([string]$packageVersion.name -eq $Version) {
                $versionIds.Add([long]$packageVersion.id)
            }
        }

        if ($versions.Count -lt 100) {
            break
        }

        $page++
    }

    foreach ($versionId in $versionIds) {
        $deleteUri = "https://api.github.com/users/$RepositoryOwner/packages/nuget/$escapedPackageId/versions/$versionId"
        $deleteResponse = Invoke-GitHubRequest -Method DELETE -Uri $deleteUri
        if ($null -eq $deleteResponse) {
            continue
        }

        $deleteStatus = [int]$deleteResponse.StatusCode
        if ($deleteStatus -in @(204, 404)) {
            Write-Host "GitHub Packages: deleted $packageId $Version (version id $versionId)."
        }
        else {
            Add-RollbackFailure "GitHub Packages deletion for $packageId $Version returned HTTP $deleteStatus."
        }
    }
}

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

# Remove the release tag last, after registry compensation has been attempted.
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

if ($failures.Count -gt 0) {
    throw "Release rollback completed with $($failures.Count) compensation error(s). Review the rollback log before starting another release."
}

Write-Host "Compensating rollback completed for $ReleaseTag."
