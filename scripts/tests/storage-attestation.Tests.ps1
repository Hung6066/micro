$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot '..\validate-production-storage-attestation.ps1'
$tempPath = Join-Path ([IO.Path]::GetTempPath()) "his-hope-storage-attestation-$([guid]::NewGuid()).json"

try {
    $incomplete = @{ metadata = @{ changeTicket = 'CHG-TEST'; evidenceBundleUri = 'protected://evidence'; storageOwner = 'owner'; securityApprover = 'approver' } } |
        ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($tempPath, $incomplete, [Text.UTF8Encoding]::new($false))
    $blockedOutput = & $validator -AttestationPath $tempPath 2>&1
    if ($LASTEXITCODE -ne 70 -or ($blockedOutput -join "`n") -notmatch '"status"\s*:\s*"blocked"') {
        throw 'Incomplete storage attestation must be blocked.'
    }

    $complete = @{
        metadata = @{ changeTicket = 'CHG-TEST'; evidenceBundleUri = 'protected://evidence'; storageOwner = 'owner'; securityApprover = 'approver' }
        azure = @{
            httpsOnly = $true; minimumTlsVersion = 'TLS1_2'; privateAccess = $true
            allowBlobPublicAccessDisabled = $true; keySource = 'Microsoft.Keyvault'
            infrastructureEncryption = $true; immutableStorageWithVersioningEnabled = $true
            immutabilityPolicyMode = 'Locked'; retentionDays = 30
        }
        csi = @{ externalCsi = $true; encryptionAtRest = $true; kmsBinding = $true; failureDomain = $true; snapshotRestore = $true }
        backup = @{ objectStoreReady = $true; restoreVerified = $true; checksumVerified = $true; rpoRtoMeasured = $true }
    } | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($tempPath, $complete, [Text.UTF8Encoding]::new($false))
    $passOutput = & $validator -AttestationPath $tempPath 2>&1
    if ($LASTEXITCODE -ne 0 -or ($passOutput -join "`n") -notmatch '"status"\s*:\s*"pass"') {
        throw 'Complete storage attestation must pass.'
    }
} finally {
    Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
}

Write-Output 'Production storage attestation validator: PASS (incomplete blocked, complete accepted)'
