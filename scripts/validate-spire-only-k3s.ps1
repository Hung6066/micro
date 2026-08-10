[CmdletBinding()]
param(
    [string]$Context = 'k3d-his-hope',
    [string]$DevNamespace = 'his-hope-dev',
    [string]$ProdNamespace = 'his-hope',
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (Test-Path -LiteralPath $Kubeconfig -PathType Leaf) {
    $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
    $Context = (& kubectl config current-context).Trim()
}

function Invoke-Kubectl {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & kubectl --context $Context @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join [Environment]::NewLine) }
    return ($output -join [Environment]::NewLine)
}

function Assert-NoLegacyMarkers {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Manifest)
    $legacy = @()
    if ($Manifest -match 'Vault__JwtTokenFile') { $legacy += 'Vault__JwtTokenFile' }
    if ($Manifest -match '(?ms)Vault__AuthMount\s*\n\s*value:\s*kubernetes') {
        $legacy += 'Vault__AuthMount=kubernetes'
    }
    if ($legacy.Count -gt 0) {
        throw "$Name contains legacy workload identity markers: $($legacy -join ', ')"
    }
    $fetchers = ([regex]::Matches($Manifest, 'name: spire-jwt-fetcher')).Count
    if ($fetchers -lt 7) { throw "$Name contains only $fetchers SPIRE fetcher entries; expected at least 7" }
    Write-Host "$Name render: PASS (SPIRE-only workload auth markers)"
}

$dev = Invoke-Kubectl -Arguments @('kustomize', (Join-Path $repoRoot 'k8s/overlays/dev'), '--load-restrictor', 'LoadRestrictionsNone')
Assert-NoLegacyMarkers -Name 'dev' -Manifest $dev

# Production references selected files outside the overlay directory. Keep the
# load restriction explicit so the validator uses the same safe, declarative
# build mode as the production deployment wrapper.
$prod = Invoke-Kubectl -Arguments @('kustomize', '--load-restrictor', 'LoadRestrictionsNone', (Join-Path $repoRoot 'k8s/overlays/prod'))
Assert-NoLegacyMarkers -Name 'prod' -Manifest $prod

foreach ($namespace in @($DevNamespace, $ProdNamespace)) {
    $exists = & kubectl --context $Context get namespace $namespace --ignore-not-found -o name 2>$null
    if (-not $exists) {
        Write-Host "$namespace runtime: SKIP (namespace is not deployed)"
        continue
    }

    $pods = Invoke-Kubectl -Arguments @('get', 'pods', '-n', $namespace, '-l', 'app.kubernetes.io/component=backend', '-o', 'json') | ConvertFrom-Json
    $backendPods = @($pods.items | Where-Object { -not $_.metadata.deletionTimestamp })
    if ($backendPods.Count -eq 0) {
        Write-Host "$namespace runtime: SKIP (no backend pods)"
        continue
    }

    foreach ($pod in $backendPods) {
        $names = @($pod.spec.initContainers.name)
        if ('linkerd-proxy' -notin $names -or 'linkerd-network-validator' -notin $names) {
            throw "$namespace/$($pod.metadata.name) is missing Linkerd/SPIRE network validation init containers"
        }

        $mode = Invoke-Kubectl -Arguments @(
            'exec', '-n', $namespace, $pod.metadata.name,
            '-c', 'spire-jwt-fetcher', '--', 'stat', '-c', '%a',
            '/run/spire/jwt/vault.jwt'
        )
        if ($mode.Trim() -ne '440') {
            throw "$namespace/$($pod.metadata.name) JWT-SVID file mode is '$($mode.Trim())'; expected 0440"
        }
    }
    Write-Host "$namespace runtime: PASS ($($backendPods.Count) backend pods have Linkerd/SPIRE gates)"
}

$spireNamespace = & kubectl --context $Context get namespace spire --ignore-not-found -o name 2>$null
if ($spireNamespace) {
    $devExists = & kubectl --context $Context get namespace $DevNamespace --ignore-not-found -o name 2>$null
    if ($devExists) {
        $postgresPolicy = Invoke-Kubectl -Arguments @(
            'get', 'netpol', 'allow-dev-postgres-from-namespace', '-n', $DevNamespace, '-o', 'yaml'
        )
        if ($postgresPolicy -notmatch 'kubernetes.io/metadata.name: spire') {
            throw 'SPIRE namespace is not allowed to reach the SPIRE PostgreSQL datastore from the deployed dev boundary.'
        }
    } else {
        Write-Host "$DevNamespace SPIRE PostgreSQL policy: SKIP (dev namespace is not deployed)"
    }

    $serverErrors = & kubectl --context $Context logs -n spire statefulset/spire-server --since=5m 2>&1 |
        Select-String -Pattern 'connection refused|Failed to reload|Unable to look up agent|Failed to fetch bundle'
    if ($serverErrors) {
        throw "SPIRE Server reported datastore/runtime errors in the last 5 minutes: $($serverErrors -join ' ')"
    }
    Write-Host 'SPIRE PostgreSQL datastore connectivity: PASS'
}

Write-Host 'SPIRE-only K3s validation: PASS'
