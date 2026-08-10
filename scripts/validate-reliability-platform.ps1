[CmdletBinding()]
param(
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string]$Namespace = 'his-hope',
    [string]$RenderedProductionManifest = 'artifacts/k8s/prod.yaml',
    [string]$OutputPath,
    [switch]$RequireCluster
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param(
        [string]$Name,
        [ValidateSet('pass', 'fail', 'skipped', 'unavailable', 'environment-blocked')]
        [string]$Status,
        [string]$Detail
    )
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

function Invoke-KubectlJson {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $errorFile = [IO.Path]::GetTempFileName()
    try {
        $output = & kubectl --kubeconfig $Kubeconfig @Arguments -o json --request-timeout=8s 2> $errorFile
        if ($LASTEXITCODE -ne 0) { throw (Get-Content -LiteralPath $errorFile -Raw -ErrorAction SilentlyContinue) }
        return (($output -join "`n") | ConvertFrom-Json)
    } finally {
        Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repoRoot $RenderedProductionManifest
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Add-Check 'migration-isolation' 'fail' "Rendered production manifest not found: $manifestPath"
} else {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    # Migration Jobs intentionally enable startup migrations. Only serving API
    # Deployments must be checked here; a repository-wide regex would reject
    # the one-shot migration hooks that this gate is meant to require.
    $apiDeployments = @($manifest -split '(?m)^---\s*$' | Where-Object {
            $_ -match '(?m)^kind:\s*Deployment\s*$'
        })
    $startupMigrationDeployments = @($apiDeployments | Where-Object {
            $_ -match '(?im)Persistence__RunMigrationsOnStartup\s*\r?\n\s*value:\s*["'']?true["'']?'
        })
    if ($startupMigrationDeployments.Count -gt 0) {
        Add-Check 'migration-isolation' 'fail' 'A production API enables startup migrations; use the one-shot migration job.'
    } else {
        Add-Check 'migration-isolation' 'pass' ("Production API startup migrations are disabled across {0} Deployment document(s)." -f $apiDeployments.Count)
    }
}

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    $status = if ($RequireCluster) { 'environment-blocked' } else { 'skipped' }
    Add-Check 'cluster-connectivity' $status "Kubeconfig not found: $Kubeconfig"
} else {
    try {
        $null = Invoke-KubectlJson @('version')
        Add-Check 'cluster-connectivity' 'pass' 'Configured Kubernetes API is reachable.'

        $pods = Invoke-KubectlJson @('get', 'pods', '-n', $Namespace)
        if (@($pods.items).Count -eq 0) {
            Add-Check 'pod-health' 'fail' "Namespace $Namespace has no pods."
        } else {
            $bad = @($pods.items | Where-Object {
                    $_.status.phase -ne 'Running' -or
                    @($_.status.containerStatuses | Where-Object {
                            $_.ready -ne $true -or
                            ($_.state.PSObject.Properties.Name -contains 'waiting' -and
                                $_.state.waiting.reason -in @('CrashLoopBackOff', 'ImagePullBackOff', 'ErrImagePull', 'CreateContainerError'))
                        }).Count -gt 0
                })
            if ($bad.Count -eq 0) {
                Add-Check 'pod-health' 'pass' "$(@($pods.items).Count) pod(s) are Running and Ready."
            } else {
                Add-Check 'pod-health' 'fail' "Unhealthy pod count: $($bad.Count)."
            }
        }

        $deployments = Invoke-KubectlJson @('get', 'deployments', '-n', $Namespace)
        $unavailable = @($deployments.items | Where-Object {
                $desired = [int]$_.spec.replicas
                $availableProperty = $_.status.PSObject.Properties['availableReplicas']
                $unavailableProperty = $_.status.PSObject.Properties['unavailableReplicas']
                $available = if ($null -eq $availableProperty) { 0 } else { [int]$availableProperty.Value }
                $notAvailable = if ($null -eq $unavailableProperty) { 0 } else { [int]$unavailableProperty.Value }
                $desired -gt 0 -and ($available -lt $desired -or $notAvailable -gt 0)
            })
        if ($unavailable.Count -eq 0) {
            Add-Check 'deployment-availability' 'pass' "$(@($deployments.items).Count) deployment(s) have all replicas available."
        } else {
            Add-Check 'deployment-availability' 'fail' "Unavailable deployment count: $($unavailable.Count)."
        }
    } catch {
        Add-Check 'cluster-health-queries' 'unavailable' 'Reliability queries failed without exposing command output.'
    }
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -in @('unavailable', 'environment-blocked'))
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'environment-blocked' } elseif (@($checks | Where-Object status -eq 'skipped').Count -gt 0) { 'skipped' } else { 'pass' }
$result = [pscustomobject]@{
    status = $status
    checks = @($checks)
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
}
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 50 }
if ($status -eq 'environment-blocked') { exit 70 }
if ($status -eq 'skipped') { exit 0 }
exit 0
