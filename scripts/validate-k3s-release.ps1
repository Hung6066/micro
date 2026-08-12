[CmdletBinding()]
param(
    [ValidateSet('dev', 'staging', 'prod', 'production')]
    [string]$Environment = 'production',
    [string]$Namespace = 'his-hope',
    [ValidatePattern('^[a-z0-9]([-a-z0-9]*[a-z0-9])?$')]
    [string]$StorageClassName = 'longhorn',
    [string]$Kubeconfig,
    [string]$OutputPath,
    [switch]$RequireCluster,
    [switch]$RequirePodSecurity
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$startedAt = [DateTime]::UtcNow
$checks = [System.Collections.Generic.List[object]]::new()
$imageDigests = @()
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$pinnedKubectlDirectory = Join-Path $repositoryRoot '.runtime\toolchain'
if (Test-Path -LiteralPath (Join-Path $pinnedKubectlDirectory 'kubectl.exe') -PathType Leaf) {
    $env:PATH = "$pinnedKubectlDirectory;$env:PATH"
}

function Add-Check {
    param(
        [string]$Name,
        [ValidateSet('pass', 'fail', 'skipped', 'unavailable', 'environment-blocked')]
        [string]$Status,
        [string]$Message
    )
    $checks.Add([pscustomobject]@{
            name    = $Name
            status  = $Status
            message = $Message
        })
}

function Invoke-KubectlJson {
    param([string[]]$Arguments)
    $output = & kubectl @Arguments -o json 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join "`n")
    }
    return ($output -join "`n") | ConvertFrom-Json
}

function Invoke-ToolVersion {
    param([string]$Name, [string[]]$Arguments)
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        Add-Check "tool-$Name" 'fail' "$Name is not installed on the release runner."
        return $false
    }
    $output = & $command.Source @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        Add-Check "tool-$Name" 'fail' "$Name failed its version probe."
        return $false
    }
    $lines = @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    $firstLine = if ($Name -eq 'kubectl') {
        @($lines | Where-Object { $_ -match 'gitVersion|GitVersion' } | Select-Object -First 1)
    } else {
        @($lines | Select-Object -First 1)
    }
    $message = if ($null -eq $firstLine) { "$Name version probe passed." } else { [string]$firstLine }
    Add-Check "tool-$Name" 'pass' $message
    return $true
}

if ($Kubeconfig) {
    $resolvedKubeconfig = [IO.Path]::GetFullPath($Kubeconfig)
    if (Test-Path -LiteralPath $resolvedKubeconfig -PathType Leaf) {
        $env:KUBECONFIG = $resolvedKubeconfig
    } else {
        Add-Check 'cluster-kubeconfig' 'environment-blocked' "Explicit kubeconfig was not found: $resolvedKubeconfig"
    }
}

# Toolchain checks are deterministic and safe to run without a cluster.
$null = Invoke-ToolVersion -Name 'kubectl' -Arguments @('version', '--client=true', '--output=yaml')
if (Get-Command kustomize -ErrorAction SilentlyContinue) {
    $null = Invoke-ToolVersion -Name 'kustomize' -Arguments @('version')
} elseif (Get-Command kubectl -ErrorAction SilentlyContinue) {
    Add-Check 'tool-kustomize' 'pass' 'kubectl kustomize is used; CI pins standalone kustomize.'
} else {
    Add-Check 'tool-kustomize' 'fail' 'Neither standalone kustomize nor kubectl kustomize is available.'
}

$overlayName = if ($Environment -eq 'production' -and $StorageClassName -ne 'longhorn') { 'prod-shared-storage' } elseif ($Environment -eq 'production') { 'prod' } else { $Environment }
$effectiveRequirePodSecurity = $RequirePodSecurity -or ($RequireCluster -and $Environment -eq 'production')
$repoRoot = $repositoryRoot
$overlayPath = Join-Path $repoRoot "k8s\overlays\$overlayName"
if (-not (Test-Path -LiteralPath $overlayPath -PathType Container)) {
    Add-Check 'manifest-overlay' 'fail' "Overlay does not exist: $overlayPath"
} else {
    $rendered = & kubectl kustomize $overlayPath --load-restrictor LoadRestrictionsNone 2>&1
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'manifest-render' 'fail' 'Kustomize render failed.'
    } else {
        $text = $rendered -join "`n"
        $images = @([regex]::Matches($text, '(?m)^\s*image:\s*(?<image>[^\s]+)') | ForEach-Object { $_.Groups['image'].Value } | Sort-Object -Unique)
        $imageDigests = @($images | Where-Object { $_ -match '@sha256:[0-9a-f]{64}$' })
        $unpinned = @($images | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
        if ($Environment -eq 'production' -and $unpinned.Count -gt 0) {
            Add-Check 'image-digest-pinning' 'fail' "Unpinned production images: $($unpinned -join ', ')"
        } else {
            Add-Check 'image-digest-pinning' 'pass' "$($images.Count) rendered image reference(s) checked."
        }
        if ($text -match '(?im)^\s*(password|token|privateKey|clientSecret):\s*[^$<{\s]+') {
            Add-Check 'manifest-secret-scan' 'fail' 'Rendered manifests contain a possible literal secret value.'
        } else {
            Add-Check 'manifest-secret-scan' 'pass' 'No literal secret values detected in rendered manifests.'
        }
    }
}

