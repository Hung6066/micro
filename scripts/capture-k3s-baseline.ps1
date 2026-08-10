[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Kubeconfig,
    [string]$OutputPath = 'artifacts/evidence/k3s-baseline.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path

function Protect-Output([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    # Baseline commands intentionally request metadata/status only. Redact any
    # accidental credential-shaped values before writing the evidence artifact.
    return ($Text -replace '(?im)(password|token|clientSecret|client_secret|sasToken|privateKey)(\s*[=:]\s*)[^\s,;}]+' ,'$1$2[REDACTED]')
}

function Invoke-KubectlRead([string]$Name, [string[]]$Arguments) {
    $cmdArgs = @($Arguments) + @('--request-timeout=15s')
    $output = @(& kubectl @cmdArgs 2>&1)
    $exitCode = $LASTEXITCODE
    [pscustomobject]@{
        name = $Name
        status = if ($exitCode -eq 0) { 'pass' } else { 'unavailable' }
        exitCode = $exitCode
        output = Protect-Output (($output | ForEach-Object { $_.ToString() }) -join "`n")
    }
}

$checks = @()
$checks += Invoke-KubectlRead -Name 'context' -Arguments @('config','current-context')
$checks += Invoke-KubectlRead -Name 'version' -Arguments @('version','-o','yaml')
$checks += Invoke-KubectlRead -Name 'nodes' -Arguments @('get','nodes','-o','wide')
$checks += Invoke-KubectlRead -Name 'pods' -Arguments @('get','pods','-A','-o','wide')
$checks += Invoke-KubectlRead -Name 'deployments' -Arguments @('get','deploy','-A')
$checks += Invoke-KubectlRead -Name 'events' -Arguments @('get','events','-A','--sort-by=.lastTimestamp','-o','custom-columns=LAST:.lastTimestamp,TYPE:.type,REASON:.reason,OBJECT:.involvedObject.kind/.involvedObject.name')
$checks += Invoke-KubectlRead -Name 'crds' -Arguments @('get','crd')
$checks += Invoke-KubectlRead -Name 'webhooks' -Arguments @('get','validatingwebhookconfiguration,mutatingwebhookconfiguration')
$checks += Invoke-KubectlRead -Name 'network-policies' -Arguments @('get','networkpolicy','-A')
$checks += Invoke-KubectlRead -Name 'ingress' -Arguments @('get','ingress','-A')
$checks += Invoke-KubectlRead -Name 'namespaces' -Arguments @('get','namespace','--show-labels')

$status = if (@($checks | Where-Object status -eq 'unavailable').Count -gt 0) { 'blocked' } else { 'pass' }
$result = [pscustomobject]@{
    status = $status
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    checks = @($checks)
}
$json = $result | ConvertTo-Json -Depth 8
$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
Write-Output $json
if ($status -eq 'blocked') { exit 70 }
exit 0
