[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [ValidateSet('docker','kubernetes','vm')] [string]$Runtime,
    [string]$ComposeFile = "$PSScriptRoot\..\..\docker\docker-compose.yml",
    [string]$Namespace = 'his-hope',
    [string]$Deployment = ''
)
$ErrorActionPreference = 'Stop'
switch ($Runtime) {
    'docker' {
        if ($PSCmdlet.ShouldProcess($ComposeFile, 'restart compose stack')) { docker compose -f $ComposeFile restart }
    }
    'kubernetes' {
        if ([string]::IsNullOrWhiteSpace($Deployment)) { throw 'Deployment is required for Kubernetes rollback.' }
        if ($PSCmdlet.ShouldProcess($Deployment, 'undo rollout')) { kubectl -n $Namespace rollout undo deployment/$Deployment }
    }
    'vm' { Write-Output 'VM rollback is service-manager controlled; restore the previous signed release and restart the unit.' }
}
