[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('development', 'staging', 'production')]
    [string]$Environment,

    [Parameter(Mandatory)]
    [string]$OutputFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-EnvironmentFile {
    param([string]$Path)

    $values = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) {
            throw "Invalid environment line format in $Path."
        }

        $values[$trimmed.Substring(0, $separatorIndex).Trim()] = $trimmed.Substring($separatorIndex + 1).Trim()
    }

    return $values
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$contractEnvironmentFile = Join-Path $repoRoot ("config\environments\{0}.env.example" -f $Environment)

if (-not (Test-Path -LiteralPath $contractEnvironmentFile)) {
    throw "Environment file not found: $contractEnvironmentFile"
}

$contractValues = Read-EnvironmentFile -Path $contractEnvironmentFile
$adapterValues = [ordered]@{
    HIS_HOPE_ENVIRONMENT                     = [string]$contractValues['HIS_HOPE_ENVIRONMENT']
    ASPNETCORE_ENVIRONMENT                   = switch ($Environment) { 'development' { 'Development' } 'staging' { 'Staging' } 'production' { 'Production' } }
    HIS_HOPE_PUBLIC_API_ORIGIN               = [string]$contractValues['HIS_HOPE_PUBLIC_API_ORIGIN']
    HIS_HOPE_PUBLIC_WEB_ORIGIN               = [string]$contractValues['HIS_HOPE_PUBLIC_WEB_ORIGIN']
    HIS_HOPE_PUBLIC_ADMIN_ORIGIN             = [string]$contractValues['HIS_HOPE_PUBLIC_ADMIN_ORIGIN']
    HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN         = [string]$contractValues['HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN']
    HIS_HOPE_OIDC_AUTHORITY                  = [string]$contractValues['HIS_HOPE_OIDC_AUTHORITY']
    SERVICE_API_GATEWAY_URL                  = [string]$contractValues['SERVICE_API_GATEWAY_URL']
    SERVICE_IDENTITY_URL                     = [string]$contractValues['SERVICE_IDENTITY_URL']
    SERVICE_PATIENT_URL                      = [string]$contractValues['SERVICE_PATIENT_URL']
    SERVICE_PATIENT_GRPC_URL                 = 'http://patientservice:5006'
    SERVICE_APPOINTMENT_URL                  = [string]$contractValues['SERVICE_APPOINTMENT_URL']
    SERVICE_APPOINTMENT_GRPC_URL             = 'http://appointmentservice:5007'
    SERVICE_CLINICAL_URL                     = [string]$contractValues['SERVICE_CLINICAL_URL']
    SERVICE_LAB_URL                          = [string]$contractValues['SERVICE_LAB_URL']
    SERVICE_BILLING_URL                      = [string]$contractValues['SERVICE_BILLING_URL']
    SERVICE_PHARMACY_URL                     = [string]$contractValues['SERVICE_PHARMACY_URL']
    SERVICE_DASHBOARD_BFF_URL                = [string]$contractValues['SERVICE_DASHBOARD_BFF_URL']
    SERVICE_DATABASE_CONTINUITY_URL          = [string]$contractValues['SERVICE_DATABASE_CONTINUITY_URL']
    SERVICE_CONSUL_URL                       = 'http://consul:8500'
    OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT= [string]$contractValues['OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT']
    OBSERVABILITY_PROMETHEUS_URL             = [string]$contractValues['OBSERVABILITY_PROMETHEUS_URL']
    OBSERVABILITY_LOKI_URL                   = [string]$contractValues['OBSERVABILITY_LOKI_URL']
    OBSERVABILITY_ELASTICSEARCH_URL          = [string]$contractValues['OBSERVABILITY_ELASTICSEARCH_URL']
    OBSERVABILITY_JAEGER_URL                 = [string]$contractValues['OBSERVABILITY_JAEGER_URL']
    OBSERVABILITY_ALERTMANAGER_URL           = [string]$contractValues['OBSERVABILITY_ALERTMANAGER_URL']
    DATABASE_POSTGRES_URL                    = [string]$contractValues['DATABASE_POSTGRES_URL']
    DATABASE_IDENTITY_URL                    = 'Host=postgres;Database=identitydb;Username=postgres'
    DATABASE_PATIENT_URL                     = 'Host=postgres;Database=patientdb;Username=postgres'
    DATABASE_APPOINTMENT_URL                 = 'Host=postgres;Database=appointmentdb;Username=postgres'
    DATABASE_CLINICAL_URL                    = 'Host=postgres;Database=clinicaldb;Username=postgres'
    DATABASE_LAB_URL                         = 'Host=postgres;Database=labdb;Username=postgres'
    DATABASE_BILLING_URL                     = 'Host=postgres;Database=billingdb;Username=postgres'
    DATABASE_PHARMACY_URL                    = 'Host=postgres;Database=pharmacydb;Username=postgres'
    DATABASE_AUDIT_URL                       = 'Host=postgres;Database=postgres;Username=postgres'
    DATABASE_HARNESS_URL                     = 'Host=postgres;Database=harnessdb;Username=postgres'
    REDIS_URL                                = 'redis:6379'
    RABBITMQ_URL                             = 'rabbitmq'
}

if ($Environment -eq 'development') {
    $adapterValues['PASSKEYS_RP_ID'] = 'localhost'
    $adapterValues['PASSKEYS_ORIGIN_0'] = 'http://localhost:5000'
    $adapterValues['PASSKEYS_ORIGIN_1'] = 'http://localhost:8083'
    $adapterValues['PASSKEYS_ORIGIN_2'] = 'https://localhost'
    $adapterValues['PASSKEYS_ORIGIN_3'] = 'http://localhost:8081'
    $adapterValues['PASSKEYS_ORIGIN_4'] = 'http://localhost:8082'
}
else {
    $publicApiUri = [System.Uri][string]$contractValues['HIS_HOPE_PUBLIC_API_ORIGIN']
    $adapterValues['PASSKEYS_RP_ID'] = $publicApiUri.Host
    $adapterValues['PASSKEYS_ORIGIN_0'] = [string]$contractValues['HIS_HOPE_PUBLIC_API_ORIGIN']
    $adapterValues['PASSKEYS_ORIGIN_1'] = [string]$contractValues['HIS_HOPE_PUBLIC_ADMIN_ORIGIN']
    $adapterValues['PASSKEYS_ORIGIN_2'] = [string]$contractValues['HIS_HOPE_PUBLIC_WEB_ORIGIN']
    $adapterValues['PASSKEYS_ORIGIN_3'] = [string]$contractValues['HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN']
    $adapterValues['PASSKEYS_ORIGIN_4'] = [string]$contractValues['HIS_HOPE_PUBLIC_WEB_ORIGIN']
}

$outputDirectory = Split-Path -Parent $OutputFile
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    $null = New-Item -ItemType Directory -Path $outputDirectory -Force
}

$lines = foreach ($entry in $adapterValues.GetEnumerator()) {
    '{0}={1}' -f $entry.Key, $entry.Value
}

Set-Content -LiteralPath $OutputFile -Value $lines -Encoding UTF8
Write-Output "COMPOSE_RUNTIME_ENV_RENDERED environment=$Environment output=$OutputFile"
