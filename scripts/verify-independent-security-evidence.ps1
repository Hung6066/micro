[CmdletBinding()]
param(
    [string]$EvidenceRoot = "artifacts/security"
)

$ErrorActionPreference = "Stop"
$required = @(
    "oidc-conformance/report.json",
    "penetration-test/report.json"
)

foreach ($relative in $required) {
    $path = Join-Path $EvidenceRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing independent security evidence: $path"
    }

    $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($document.status -ne "passed") {
        throw "Security evidence is not passed: $path"
    }
    $expectedType = if ($relative.StartsWith("oidc-conformance/", [StringComparison]::OrdinalIgnoreCase)) {
        "oidc-conformance"
    } else {
        "penetration-test"
    }
    if ($document.assessmentType -ne $expectedType) {
        throw "Security evidence has the wrong assessmentType: $path"
    }
    if ($document.evidenceSource -ne "external-independent") {
        throw "Security evidence is not independently produced: $path"
    }
    if ($null -eq $document.signature -or
        $document.signature.verified -ne $true -or
        [string]::IsNullOrWhiteSpace([string]$document.signature.algorithm) -or
        [string]::IsNullOrWhiteSpace([string]$document.signature.verificationUri)) {
        throw "Security evidence lacks verified signature metadata: $path"
    }
    $signatureUri = $null
    if (-not [Uri]::TryCreate([string]$document.signature.verificationUri, [UriKind]::Absolute, [ref]$signatureUri) -or
        $signatureUri.Scheme -ne "https") {
        throw "Security evidence signature metadata is malformed: $path"
    }
    if ([string]::IsNullOrWhiteSpace([string]$document.assessor) -or
        [string]::IsNullOrWhiteSpace([string]$document.reportUri) -or
        [string]::IsNullOrWhiteSpace([string]$document.completedAt)) {
        throw "Security evidence lacks independent assessor metadata: $path"
    }
    $reportUri = $null
    if (-not [Uri]::TryCreate([string]$document.reportUri, [UriKind]::Absolute, [ref]$reportUri) -or
        $reportUri.Scheme -ne "https") {
        throw "Security evidence metadata is malformed: $path"
    }

    try {
        [void][DateTimeOffset]::Parse([string]$document.completedAt, [Globalization.CultureInfo]::InvariantCulture)
    } catch {
        throw "Security evidence metadata is malformed: $path"
    }
}

Write-Host "Independent OIDC conformance and penetration-test evidence is present and passed."
