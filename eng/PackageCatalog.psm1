Set-StrictMode -Version Latest

function Get-FluentMapPackageCatalog {
  [CmdletBinding()]
  param(
    [string]$CatalogPath
  )

  if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = Join-Path $PSScriptRoot 'package-catalog.json'
  }

  if (-not (Test-Path -LiteralPath $CatalogPath -PathType Leaf)) {
    throw "Package catalog '$CatalogPath' does not exist."
  }

  $catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
  if ($catalog.schemaVersion -ne '1.0') {
    throw "Package catalog '$CatalogPath' has unsupported schemaVersion '$($catalog.schemaVersion)'."
  }

  $packages = @($catalog.packages)
  if ($packages.Count -eq 0) {
    throw "Package catalog '$CatalogPath' does not define any packages."
  }

  $duplicateProjects = @(
    $packages |
      Group-Object project |
      Where-Object { $_.Count -gt 1 } |
      ForEach-Object { $_.Name }
  )
  if ($duplicateProjects.Count -gt 0) {
    throw "Package catalog '$CatalogPath' contains duplicate project identities: $($duplicateProjects -join ', ')."
  }

  $duplicatePackageIds = @(
    $packages |
      Group-Object packageId |
      Where-Object { $_.Count -gt 1 } |
      ForEach-Object { $_.Name }
  )
  if ($duplicatePackageIds.Count -gt 0) {
    throw "Package catalog '$CatalogPath' contains duplicate package identities: $($duplicatePackageIds -join ', ')."
  }

  foreach ($package in $packages) {
    foreach ($propertyName in @('project', 'projectPath', 'packageId', 'assetKind')) {
      if ([string]::IsNullOrWhiteSpace([string]$package.$propertyName)) {
        throw "Package catalog '$CatalogPath' contains a package with missing '$propertyName'."
      }
    }

    if ($package.assetKind -notin @('library', 'analyzer')) {
      throw "Package catalog '$CatalogPath' contains unsupported assetKind '$($package.assetKind)' for '$($package.project)'."
    }
  }

  return $catalog
}

function Get-FluentMapPackages {
  [CmdletBinding()]
  param(
    [string]$CatalogPath,

    [ValidateSet('library', 'analyzer')]
    [string]$AssetKind,

    [switch]$WithSymbols
  )

  $packages = @((Get-FluentMapPackageCatalog -CatalogPath $CatalogPath).packages)

  if (-not [string]::IsNullOrWhiteSpace($AssetKind)) {
    $packages = @($packages | Where-Object { $_.assetKind -eq $AssetKind })
  }

  if ($PSBoundParameters.ContainsKey('WithSymbols')) {
    $packages = @($packages | Where-Object { [bool]$_.symbols })
  }

  return $packages
}

function Get-FluentMapPackageIds {
  [CmdletBinding()]
  param(
    [string]$CatalogPath,

    [ValidateSet('library', 'analyzer')]
    [string]$AssetKind,

    [switch]$WithSymbols
  )

  $arguments = @{
    CatalogPath = $CatalogPath
  }

  if ($PSBoundParameters.ContainsKey('AssetKind')) {
    $arguments.AssetKind = $AssetKind
  }

  if ($PSBoundParameters.ContainsKey('WithSymbols')) {
    $arguments.WithSymbols = $true
  }

  return @(Get-FluentMapPackages @arguments | ForEach-Object { [string]$_.packageId })
}

function Get-FluentMapPackageFileName {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)]
    [pscustomobject]$Package,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet('package', 'symbols')]
    [string]$Kind = 'package'
  )

  $extension = if ($Kind -eq 'symbols') { 'snupkg' } else { 'nupkg' }
  return "$($Package.packageId).$Version.$extension"
}

Export-ModuleMember `
  -Function `
    Get-FluentMapPackageCatalog, `
    Get-FluentMapPackages, `
    Get-FluentMapPackageIds, `
    Get-FluentMapPackageFileName
