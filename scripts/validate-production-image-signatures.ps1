[CmdletBinding()]
param(
    [string]$Overlay = 'k8s/overlays/prod',
    [switch]$RequireSigned,
    [string]$CosignKey,
    [string]$CosignPath,
    [string]$CosignCertificateIdentityRegex,
    [string]$CosignCertificateOidcIssuerRegex
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$rendered = & kubectl kustomize $Overlay --load-restrictor LoadRestrictionsNone 2>&1
if ($LASTEXITCODE -ne 0) { throw "Unable to render $Overlay.`n$($rendered -join "`n")" }

$refs = [regex]::Matches(($rendered -join "`n"), '(?m)^\s*image:\s*(?<image>[^\s]+)') |
    ForEach-Object { $_.Groups['image'].Value } |
    Where-Object { $_ -and $_ -notmatch '^\$' -and $_ -notmatch '^description:$' } |
    Sort-Object -Unique

if (-not $refs) { throw "No container images were found in $Overlay." }

$unresolved = @($refs | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
$mutableTagged = @($refs | Where-Object { $_ -match ':(latest|production)@sha256:[0-9a-f]{64}$' })
$zero = @($refs | Where-Object { $_ -match 'sha256:0{64}' })
if ($zero.Count -gt 0) { throw "Zero placeholder image digests remain: $($zero -join ', ')" }

if ($mutableTagged.Count -gt 0) {
    $message = "Mutable production tags remain even though the references are digest-pinned: $($mutableTagged -join ', ')"
    if ($RequireSigned) { throw $message }
    Write-Warning $message
}

if ($unresolved.Count -gt 0) {
    $message = "Unpinned image references remain: $($unresolved -join ', ')"
    if ($RequireSigned) { throw $message }
    Write-Warning $message
}

$cosign = if (-not [string]::IsNullOrWhiteSpace($CosignPath)) {
    if (-not (Test-Path $CosignPath)) { throw "Cosign binary not found: $CosignPath" }
    [pscustomobject]@{ Source = (Resolve-Path $CosignPath).Path }
} else {
    Get-Command cosign -ErrorAction SilentlyContinue
}
if ($RequireSigned -and -not $cosign) {
    throw 'Signed image gate is blocked: cosign is not installed on this runner.'
}

if ($RequireSigned -and [string]::IsNullOrWhiteSpace($CosignKey) -and [string]::IsNullOrWhiteSpace($CosignCertificateIdentityRegex)) {
    throw 'Signed image gate requires -CosignKey or keyless identity parameters.'
}

$unsigned = [System.Collections.Generic.List[string]]::new()
function Invoke-CosignVerify([string[]]$Arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $cosign.Source @Arguments 2>&1 | Out-Null
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previous
    return $exitCode
}
if ($cosign) {
    foreach ($ref in $refs | Where-Object { $_ -match '@sha256:[0-9a-f]{64}$' }) {
        $args = @('verify')
        if (-not [string]::IsNullOrWhiteSpace($CosignKey)) {
            $args += @('--key', $CosignKey)
        } else {
            $args += @('--certificate-identity-regexp', $CosignCertificateIdentityRegex,
                '--certificate-oidc-issuer-regexp', $CosignCertificateOidcIssuerRegex)
        }
        $args += $ref
        if ((Invoke-CosignVerify $args) -ne 0) { $unsigned.Add($ref) }
    }
}

if ($RequireSigned -and $unsigned.Count -gt 0) {
    throw "Cosign verification failed for $($unsigned.Count) image(s): $($unsigned -join ', ')"
}

if ($unresolved.Count -eq 0 -and $mutableTagged.Count -eq 0 -and $unsigned.Count -eq 0 -and $cosign) {
    Write-Output "Production image gate PASS: $($refs.Count) image references are digest-pinned and cosign-verified."
} elseif ($unresolved.Count -eq 0 -and $mutableTagged.Count -eq 0) {
    Write-Output "Production image gate PARTIAL: $($refs.Count) image references are real digest-pinned, but signed verification is unavailable or not requested."
} else {
    Write-Output "Production image gate BLOCKED: digest pinning or immutable tag policy is incomplete; signed verification was not claimed."
}
