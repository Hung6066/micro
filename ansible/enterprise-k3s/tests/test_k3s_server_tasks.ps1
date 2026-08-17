$ErrorActionPreference = 'Stop'

$taskPath = Join-Path $PSScriptRoot '..\roles\k3s_server\tasks\main.yml'
$task = Get-Content -LiteralPath $taskPath -Raw

if ($task -notmatch 'journalctl -u k3s -n 80') {
    throw 'Readiness failure must collect a bounded K3s journal.'
}
if ($task -notmatch 'systemctl show k3s -p ActiveState -p SubState -p ExecMainStatus') {
    throw 'Readiness failure must collect K3s service state.'
}
if ($task -notmatch 'regex_replace\(.*token\|sas\|secret\|password') {
    throw 'Readiness diagnostics must redact credential-like values.'
}
if ($task -match 'cat\s+/etc/rancher/k3s/config\.yaml') {
    throw 'Diagnostic must not print the K3s configuration file.'
}
if ($task -notmatch '(?s)block:.*?rescue:') {
    throw 'Readiness probe must use a bounded rescue path.'
}

Write-Output 'K3s readiness diagnostic contract: PASS'
