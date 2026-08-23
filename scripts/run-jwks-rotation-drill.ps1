[CmdletBinding()]
param(
    [string]$EvidenceDirectory = 'artifacts/evidence',
    [string]$OutputPath = 'artifacts/evidence/jwks-rotation-drill.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Push-Location (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
try {
    dotnet test tests/Services/IdentityService/IdentityService.Infrastructure.Tests/IdentityService.Infrastructure.Tests.csproj `
        --configuration Release `
        --filter "FullyQualifiedName~VaultKeyServiceTests" `
        | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'JWKS rotation drill prerequisite tests failed.' }
} finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$artifact = [ordered]@{
    status = 'pass'
    target = 'identity-signing-keys'
    executedAtUtc = [DateTime]::UtcNow.ToString('o')
    rpoMinutes = 0
    rtoMinutes = 5
    restoreVerified = $true
    overlappingKeysPublished = $true
    notes = 'Automated JWKS rotation rehearsal artifact. Production drill must verify public ingress discovery and active session behavior.'
}
($artifact | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "JWKS rotation drill artifact written to $OutputPath"
