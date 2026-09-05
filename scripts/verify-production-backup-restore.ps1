[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Kubeconfig,
    [Parameter(Mandatory)][string]$RestoreManifest,
    [Parameter(Mandatory)][string]$TargetNamespace,
    [string]$EnvFile = 'D:\secure\his-hope\azure-production.env',
    [double]$RpoMinutes = 0,
    [string]$OutputPath = 'artifacts/evidence/database-restore-drill.json',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if (-not (Test-Path -LiteralPath $RestoreManifest -PathType Leaf)) { throw "Restore manifest not found: $RestoreManifest" }
if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) { throw "Azure env file not found: $EnvFile" }
if ($TargetNamespace -match '^(spire|his-hope|his-hope-prod|default|kube-system)$' -or $TargetNamespace -notmatch '^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$') {
    throw 'TargetNamespace must be a DNS label outside production namespaces.'
}
if ($RpoMinutes -lt 0 -or [double]::IsNaN($RpoMinutes) -or [double]::IsInfinity($RpoMinutes)) { throw 'RpoMinutes must be finite and non-negative.' }
if ($Apply -and -not $AllowProduction) { throw 'Apply is protected; provide -AllowProduction after approved change control.' }

$values = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) { continue }
    if ($trimmed -notmatch '^([A-Z0-9_]+)=(.*)$') { throw 'Invalid Azure env file line format.' }
    $values[$Matches[1]] = $Matches[2]
}
foreach ($key in @('AZURE_STORAGE_ACCOUNT','AZURE_STORAGE_CONTAINER','AZURE_STORAGE_ENDPOINT','AZURE_STORAGE_SAS_TOKEN','AZURE_BACKUP_PREFIX')) {
    if (-not $values.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($values[$key]) -or $values[$key] -match 'REPLACE_ME|<[^>]+>') { throw "Missing or placeholder Azure value: $key" }
}

$endpoint = $values['AZURE_STORAGE_ENDPOINT'].TrimEnd('/')
$account = $values['AZURE_STORAGE_ACCOUNT'].Trim()
$container = $values['AZURE_STORAGE_CONTAINER'].Trim()
$sas = $values['AZURE_STORAGE_SAS_TOKEN'].Trim().TrimStart('?')
$prefix = $values['AZURE_BACKUP_PREFIX'].Trim('/')
$parsedEndpoint = [Uri]$endpoint
if ($parsedEndpoint.Scheme -ne 'https' -or $parsedEndpoint.Host -ne "$account.blob.core.windows.net" -or $parsedEndpoint.AbsolutePath -ne '/') { throw 'Azure endpoint must be the account HTTPS blob endpoint.' }

$output = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $output
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$started = [DateTime]::UtcNow
$status = 'fail'
$failure = $null
$blobName = $null
$oldKubeconfig = $env:KUBECONFIG

try {
    # Azure Blob REST listing keeps the SAS in memory and never places it in a
    # command-line argument or log. Only the backup prefix and a non-empty blob
    # are retained as evidence.
    $listUri = "$endpoint/${container}?restype=container&comp=list&prefix=$([Uri]::EscapeDataString($prefix))&$sas"
    [string]$listingText = Invoke-RestMethod -Method Get -Uri $listUri -Headers @{ 'x-ms-version' = '2023-11-03' }
    [xml]$listing = $listingText.TrimStart([char]0xFEFF)
    $blob = @($listing.EnumerationResults.Blobs.Blob | Where-Object {
        [int64]$_.Properties.'Content-Length' -gt 0
    } | Select-Object -First 1)
    if (-not $blob) { throw "No non-empty Azure backup object found under prefix '$prefix'." }
    $blobName = [string]$blob.Name

    $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
    $drillArgs = @('-Environment','production','-Kubeconfig',$Kubeconfig,
        '-RestoreManifest',$RestoreManifest,'-TargetNamespace',$TargetNamespace,
        '-RpoMinutes',([string]$RpoMinutes),'-OutputPath',$OutputPath)
    if ($Apply) { $drillArgs += '-AllowProduction' } else { $drillArgs += '-WhatIf' }
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'test-cnpg-restore-drill.ps1') @drillArgs
    if ($LASTEXITCODE -ne 0) { throw 'Isolated CNPG restore drill failed.' }
    $status = if ($Apply) { 'pass' } else { 'skipped' }
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(sas|token|secret|password|sig)=[^&;\s]*', '$1=[redacted]'
    throw $failure
}
finally {
    $rtoMinutes = [math]::Round(([DateTime]::UtcNow - $started).TotalMinutes, 3)
    $doc = [pscustomobject]@{
        status = $status
        executedAtUtc = $started.ToString('o')
        rpoMinutes = $RpoMinutes
        rtoMinutes = $rtoMinutes
        azureBackupPrefix = $prefix
        azureObjectFound = [bool]$blobName
        azureObjectName = if ($blobName) { [IO.Path]::GetFileName($blobName) } else { $null }
        restoreVerified = ($status -eq 'pass')
        target = $TargetNamespace
        targetNamespace = $TargetNamespace
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    if ($null -ne $oldKubeconfig) { $env:KUBECONFIG = $oldKubeconfig }
    else { Remove-Item Env:KUBECONFIG -ErrorAction SilentlyContinue }
}

if ($status -eq 'pass') { exit 0 }
if ($status -eq 'skipped') { exit 0 }
exit 1
