[CmdletBinding()]
param([switch]$RequireAll)

$ErrorActionPreference = 'Stop'

function Test-AllValues([string[]]$Names) {
    foreach ($name in $Names) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { return $false }
    }
    return $true
}

function Test-UrlValue([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    $uri = $null
    return -not [string]::IsNullOrWhiteSpace($value) -and [Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -and $uri.Scheme -in @('http', 'https')
}

function Test-Truthy([string]$Name) {
    return [Environment]::GetEnvironmentVariable($Name) -in @('1', 'true', 'enabled', 'live', 'yes')
}

function Test-Ready($Check) {
    if ($Check.Mode -and ([Environment]::GetEnvironmentVariable('PROVISIONING_MODE') -notin @('enabled', 'live'))) { return $false }
    if ($Check.Enabled -and -not (Test-Truthy $Check.Enabled)) { return $false }
    if ($Check.Required -and -not (Test-AllValues $Check.Required)) { return $false }
    if ($Check.Url -and -not (Test-UrlValue $Check.Url)) { return $false }
    if ($Check.Https) {
        foreach ($urlName in @($Check.Https)) {
            if (-not (Test-UrlValue $urlName)) { return $false }
        }
    }
    if ($Check.File) { return Test-Path -LiteralPath ([Environment]::GetEnvironmentVariable($Check.File)) -PathType Leaf }
    return $true
}

function Get-MissingPrerequisites($Check) {
    $missing = [System.Collections.Generic.List[string]]::new()
    if ($Check.Mode -and ([Environment]::GetEnvironmentVariable('PROVISIONING_MODE') -notin @('enabled', 'live'))) {
        $missing.Add('PROVISIONING_MODE=enabled|live')
    }
    if ($Check.Enabled -and -not (Test-Truthy $Check.Enabled)) {
        $missing.Add("$($Check.Enabled)=true")
    }
    if ($Check.Required) {
        foreach ($name in @($Check.Required)) {
            if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
                $missing.Add($name)
            }
        }
    }
    if ($Check.Url -and -not (Test-UrlValue $Check.Url)) { $missing.Add($Check.Url) }
    if ($Check.Https) {
        foreach ($urlName in @($Check.Https)) {
            if (-not (Test-UrlValue $urlName)) { $missing.Add("$urlName=https-url") }
        }
    }
    if ($Check.File) {
        $filePath = [Environment]::GetEnvironmentVariable($Check.File)
        if ([string]::IsNullOrWhiteSpace($filePath) -or -not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            $missing.Add("$($Check.File)=existing-file")
        }
    }
    return $missing
}

$checks = @(
    # Outbound provisioning gates must use their own enablement/configuration,
    # not inbound OAuth login credentials which can exist independently.
    @{ Name = 'google-workspace'; Mode = $true; Enabled = 'PROVISIONING_GOOGLE_WORKSPACE_ENABLED'; Required = @('PROVISIONING_GOOGLE_WORKSPACE_SECRET_ID', 'PROVISIONING_GOOGLE_WORKSPACE_DELEGATED_ADMIN'); Url = 'PROVISIONING_GOOGLE_WORKSPACE_TOKEN_URL' },
    @{ Name = 'entra-id'; Mode = $true; Enabled = 'PROVISIONING_ENTRA_ENABLED'; Required = @('ENTRA_TENANT_ID', 'PROVISIONING_ENTRA_CLIENT_ID'); Url = 'PROVISIONING_ENTRA_TOKEN_URL' },
    @{ Name = 'ssf-receiver'; Enabled = 'SSF_ENABLED'; Required = @('SSF_RECEIVER_AUDIENCE'); Https = 'SSF_RECEIVER_URL' },
    @{ Name = 'mtls'; Enabled = 'MTLS_ENABLED'; File = 'MTLS_TRUSTED_CA_FILE' },
    @{ Name = 'radius-eap-tls'; Enabled = 'RADIUS_EAP_TLS_ENABLED'; Required = @('RADIUS_SERVER') },
    @{ Name = 'chrome-device-trust'; Https = 'CHROME_VERIFIED_ACCESS_URL' },
    @{ Name = 'windows-local-login'; Required = @('WINDOWS_DEVICE_LOGIN_LAB') },
    @{ Name = 'siem-worm'; Required = @('AUDIT_APPEND_ONLY', 'AUDIT_REDACTION_ENABLED', 'AUDIT_WORM_BUCKET', 'AUDIT_WORM_RETENTION_DAYS', 'AUDIT_WORM_EVIDENCE_URI'); Https = @('AUDIT_SIEM_URL', 'AUDIT_WORM_ENDPOINT') },
    @{ Name = 'ha-dr'; Required = @('HA_DR_EVIDENCE_URI', 'HA_DR_RPO_MINUTES', 'HA_DR_RTO_MINUTES'); Https = 'HA_DR_EVIDENCE_URI' },
    @{ Name = 'fapi-conformance'; Required = @('FAPI_CONFORMANCE_PROFILE', 'FAPI_CONFORMANCE_TEST_CLIENT_ID', 'FAPI_CONFORMANCE_SECRET_REF'); Https = @('FAPI_CONFORMANCE_ISSUER', 'FAPI_CONFORMANCE_REPORT_URI') }
)

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($check in $checks) {
    if (Test-Ready $check) {
        Write-Output "LIVE_GATE_READY $($check.Name)"
    }
    else {
        $missing.Add($check.Name)
        $details = (Get-MissingPrerequisites $check) -join ','
        Write-Output "LIVE_GATE_SKIPPED $($check.Name) missing_prerequisite=$details"
    }
}

if ($RequireAll -and $missing.Count -gt 0) {
    throw "Missing live prerequisites: $($missing -join ', ')"
}

if ($missing.Count -eq 0) {
    Write-Output 'IDENTITY_LIVE_PREREQUISITES_READY'
}
else {
    Write-Output "IDENTITY_LIVE_PREREQUISITES_PARTIAL skipped=$($missing.Count)"
}
