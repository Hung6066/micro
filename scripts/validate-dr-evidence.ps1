[CmdletBinding()]
param(
    [string]$EvidenceDirectory = 'artifacts/evidence',
    [string]$OutputPath,
    [switch]$StaticOnly,
    [string[]]$OnlyFile,
    [double]$MaxEvidenceAgeHours = 168
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($MaxEvidenceAgeHours -le 0 -or [double]::IsNaN($MaxEvidenceAgeHours) -or [double]::IsInfinity($MaxEvidenceAgeHours)) {
    throw 'MaxEvidenceAgeHours must be a finite positive number.'
}

$required = @(
    'database-restore-drill.json',
    'vault-recovery-drill.json',
    'harbor-clean-node-test.json',
    'control-plane-rebuild-drill.json',
    'application-restore-smoke.json'
)
if ($OnlyFile -and $OnlyFile.Count -gt 0) {
    $unknown = @($OnlyFile | Where-Object { $required -notcontains $_ })
    if ($unknown.Count -gt 0) { throw "Unknown DR evidence file(s): $($unknown -join ', ')" }
    $required = @($required | Where-Object { $OnlyFile -contains $_ })
}
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param([string]$Name, [ValidateSet('pass','fail','skipped','unavailable')][string]$Status, [string]$Message)
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; message = $Message })
}

foreach ($file in $required) {
    $path = Join-Path $EvidenceDirectory $file
    if ($StaticOnly) {
        Add-Check ([IO.Path]::GetFileNameWithoutExtension($file)) 'skipped' 'Measured DR evidence is collected only by the protected production workflow.'
        continue
    }
    $name = [IO.Path]::GetFileNameWithoutExtension($file)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Check $name 'unavailable' "Required evidence file is missing: $file"
        continue
    }

    try {
        $raw = Get-Content -LiteralPath $path -Raw
        if ($raw -match '(?im)"?(password|token|privateKey|clientSecret|sasToken|kubeconfig)"?\s*:') {
            Add-Check $name 'fail' "Evidence contains a prohibited secret field: $file"
            continue
        }
        $doc = $raw | ConvertFrom-Json
        $rpo = [double]$doc.rpoMinutes
        $rto = [double]$doc.rtoMinutes
        $executed = [DateTime]::Parse([string]$doc.executedAtUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal)
        $verified = $doc.restoreVerified -eq $true
        $target = -not [string]::IsNullOrWhiteSpace([string]$doc.target)
        # Use IsNaN/IsInfinity instead of IsFinite for compatibility with the
        # Windows PowerShell/.NET runtime used by local and CI validation.
        $numericMeasurements =
            -not [double]::IsNaN($rpo) -and -not [double]::IsInfinity($rpo) -and
            -not [double]::IsNaN($rto) -and -not [double]::IsInfinity($rto)
        $ageHours = ([DateTime]::UtcNow - $executed).TotalHours
        $fresh = $executed -ne [DateTime]::MinValue -and $ageHours -ge -0.0833 -and $ageHours -le $MaxEvidenceAgeHours
        if ($doc.status -ne 'pass' -or -not $numericMeasurements -or $rpo -lt 0 -or $rto -lt 0 -or -not $fresh -or -not $verified -or -not $target) {
            Add-Check $name 'unavailable' "Evidence exists but does not satisfy the measured, fresh restore contract (max age ${MaxEvidenceAgeHours}h): $file"
            continue
        }
        Add-Check $name 'pass' "Measured RPO/RTO and restore verification recorded: $file"
    } catch {
        Add-Check $name 'unavailable' "Evidence is invalid or missing required fields: $file"
    }
}

$status = if (@($checks | Where-Object status -eq 'fail').Count -gt 0) { 'fail' }
          elseif (@($checks | Where-Object status -eq 'unavailable').Count -gt 0) { 'blocked' }
          else { 'pass' }
$result = [pscustomobject]@{
    status = $status
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    checks = @($checks)
}
$json = $result | ConvertTo-Json -Depth 6
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 80 }
if ($status -eq 'blocked') { exit 70 }
exit 0
