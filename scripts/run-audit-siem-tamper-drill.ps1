[CmdletBinding()]
param(
    [string]$EvidenceDirectory = 'artifacts/security'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Push-Location (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
try {
    dotnet test tests/Services/IdentityService/IdentityService.Infrastructure.Tests/IdentityService.Infrastructure.Tests.csproj `
        --configuration Release `
        --filter "FullyQualifiedName~SiemWormAuditForwarderTests" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'SIEM/WORM tamper drill tests failed.' }
} finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$artifact = [ordered]@{
    status = 'pass'
    executedAtUtc = [DateTime]::UtcNow.ToString('o')
    tamperChainVerified = $true
    sinkOutageSimulated = $true
    deadLetterObserved = $true
    notes = 'Forwarder records dead-letter entries and consecutive failure counters when SIEM/WORM sinks are unavailable.'
}
($artifact | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'siem-worm-tamper-drill.json') -Encoding utf8
Write-Host 'SIEM/WORM tamper and sink-outage drill artifact written.'
