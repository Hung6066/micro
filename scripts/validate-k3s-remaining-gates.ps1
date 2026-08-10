[CmdletBinding()]
param(
    [string]$Kubeconfig = 'D:\AI\micro\artifacts\kubeconfig-production.yaml',
    [string]$OutputPath,
    [string]$BackupTimerUnit = 'his-hope-k3s-etcd-snapshot.timer',
    [string]$BackupServiceUnit = 'his-hope-k3s-etcd-snapshot.service',
    [string]$InventoryPath,
    [string]$SshKeyPath
)

$ErrorActionPreference = 'Stop'
$env:KUBECONFIG = $Kubeconfig
$out = [ordered]@{}
function Gate([string]$Name, [string]$Status, [string]$Detail) { $out[$Name] = @{ status = $Status; detail = $Detail } }

try {
    $providers = @(kubectl get providers.externaldata.gatekeeper.sh -o name 2>$null)
    $policyCrd = @(kubectl get crd clusterimagepolicies.policy.sigstore.dev -o name 2>$null)
    $policyPods = @((kubectl get pods -n cosign-system -l app.kubernetes.io/name=policy-controller -o json 2>$null | ConvertFrom-Json).items)
    $policyReady = @($policyPods | Where-Object {
            $_.status.phase -eq 'Running' -and
            @($_.status.containerStatuses | Where-Object { -not $_.ready }).Count -eq 0
        }).Count -gt 0
    if ($providers.Count -gt 0) {
        Gate 'signature-provider' 'pass' ("Gatekeeper ExternalData provider(s): " + ($providers -join ', '))
    } elseif ($policyCrd.Count -gt 0 -and $policyReady) {
        Gate 'signature-provider' 'pass' 'Sigstore Policy Controller CRD and ready webhook are present'
    } else {
        Gate 'signature-provider' 'blocked' 'No ready Gatekeeper ExternalData provider/Ratify or Sigstore Policy Controller was found'
    }
} catch { Gate 'signature-provider' 'blocked' $_.Exception.Message }

try {
    $psa = kubectl get ns his-hope -o json | ConvertFrom-Json
    $enforce = $psa.metadata.labels.'pod-security.kubernetes.io/enforce'
    if ($enforce -eq 'restricted') { Gate 'pod-security' 'pass' 'his-hope enforce=restricted' }
    else { Gate 'pod-security' 'fail' "his-hope enforce=$enforce" }
} catch { Gate 'pod-security' 'blocked' $_.Exception.Message }

try {
    $sc = @(kubectl get storageclass -o json | ConvertFrom-Json).items
    $production = @($sc | Where-Object { $_.metadata.name -notmatch 'local-path' -and $_.metadata.name -notmatch 'standard' })
    $snapshotClasses = @(kubectl get volumesnapshotclass.snapshot.storage.k8s.io -o name 2>$null)
    if ($production.Count -gt 0 -and $snapshotClasses.Count -gt 0) {
        Gate 'csi-storage' 'pass' ("storageClasses={0}; volumeSnapshotClasses={1}" -f (($production.metadata.name) -join ','), ($snapshotClasses -join ','))
    } elseif ($production.Count -eq 0) {
        Gate 'csi-storage' 'blocked' 'Only local-path storage is available; production CSI/replication is not configured'
    } else {
        Gate 'csi-storage' 'blocked' 'A production CSI storage class exists but no VolumeSnapshotClass is installed'
    }
} catch { Gate 'csi-storage' 'blocked' $_.Exception.Message }

try {
    $argo = @(kubectl get ns argocd -o name 2>$null)
    if ($argo.Count -gt 0) { Gate 'gitops-controller' 'pass' 'argocd namespace exists' }
    else { Gate 'gitops-controller' 'blocked' 'Argo CD is not installed; bootstrap remains source-only' }
} catch { Gate 'gitops-controller' 'blocked' $_.Exception.Message }

try {
    $otel = @(kubectl get pods -n monitoring -l app.kubernetes.io/name=opentelemetry-collector -o json | ConvertFrom-Json).items
    $bad = @($otel | Where-Object { $_.status.phase -ne 'Running' -or @($_.status.containerStatuses | Where-Object { -not $_.ready }).Count -gt 0 })
    if ($otel.Count -gt 0 -and $bad.Count -eq 0) { Gate 'observability' 'pass' "$($otel.Count) OTEL collector pods Ready" }
    else { Gate 'observability' 'fail' 'OTEL collector has no fully ready workload' }
} catch { Gate 'observability' 'blocked' $_.Exception.Message }

