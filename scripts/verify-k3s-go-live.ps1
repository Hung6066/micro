[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')]
    [string]$Environment = 'production',
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string]$Namespace = 'his-hope',
    [string]$EvidenceDirectory = 'artifacts/evidence',
    [string]$OutputPath,
    [switch]$RequireCluster
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Keep the historical plan entry as a stable public command while the
# implementation remains in the canonical validator.
$validator = Join-Path $PSScriptRoot 'validate-k3s-go-live.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw "Canonical go-live validator not found: $validator"
}

$arguments = @{
    Environment = $Environment
    Kubeconfig = $Kubeconfig
    Namespace = $Namespace
    EvidenceDirectory = $EvidenceDirectory
}
if ($OutputPath) { $arguments.OutputPath = $OutputPath }
if ($RequireCluster) { $arguments.RequireCluster = $true }

& $validator @arguments
exit $LASTEXITCODE
