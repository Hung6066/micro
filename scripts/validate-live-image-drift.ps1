[CmdletBinding()]
param(
    [ValidateSet('prod', 'staging')][string]$Environment = 'prod',
    [Parameter(Mandatory = $true)][string]$Kubeconfig,
    [string]$Namespace = 'his-hope'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$overlay = if ($Environment -eq 'prod') { 'prod' } else { 'staging' }
$rendered = kubectl kustomize "k8s/overlays/$overlay" --load-restrictor LoadRestrictionsNone
if ($LASTEXITCODE -ne 0) { throw "Kustomize render failed for $overlay." }
$text = $rendered -join "`n"
$expected = @{}
foreach ($match in [regex]::Matches($text, '(?m)^\s*image:\s*(?<image>[^\s]+@sha256:[0-9a-f]{64})\s*$')) {
    $image = $match.Groups['image'].Value
    if ($image -match '/(?<component>[a-z0-9][a-z0-9-]*)(?::[^@]+)?@sha256:[0-9a-f]{64}$') {
        $expected[$Matches['component']] = $image
    }
}
if ($expected.Count -eq 0) { throw 'No immutable production image references were rendered.' }
$deployments = (kubectl get deployments -n $Namespace -o json | ConvertFrom-Json).items
$drifts = [System.Collections.Generic.List[string]]::new()
foreach ($deployment in @($deployments)) {
    foreach ($container in @($deployment.spec.template.spec.containers)) {
        $component = [string]$container.name
        $live = [string]$container.image
        if (-not $expected.ContainsKey($component)) { continue }
        if ($live -ne $expected[$component]) { $drifts.Add("$($deployment.metadata.name):$component") }
    }
}
if ($drifts.Count -gt 0) {
    throw "Live image drift detected: $($drifts -join ', ')"
}
Write-Output "Live image drift PASS: $($expected.Count) reviewed component image(s) match."
