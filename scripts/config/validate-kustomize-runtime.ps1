[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Overlay
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-YamlDocuments {
    param([string]$OverlayPath)

    $rendered = & kubectl kustomize $OverlayPath --load-restrictor LoadRestrictionsNone
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl kustomize failed for [$OverlayPath]."
    }

    $tempPath = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -LiteralPath $tempPath -Value ($rendered -join [Environment]::NewLine)
        # Parse locally so this contract gate never needs a live API server.
        # PyYAML is already a required repository validation dependency.
        $json = & python -c "import json,sys,yaml; print(json.dumps([d for d in yaml.safe_load_all(open(sys.argv[1], encoding='utf-8')) if d is not None]))" $tempPath
        if ($LASTEXITCODE -ne 0) {
            throw "Local YAML parse failed for [$OverlayPath]."
        }

        $parsed = $json | ConvertFrom-Json
        return @($parsed)
    }
    finally {
        Remove-Item -LiteralPath $tempPath -ErrorAction SilentlyContinue
    }
}

function Get-ServiceMap {
    param($Documents)

    $map = @{}
    foreach ($doc in $Documents) {
        if ($doc.kind -ne 'Service') {
            continue
        }

        $ports = @()
        foreach ($port in @($doc.spec.ports)) {
            $ports += [int]$port.port
        }

        $map[[string]$doc.metadata.name] = $ports
    }

    return $map
}

function Add-Error {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Message
    )

    $Errors.Add($Message)
}

$overlayPath = Join-Path 'k8s\overlays' $Overlay
$documents = Get-YamlDocuments -OverlayPath $overlayPath
$errors = [System.Collections.Generic.List[string]]::new()
$serviceMap = Get-ServiceMap -Documents $documents
$runtimeConfig = $documents | Where-Object { $_.kind -eq 'ConfigMap' -and $_.metadata.name -like '*runtime-contract-config' } | Select-Object -First 1

if (-not $runtimeConfig) {
    Add-Error -Errors $errors -Message 'runtime-contract-config ConfigMap is missing from rendered overlay.'
}
else {
    foreach ($key in @('SERVICE_API_GATEWAY_URL', 'SERVICE_IDENTITY_URL', 'SERVICE_PATIENT_URL', 'SERVICE_APPOINTMENT_URL', 'SERVICE_CLINICAL_URL', 'SERVICE_LAB_URL', 'SERVICE_BILLING_URL', 'SERVICE_PHARMACY_URL', 'SERVICE_DASHBOARD_BFF_URL', 'SERVICE_DATABASE_CONTINUITY_URL', 'REDIS_URL', 'RABBITMQ_URL')) {
        $value = [string]$runtimeConfig.data.$key
        if (-not $value) {
            Add-Error -Errors $errors -Message "runtime-contract-config missing [$key]."
            continue
        }

        $uri = [uri]$value
        $serviceHost = $uri.Host.Split('.')[0]
        $port = if ($uri.IsDefaultPort) {
            switch ($uri.Scheme) {
                'http' { 80 }
                'https' { 443 }
                'redis' { 6379 }
                'amqp' { 5672 }
                default { -1 }
            }
        }
        else {
            $uri.Port
        }

        if ($serviceMap.ContainsKey($serviceHost)) {
            if ($port -notin $serviceMap[$serviceHost]) {
                Add-Error -Errors $errors -Message "Service target mismatch for [$key]: expected service [$serviceHost] to expose port [$port]."
            }
        }
        elseif ($key -in @('REDIS_URL', 'RABBITMQ_URL')) {
            Add-Error -Errors $errors -Message "Missing Service for runtime target [$key] host [$serviceHost]."
        }

        if ($Overlay -eq 'prod' -and $value -match 'localhost') {
            Add-Error -Errors $errors -Message "Production runtime contract must not contain localhost: [$key]."
        }
    }
}

