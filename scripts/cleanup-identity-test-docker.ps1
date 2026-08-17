[CmdletBinding()]
param(
    [string]$RunId,
    [switch]$IncludeRunning
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$namePattern = if ([string]::IsNullOrWhiteSpace($RunId)) {
    '^identity-docker-(runner|pg|redis)-[0-9a-f]{10}$'
} else {
    $escaped = [regex]::Escape($RunId)
    "^identity-docker-(runner|pg|redis)-$escaped$"
}
$networkPattern = if ([string]::IsNullOrWhiteSpace($RunId)) {
    '^identity-docker-net-[0-9a-f]{10}$'
} else {
    "^identity-docker-net-$([regex]::Escape($RunId))$"
}

$containers = @(docker ps -a --format '{{.Names}} {{.Status}}' | ForEach-Object {
    $parts = $_ -split ' ', 2
    if ($parts.Count -eq 2 -and $parts[0] -match $namePattern) {
        [pscustomobject]@{ Name = $parts[0]; Status = $parts[1] }
    }
})

foreach ($container in $containers) {
    $isRunning = $container.Status -match '^(Up|Restarting)'
    if ($isRunning -and -not $IncludeRunning) {
        Write-Warning "Skipping running test container '$($container.Name)'. Use -IncludeRunning only for an explicitly abandoned run."
        continue
    }
    docker rm -f $container.Name | Out-Null
    Write-Output "Removed test container: $($container.Name)"
}

$networks = @(docker network ls --format '{{.Name}}' | Where-Object {
    $_ -match $networkPattern
})
foreach ($network in $networks) {
    docker network rm $network 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Output "Removed test network: $network" }
}
