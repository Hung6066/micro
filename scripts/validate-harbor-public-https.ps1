[CmdletBinding()]
param(
    [string]$HostName = 'harbor.myduchospital.com',
    [string]$Vip = '172.16.102.100',
    [string]$Kubeconfig = 'D:\AI\micro\artifacts\kubeconfig-production.yaml'
)

$ErrorActionPreference = 'Stop'
$results = [ordered]@{}

function Set-Result([string]$Name, [string]$Status, [string]$Detail) {
    $results[$Name] = [ordered]@{ status = $Status; detail = $Detail }
}

try {
    $dns = @(Resolve-DnsName $HostName -Type A -ErrorAction Stop | ForEach-Object IPAddress)
    if ($dns -contains $Vip) { Set-Result 'dns-vip' 'pass' "$HostName resolves to $Vip" }
    else { Set-Result 'dns-vip' 'fail' "Resolved addresses: $($dns -join ', '); expected $Vip" }
} catch { Set-Result 'dns-vip' 'fail' $_.Exception.Message }

$tcp = Test-NetConnection $HostName -Port 443 -WarningAction SilentlyContinue
if ($tcp.TcpTestSucceeded) { Set-Result 'tcp-443' 'pass' 'VIP accepts TCP/443' }
else { Set-Result 'tcp-443' 'fail' 'VIP does not accept TCP/443; configure HAProxy frontend harbor-https on 443.' }

if (Test-Path $Kubeconfig) {
    $env:KUBECONFIG = $Kubeconfig
    try {
        $ing = kubectl -n harbor get ingress harbor-public -o json 2>$null | ConvertFrom-Json
        if ($null -eq $ing) { throw 'harbor-public ingress not found' }
        if ($ing.spec.tls.hosts -contains $HostName -and $ing.spec.tls.secretName -eq 'harbor-public-tls') {
            Set-Result 'ingress' 'pass' 'Public host and dedicated TLS secret are configured'
        } else { Set-Result 'ingress' 'fail' 'Ingress host/TLS secret does not match the public contract' }
    } catch { Set-Result 'ingress' 'blocked' 'harbor-public ingress is not applied yet' }
    try {
        $secret = kubectl -n harbor get secret harbor-public-tls -o json 2>$null | ConvertFrom-Json
        if ($null -eq $secret) { throw 'harbor-public-tls not found' }
        if ($secret.type -eq 'kubernetes.io/tls') { Set-Result 'tls-secret' 'pass' 'harbor-public-tls exists' }
        else { Set-Result 'tls-secret' 'fail' 'harbor-public-tls has an unexpected type' }
    } catch { Set-Result 'tls-secret' 'blocked' 'Trusted certificate secret has not been provisioned' }
} else { Set-Result 'cluster' 'blocked' "Kubeconfig not found: $Kubeconfig" }

$results | ConvertTo-Json -Depth 5
if (@($results.Values | Where-Object status -eq 'fail').Count -gt 0) { exit 30 }
if (@($results.Values | Where-Object status -in @('blocked','environment-blocked')).Count -gt 0) { exit 70 }
exit 0
