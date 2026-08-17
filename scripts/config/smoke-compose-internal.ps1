[CmdletBinding()]
param(
    [string]$Network = 'docker_default',
    [string]$CurlImage = 'curlimages/curl:8.10.1'
)

$ErrorActionPreference = 'Stop'

$targets = @(
    @{ Name = 'identity-login'; Url = 'http://identityservice:5003/Account/Login' },
    @{ Name = 'external-providers'; Url = 'http://identityservice:5003/api/v1/auth/external-providers' },
    @{ Name = 'gateway-health'; Url = 'http://api-gateway:5000/health' },
    @{ Name = 'frontend'; Url = 'http://frontend:8080/' },
    @{ Name = 'dashboard'; Url = 'http://dashboard-app:8080/' },
    @{ Name = 'admin'; Url = 'http://admin-app:8080/' }
)

$protectedTargets = @(
    @{ Name = 'patient-api-unauthenticated'; Url = 'http://patientservice:5002/api/v1/patients' },
    @{ Name = 'gateway-api-unauthenticated'; Url = 'http://api-gateway:5000/api/v1/patients' },
    @{ Name = 'dashboard-bff-resources-unauthenticated'; Url = 'http://systemdashboard-bff:5700/api/resources' },
    @{ Name = 'dashboard-bff-metrics-unauthenticated'; Url = 'http://systemdashboard-bff:5700/api/metrics/service' }
)

foreach ($target in $targets) {
    & docker run --rm --network $Network $CurlImage -sS --fail --max-time 8 -o /dev/null -w "INTERNAL_SMOKE_PASS $($target.Name) status=%{http_code}\n" $target.Url
    if ($LASTEXITCODE -ne 0) {
        Write-Error "INTERNAL_SMOKE_FAIL $($target.Name) url=$($target.Url) network=$Network"
        exit $LASTEXITCODE
    }
}

foreach ($target in $protectedTargets) {
    $status = (& docker run --rm --network $Network $CurlImage -sS --max-time 8 -o /dev/null -w '%{http_code}' $target.Url).Trim()
    if ($LASTEXITCODE -ne 0 -or $status -ne '401') {
        Write-Error "INTERNAL_SMOKE_FAIL $($target.Name) expected=401 actual=$status url=$($target.Url) network=$Network"
        exit 1
    }
    Write-Output "INTERNAL_SMOKE_PASS $($target.Name) status=$status"
}

Write-Output "COMPOSE_INTERNAL_SMOKE_PASS network=$Network"
