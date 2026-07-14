$ErrorActionPreference = 'Stop'

$RootDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$OutputDir = if ($args.Count -gt 0 -and $args[0]) { $args[0] } else { Join-Path $RootDir 'generated-manifests' }
$ReleaseName = if ($env:RELEASE_NAME) { $env:RELEASE_NAME } else { 'vllm' }
$ChartDir = Join-Path $RootDir 'helm'
$ValuesFile = Join-Path $RootDir 'values.yaml'
$ObservabilityValuesFile = Join-Path $RootDir 'values.observability.yaml'

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function Render-Manifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputFile,
        [Parameter(Mandatory = $true)]
        [string[]]$ValuesArgs
    )

    Write-Host "Rendering $OutputFile"
    $OutputPath = Join-Path $OutputDir $OutputFile
    & helm template $ReleaseName $ChartDir @ValuesArgs | Set-Content -NoNewline $OutputPath
}

Render-Manifest -OutputFile 'baseline.yaml' -ValuesArgs @('-f', $ValuesFile)
Render-Manifest -OutputFile 'observability.yaml' -ValuesArgs @('-f', $ValuesFile, '-f', $ObservabilityValuesFile)

Write-Host "Rendered manifests into $OutputDir"