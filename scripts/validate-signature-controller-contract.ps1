[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path (Split-Path -Parent $PSCommandPath) '..'
}
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param([string]$Name, [ValidateSet('pass','fail')][string]$Status, [string]$Detail)
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}
function Read-File([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    return Get-Content -LiteralPath $path -Raw
}

$policy = Read-File 'k8s/gitops/signature-policy/cluster-image-policy.yaml'
if ($policy -and $policy -match 'kind:\s+ClusterImagePolicy' -and
    $policy -match 'policy\.sigstore\.dev/v1beta1' -and
    $policy -match 'harbor\.his-hope\.local:9443/his-hope/\*\*' -and
    $policy -match 'keyless:' -and
    $policy -match 'issuer:\s*https://token\.actions\.githubusercontent\.com' -and
    $policy -match 'subjectRegExp:' -and
    $policy -match 'predicateType:\s*https://slsa\.dev/provenance/v1') {
    Add-Check 'cluster-image-policy' 'pass' 'ClusterImagePolicy is scoped to Harbor and requires the approved GitHub OIDC keyless signature plus SLSA provenance.'
} else {
    Add-Check 'cluster-image-policy' 'fail' 'ClusterImagePolicy is missing, malformed or not scoped to the approved Harbor registry.'
}

$productionImages = Read-File 'k8s/overlays/prod/image-digests/kustomization.yaml'
if ($policy -and $productionImages) {
    $policyMatch = [regex]::Match($policy, 'glob:\s*(\S+/his-hope)/\*\*')
    $imageMatch = [regex]::Match($productionImages, 'newName:\s*(\S+/his-hope)/[^\s#]+')
    if (-not $policyMatch.Success -or -not $imageMatch.Success) {
        Add-Check 'production-image-registry-alignment' 'fail' 'Production digest component or ClusterImagePolicy does not declare a canonical Harbor registry.'
    } else {
        $policyRegistry = $policyMatch.Groups[1].Value
    $imageRegistries = @([regex]::Matches($productionImages, 'newName:\s*(\S+/his-hope)/[^\s#]+') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
    if ($imageRegistries.Count -eq 1 -and $imageRegistries[0] -eq $policyRegistry) {
        Add-Check 'production-image-registry-alignment' 'pass' "Production digest component and ClusterImagePolicy both use $policyRegistry."
    } else {
        Add-Check 'production-image-registry-alignment' 'fail' "Production image registry differs from ClusterImagePolicy: policy=$policyRegistry; images=$($imageRegistries -join ', ')."
    }
    }
} else {
    Add-Check 'production-image-registry-alignment' 'fail' 'Production digest component or ClusterImagePolicy does not declare a canonical Harbor registry.'
}

$prodNamespace = Read-File 'k8s/overlays/prod/namespace-pod-security-patch.yaml'
$stagingNamespace = Read-File 'k8s/overlays/staging/namespace-policy-patch.yaml'
foreach ($entry in @(@{ Name = 'production-namespace-opt-in'; Text = $prodNamespace }, @{ Name = 'staging-namespace-opt-in'; Text = $stagingNamespace })) {
    if ($entry.Text -and $entry.Text -match 'policy\.sigstore\.dev/include:\s*["'']?true') {
        Add-Check $entry.Name 'pass' 'Namespace opts into Sigstore Policy Controller verification.'
    } else {
        Add-Check $entry.Name 'fail' 'Namespace does not explicitly opt into signature admission.'
    }
}

$bootstrap = Read-File 'scripts/bootstrap-sigstore-policy-controller.ps1'
if ($bootstrap -and $bootstrap -match "Version = '0\.10\.5'" -and
    $bootstrap -match "webhook\.failurePolicy=Fail" -and
    $bootstrap -match 'AllowProduction') {
    Add-Check 'bootstrap-safety' 'pass' 'Controller bootstrap is version-pinned, fail-closed and production-protected.'
} else {
    Add-Check 'bootstrap-safety' 'fail' 'Controller bootstrap is not pinned or does not fail closed for production.'
}

$workflow = Read-File '.github/workflows/sigstore-policy-controller-bootstrap.yml'
if ($workflow -and $workflow -match 'test-signature-admission.ps1' -and
    $workflow -match 'signed_image' -and $workflow -match 'unsigned_image') {
    Add-Check 'admission-probe' 'pass' 'Protected workflow contains signed-accept and unsigned-reject server-side admission probes.'
} else {
    Add-Check 'admission-probe' 'fail' 'Protected workflow is missing positive/negative signature admission probes.'
}

$status = if (@($checks | Where-Object status -eq 'fail').Count -gt 0) { 'fail' } else { 'pass' }
$result = [pscustomobject]@{ status = $status; generatedAtUtc = [DateTime]::UtcNow.ToString('o'); checks = @($checks) }
$json = $result | ConvertTo-Json -Depth 6
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 30 }
exit 0
