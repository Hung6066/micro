[CmdletBinding()]
param(
    [string]$Inventory = 'ansible/enterprise-k3s/inventory/production.yml',
    [switch]$ValidationOnly,
    [string]$FromPhase,
    [string]$ToPhase,
    [string]$AzureEnvFile = 'D:\secure\his-hope\azure-production.env',
    [string]$OutputRoot = 'artifacts/k3s-production'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Convert-ToWslPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if ($full -notmatch '^([A-Za-z]):\\(.*)$') { throw "Path must be on a Windows drive: $Path" }
    return "/mnt/$($matches[1].ToLower())/$($matches[2] -replace '\\','/')"
}

function Assert-Path([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Description not found: $Path" }
}

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$inventoryPath = [IO.Path]::GetFullPath((Join-Path $repo $Inventory))
$vaultPath = Join-Path $repo 'ansible/enterprise-k3s/group_vars/vault.yml'
$playbookPath = Join-Path $repo 'ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml'
$run = Join-Path ([IO.Path]::GetFullPath((Join-Path $repo $OutputRoot))) ("run-" + (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss'))
$summaryPath = Join-Path $run 'summary.json'
$logPath = Join-Path $run 'ansible.log'

$phaseOrder = @('preflight','load-balancer','control-plane','verify','workers','backup')
$phaseTags = @{
    'preflight' = 'phase-preflight'; 'load-balancer' = 'phase-load-balancer'
    'control-plane' = 'phase-control-plane'; 'verify' = 'phase-verify'
    'workers' = 'phase-workers'; 'backup' = 'phase-backup'
}
if (($FromPhase -and -not $phaseOrder.Contains($FromPhase)) -or ($ToPhase -and -not $phaseOrder.Contains($ToPhase))) {
    throw "Unknown phase. Valid phases: $($phaseOrder -join ', ')"
}
$fromIndex = if ($FromPhase) { [array]::IndexOf($phaseOrder, $FromPhase) } else { 0 }
$toIndex = if ($ToPhase) { [array]::IndexOf($phaseOrder, $ToPhase) } else { $phaseOrder.Count - 1 }
if ($fromIndex -gt $toIndex) { throw '-FromPhase must not come after -ToPhase.' }
$requestedPhases = @($phaseOrder[$fromIndex..$toIndex])
$requestedTags = @($requestedPhases | ForEach-Object { $phaseTags[$_] })
Assert-Path $inventoryPath 'Production inventory'
Assert-Path $vaultPath 'Encrypted Ansible Vault file'
Assert-Path $playbookPath 'Production orchestrator playbook'
if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) { throw 'WSL is required.' }

New-Item -ItemType Directory -Force -Path $run | Out-Null
$started = [DateTime]::UtcNow
$status = 'BLOCKED'
$exitCode = $null
$vaultPasswordFile = $null
$oldBecomePass = $env:ANSIBLE_BECOME_PASS

try {
    $wslProbe = & wsl.exe -e bash -lc 'command -v ansible-playbook >/dev/null && command -v ansible >/dev/null' 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'WSL Ansible is not installed.' }
    if (-not (Test-Path -LiteralPath $AzureEnvFile -PathType Leaf)) { throw "Azure environment file not found: $AzureEnvFile" }

    if ($ValidationOnly) {
        $status = 'PASS'
        [pscustomobject]@{
            phase = 'prerequisites'; requestedPhases = $requestedPhases; status = $status; startedAt = $started.ToString('o')
            completedAt = [DateTime]::UtcNow.ToString('o'); logPath = $null; evidencePath = $null
            validationOnly = $true; inventory = $inventoryPath
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
        Write-Output "Validation PASS. Report: $summaryPath"
        exit 0
    }

    $vaultSecure = Read-Host 'Ansible Vault password' -AsSecureString
    $becomeSecure = Read-Host 'SSH sudo/become password (leave empty only if passwordless)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($vaultSecure)
    try { $vaultPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    $vaultPasswordFile = Join-Path ([IO.Path]::GetTempPath()) ("his-hope-vault-" + [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($vaultPasswordFile, $vaultPlain, [Text.UTF8Encoding]::new($false))
    $vaultPlain = $null
    & icacls.exe $vaultPasswordFile /inheritance:r /grant:r "$env:USERNAME`:F" | Out-Null

    $becomeBstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($becomeSecure)
    try { $env:ANSIBLE_BECOME_PASS = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($becomeBstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($becomeBstr) }

    $invWsl = Convert-ToWslPath $inventoryPath
    $playWsl = Convert-ToWslPath $playbookPath
    $vaultWsl = Convert-ToWslPath $vaultPasswordFile
    $repoWsl = Convert-ToWslPath $repo
    $mode = if ($ValidationOnly) { '--check' } else { '' }
    $tags = if ($requestedPhases.Count -lt $phaseOrder.Count) { "--tags '$($requestedTags -join ',')'" } else { '' }
    $cmd = "cd '$repoWsl' && ANSIBLE_NOCOLOR=1 ansible-playbook -i '$invWsl' '$playWsl' --vault-password-file '$vaultWsl' $mode $tags"
    & wsl.exe -e bash -lc $cmd 2>&1 | Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
}
catch {
    $_ | Out-String | Set-Content -LiteralPath $logPath -Encoding utf8
    $status = if ($status -eq 'BLOCKED') { 'BLOCKED' } else { 'FAIL' }
    $exitCode = 1
}
finally {
    if ($null -ne $oldBecomePass) { $env:ANSIBLE_BECOME_PASS = $oldBecomePass } else { Remove-Item Env:ANSIBLE_BECOME_PASS -ErrorAction SilentlyContinue }
    if ($vaultPasswordFile) { Remove-Item -LiteralPath $vaultPasswordFile -Force -ErrorAction SilentlyContinue }
    $completed = [DateTime]::UtcNow
    [pscustomobject]@{
        phase = 'production-orchestrator'; requestedPhases = $requestedPhases; status = $status; startedAt = $started.ToString('o')
        completedAt = $completed.ToString('o'); logPath = $logPath; evidencePath = $summaryPath
        validationOnly = [bool]$ValidationOnly; exitCode = $exitCode; inventory = $inventoryPath
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
}

if ($status -eq 'PASS') { exit 0 }
if ($status -eq 'BLOCKED') { exit 70 }
exit 1
