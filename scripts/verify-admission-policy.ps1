[CmdletBinding()]
param(
    [string]$PolicyPath = 'k8s/security/gatekeeper-production-constraints.yaml',
    [string]$OverlayPath = 'k8s/overlays/prod',
    [string]$GitOpsPolicyPath = 'k8s/gitops/policies/kustomization.yaml',
    [string]$SignaturePolicyPath = 'k8s/gitops/signature-policy/cluster-image-policy.yaml',
    [string]$GitOpsApplicationsPath = 'k8s/gitops/bootstrap/applications.yaml',
    [string]$SecurityBoundaryPath = 'k8s/policies/security-boundary-namespaces.yaml'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$policy = (Resolve-Path -LiteralPath $PolicyPath).Path
$overlay = (Resolve-Path -LiteralPath $OverlayPath).Path
$text = Get-Content -LiteralPath $policy -Raw
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($kind in @('K8sApprovedImageRegistry', 'K8sRestrictedWorkload', 'K8sRequiredResources')) {
    if ($text -notmatch "kind:\s+$kind") { $errors.Add("Missing Gatekeeper kind $kind") }
}
if (([regex]::Matches($text, 'enforcementAction:\s*deny')).Count -lt 3) {
    $errors.Add('Every production Gatekeeper constraint must use enforcementAction: deny.')
}
if ($text -notmatch 'harbor\.his-hope\.local:9443/his-hope/') {
    $errors.Add('Harbor registry prefix is not present in the approved registry policy.')
}
if (-not (Test-Path -LiteralPath $GitOpsPolicyPath -PathType Leaf)) {
    $errors.Add("GitOps policy kustomization is missing: $GitOpsPolicyPath")
} else {
    $gitOpsPolicy = Get-Content -LiteralPath $GitOpsPolicyPath -Raw
    if ($gitOpsPolicy -notmatch [regex]::Escape((Split-Path -Leaf $PolicyPath))) {
        $errors.Add('GitOps policy kustomization does not reference the production policy bundle.')
    }
}
if (-not (Test-Path -LiteralPath $GitOpsApplicationsPath -PathType Leaf) -or
    (Get-Content -LiteralPath $GitOpsApplicationsPath -Raw) -notmatch 'name:\s*his-hope-production-policies') {
    $errors.Add('GitOps bootstrap does not define the production policy Application.')
}
if (-not (Test-Path -LiteralPath $SignaturePolicyPath -PathType Leaf)) {
    $errors.Add("Signature policy manifest is missing: $SignaturePolicyPath")
} else {
    $signature = Get-Content -LiteralPath $SignaturePolicyPath -Raw
    $signatureRequirements = @(
        @{ Pattern = 'kind:\s+ClusterImagePolicy'; Label = 'ClusterImagePolicy kind' },
        @{ Pattern = 'policy\.sigstore\.dev/v1beta1'; Label = 'policy API version' },
        @{ Pattern = [regex]::Escape('harbor.his-hope.local:9443/his-hope/**'); Label = 'Harbor image glob' },
        @{ Pattern = 'his-hope\.io/signing-identity:\s*github-actions-container-release'; Label = 'approved signing identity' },
        @{ Pattern = 'issuer:\s*https://token\.actions\.githubusercontent\.com'; Label = 'GitHub OIDC issuer' },
        @{ Pattern = 'subjectRegExp:\s*\^https://github'; Label = 'release workflow subject restriction' },
        @{ Pattern = 'predicateType:\s*https://slsa\.dev/provenance/v1'; Label = 'SLSA provenance attestation' }
    )
    foreach ($required in $signatureRequirements) {
        if ($signature -notmatch $required.Pattern) { $errors.Add("Signature policy is missing: $($required.Label)") }
    }
}
if ((Get-Content -LiteralPath $GitOpsApplicationsPath -Raw) -notmatch 'name:\s*his-hope-signature-policy') {
    $errors.Add('GitOps bootstrap does not define the signature policy Application.')
}
if (-not (Test-Path -LiteralPath $SecurityBoundaryPath -PathType Leaf)) {
    $errors.Add("Security boundary namespace manifest is missing: $SecurityBoundaryPath")
} else {
    $boundaries = Get-Content -LiteralPath $SecurityBoundaryPath -Raw
    foreach ($name in @('his-hope-data', 'his-hope-system')) {
        if ($boundaries -notmatch "name:\s*$name") { $errors.Add("Missing security boundary namespace: $name") }
    }
    if ($boundaries -notmatch 'his-hope.io/migration-required:\s*"true"') {
        $errors.Add('Security boundary namespaces must be explicitly marked as migration-required.')
    }
}
if ((Get-Content -LiteralPath $GitOpsApplicationsPath -Raw) -notmatch 'name:\s*his-hope-security-boundaries') {
    $errors.Add('GitOps bootstrap does not define the security boundary Application.')
}

$rendered = & kubectl kustomize $overlay --load-restrictor LoadRestrictionsNone 2>&1
if ($LASTEXITCODE -ne 0) { $errors.Add('Production overlay did not render with kubectl kustomize.') }
if (($rendered -join "`n") -match '(?im)^\s*image:\s*[^\s@]+:(latest|dev|main)\s*$') {
    $errors.Add('Mutable image tag found in the rendered production overlay.')
}
if (($rendered -join "`n") -match 'harbor\.his-hope\.local:9443/his-hope/his-hope/') {
    $errors.Add('Duplicated Harbor project path found in the rendered production overlay.')
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 30
}
Write-Output 'Admission policy source gate PASS.'
