[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$overlay = Join-Path $repoRoot "k8s\overlays\$Environment"
if (-not (Test-Path -LiteralPath $overlay -PathType Container)) {
    throw "Kustomize overlay not found: $overlay"
}

$rendered = @(kubectl kustomize $overlay --load-restrictor LoadRestrictionsNone 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Kustomize render failed for [$Environment].`n$($rendered -join "`n")"
}

$yaml = $rendered -join "`n"
if ([string]::IsNullOrWhiteSpace($yaml)) {
    throw "Kustomize rendered an empty manifest for [$Environment]."
}

$output = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $repoRoot "artifacts\k8s\$Environment.yaml"
} else {
    $OutputPath
}

$outputDirectory = Split-Path -Parent $output
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path (Resolve-Path $outputDirectory).Path (Split-Path -Leaf $output)
[System.IO.File]::WriteAllText($outputFile, $yaml, [System.Text.UTF8Encoding]::new($false))

$documentCount = ([regex]::Matches($yaml, '(?m)^---\s*$')).Count + 1
Write-Output "Kustomize render PASS: environment=$Environment documents=$documentCount output=$output"