if ($Kubeconfig -or $RequireCluster) {
    try {
        $version = & kubectl version -o json --request-timeout=8s 2>$null
        if ($LASTEXITCODE -ne 0) {
            Add-Check 'cluster-connectivity' 'environment-blocked' 'kubectl cannot reach the configured cluster.'
        } else {
        $versionJson = ($version -join "`n") | ConvertFrom-Json
        Add-Check 'cluster-connectivity' 'pass' "Server $($versionJson.serverVersion.gitVersion) is reachable."
        $clientVersion = [string]$versionJson.clientVersion.gitVersion
        $serverVersion = [string]$versionJson.serverVersion.gitVersion
        $clientMatch = [regex]::Match($clientVersion, '^v(?<major>\d+)\.(?<minor>\d+)')
        $serverMatch = [regex]::Match($serverVersion, '^v(?<major>\d+)\.(?<minor>\d+)')
        if ($clientMatch.Success -and $serverMatch.Success) {
            $minorSkew = [math]::Abs([int]$clientMatch.Groups['minor'].Value - [int]$serverMatch.Groups['minor'].Value)
            if ($minorSkew -gt 1) {
                Add-Check 'toolchain-skew' 'fail' "kubectl $clientVersion is incompatible with Kubernetes server $serverVersion (minor skew=$minorSkew)."
            } else {
                Add-Check 'toolchain-skew' 'pass' "kubectl $clientVersion and server $serverVersion are within supported minor skew."
            }
        } else {
            Add-Check 'toolchain-skew' 'unavailable' 'Could not parse client/server Kubernetes versions.'
        }
        }
    } catch {
        Add-Check 'cluster-connectivity' 'environment-blocked' 'kubectl cannot reach the configured cluster.'
    }
} else {
    Add-Check 'cluster-connectivity' 'skipped' 'Live cluster validation was not requested.'
}

