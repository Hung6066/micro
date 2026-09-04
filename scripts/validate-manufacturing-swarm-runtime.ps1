[CmdletBinding()]
param(
    [string]$StackName = 'manufacturing',
    [string]$NetworkName = 'manufacturing_swarm',
    [int]$TimeoutSeconds = 120,
    [switch]$ExerciseScaleAndRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string]$Message) { throw "MANUFACTURING_SWARM_RUNTIME_FAIL $Message" }
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { Fail $Message } }

$expected = @{
    "${StackName}_identityservice" = 2
    "${StackName}_commerceservice" = 2
    "${StackName}_contentservice" = 2
    "${StackName}_manufacturingservice" = 3
    "${StackName}_manufacturing-worker" = 1
}

function Get-ReplicaCount([string]$Service) {
    $value = (& docker service ls --filter "name=$Service" --format '{{.Replicas}}' 2>&1 | Select-Object -First 1).ToString().Trim()
    Require ($LASTEXITCODE -eq 0 -and $value -match '^([0-9]+)/([0-9]+)$') "service not found or invalid replicas: $Service ($value)"
    return [int]$Matches[1], [int]$Matches[2]
}

function WaitForReplicas([string]$Service, [int]$Expected) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $counts = Get-ReplicaCount $Service
            if ($counts[0] -eq $Expected -and $counts[1] -eq $Expected) { return }
        } catch { }
        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)
    $counts = Get-ReplicaCount $Service
    Fail "replicas not converged: $Service expected $Expected, got $($counts[0])/$($counts[1])"
}

Require ((& docker info --format '{{.Swarm.LocalNodeState}}').ToString().Trim() -eq 'active') 'Swarm manager is not active'
Require ((& docker network inspect $NetworkName --format '{{.Driver}} {{.Attachable}}').ToString().Trim() -eq 'overlay true') 'expected attachable overlay network is unavailable'

foreach ($entry in $expected.GetEnumerator()) {
    WaitForReplicas $entry.Key $entry.Value
}

$containerIds = & docker ps --filter "label=com.docker.swarm.service.name=${StackName}_manufacturingservice" --format '{{.ID}}'
$containerIds += & docker ps --filter "label=com.docker.swarm.service.name=${StackName}_manufacturing-worker" --format '{{.ID}}'
Require ($containerIds.Count -eq 4) "expected four Manufacturing containers, found $($containerIds.Count)"
foreach ($id in $containerIds) {
    $health = (& docker inspect $id --format '{{.State.Health.Status}}').ToString().Trim()
    Require ($health -eq 'healthy') "container is not healthy: $id ($health)"
}

function Probe([string]$Url, [int]$ExpectedStatus) {
    $statusText = (& docker run --rm --network $NetworkName curlimages/curl:8.10.1 -sS -o /dev/null -w '%{http_code}' $Url) -join ''
    $status = [int]$statusText.Trim()
    Require ($status -eq $ExpectedStatus) "probe failed: $Url expected $ExpectedStatus got $status"
}

Probe 'http://manufacturingservice:5050/health/live' 200
Probe 'http://manufacturingservice:5050/health/ready' 200
Probe 'http://identityservice:5003/health/ready' 200
Probe 'http://commerceservice:5015/health/ready' 200
Probe 'http://contentservice:5016/health/ready' 200
Probe 'http://manufacturingservice:5050/api/v1/manufacturing/recipes' 401

if ($ExerciseScaleAndRestart) {
    & docker service scale --detach=true "${StackName}_manufacturingservice=4" | Out-Null
    WaitForReplicas "${StackName}_manufacturingservice" 4

    & docker service scale --detach=true "${StackName}_manufacturingservice=3" | Out-Null
    & docker service update --force --detach=true "${StackName}_manufacturingservice" | Out-Null
    WaitForReplicas "${StackName}_manufacturingservice" 3
}

Write-Output "MANUFACTURING_SWARM_RUNTIME_PASS stack=$StackName network=$NetworkName"
