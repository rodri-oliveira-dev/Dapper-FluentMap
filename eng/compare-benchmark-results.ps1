[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$BaselinePath,

  [Parameter(Mandatory = $true)]
  [string]$CurrentPath,

  [Parameter(Mandatory = $true)]
  [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$baseline = Get-Content -Raw -LiteralPath $BaselinePath | ConvertFrom-Json
$current = Get-Content -Raw -LiteralPath $CurrentPath | ConvertFrom-Json
$shortRun = @($current.Benchmarks | Where-Object { $_.DisplayInfo -like '*ShortRun*' })

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$comparisons = foreach ($scenario in $baseline.scenarios) {
  $matches = @($shortRun | Where-Object { $_.Method -eq $scenario.method })
  if ($matches.Count -ne 1) {
    throw "Expected one ShortRun result for '$($scenario.method)', found $($matches.Count)."
  }

  $result = $matches[0]
  $timeDelta = (($result.Statistics.Mean - $scenario.meanNanoseconds) / $scenario.meanNanoseconds) * 100
  $allocationDelta = (($result.Memory.BytesAllocatedPerOperation - $scenario.allocatedBytes) / $scenario.allocatedBytes) * 100
  $material = $timeDelta -gt $baseline.timeRegressionThresholdPercent -or
    $allocationDelta -gt $baseline.allocationRegressionThresholdPercent

  [pscustomobject]@{
    method = $scenario.method
    baselineMeanNanoseconds = [math]::Round($scenario.meanNanoseconds, 2)
    currentMeanNanoseconds = [math]::Round($result.Statistics.Mean, 2)
    timeDeltaPercent = [math]::Round($timeDelta, 2)
    baselineAllocatedBytes = [long]$scenario.allocatedBytes
    currentAllocatedBytes = [long]$result.Memory.BytesAllocatedPerOperation
    allocationDeltaPercent = [math]::Round($allocationDelta, 2)
    materialRegression = $material
  }
}

$report = [pscustomobject]@{
  schemaVersion = '1.0'
  reportOnly = [bool]$baseline.reportOnly
  timeRegressionThresholdPercent = $baseline.timeRegressionThresholdPercent
  allocationRegressionThresholdPercent = $baseline.allocationRegressionThresholdPercent
  generatedAtUtc = [DateTime]::UtcNow.ToString('O')
  comparisons = @($comparisons)
}

$jsonPath = Join-Path $OutputDirectory 'benchmark-comparison.json'
$markdownPath = Join-Path $OutputDirectory 'benchmark-comparison.md'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$lines = @(
  '# Materialization benchmark comparison',
  '',
  "Report-only thresholds: time +$($baseline.timeRegressionThresholdPercent)%; allocation +$($baseline.allocationRegressionThresholdPercent)%.",
  '',
  '| Scenario | Time delta | Allocation delta | Signal |',
  '| --- | ---: | ---: | --- |'
)

foreach ($comparison in $comparisons) {
  $signal = if ($comparison.materialRegression) { 'Review' } else { 'Within threshold' }
  $lines += "| $($comparison.method) | $($comparison.timeDeltaPercent)% | $($comparison.allocationDeltaPercent)% | $signal |"
}

$lines += ''
$lines += 'Hosted-runner measurements are evidence for review and do not fail the workflow on threshold crossings.'
$lines | Set-Content -LiteralPath $markdownPath -Encoding utf8

Get-Content -LiteralPath $markdownPath