if (($checks | Where-Object name -eq 'cluster-connectivity').status -eq 'pass') {
    try {
        $nodes = Invoke-KubectlJson @('get', 'nodes')
        $notReady = @($nodes.items | Where-Object {
                -not ($_.status.conditions | Where-Object { $_.type -eq 'Ready' -and $_.status -eq 'True' })
            })
        if ($notReady.Count -eq 0) {
            Add-Check 'nodes-ready' 'pass' "$($nodes.items.Count) node(s) are Ready."
        } else {
            Add-Check 'nodes-ready' 'fail' "NotReady nodes: $(($notReady | ForEach-Object metadata.name) -join ', ')"
        }

        $namespaceJson = Invoke-KubectlJson @('get', 'namespace', $Namespace)
        $enforce = $namespaceJson.metadata.labels.'pod-security.kubernetes.io/enforce'
        if ($enforce -eq 'restricted') {
            Add-Check 'pod-security' 'pass' "$Namespace enforces restricted Pod Security."
        } elseif (-not $effectiveRequirePodSecurity) {
            Add-Check 'pod-security' 'skipped' "$Namespace does not enforce restricted Pod Security yet; required only for the production cutover."
        } else {
            Add-Check 'pod-security' 'fail' "$Namespace enforces [$enforce], expected [restricted]."
        }

        $pods = Invoke-KubectlJson @('get', 'pods', '-n', $Namespace)
        $badPods = @($pods.items | Where-Object {
                $_.status.phase -in @('Pending', 'Failed') -or
                @($_.status.containerStatuses | Where-Object {
                        $_.ready -ne $true -or
                        ($_.state.PSObject.Properties.Name -contains 'waiting' -and $_.state.waiting.reason -in @('CrashLoopBackOff', 'ImagePullBackOff', 'ErrImagePull'))
                    }).Count -gt 0
            })
        if ($badPods.Count -eq 0) {
            Add-Check 'application-health' 'pass' "All pods in $Namespace are ready/running."
        } else {
            $names = ($badPods | ForEach-Object metadata.name) -join ', '
            Add-Check 'application-health' 'fail' "Unhealthy pods: $names"
        }

        if ($Environment -eq 'production') {
            try {
                $storageClass = Invoke-KubectlJson @('get', 'storageclass', $StorageClassName)
                $provisioner = [string]$storageClass.provisioner
                if ($StorageClassName -eq 'longhorn' -and $provisioner -eq 'driver.longhorn.io') {
                    Add-Check 'storage-backend' 'pass' 'Reviewed Longhorn storage class is installed.'
                } elseif ($StorageClassName -ne 'longhorn' -and $provisioner -match '^csi\.' -and $provisioner -notmatch '(?i)(local-path|longhorn)') {
                    Add-Check 'storage-backend' 'pass' "Reviewed external shared CSI storage class [$StorageClassName] uses [$provisioner]."
                } else {
                    Add-Check 'storage-backend' 'fail' "StorageClass [$StorageClassName] is not an approved production backend (provisioner=[$provisioner])."
                }
            } catch {
                Add-Check 'storage-backend' 'unavailable' "Reviewed production StorageClass [$StorageClassName] is not installed."
            }
        }

        $linkerd = Invoke-KubectlJson @('get', 'pods', '-n', 'linkerd')
        $linkerdBad = @($linkerd.items | Where-Object {
                $_.status.phase -ne 'Running' -or
                @($_.status.containerStatuses | Where-Object { $_.ready -ne $true }).Count -gt 0
            })
        if ($linkerd.items.Count -eq 0) {
            Add-Check 'linkerd-control-plane' 'unavailable' 'linkerd namespace is not available.'
        } elseif ($linkerdBad.Count -gt 0) {
            Add-Check 'linkerd-control-plane' 'fail' 'One or more Linkerd control-plane pods are not healthy.'
        } else {
            Add-Check 'linkerd-control-plane' 'pass' 'Linkerd control-plane pods are Running.'
        }
    } catch {
        Add-Check 'cluster-health-queries' 'unavailable' "Cluster health queries failed: $($_.Exception.Message)"
    }
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -in @('environment-blocked', 'unavailable'))
$requiredSkipped = @($checks | Where-Object {
        $_.status -eq 'skipped' -and (
            ($effectiveRequirePodSecurity -and $_.name -eq 'pod-security') -or
            ($RequireCluster -and $_.name -eq 'cluster-connectivity')
        )
    })
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'environment-blocked' } elseif ($requiredSkipped.Count -gt 0) { 'fail' } else { 'pass' }
$evidence = [pscustomobject]@{
    release     = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { 'local' }
    environment = $Environment
    storageClass = $StorageClassName
    imageDigests = @($imageDigests)
    checks      = @($checks)
    status      = $status
    startedAtUtc = $startedAt.ToString('o')
    finishedAtUtc = [DateTime]::UtcNow.ToString('o')
}

$json = $evidence | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ([string]::IsNullOrWhiteSpace($directory)) { $directory = (Get-Location).Path }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $fullPath = [System.IO.Path]::GetFullPath($OutputPath)
    [System.IO.File]::WriteAllText($fullPath, $json, [System.Text.UTF8Encoding]::new($false))
}
Write-Output $json

if ($status -eq 'fail') {
    if (@($checks | Where-Object { ($_.name -like 'tool-*' -or $_.name -eq 'toolchain-skew') -and $_.status -eq 'fail' }).Count -gt 0) { exit 10 }
    if (@($checks | Where-Object { $_.name -eq 'manifest-render' -and $_.status -eq 'fail' }).Count -gt 0) { exit 20 }
    if (@($checks | Where-Object { $_.name -in @('image-digest-pinning', 'manifest-secret-scan') -and $_.status -eq 'fail' }).Count -gt 0) { exit 40 }
    if (@($checks | Where-Object { $_.name -eq 'pod-security' -and $_.status -eq 'fail' }).Count -gt 0) { exit 30 }
    exit 70
}
if ($status -eq 'environment-blocked') { exit 70 }
exit 0
