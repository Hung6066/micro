[CmdletBinding()]
param([string]$Path = 'k8s/gitops/bootstrap', [string]$OutputPath)

$ErrorActionPreference = 'Stop'
$rendered = (& kubectl kustomize $Path --load-restrictor LoadRestrictionsNone 2>&1) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "Argo bootstrap render failed: $Path" }
$applications = ([regex]::Matches($rendered, '(?m)^kind:\s*Application\s*$')).Count
$productionRevisions = ([regex]::Matches($rendered, '(?m)^\s*targetRevision:\s*production\s*$')).Count
$failures = @()
if ($applications -ne 9) { $failures += "expected 9 Applications, found $applications" }
if ($productionRevisions -ne 9) { $failures += "expected 9 production target revisions, found $productionRevisions" }
$result = [pscustomobject]@{ status = if ($failures.Count) { 'fail' } else { 'pass' }; applications = $applications; productionTargetRevisions = $productionRevisions; failures = @($failures); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $result | ConvertTo-Json -Depth 6
if ($OutputPath) { $dir = Split-Path -Parent $OutputPath; if ($dir) { New-Item -ItemType Directory -Force $dir | Out-Null }; [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false)) }
Write-Output $json
if ($failures.Count) { exit 30 }
