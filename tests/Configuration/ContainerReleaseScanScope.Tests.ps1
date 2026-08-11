$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflowPath = Join-Path $repositoryRoot '.github\workflows\container-release.yml'
$workflow = Get-Content -LiteralPath $workflowPath -Raw
$dashboardDockerfile = Get-Content -LiteralPath (Join-Path $repositoryRoot 'dashboard-app\Dockerfile') -Raw
$dashboardNginx = Get-Content -LiteralPath (Join-Path $repositoryRoot 'dashboard-app\nginx.conf') -Raw
$dashboardDeployment = Get-Content -LiteralPath (Join-Path $repositoryRoot 'k8s\base\dashboard-app-deployment.yaml') -Raw
$dashboardService = Get-Content -LiteralPath (Join-Path $repositoryRoot 'k8s\base\dashboard-app-service.yaml') -Raw
$migrationJob = Get-Content -LiteralPath (Join-Path $repositoryRoot 'cockroach\config\migration-job.yaml') -Raw
$cockroachStatefulSet = Get-Content -LiteralPath (Join-Path $repositoryRoot 'cockroach\config\cockroachdb-statefulset.yaml') -Raw

if ($workflow -notmatch '(?ms)scan-type:\s*fs.*?scan-ref:\s*\..*?skip-dirs:\s*k8s,docker/spire.*?skip-files:\s*docker/postgres-production\.Dockerfile') {
    throw 'The filesystem Trivy preflight must exclude k8s, docker/spire, and the non-release postgres bootstrap helper. Rendered production manifests and release images remain covered by dedicated validators and image scans.'
}

Write-Host 'Container release Trivy scope PASS: raw Kubernetes manifests and non-release SPIRE Compose helpers are excluded from the filesystem preflight.'

if ($dashboardDockerfile -notmatch '(?m)^FROM nginxinc/nginx-unprivileged:alpine AS final$' -or
    $dashboardDockerfile -notmatch '(?m)^EXPOSE 8080$' -or
    $dashboardNginx -notmatch '(?m)^\s*listen 8080;') {
    throw 'dashboard-app must use the non-root nginx runtime and listen on 8080.'
}

if ($dashboardDeployment -notmatch '(?m)containerPort: 8080' -or
    $dashboardDeployment -notmatch '(?m)port: 8080') {
    throw 'dashboard-app Kubernetes probes and container port must target the non-root nginx port 8080.'
}

if ($dashboardService -notmatch '(?m)targetPort: 8080') {
    throw 'dashboard-app Service must translate port 80 to the non-root container port 8080.'
}

foreach ($required in @(
    'runAsNonRoot: true',
    'allowPrivilegeEscalation: false',
    'readOnlyRootFilesystem: true',
    'type: RuntimeDefault'
)) {
    if ($migrationJob -notmatch [regex]::Escape($required)) {
        throw "Cockroach migration Job is missing required security control: $required"
    }
}

Write-Host 'Container runtime security PASS: dashboard nginx and Cockroach migration Job are non-root and restricted.'

foreach ($required in @(
    'runAsNonRoot: true',
    'allowPrivilegeEscalation: false',
    'readOnlyRootFilesystem: true',
    'type: RuntimeDefault'
)) {
    if ($cockroachStatefulSet -notmatch [regex]::Escape($required)) {
        throw "CockroachDB StatefulSet is missing required security control: $required"
    }
}

Write-Host 'Container runtime security PASS: CockroachDB StatefulSet is non-root and restricted.'
