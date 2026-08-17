[CmdletBinding()]
param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $RepoRoot 'config\identity-workbench-12-parts.v1.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'missing_identity_workbench_manifest' }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.schemaVersion -ne 'identity-workbench-12-parts.v1') { throw 'invalid_identity_workbench_manifest_version' }
if ($manifest.parts.Count -ne 12) { throw "expected_12_parts_actual_$($manifest.parts.Count)" }
$allowed = @($manifest.statusSemantics)
foreach ($part in $manifest.parts) {
  if ([string]::IsNullOrWhiteSpace($part.id) -or [string]::IsNullOrWhiteSpace($part.evidence)) { throw 'part_missing_id_or_evidence' }
  if ($allowed -notcontains $part.status) { throw "invalid_part_status:$($part.id)" }
  $evidencePath = Join-Path $RepoRoot $part.evidence
  if (-not (Test-Path -LiteralPath $evidencePath)) { throw "missing_part_evidence:$($part.id):$($part.evidence)" }
}
Write-Output "IDENTITY_WORKBENCH_12_PARTS_VALIDATED parts=$($manifest.parts.Count)"
