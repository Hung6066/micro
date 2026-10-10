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
    HIS_HOPE_RUNTIME_CONTRACT_VERSION        = [string]$contractValues['HIS_HOPE_RUNTIME_CONTRACT_VERSION']
    HIS_HOPE_SECRET_PROVIDER                 = [string]$contractValues['HIS_HOPE_SECRET_PROVIDER']
    HIS_HOPE_SECRET_PROVIDER_REF             = [string]$contractValues['HIS_HOPE_SECRET_PROVIDER_REF']
    IDENTITY_BOOTSTRAP_ADMIN_RESET_PASSWORD  = [string]$contractValues['IDENTITY_BOOTSTRAP_ADMIN_RESET_PASSWORD']
    ASPNETCORE_ENVIRONMENT                   = switch ($Environment) { 'development' { 'Development' } 'staging' { 'Staging' } 'production' { 'Production' } }
    HIS_HOPE_PUBLIC_API_ORIGIN               = [string]$contractValues['HIS_HOPE_PUBLIC_API_ORIGIN']
    HIS_HOPE_PUBLIC_WEB_ORIGIN               = [string]$contractValues['HIS_HOPE_PUBLIC_WEB_ORIGIN']
    HIS_HOPE_PUBLIC_ADMIN_ORIGIN             = [string]$contractValues['HIS_HOPE_PUBLIC_ADMIN_ORIGIN']
    HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN         = [string]$contractValues['HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN']
    HIS_HOPE_OIDC_AUTHORITY                  = [string]$contractValues['HIS_HOPE_OIDC_AUTHORITY']
    # Preserve the public development OIDC port (5001); service-to-service
    # discovery remains on identityservice:5003 below.
    OIDC_ISSUER                               = [string]$contractValues['HIS_HOPE_OIDC_AUTHORITY']
    AUTHZ_PDP_MODE                            = [string]$contractValues['AUTHZ_PDP_MODE']
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
    SERVICE_SYSTEMDASHBOARD_BFF_URL          = [string]$contractValues['SERVICE_SYSTEMDASHBOARD_BFF_URL']
    SERVICE_DATABASE_CONTINUITY_URL          = [string]$contractValues['SERVICE_DATABASE_CONTINUITY_URL']
    SERVICE_CONSUL_URL                       = 'http://consul:8500'
    OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT= [string]$contractValues['OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT']
    OBSERVABILITY_PROMETHEUS_URL             = [string]$contractValues['OBSERVABILITY_PROMETHEUS_URL']
    OBSERVABILITY_LOKI_URL                   = [string]$contractValues['OBSERVABILITY_LOKI_URL']
    OBSERVABILITY_ELASTICSEARCH_URL          = [string]$contractValues['OBSERVABILITY_ELASTICSEARCH_URL']
    OBSERVABILITY_JAEGER_URL                 = [string]$contractValues['OBSERVABILITY_JAEGER_URL']
    OBSERVABILITY_ALERTMANAGER_URL           = [string]$contractValues['OBSERVABILITY_ALERTMANAGER_URL']
    DATABASE_POSTGRES_URL                    = [string]$contractValues['DATABASE_POSTGRES_URL']
    DATABASE_IDENTITY_URL                    = 'Host=postgres;Database=identitydb;Username=postgres;Password=postgres'
    DATABASE_PATIENT_URL                     = 'Host=postgres;Database=patientdb;Username=postgres;Password=postgres'
    DATABASE_APPOINTMENT_URL                 = 'Host=postgres;Database=appointmentdb;Username=postgres;Password=postgres'
    DATABASE_CLINICAL_URL                    = 'Host=postgres;Database=clinicaldb;Username=postgres;Password=postgres'
    DATABASE_LAB_URL                         = 'Host=postgres;Database=labdb;Username=postgres;Password=postgres'
    DATABASE_BILLING_URL                     = 'Host=postgres;Database=billingdb;Username=postgres;Password=postgres'
    DATABASE_PHARMACY_URL                    = 'Host=postgres;Database=pharmacydb;Username=postgres;Password=postgres'
    DATABASE_AUDIT_URL                       = 'Host=postgres;Database=postgres;Username=postgres;Password=postgres'
    DATABASE_HARNESS_URL                     = 'Host=postgres;Database=harnessdb;Username=postgres;Password=postgres'
    DATABASE_COMMERCE_URL                    = 'Host=postgres;Database=commercedb;Username=postgres;Password=postgres'
    POSTGRES_PASSWORD                        = 'postgres'
    RABBITMQ_PASSWORD                        = 'admin'
    REDIS_URL                                = 'redis://redis:6379'
    RABBITMQ_URL                             = [string]$contractValues['RABBITMQ_URL']
    RESILIENCE_HTTP_TIMEOUT_SECONDS          = [string]$contractValues['RESILIENCE_HTTP_TIMEOUT_SECONDS']
    RESILIENCE_HTTP_RETRY_COUNT              = [string]$contractValues['RESILIENCE_HTTP_RETRY_COUNT']
    OBSERVABILITY_PROMETHEUS_REQUIRED        = [string]$contractValues['OBSERVABILITY_PROMETHEUS_REQUIRED']
    OBSERVABILITY_ELASTICSEARCH_REQUIRED     = [string]$contractValues['OBSERVABILITY_ELASTICSEARCH_REQUIRED']
    OBSERVABILITY_JAEGER_REQUIRED            = [string]$contractValues['OBSERVABILITY_JAEGER_REQUIRED']
    SECRET_POSTGRES_PASSWORD                 = [string]$contractValues['SECRET_POSTGRES_PASSWORD']
    SECRET_POSTGRES_PASSWORD_REF             = [string]$contractValues['SECRET_POSTGRES_PASSWORD_REF']
    SECRET_RABBITMQ_PASSWORD                 = [string]$contractValues['SECRET_RABBITMQ_PASSWORD']
    SECRET_RABBITMQ_PASSWORD_REF             = [string]$contractValues['SECRET_RABBITMQ_PASSWORD_REF']
    SECRET_REDIS_PASSWORD                    = [string]$contractValues['SECRET_REDIS_PASSWORD']
    SECRET_REDIS_PASSWORD_REF                = [string]$contractValues['SECRET_REDIS_PASSWORD_REF']
    SECRET_OIDC_CLIENT_SECRET                = [string]$contractValues['SECRET_OIDC_CLIENT_SECRET']
    SECRET_OIDC_CLIENT_SECRET_REF            = [string]$contractValues['SECRET_OIDC_CLIENT_SECRET_REF']
    DEVICE_POSTURE_MODE                     = [string]$contractValues['DEVICE_POSTURE_MODE']
    DEVICE_POSTURE_PROVIDERS                = [string]$contractValues['DEVICE_POSTURE_PROVIDERS']
    DEVICE_POSTURE_TTL_SECONDS              = [string]$contractValues['DEVICE_POSTURE_TTL_SECONDS']
    DEVICE_POSTURE_ENFORCE_CLINICAL         = [string]$contractValues['DEVICE_POSTURE_ENFORCE_CLINICAL']
    PASSWORD_HISTORY_ENABLED                = [string]$contractValues['PASSWORD_HISTORY_ENABLED']
    PASSWORD_HISTORY_COUNT                  = [string]$contractValues['PASSWORD_HISTORY_COUNT']
    AUDIT_APPEND_ONLY                       = [string]$contractValues['AUDIT_APPEND_ONLY']
    AUDIT_REDACTION_ENABLED                 = [string]$contractValues['AUDIT_REDACTION_ENABLED']
    EXTERNAL_FEDERATION_ENABLED             = [string]$contractValues['EXTERNAL_FEDERATION_ENABLED']
    SCIM_M2M_ENABLED                        = [string]$contractValues['SCIM_M2M_ENABLED']
    PROVISIONING_MODE                       = [string]$contractValues['PROVISIONING_MODE']
    PROVISIONING_TARGETS                    = [string]$contractValues['PROVISIONING_TARGETS']
    SSF_ENABLED                             = [string]$contractValues['SSF_ENABLED']
    MTLS_ENABLED                            = [string]$contractValues['MTLS_ENABLED']
    RADIUS_EAP_TLS_ENABLED                  = [string]$contractValues['RADIUS_EAP_TLS_ENABLED']
    CSV_EXPORT_ENABLED                      = [string]$contractValues['CSV_EXPORT_ENABLED']
    PROVISIONING_SCIM_BASE_URL               = [string]$contractValues['PROVISIONING_SCIM_BASE_URL']
    PROVISIONING_SCIM_TOKEN_URL              = [string]$contractValues['PROVISIONING_SCIM_TOKEN_URL']
    PROVISIONING_SCIM_CLIENT_ID              = [string]$contractValues['PROVISIONING_SCIM_CLIENT_ID']
    PROVISIONING_SCIM_SCOPE                  = [string]$contractValues['PROVISIONING_SCIM_SCOPE']
    PROVISIONING_ENTRA_ENABLED               = [string]$contractValues['PROVISIONING_ENTRA_ENABLED']
    PROVISIONING_ENTRA_BASE_URL              = [string]$contractValues['PROVISIONING_ENTRA_BASE_URL']
    PROVISIONING_ENTRA_TOKEN_URL             = [string]$contractValues['PROVISIONING_ENTRA_TOKEN_URL']
    PROVISIONING_ENTRA_CLIENT_ID             = [string]$contractValues['PROVISIONING_ENTRA_CLIENT_ID']
    PROVISIONING_ENTRA_SCOPE                 = [string]$contractValues['PROVISIONING_ENTRA_SCOPE']
    PROVISIONING_GOOGLE_WORKSPACE_ENABLED    = [string]$contractValues['PROVISIONING_GOOGLE_WORKSPACE_ENABLED']
    PROVISIONING_GOOGLE_WORKSPACE_BASE_URL   = [string]$contractValues['PROVISIONING_GOOGLE_WORKSPACE_BASE_URL']
    PROVISIONING_GOOGLE_WORKSPACE_TOKEN_URL  = [string]$contractValues['PROVISIONING_GOOGLE_WORKSPACE_TOKEN_URL']
    PROVISIONING_GOOGLE_WORKSPACE_SECRET_ID  = [string]$contractValues['PROVISIONING_GOOGLE_WORKSPACE_SECRET_ID']
    PROVISIONING_GOOGLE_WORKSPACE_DELEGATED_ADMIN = [string]$contractValues['PROVISIONING_GOOGLE_WORKSPACE_DELEGATED_ADMIN']
    SSF_RECEIVER_URL                         = [string]$contractValues['SSF_RECEIVER_URL']
    SSF_RECEIVER_AUDIENCE                    = [string]$contractValues['SSF_RECEIVER_AUDIENCE']
    MTLS_TRUSTED_CA_FILE                     = [string]$contractValues['MTLS_TRUSTED_CA_FILE']
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
