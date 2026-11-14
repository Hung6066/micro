[CmdletBinding()]
param(
    [string]$ComposeDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'docker'),
    [string]$PostgresHost = 'localhost',
    [int]$PostgresPort = 5433,
    [string]$PostgresUser = 'postgres',
    [string]$PostgresPassword = 'postgres',
    [string]$Database = 'identitydb',
    [switch]$SkipRestart
)

$ErrorActionPreference = 'Stop'

function Get-PilotUsersFromDatabase {
    $env:PGPASSWORD = $PostgresPassword
    $query = @'
SELECT user_name, email, is_active
FROM asp_net_users
WHERE user_name LIKE '%.pilot'
ORDER BY user_name;
'@
    $output = & psql -h $PostgresHost -p $PostgresPort -U $PostgresUser -d $Database -At -F '|' -c $query
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query pilot users from ${Database}."
    }

    foreach ($line in $output) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '\|', 3
        [pscustomobject]@{
            UserName = $parts[0]
            Email = $parts[1]
            IsActive = $parts[2]
        }
    }
}

function Test-PilotLogin {
    param(
        [string]$BaseUrl,
        [string]$UserName,
        [string]$Password
    )

    $body = @{ username = $UserName; password = $Password } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/v1/auth/login" -Method POST -ContentType 'application/json' -Body $body | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

$expectedPilots = @('manufacturing.pilot', 'tech.pilot', 'hq.pilot')
$password = $env:CONGLOMERATE_PILOT_PASSWORD
if ([string]::IsNullOrWhiteSpace($password)) {
    $password = 'ConglomeratePilot@Dev1'
}

Write-Host "Checking pilot users in ${Database}..."
$existing = @(Get-PilotUsersFromDatabase)
$missing = @($expectedPilots | Where-Object { $_ -notin $existing.UserName })

if ($missing.Count -gt 0) {
    Write-Host "Missing pilot users: $($missing -join ', ')"
}
else {
    Write-Host "All pilot users exist in the database."
}

if (-not $SkipRestart) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'docker is required to restart identityservice. Re-run with -SkipRestart after restarting Identity manually.'
    }

    Push-Location $ComposeDirectory
    try {
        $env:CONGLOMERATE_PILOT_PASSWORD = $password
        $env:CONGLOMERATE_RESET_PILOT_PASSWORDS = 'true'
        Write-Host 'Restarting identityservice to run conglomerate pilot seed...'
        docker compose up -d --build identityservice | Out-Host
        Start-Sleep -Seconds 20
    }
    finally {
        Pop-Location
    }
}

$existing = @(Get-PilotUsersFromDatabase)
$missing = @($expectedPilots | Where-Object { $_ -notin $existing.UserName })
if ($missing.Count -gt 0) {
    throw "Pilot seed incomplete. Still missing: $($missing -join ', '). Check identity logs for 'conglomerate pilot user'."
}

Write-Host 'Pilot users present:'
$existing | Format-Table -AutoSize

foreach ($baseUrl in @('http://localhost:5000', 'http://localhost:5001')) {
    $okCount = 0
    foreach ($pilot in $expectedPilots) {
        if (Test-PilotLogin -BaseUrl $baseUrl -UserName $pilot -Password $password) {
            $okCount++
        }
    }

    if ($okCount -gt 0) {
        Write-Host "Login API check via $baseUrl : $okCount/$($expectedPilots.Count) pilots succeeded."
        break
    }
}

Write-Host "Pilot password: $password"
Write-Host 'Use email or username on the Identity login page.'
