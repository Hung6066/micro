[CmdletBinding()]
param(
    [string]$OverlayFile = 'D:\AI\micro\k8s\overlays\prod\image-digests\kustomization.yaml',
    [string]$Registry = 'harbor.his-hope.local:9443/his-hope',
    [string]$AdminPasswordFile = 'D:\secure\his-hope\harbor_admin_password',
    [string]$CosignPath,
    [string]$CosignKey = 'D:\secure\his-hope\cosign.key',
    [string]$CosignPasswordFile = 'D:\secure\his-hope\cosign_password',
    [string]$RegistryCa = 'D:\secure\his-hope\his_hope_ca.pem',
    [int]$StartIndex = 0,
    [int]$Count = 0,
    [switch]$NoOverlayUpdate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $OverlayFile)) { throw "Overlay file not found: $OverlayFile" }
foreach ($path in @($AdminPasswordFile, $CosignKey, $CosignPasswordFile, $RegistryCa)) {
    if (-not (Test-Path $path)) { throw "Required runtime file not found: $path" }
}
if ([string]::IsNullOrWhiteSpace($CosignPath)) {
    $CosignPath = (Get-Command cosign -ErrorAction SilentlyContinue).Source
}
if ([string]::IsNullOrWhiteSpace($CosignPath) -or -not (Test-Path $CosignPath)) {
    throw 'Cosign binary is required for the Harbor migration.'
}

$content = Get-Content -Raw $OverlayFile
$pattern = '(?ms)(?<block>^  - name: (?<name>[^\r\n]+)\r?\n    newTag: (?<tag>[^\r\n]+)\r?\n    digest: (?<digest>sha256:[0-9a-f]{64})\r?\n)'
$matches = [regex]::Matches($content, $pattern)
if (@($matches).Length -eq 0) { throw "No image records found in $OverlayFile" }
$selectedMatches = if ($Count -gt 0) {
    @($matches | Select-Object -Skip $StartIndex -First $Count)
} else {
    @($matches | Select-Object -Skip $StartIndex)
}
if (@($selectedMatches).Length -eq 0) { throw "No image records selected at index $StartIndex" }

$adminPassword = (Get-Content -Raw $AdminPasswordFile).Trim()
$adminPassword | docker login ($Registry.Split('/')[0]) --username admin --password-stdin | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Harbor admin login failed.' }
$env:COSIGN_PASSWORD = (Get-Content -Raw $CosignPasswordFile).Trim()
$records = @()
function Invoke-Cosign([string[]]$Arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $CosignPath @Arguments 2>&1 | Out-Null
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previous
    return $exitCode
}

function Get-DestinationDigest([string]$Destination) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $inspect = @(docker image inspect $Destination --format '{{json .RepoDigests}}' 2>$null)
    $inspectExit = $LASTEXITCODE
    $ErrorActionPreference = $previous
    if ($inspectExit -eq 0 -and @($inspect).Length -gt 0) {
        try {
            $repoDigests = $inspect[0] | ConvertFrom-Json
            $match = $repoDigests | Where-Object { $_ -like "$Registry/*@sha256:*" } | Select-Object -First 1
            if ($null -ne $match) {
                return ($match -split '@', 2)[1]
            }
        } catch {
            # Fall through to the registry API-free push output path.
        }
    }
    return $null
}

foreach ($match in $selectedMatches) {
    $name = $match.Groups['name'].Value.Trim()
    $tag = $match.Groups['tag'].Value.Trim().Trim('"')
    $digest = $match.Groups['digest'].Value.Trim()
    $source = "$name`:$tag"
    $sourceDigest = (docker image inspect $source --format '{{index .RepoDigests 0}}').Trim()
    if ($sourceDigest -ne "$name@$digest") {
        throw "Local source digest mismatch or missing: $source expected $digest"
    }
    $destination = "$Registry/$name`:$tag"
    $destinationDigest = Get-DestinationDigest $destination
    $reference = if ($destinationDigest) { "$Registry/$name@$destinationDigest" } else { "$Registry/$name@$digest" }
    $verifyExit = if ($destinationDigest) {
        Invoke-Cosign @('verify', '--registry-cacert', $RegistryCa, '--key', ($CosignKey -replace '\.key$', '.pub'), $reference)
    } else {
        1
    }
    if ($verifyExit -ne 0) {
        docker tag $source $destination
        if ($LASTEXITCODE -ne 0) { throw "Unable to tag $source" }
        $pushOutput = @(docker push $destination 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Unable to push $destination" }
        $pushMatch = $pushOutput | Select-String -Pattern 'digest:\s+(sha256:[0-9a-f]{64})' | Select-Object -Last 1
        $pushDigest = if ($null -ne $pushMatch) { $pushMatch.Matches.Groups[1].Value } else { $null }
        $destinationDigest = if ($pushDigest) { $pushDigest } else { Get-DestinationDigest $destination }
        if ([string]::IsNullOrWhiteSpace($destinationDigest)) {
            throw "Unable to resolve Harbor manifest digest after pushing $destination"
        }
        $reference = "$Registry/$name@$destinationDigest"
        $signExit = Invoke-Cosign @('sign', '--yes', '--registry-cacert', $RegistryCa, '--key', $CosignKey, $reference)
        if ($signExit -ne 0) { throw "Cosign signing failed: $reference" }
        $verifyExit = Invoke-Cosign @('verify', '--registry-cacert', $RegistryCa, '--key', ($CosignKey -replace '\.key$', '.pub'), $reference)
        if ($verifyExit -ne 0) { throw "Cosign verification failed: $reference" }
        Write-Output "SIGNED $name@$destinationDigest"
    } else {
        Write-Output "ALREADY-SIGNED $name@$destinationDigest"
    }
    $records += [pscustomobject]@{ Name = $name; Tag = $tag; Digest = $destinationDigest; Destination = "$Registry/$name" }
}

$updated = $content
foreach ($record in $records) {
    $escapedName = [regex]::Escape($record.Name)
    $escapedTag = [regex]::Escape($record.Tag)
    $replacement = "  - name: $($record.Name)`r`n    newName: $($record.Destination)`r`n    newTag: " + '"' + $record.Tag + '"' + "`r`n    digest: $($record.Digest)`r`n"
    $replacementPattern = '(?ms)^  - name: ' + $escapedName + '\r?\n(?:    newName: [^\r\n]+\r?\n)?    newTag: "?' + $escapedTag + '"?\r?\n    digest: sha256:[0-9a-f]{64}\r?\n'
    $updated = [regex]::Replace($updated, $replacementPattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $replacement }, 1)
}
if (-not $NoOverlayUpdate) {
    Set-Content -Path $OverlayFile -Value $updated -Encoding utf8
    Write-Output "Harbor migration PASS: $($records.Count) selected production image references pushed, signed, verified, and rewritten."
} else {
    Write-Output "Harbor batch PASS: $($records.Count) selected production image references are pushed and signed; overlay update deferred."
}
