[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$ImageRef,
    [string]$CosignPath,
    [string]$CertificateIdentityRegex,
    [string]$CertificateOidcIssuerRegex = '^https://token.actions.githubusercontent.com$'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$cosign = if ($CosignPath) {
    if (-not (Test-Path -LiteralPath $CosignPath -PathType Leaf)) {
        throw "Cosign binary not found: $CosignPath"
    }
    (Resolve-Path -LiteralPath $CosignPath).Path
} else {
    $command = Get-Command cosign -ErrorAction SilentlyContinue
    if ($command) { $command.Source } else { $null }
}

if (-not $cosign) {
    throw 'Cosign is required for image attestation verification.'
}
if ([string]::IsNullOrWhiteSpace($CertificateIdentityRegex)) {
    throw 'CertificateIdentityRegex is required for keyless verification.'
}

$failed = [System.Collections.Generic.List[string]]::new()
foreach ($ref in ($ImageRef | Sort-Object -Unique)) {
    if ($ref -notmatch '@sha256:[0-9a-f]{64}$') {
        $failed.Add("not-digest-pinned:$ref")
        continue
    }

    & $cosign verify `
        '--certificate-identity-regexp' $CertificateIdentityRegex `
        '--certificate-oidc-issuer-regexp' $CertificateOidcIssuerRegex `
        $ref 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        $failed.Add("signature:$ref")
        continue
    }

    & $cosign verify-attestation `
        '--type' 'https://slsa.dev/provenance/v1' `
        '--certificate-identity-regexp' $CertificateIdentityRegex `
        '--certificate-oidc-issuer-regexp' $CertificateOidcIssuerRegex `
        $ref 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        $failed.Add("provenance:$ref")
    }
}

if ($failed.Count -gt 0) {
    throw "Image attestation gate FAILED: $($failed -join ', ')"
}

Write-Output "Image attestation gate PASS: $($ImageRef.Count) unique digest(s) verified."
