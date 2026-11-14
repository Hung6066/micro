param(
    [string]$Spec = '',
    [string]$Config = '',
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
$configTarget = if ([string]::IsNullOrWhiteSpace($Config)) { '' } else { " --config=$Config" }
$testTarget = if ([string]::IsNullOrWhiteSpace($Spec)) { '' } else { " $Spec" }
$publicSuite = $Config -match 'public|manufacturing-buyer\.playwright' -or
    $Spec -match 'public|manufacturing-buyer-ui-tests'
$authSpec = -not $publicSuite -and
    ([string]::IsNullOrWhiteSpace($Spec) -or $Spec -match '00-sso|01-auth|shared-foundation')
if (($authSpec -or $authRequired -eq 'true') -and
    ([string]::IsNullOrWhiteSpace($env:E2E_EMAIL) -or [string]::IsNullOrWhiteSpace($env:E2E_PASSWORD))) {
    throw 'Authenticated E2E requires E2E_EMAIL and E2E_PASSWORD from local secret storage; refusing default credentials.'
}

# The browser container is disposable. The proxy only forwards to existing
# application services on docker_default and never creates or removes them.
$inner = @"
set -e
node support/docker-network-proxy.js >/tmp/his-hope-e2e-proxy.log 2>&1 &
npm ci --ignore-scripts >/dev/null
E2E_RETAIN_ARTIFACTS=$artifactMode E2E_CLINICAL_URL=http://localhost:8081 E2E_DASHBOARD_URL=http://localhost:8082 E2E_ADMIN_URL=http://localhost:8083 node node_modules/playwright/cli.js test$configTarget$testTarget --project=chromium --workers=$Workers --reporter=line
"@
# PowerShell preserves the repository's CRLF line endings in here-strings.
# Bash in the Linux Playwright container requires LF-only commands; normalize
# before passing the script through `bash -lc`.
$inner = $inner -replace "`r`n", "`n"

$dockerEnv = @('-e', "E2E_AUTH_REQUIRED=$authRequired")
if ($env:E2E_EMAIL) { $dockerEnv += @('-e', "E2E_EMAIL=$env:E2E_EMAIL") }
if ($env:E2E_PASSWORD) { $dockerEnv += @('-e', "E2E_PASSWORD=$env:E2E_PASSWORD") }

$dockerArgs = @(
    'run', '--rm', '--network', $Network,
    '-v', "$sourceRoot`:/src",
    '-v', "$e2eRoot`:/work",
    '-w', '/work',
    # Keep npm ci isolated per disposable runner. The test tree is bind
    # mounted for specs/artifacts, but dependencies must not race between
    # parallel public/authenticated suites.
    '--tmpfs', '/work/node_modules:rw,nosuid,nodev,size=512m'
) + $dockerEnv + @(
    $Image, 'bash', '-lc', $inner
)
& docker @dockerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Docker E2E runner failed with exit code $LASTEXITCODE."
}