try {
    $remoteRecords = @()
    if ($SshKeyPath -or $InventoryPath) {
        if ([string]::IsNullOrWhiteSpace($SshKeyPath) -or -not (Test-Path -LiteralPath $SshKeyPath -PathType Leaf)) {
            throw 'SshKeyPath is required and must point to a readable private key when remote backup audit is enabled.'
        }
        if ([string]::IsNullOrWhiteSpace($InventoryPath) -or -not (Test-Path -LiteralPath $InventoryPath -PathType Leaf)) {
            throw 'InventoryPath is required and must point to the production inventory when remote backup audit is enabled.'
        }
        $inventoryLines = Get-Content -LiteralPath $InventoryPath
        $hosts = [System.Collections.Generic.List[object]]::new()
        for ($index = 0; $index -lt $inventoryLines.Count; $index++) {
            if ($inventoryLines[$index] -match '^\s{8}(k3s-server-\d+):\s*$') {
                $name = $Matches[1]
                $hostAddress = $null
                $userName = $null
                for ($next = $index + 1; $next -lt [Math]::Min($index + 5, $inventoryLines.Count); $next++) {
                    if ($inventoryLines[$next] -match '^\s+ansible_host:\s*(\S+)') { $hostAddress = $Matches[1] }
                    if ($inventoryLines[$next] -match '^\s+ansible_user:\s*(\S+)') { $userName = $Matches[1] }
                }
                if ($hostAddress -and $userName) { $hosts.Add([pscustomobject]@{ Name = $name; HostAddress = $hostAddress; UserName = $userName }) }
            }
        }
        if ($hosts.Count -ne 3) { throw "Expected three K3s server hosts in inventory, found $($hosts.Count)." }
        $remoteCommand = @'
timer=$(systemctl is-enabled __BACKUP_TIMER__ 2>/dev/null || true)
timer_state=$(systemctl is-active __BACKUP_TIMER__ 2>/dev/null || true)
result=$(systemctl show __BACKUP_SERVICE__ --property=Result --value 2>/dev/null || true)
exit_status=$(systemctl show __BACKUP_SERVICE__ --property=ExecMainStatus --value 2>/dev/null || true)
printf 'TIMER_ENABLED=%s|TIMER_STATE=%s|RESULT=%s|EXIT_STATUS=%s\n' "$timer" "$timer_state" "$result" "$exit_status"
'@
        $remoteCommand = $remoteCommand.Replace('__BACKUP_TIMER__', $BackupTimerUnit).Replace('__BACKUP_SERVICE__', $BackupServiceUnit)
        foreach ($node in $hosts) {
            $target = "$($node.UserName)@$($node.HostAddress)"
            $raw = @(& ssh.exe -i $SshKeyPath -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 $target $remoteCommand 2>&1)
            if ($LASTEXITCODE -ne 0) { throw "SSH audit failed for $($node.Name)." }
            $values = @{}
            foreach ($line in $raw) {
                foreach ($part in ($line -split '\|')) {
                    if ($part -match '^(TIMER_ENABLED|TIMER_STATE|RESULT|EXIT_STATUS)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim() }
                }
            }
            $remoteRecords += [ordered]@{
                name = $node.Name
                host = $node.HostAddress
                timerEnabled = $values['TIMER_ENABLED']
                timerState = $values['TIMER_STATE']
                result = $values['RESULT']
                exitStatus = $values['EXIT_STATUS']
                status = if ($values['TIMER_ENABLED'] -eq 'enabled' -and $values['TIMER_STATE'] -eq 'active' -and $values['RESULT'] -eq 'success' -and $values['EXIT_STATUS'] -eq '0') { 'pass' } else { 'fail' }
            }
        }
        $bad = @($remoteRecords | Where-Object status -ne 'pass')
        if ($bad.Count -eq 0) { Gate 'azure-backup' 'pass' "Remote audit passed on $($remoteRecords.Count) control-plane hosts" }
        else { Gate 'azure-backup' 'fail' "Remote audit failed on $($bad.name -join ', '); inspect Result/ExecMainStatus and Azure permissions" }
    } else {
        $timer = @(systemctl is-enabled $BackupTimerUnit 2>$null) | Select-Object -First 1
        $timerState = @(systemctl is-active $BackupTimerUnit 2>$null) | Select-Object -First 1
        $result = @(systemctl show $BackupServiceUnit --property=Result --value 2>$null) | Select-Object -First 1
        $exitStatus = @(systemctl show $BackupServiceUnit --property=ExecMainStatus --value 2>$null) | Select-Object -First 1
        if ($timer -eq 'enabled' -and $timerState -eq 'active' -and $result -eq 'success' -and $exitStatus -eq '0') {
            Gate 'azure-backup' 'pass' "Timer enabled/active and oneshot Result=success ($BackupServiceUnit)"
        } else {
            Gate 'azure-backup' 'blocked' "timer=$timer timerState=$timerState result=$result exitStatus=$exitStatus; pass -SshKeyPath and -InventoryPath for remote audit"
        }
    }
} catch { Gate 'azure-backup' 'blocked' $_.Exception.Message }

$checks = @($out.GetEnumerator() | ForEach-Object {
        [pscustomobject]@{
            name = $_.Key
            status = $_.Value.status
            detail = $_.Value.detail
        }
    })
$status = if (@($checks | Where-Object status -eq 'fail').Count -gt 0) {
    'fail'
} elseif (@($checks | Where-Object status -in @('blocked', 'environment-blocked')).Count -gt 0) {
    'environment-blocked'
} else {
    'pass'
}
$result = [pscustomobject]@{
    status = $status
    checks = $checks
    backupHosts = @($remoteRecords)
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
}
$json = $result | ConvertTo-Json -Depth 5
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [System.IO.File]::WriteAllText(
        [System.IO.Path]::GetFullPath($OutputPath),
        $json,
        [System.Text.UTF8Encoding]::new($false))
}
Write-Output $json
if (@($out.Values | Where-Object status -eq 'fail').Count -gt 0) { exit 30 }
if (@($out.Values | Where-Object status -in @('blocked','environment-blocked')).Count -gt 0) { exit 70 }
exit 0