# Every gRPC consumer must receive an explicit production-safe target. Relying
# on an appsettings fallback silently points health checks at localhost or an
# obsolete Docker hostname and can leave a Ready pod returning 503.
$grpcContracts = @{
    'appointment-service' = @{ 'GrpcServices__PatientService' = 'ADAPTER_GRPC_PATIENT_URL' }
    'billing-service' = @{ 'GrpcServices__PatientService' = 'ADAPTER_GRPC_PATIENT_URL'; 'GrpcServices__AppointmentService' = 'ADAPTER_GRPC_APPOINTMENT_URL'; 'GrpcServices__LabService' = 'ADAPTER_GRPC_LAB_URL'; 'GrpcServices__PharmacyService' = 'ADAPTER_GRPC_PHARMACY_URL' }
    'lab-service' = @{ 'GrpcServices__PatientService' = 'ADAPTER_GRPC_PATIENT_URL'; 'GrpcServices__ClinicalService' = 'ADAPTER_GRPC_CLINICAL_URL' }
    'pharmacy-service' = @{ 'GrpcServices__PatientService' = 'ADAPTER_GRPC_PATIENT_URL'; 'GrpcServices__ClinicalService' = 'ADAPTER_GRPC_CLINICAL_URL' }
}
foreach ($entry in $grpcContracts.GetEnumerator()) {
    $deployment = $documents | Where-Object { $_.kind -eq 'Deployment' -and [string]$_.metadata.name -like "*$($entry.Key)" } | Select-Object -First 1
    if (-not $deployment) {
        Add-Error -Errors $errors -Message "gRPC consumer deployment [$($entry.Key)] is missing from rendered [$Overlay] overlay."
        continue
    }
    $container = @($deployment.spec.template.spec.containers) | Where-Object { $_.name -eq $entry.Key } | Select-Object -First 1
    if (-not $container) { $container = @($deployment.spec.template.spec.containers) | Select-Object -First 1 }
    foreach ($envName in $entry.Value.Keys) {
        $env = @($container.env) | Where-Object { $_.name -eq $envName } | Select-Object -First 1
        $key = if ($env -and $env.valueFrom -and $env.valueFrom.configMapKeyRef) { [string]$env.valueFrom.configMapKeyRef.key } else { '' }
        if ($key -ne $entry.Value[$envName]) {
            Add-Error -Errors $errors -Message "gRPC consumer [$($entry.Key)] maps [$envName] to [$key], expected [$($entry.Value[$envName])]."
        }
    }
}

# Liveness must measure process health only. The aggregate /health endpoint
# includes database/cache/downstream checks and can restart a healthy process
# during a dependency outage; readiness owns those dependency checks.
if ($Overlay -eq 'prod') {
    foreach ($serviceName in @('appointment-service', 'billing-service', 'clinical-service', 'lab-service', 'patient-service', 'pharmacy-service')) {
        $deployment = $documents | Where-Object { $_.kind -eq 'Deployment' -and [string]$_.metadata.name -like "*$serviceName" } | Select-Object -First 1
        $container = if ($deployment) { @($deployment.spec.template.spec.containers) | Where-Object { $_.name -eq $serviceName } | Select-Object -First 1 } else { $null }
        $path = if ($container -and $container.livenessProbe -and $container.livenessProbe.httpGet) { [string]$container.livenessProbe.httpGet.path } else { '' }
        if ($path -ne '/health/live') {
            Add-Error -Errors $errors -Message "Production liveness probe for [$serviceName] must use [/health/live], found [$path]."
        }
    }
}

foreach ($service in ($documents | Where-Object { $_.kind -eq 'Service' })) {
    $ports = @($service.spec.ports | ForEach-Object { [int]$_.port })
    if (($ports | Group-Object | Where-Object Count -gt 1)) {
        Add-Error -Errors $errors -Message "Duplicate Service port detected on [$($service.metadata.name)]."
    }
}

$secretProviderClasses = @($documents | Where-Object { $_.kind -eq 'SecretProviderClass' } | ForEach-Object { [string]$_.metadata.name })
$csiReferences = @($documents | Where-Object { $_.kind -eq 'Deployment' } | ForEach-Object {
    $podSpec = if ($_.spec -and $_.spec.template -and $_.spec.template.spec) { $_.spec.template.spec } else { $null }
    if ($podSpec -and $podSpec.PSObject.Properties.Name -contains 'volumes') {
        foreach ($volume in @($podSpec.volumes)) {
            if (($volume.PSObject.Properties.Name -contains 'csi') -and $volume.csi.driver -eq 'secrets-store.csi.k8s.io') {
                [string]$volume.csi.volumeAttributes.secretProviderClass
            }
        }
    }
}) | Where-Object { $_ }

if ($Overlay -eq 'prod') {
    foreach ($reference in $csiReferences) {
        if ($reference -notin $secretProviderClasses) {
            Add-Error -Errors $errors -Message "SecretProviderClass reference [$reference] is missing from rendered prod overlay."
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
    exit 1
}

Write-Output "RUNTIME_KUSTOMIZE_VALID overlay=$Overlay"
exit 0
