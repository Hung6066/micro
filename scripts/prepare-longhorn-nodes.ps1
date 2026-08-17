[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [Parameter(Mandatory = $true)][string]$Kubeconfig,
    [Parameter(Mandatory = $true)][string]$Inventory,
    [Parameter(Mandatory = $true)][string]$SshKeyPath,
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if (-not (Test-Path -LiteralPath $Inventory -PathType Leaf)) { throw "Inventory not found: $Inventory" }
if (-not (Test-Path -LiteralPath $SshKeyPath -PathType Leaf)) { throw "SSH key not found: $SshKeyPath" }
if ($Environment -eq 'production' -and $Apply -and -not $AllowProduction) {
    throw 'Production node labeling requires explicit -AllowProduction.'
}
if (-not (Get-Command ansible-playbook -ErrorAction SilentlyContinue)) {
    throw 'ansible-playbook is required for the read-only storage audit.'
}

$audit = Join-Path $PSScriptRoot '..\ansible\enterprise-k3s\playbooks\25-validate-storage-prerequisites.yml'
$audit = (Resolve-Path -LiteralPath $audit).Path
$env:ANSIBLE_HOST_KEY_CHECKING = 'False'
& ansible-playbook -i $Inventory $audit --private-key $SshKeyPath --forks 5
if ($LASTEXITCODE -ne 0) { throw 'Storage prerequisite audit failed; no Kubernetes labels were changed.' }

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$nodeDocument = kubectl get nodes -o json | ConvertFrom-Json
$nodes = @($nodeDocument.items)
if ($LASTEXITCODE -ne 0 -or $nodes.Count -eq 0) { throw 'Unable to read Kubernetes nodes.' }
$missing = @($nodes | Where-Object {
        $labels = $_.metadata.labels
        $null -eq $labels -or
        @($labels.PSObject.Properties.Name) -notcontains 'his-hope.io/longhorn-data-ready' -or
        $labels.PSObject.Properties['his-hope.io/longhorn-data-ready'].Value -ne 'true'
    })

if (-not $Apply) {
    if ($missing.Count -gt 0) {
        Write-Output ("DRY-RUN: would label nodes: " + (($missing | ForEach-Object { $_.metadata.name }) -join ', '))
    } else {
        Write-Output 'DRY-RUN: all nodes already have his-hope.io/longhorn-data-ready=true.'
    }
    exit 0
}

foreach ($node in $missing) {
    $name = [string]$node.metadata.name
    if ($PSCmdlet.ShouldProcess("node/$name", 'Set Longhorn data readiness label')) {
        & kubectl label node $name 'his-hope.io/longhorn-data-ready=true' '--overwrite'
        if ($LASTEXITCODE -ne 0) { throw "Failed to label node $name." }
    }
}

$verificationDocument = kubectl get nodes -o json | ConvertFrom-Json
$unready = @($verificationDocument.items | Where-Object {
    $labels = $_.metadata.labels
    $null -eq $labels -or
    @($labels.PSObject.Properties.Name) -notcontains 'his-hope.io/longhorn-data-ready' -or
    $labels.PSObject.Properties['his-hope.io/longhorn-data-ready'].Value -ne 'true'
})
if (@($unready).Count -gt 0) { throw 'Readiness label verification failed.' }
Write-Output 'Longhorn node preparation PASS: audit passed and every node is labeled.'
