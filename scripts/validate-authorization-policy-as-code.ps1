[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path,
    [string]$CatalogPath = 'config/authorization-policies/catalog.v1.json',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$path = Join-Path $RepositoryRoot $CatalogPath
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Policy catalog not found: $CatalogPath" }

# ConvertFrom-Json has no -Depth parameter on Windows PowerShell 5.1.
# The catalog schema is shallow enough that the default parser depth is sufficient,
# while this keeps the validator runnable in both Windows PowerShell and pwsh.
try { $catalog = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
catch { throw "Policy catalog is not valid JSON: $($_.Exception.Message)" }

if ($catalog.schemaVersion -ne 'authorization-policy-catalog.v1') { throw 'Unsupported policy catalog schema.' }
if ($null -eq $catalog.policies -or @($catalog.policies).Count -eq 0) { throw 'Policy catalog must contain at least one policy.' }

$allowedKeys = @('requiredFacility', 'allowedPurposeOfUse', 'requireFreshDevicePosture', 'allowBreakGlass', 'requiredAssurance')
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

function Get-PropertyNames($object) {
    @($object.PSObject.Properties | ForEach-Object Name)
}

function Get-ContextValue($context, [string]$name, $default) {
    $property = $context.PSObject.Properties[$name]
    if ($null -eq $property -or $null -eq $property.Value) { return $default }
    return $property.Value
}

function Invoke-PolicyDecision($rules, $context) {
    if ([bool](Get-ContextValue $rules 'requiredFacility' $false) -and [string]::IsNullOrWhiteSpace([string](Get-ContextValue $context 'facilityId' ''))) { return 'facility_required' }
    $purposes = $rules.PSObject.Properties['allowedPurposeOfUse']
    if ($null -ne $purposes -and @($purposes.Value) -notcontains (Get-ContextValue $context 'purposeOfUse' $null)) { return 'purpose_of_use_denied' }
    if ([bool](Get-ContextValue $rules 'requireFreshDevicePosture' $false) -and -not [bool](Get-ContextValue $context 'devicePostureFresh' $false)) { return 'device_posture_stale' }
    if ($rules.PSObject.Properties['allowBreakGlass'] -and -not [bool]$rules.allowBreakGlass -and [bool](Get-ContextValue $context 'isBreakGlass' $false)) { return 'break_glass_denied' }
    if ($rules.PSObject.Properties['requiredAssurance'] -and $rules.requiredAssurance -ne (Get-ContextValue $context 'assurance' $null)) { return 'assurance_insufficient' }
    return 'abac_context_match'
}

foreach ($policy in @($catalog.policies)) {
    if ([string]::IsNullOrWhiteSpace($policy.key) -or $policy.key -notmatch '^[a-z0-9][a-z0-9._-]{0,127}$') { throw 'Policy key is missing or not canonical.' }
    if (-not $seen.Add($policy.key)) { throw "Duplicate policy key: $($policy.key)" }
    if ([string]::IsNullOrWhiteSpace($policy.owner) -or [string]::IsNullOrWhiteSpace($policy.description)) { throw "Policy metadata is incomplete: $($policy.key)" }
    if ($null -eq $policy.rules -or (Get-PropertyNames $policy.rules | Where-Object { $allowedKeys -notcontains $_ }).Count -gt 0) { throw "Policy has unknown rule key: $($policy.key)" }
    foreach ($name in @('requiredFacility', 'requireFreshDevicePosture', 'allowBreakGlass')) {
        $property = $policy.rules.PSObject.Properties[$name]
        if ($null -ne $property -and $property.Value -isnot [bool]) { throw "Rule $name must be boolean: $($policy.key)" }
    }
    $purpose = $policy.rules.PSObject.Properties['allowedPurposeOfUse']
    if ($null -ne $purpose -and (@($purpose.Value).Count -eq 0 -or @($purpose.Value | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count -gt 0)) { throw "allowedPurposeOfUse must be a non-empty string array: $($policy.key)" }
    $assurance = $policy.rules.PSObject.Properties['requiredAssurance']
    if ($null -ne $assurance -and @('mfa', 'passkey', 'mtls') -notcontains $assurance.Value) { throw "requiredAssurance is invalid: $($policy.key)" }
    if ($null -eq $policy.fixtures -or @($policy.fixtures).Count -eq 0) { throw "Policy has no allow/deny fixtures: $($policy.key)" }
    foreach ($fixture in @($policy.fixtures)) {
        if ([string]::IsNullOrWhiteSpace($fixture.name) -or $null -eq $fixture.allow -or [string]::IsNullOrWhiteSpace($fixture.reason)) { throw "Fixture metadata is incomplete: $($policy.key)" }
        $actualReason = Invoke-PolicyDecision $policy.rules $fixture.context
        $actualAllow = $actualReason -eq 'abac_context_match'
        if ($actualAllow -ne [bool]$fixture.allow -or $actualReason -ne $fixture.reason) { throw "Fixture failed: $($policy.key)/$($fixture.name); expected $($fixture.reason), got $actualReason" }
    }
}

$result = [ordered]@{
    schemaVersion = 'authorization-policy-as-code-evidence.v1'
    status = 'pass'
    catalog = $CatalogPath
    catalogSha256 = $null
    policyCount = @($catalog.policies).Count
    fixtureCount = @($catalog.policies | ForEach-Object { @($_.fixtures) }).Count
    validatedAtUtc = [DateTime]::UtcNow.ToString('o')
}
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $result.catalogSha256 = ([System.BitConverter]::ToString($sha256.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
}
finally { $sha256.Dispose() }
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $evidencePath = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $OutputPath))
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidencePath) | Out-Null
    [IO.File]::WriteAllText($evidencePath, ($result | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
}
Write-Output "Authorization policy-as-code catalog passed: $($result.policyCount) policies, $($result.fixtureCount) fixtures."
