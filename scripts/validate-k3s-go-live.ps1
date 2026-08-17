[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')]
    [string]$Environment = 'production',
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string]$Namespace = 'his-hope',
    [ValidatePattern('^[a-z0-9]([-a-z0-9]*[a-z0-9])?$')]
    [string]$StorageClassName = 'longhorn',
    [string]$EvidenceDirectory = 'artifacts/evidence',
    [string]$OutputPath,
    [switch]$RequireCluster
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$startedAt = [DateTime]::UtcNow
$checks = [System.Collections.Generic.List[object]]::new()
$imageDigests = @()
$renderedImagesByComponent = @{}

function Add-Check {
    param(
        [string]$Name,
        [ValidateSet('pass', 'fail', 'skipped', 'unavailable', 'environment-blocked')]
        [string]$Status,
        [string]$Message
    )
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; message = $Message })
}

function Invoke-KubectlJson {
    param([string[]]$Arguments)
    $errorFile = [IO.Path]::GetTempFileName()
    try {
        $output = & kubectl @Arguments -o json --request-timeout=8s 2> $errorFile
        if ($LASTEXITCODE -ne 0) {
            $errorText = (Get-Content -LiteralPath $errorFile -Raw -ErrorAction SilentlyContinue)
            throw $errorText
        }
        return ($output -join "`n") | ConvertFrom-Json
    } finally {
        Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
    }
}

function Test-PathEvidence {
    param([string]$Name, [string]$RelativePath)
    $path = Join-Path $EvidenceDirectory $RelativePath
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        try {
            $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $hasMeasurement = $null -ne $document.rpoMinutes -and $null -ne $document.rtoMinutes -and $null -ne $document.executedAtUtc
            $hasRestoreVerification = $document.restoreVerified -eq $true -and -not [string]::IsNullOrWhiteSpace([string]$document.target)
            if ($document.status -eq 'pass' -and $hasMeasurement -and $hasRestoreVerification) {
                Add-Check $Name 'pass' "Measured evidence is approved: $RelativePath"
            } else {
                Add-Check $Name 'unavailable' "Evidence exists but is not an approved measured/verified pass: $RelativePath"
            }
        } catch {
            Add-Check $Name 'unavailable' "Evidence is not valid JSON: $RelativePath"
        }
    } else {
        Add-Check $Name 'unavailable' "Required evidence file is missing: $RelativePath"
    }
}

function Test-BaselineEvidence {
    $path = Join-Path $EvidenceDirectory 'k3s-baseline.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Check 'k3s-baseline' 'unavailable' 'Sanitized K3s baseline artifact is missing: k3s-baseline.json'
        return
    }
    try {
        $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $checksPresent = @($document.checks).Count -ge 11
        $allReadOnlyChecksPass = @($document.checks | Where-Object status -ne 'pass').Count -eq 0
        if ($document.status -eq 'pass' -and $checksPresent -and $allReadOnlyChecksPass) {
            Add-Check 'k3s-baseline' 'pass' 'Sanitized baseline contains all required read-only inventory checks.'
        } else {
            Add-Check 'k3s-baseline' 'unavailable' 'Baseline exists but is incomplete or contains unavailable checks.'
        }
    } catch {
        Add-Check 'k3s-baseline' 'unavailable' 'Baseline artifact is not valid JSON.'
    }
}

