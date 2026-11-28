[CmdletBinding()]
param(
    [string]$Path = 'artifacts/runtime/compose-dependency-failover.json',
    [double]$MaxRecoverySeconds = 180,
    [double]$RedisSloSeconds = 30,
    [double]$RabbitMqSloSeconds = 60,
    [double]$PostgresSloSeconds = 120
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Failover artifact not found: $Path" }
$rows = @(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
if ($rows.Count -ne 3) { throw "Expected Redis, RabbitMQ and PostgreSQL results; found $($rows.Count)." }
$required = @('redis','rabbitmq','postgres')
foreach ($name in $required) {
    $row = $rows | Where-Object service -eq $name
    if ($null -eq $row -or -not [bool]$row.recovered) { throw "$name did not recover successfully." }
    $serviceSlo = switch ($name) {
        'redis' { $RedisSloSeconds }
        'rabbitmq' { $RabbitMqSloSeconds }
        'postgres' { $PostgresSloSeconds }
    }
    $threshold = [math]::Min($MaxRecoverySeconds, $serviceSlo)
    if ([double]$row.recoverySeconds -gt $threshold) { throw "$name recovery exceeded SLO ${threshold}s." }
    if ($null -eq $row.probeRecoverySeconds) { throw "$name artifact is missing HTTP probe recovery evidence." }
    if ([double]$row.probeRecoverySeconds -gt $threshold) { throw "$name HTTP probe recovery exceeded SLO ${threshold}s." }
    if ($null -eq $row.sloPassed -or -not [bool]$row.sloPassed) { throw "$name artifact does not prove its SLO passed." }
}
Write-Host "Compose dependency failover artifact passed: Redis <= ${RedisSloSeconds}s, RabbitMQ <= ${RabbitMqSloSeconds}s, PostgreSQL <= ${PostgresSloSeconds}s (capped by MaxRecoverySeconds=$MaxRecoverySeconds)."
