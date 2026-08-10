$ErrorActionPreference = 'Stop'
$p = Get-Content (Join-Path $PSScriptRoot '../playbooks/40-production-orchestrator.yml') -Raw
foreach ($name in '00-preflight.yml','05-configure-external-lb.yml','10-bootstrap-k3s.yml','20-verify-cluster.yml','15-bootstrap-workers.yml','30-backup-agents.yml') {
    if ($p.IndexOf($name, [StringComparison]::Ordinal) -lt 0) { throw "Missing phase $name" }
}
if ($p.IndexOf('00-preflight.yml', [StringComparison]::Ordinal) -gt $p.IndexOf('05-configure-external-lb.yml', [StringComparison]::Ordinal)) { throw 'Phase order is invalid.' }
foreach ($tag in 'phase-preflight','phase-load-balancer','phase-control-plane','phase-verify','phase-workers','phase-backup') {
    if ($p.IndexOf($tag, [StringComparison]::Ordinal) -lt 0) { throw "Missing phase tag $tag" }
}
Write-Output 'Orchestrator structure PASS'
