param(
    [string]$Spec = '',
    [string]$Network = 'docker_default',
    [string]$Image = 'mcr.microsoft.com/playwright:v1.61.1-noble',
    [int]$Workers = 1,
    [switch]$RetainArtifacts
)

$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path
$e2eRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\tests\e2e')).Path
$artifactMode = if ($RetainArtifacts) { 'true' } else { 'false' }
$authRequired = if ($env:E2E_AUTH_REQUIRED) { $env:E2E_AUTH_REQUIRED } else { 'false' }
$testTarget = if ([string]::IsNullOrWhiteSpace($Spec)) { '' } else { " $Spec" }
$authSpec = [string]::IsNullOrWhiteSpace($Spec) -or $Spec -match '00-sso|01-auth|shared-foundation'
if (($authSpec -or $authRequired -eq 'true') -and [string]::IsNullOrWhiteSpace($env:E2E_PASSWORD)) {
    throw 'Authenticated E2E requires E2E_PASSWORD from local secret storage; refusing to use a default password.'
}

# The browser container is disposable. The proxy only forwards to existing
# application services on docker_default and never creates or removes them.
$inner = @"
set -e
node support/docker-network-proxy.js >/tmp/his-hope-e2e-proxy.log 2>&1 &
npm ci --ignore-scripts >/dev/null
E2E_RETAIN_ARTIFACTS=$artifactMode E2E_CLINICAL_URL=http://127.0.0.1:8081 E2E_DASHBOARD_URL=http://127.0.0.1:8082 E2E_ADMIN_URL=http://127.0.0.1:8083 npx playwright test$testTarget --project=chromium --workers=$Workers --reporter=line
"@

$dockerEnv = @('-e', "E2E_AUTH_REQUIRED=$authRequired")
if ($env:E2E_EMAIL) { $dockerEnv += @('-e', "E2E_EMAIL=$env:E2E_EMAIL") }
if ($env:E2E_PASSWORD) { $dockerEnv += @('-e', "E2E_PASSWORD=$env:E2E_PASSWORD") }

$dockerArgs = @(
    'run', '--rm', '--network', $Network,
    '-v', "$sourceRoot`:/src",
    '-v', "$e2eRoot`:/work",
    '-w', '/work'
) + $dockerEnv + @(
    $Image, 'bash', '-lc', $inner
)
& docker @dockerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Docker E2E runner failed with exit code $LASTEXITCODE."
}