function Test-RuntimeServiceMappings {
    param([string]$Namespace)

    try {
        $deployment = Invoke-KubectlJson @('get', 'deployment', 'his-hope-appointment-service', '-n', $Namespace)
        $envEntries = @($deployment.spec.template.spec.containers[0].env)
        $patientEntry = $envEntries | Where-Object { $_.name -eq 'GrpcServices__PatientService' } | Select-Object -First 1
        if ($null -eq $patientEntry -or $null -eq $patientEntry.valueFrom -or $null -eq $patientEntry.valueFrom.configMapKeyRef) {
            Add-Check 'runtime-contract-mappings' 'fail' 'appointment-service does not consume ADAPTER_GRPC_PATIENT_URL from the runtime contract ConfigMap.'
            return
        }
        $key = [string]$patientEntry.valueFrom.configMapKeyRef.key
        $configMap = Invoke-KubectlJson @('get', 'configmap', 'his-hope-runtime-contract-config', '-n', $Namespace)
        $target = $configMap.data.PSObject.Properties[$key].Value
        if ($patientEntry.valueFrom.configMapKeyRef.name -ne 'his-hope-runtime-contract-config' -or $key -ne 'ADAPTER_GRPC_PATIENT_URL' -or [string]::IsNullOrWhiteSpace([string]$target)) {
            Add-Check 'runtime-contract-mappings' 'fail' 'appointment-service does not consume ADAPTER_GRPC_PATIENT_URL from the runtime contract ConfigMap.'
            return
        }
        Add-Check 'runtime-contract-mappings' 'pass' 'appointment-service consumes the reviewed patient gRPC runtime target.'
    } catch {
        Add-Check 'runtime-contract-mappings' 'unavailable' 'Could not inspect appointment-service runtime contract mapping.'
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$overlay = if ($Environment -eq 'production' -and $StorageClassName -ne 'longhorn') { 'prod-shared-storage' } elseif ($Environment -eq 'production') { 'prod' } else { 'staging' }
$overlayPath = Join-Path $repoRoot "k8s\overlays\$overlay"

if (-not (Test-Path -LiteralPath $overlayPath -PathType Container)) {
    Add-Check 'manifest-render' 'fail' "Overlay does not exist: $overlayPath"
} else {
    $rendered = & kubectl kustomize $overlayPath --load-restrictor LoadRestrictionsNone 2>&1
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'manifest-render' 'fail' 'Production Kustomize render failed.'
    } else {
        $text = $rendered -join "`n"
        $images = @([regex]::Matches($text, '(?m)^\s*image:\s*(?<image>[^\s]+)') | ForEach-Object { $_.Groups['image'].Value } | Sort-Object -Unique)
        $imageDigests = @($images | Where-Object { $_ -match '@sha256:[0-9a-f]{64}$' })
        foreach ($image in $imageDigests) {
            if ($image -match '/(?<component>[a-z0-9][a-z0-9-]*)(?::[^@]+)?@sha256:[0-9a-f]{64}$') {
                $renderedImagesByComponent[$Matches['component']] = $image
            }
        }
        $unpinned = @($images | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
        if ($Environment -eq 'production' -and $unpinned.Count -gt 0) {
            Add-Check 'image-digests' 'fail' "Unpinned production images: $($unpinned -join ', ')"
        } else {
            Add-Check 'image-digests' 'pass' "$($images.Count) rendered image reference(s) are immutable."
        }
        if ($text -match '(?im)^\s*(password|token|privateKey|clientSecret):\s*[^$<{\s]+') {
            Add-Check 'manifest-secret-scan' 'fail' 'Rendered manifests contain a possible literal secret value.'
        } else {
            Add-Check 'manifest-secret-scan' 'pass' 'No literal secret values detected in rendered manifests.'
        }
    }
}

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    if ($RequireCluster) { Add-Check 'cluster-connectivity' 'environment-blocked' "Kubeconfig not found: $Kubeconfig" }
    else { Add-Check 'cluster-connectivity' 'skipped' 'Live cluster validation was not requested.' }
} else {
    $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
    try {
        $null = Invoke-KubectlJson @('version')
        Add-Check 'cluster-connectivity' 'pass' 'Configured Kubernetes API is reachable.'

        $nodes = Invoke-KubectlJson @('get', 'nodes')
        $notReady = @($nodes.items | Where-Object { -not ($_.status.conditions | Where-Object { $_.type -eq 'Ready' -and $_.status -eq 'True' }) })
        if ($notReady.Count -eq 0) { Add-Check 'nodes-ready' 'pass' "$($nodes.items.Count) node(s) are Ready." }
        else { Add-Check 'nodes-ready' 'fail' "NotReady nodes: $(($notReady | ForEach-Object metadata.name) -join ', ')" }

        $namespaceJson = Invoke-KubectlJson @('get', 'namespace', $Namespace)
        $enforce = $namespaceJson.metadata.labels.'pod-security.kubernetes.io/enforce'
        if ($enforce -eq 'restricted') { Add-Check 'pod-security' 'pass' "$Namespace enforces restricted Pod Security." }
        else { Add-Check 'pod-security' 'fail' "$Namespace enforces [$enforce], expected [restricted]." }

        $pods = Invoke-KubectlJson @('get', 'pods', '-n', $Namespace)
        $badPods = @($pods.items | Where-Object {
                $_.status.phase -in @('Pending', 'Failed') -or
                @($_.status.containerStatuses | Where-Object {
                        $_.ready -ne $true -or
                        ($_.state.PSObject.Properties.Name -contains 'waiting' -and $_.state.waiting.reason -in @('CrashLoopBackOff', 'ImagePullBackOff', 'ErrImagePull'))
                    }).Count -gt 0
            })
        if ($badPods.Count -eq 0) { Add-Check 'application-health' 'pass' "All pods in $Namespace are ready/running." }
        else { Add-Check 'application-health' 'fail' "Unhealthy pods: $(($badPods | ForEach-Object metadata.name) -join ', ')" }

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

        if ($Environment -eq 'production' -and $renderedImagesByComponent.Count -gt 0) {
            $deployments = Invoke-KubectlJson @('get', 'deployments', '-n', $Namespace)
            $drifts = [System.Collections.Generic.List[string]]::new()
            foreach ($deployment in @($deployments.items)) {
                foreach ($container in @($deployment.spec.template.spec.containers)) {
                    $liveImage = [string]$container.image
                    if ($liveImage -match '/(?<component>[a-z0-9][a-z0-9-]*)(?::[^@]+)?@sha256:[0-9a-f]{64}$') {
                        $component = $Matches['component']
                        if ($renderedImagesByComponent.ContainsKey($component) -and $renderedImagesByComponent[$component] -ne $liveImage) {
                            $drifts.Add("$($deployment.metadata.name):$component")
                        }
                    }
                }
            }
            if ($drifts.Count -eq 0) { Add-Check 'image-drift' 'pass' 'Live workload images match reviewed production digests.' }
            else { Add-Check 'image-drift' 'fail' "Live workload image drift: $($drifts -join ', ')" }
        }

        if ($Environment -eq 'production') {
            $renderedPvc = [regex]::Match(
                $text,
                '(?ms)^kind:\s*PersistentVolumeClaim\s*$.*?^\s{2}name:\s*his-hope-database-continuity-backups\s*$.*?^\s{2}storageClassName:\s*(?<storageClass>[^\s#]+)\s*$'
            )
            if (-not $renderedPvc.Success) {
                Add-Check 'storage-class-drift' 'fail' 'Reviewed production render does not declare the database-continuity backup PVC storage class.'
            } else {
                $expectedStorageClass = $renderedPvc.Groups['storageClass'].Value
                $livePvc = Invoke-KubectlJson @('get', 'pvc', 'his-hope-database-continuity-backups', '-n', $Namespace)
                $liveStorageClass = [string]$livePvc.spec.storageClassName
                if ($expectedStorageClass -match '^(local-path|standard)$') {
                    Add-Check 'storage-class-drift' 'fail' "Reviewed production render still selects non-replicated storage class [$expectedStorageClass]; complete the protected PVC migration and restore drill before go-live."
                } elseif ([string]::IsNullOrWhiteSpace($liveStorageClass)) {
                    Add-Check 'storage-class-drift' 'unavailable' 'Live database-continuity backup PVC has no storage class.'
                } elseif ($liveStorageClass -ne $expectedStorageClass) {
                    Add-Check 'storage-class-drift' 'fail' "Live database-continuity backup PVC uses [$liveStorageClass], reviewed production render requires [$expectedStorageClass]; bound PVC storageClassName is immutable and needs a migration/restore plan."
                } else {
                    Add-Check 'storage-class-drift' 'pass' "Live database-continuity backup PVC uses the reviewed storage class [$expectedStorageClass]."
                }
            }
        }

        Test-RuntimeServiceMappings -Namespace $Namespace

        $linkerd = Invoke-KubectlJson @('get', 'pods', '-n', 'linkerd')
        $linkerdBad = @($linkerd.items | Where-Object { $_.status.phase -ne 'Running' -or @($_.status.containerStatuses | Where-Object { $_.ready -ne $true }).Count -gt 0 })
        if ($linkerd.items.Count -eq 0) { Add-Check 'linkerd-control-plane' 'unavailable' 'linkerd namespace is absent.' }
        elseif ($linkerdBad.Count -gt 0) { Add-Check 'linkerd-control-plane' 'fail' 'Linkerd control-plane has unhealthy pods.' }
        else { Add-Check 'linkerd-control-plane' 'pass' 'Linkerd control-plane pods are healthy.' }
    } catch {
        Add-Check 'cluster-health-queries' 'unavailable' 'Cluster health queries failed without exposing command output.'
    }
}

# A fresh sanitized baseline is required in the same change window.
Test-BaselineEvidence

# These files are intentionally required as evidence, not inferred from resource existence.
Test-PathEvidence 'database-restore-drill' 'database-restore-drill.json'
Test-PathEvidence 'vault-recovery-drill' 'vault-recovery-drill.json'
Test-PathEvidence 'harbor-clean-node-test' 'harbor-clean-node-test.json'
Test-PathEvidence 'control-plane-rebuild-drill' 'control-plane-rebuild-drill.json'
Test-PathEvidence 'application-restore-smoke' 'application-restore-smoke.json'

$failed = @($checks | Where-Object status -eq 'fail')
$unavailable = @($checks | Where-Object status -in @('unavailable', 'environment-blocked'))
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($unavailable.Count -gt 0) { 'environment-blocked' } else { 'pass' }
$evidence = [pscustomobject]@{
    release = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { 'local' }
    environment = $Environment
    storageClass = $StorageClassName
    imageDigests = @($imageDigests)
    checks = @($checks)
    status = $status
    startedAtUtc = $startedAt.ToString('o')
    finishedAtUtc = [DateTime]::UtcNow.ToString('o')
}
$json = $evidence | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 80 }
if ($status -eq 'environment-blocked') { exit 70 }
exit 0
