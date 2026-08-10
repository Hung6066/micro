[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EnvironmentFile,

    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-IsWindowsHost {
    return [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
}

function Read-EnvironmentFile {
    param([Parameter(Mandatory)][string]$Path)

    $values = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) {
            throw "Invalid environment line format in $Path."
        }

        $key = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim()
        $values[$key] = $value
    }

    return $values
}

function Get-ServiceNames {
    param([Parameter(Mandatory)][string]$ContractPath)

    $contract = Get-Content -LiteralPath $ContractPath -Raw | ConvertFrom-Json
    return @($contract.serviceEndpoints | ForEach-Object { [string]$_.logicalName })
}

function Add-Status {
    param(
        [System.Collections.Generic.List[object]]$Statuses,
        [string]$Status,
        [string]$Name,
        [string]$Message
    )

    $Statuses.Add([pscustomobject]@{
        Status  = $Status
        Name    = $Name
        Message = $Message
    })
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$contractValidator = Join-Path $repoRoot 'scripts\config\validate-runtime-contract.ps1'
$renderScript = Join-Path $repoRoot 'deploy\vm\render-runtime-env.ps1'
$windowsValidator = Join-Path $repoRoot 'deploy\vm\windows\Validate-HisHopeService.ps1'
$systemdTemplate = Join-Path $repoRoot 'deploy\vm\systemd\his-hope-service@.service'
$contractPath = Join-Path $repoRoot 'config\runtime-contract.v1.json'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('his-hope-vm-runtime-' + [Guid]::NewGuid().ToString('N'))
}

$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$environmentValues = Read-EnvironmentFile -Path $EnvironmentFile
$environmentName = [string]$environmentValues['HIS_HOPE_ENVIRONMENT']
$secretDirectoryRoot = Join-Path $OutputDirectory 'secrets'
$null = New-Item -ItemType Directory -Path $secretDirectoryRoot -Force

if (Test-IsWindowsHost) {
    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $icaclsOutput = & icacls $secretDirectoryRoot /inheritance:r /grant:r "BUILTIN\Administrators:(OI)(CI)(F)" "NT AUTHORITY\SYSTEM:(OI)(CI)(F)" "${currentUser}:(OI)(CI)(F)" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "icacls failed to protect [$secretDirectoryRoot]: $($icaclsOutput -join ' ')"
    }
}

$statuses = [System.Collections.Generic.List[object]]::new()
$contractOutput = & pwsh -NoProfile -File $contractValidator -EnvironmentFile $EnvironmentFile -Runtime vm -Strict 2>&1
$contractExitCode = $LASTEXITCODE
if ($contractExitCode -ne 0) {
    $contractOutput | ForEach-Object { Write-Output "$_" }
    Add-Status -Statuses $statuses -Status 'FAIL' -Name 'contract' -Message 'Runtime contract validation failed for vm.'
}
else {
    Add-Status -Statuses $statuses -Status 'PASS' -Name 'contract' -Message 'Runtime contract validation passed for vm.'
}

$serviceNames = Get-ServiceNames -ContractPath $contractPath
foreach ($serviceName in $serviceNames) {
    $renderOutput = & pwsh -NoProfile -File $renderScript -ServiceName $serviceName -EnvironmentFile $EnvironmentFile -OutputDirectory $OutputDirectory 2>&1
    $renderExitCode = $LASTEXITCODE
    if ($renderExitCode -ne 0) {
        Add-Status -Statuses $statuses -Status 'FAIL' -Name "render:$serviceName" -Message ($renderOutput -join ' ')
        continue
    }

    $renderedFile = Join-Path $OutputDirectory "$serviceName.env"
    $renderedValues = Read-EnvironmentFile -Path $renderedFile
    $hasSecretValueLine = @($renderedValues.Keys | Where-Object { $_ -like 'SECRET_*' -and $_ -notlike '*_REF' -and $_ -notlike '*_FILE' }).Count -gt 0
    if ($hasSecretValueLine) {
        Add-Status -Statuses $statuses -Status 'FAIL' -Name "render:$serviceName" -Message 'Rendered environment file contains secret value keys.'
    }
    else {
        Add-Status -Statuses $statuses -Status 'PASS' -Name "render:$serviceName" -Message 'Rendered environment file excludes secret value keys.'
    }

    $validationOutput = & pwsh -NoProfile -File $windowsValidator -ServiceName $serviceName -EnvironmentDirectory $OutputDirectory -SecretDirectory $secretDirectoryRoot -SkipServiceLookup 2>&1
    $validationExitCode = $LASTEXITCODE
    if ($validationExitCode -ne 0) {
        Add-Status -Statuses $statuses -Status 'FAIL' -Name "windows:$serviceName" -Message ($validationOutput -join ' ')
    }
    else {
        Add-Status -Statuses $statuses -Status 'PASS' -Name "windows:$serviceName" -Message 'Windows service dry-run validation passed.'
    }
}

$systemdContent = Get-Content -LiteralPath $systemdTemplate -Raw
foreach ($requiredFragment in @(
    'EnvironmentFile=/etc/his-hope/%i.env',
    'Restart=always',
    'NoNewPrivileges=yes',
    'ExecStartPost=/usr/bin/bash -lc ''curl --fail --silent --show-error "${HIS_HOPE_VM_HEALTHCHECK_URL}" > /dev/null'''
)) {
    if ($systemdContent.Contains($requiredFragment)) {
        Add-Status -Statuses $statuses -Status 'PASS' -Name 'systemdTemplate' -Message "Template contains [$requiredFragment]."
    }
    else {
        Add-Status -Statuses $statuses -Status 'FAIL' -Name 'systemdTemplate' -Message "Template missing [$requiredFragment]."
    }
}

if ($environmentName -eq 'production') {
    $localhostViolations = @(
        $environmentValues.GetEnumerator() |
            Where-Object { $_.Key -like '*URL' -or $_.Key -like '*ORIGIN' } |
            Where-Object { $_.Value -match 'localhost|127\.0\.0\.1' }
    )

    if ($localhostViolations.Count -gt 0) {
        Add-Status -Statuses $statuses -Status 'FAIL' -Name 'productionLocalhost' -Message 'Production VM inputs must not use localhost or 127.0.0.1.'
    }
    else {
        Add-Status -Statuses $statuses -Status 'PASS' -Name 'productionLocalhost' -Message 'Production VM inputs do not use localhost.'
    }
}

if (Test-IsWindowsHost) {
    Add-Status -Statuses $statuses -Status 'ENVIRONMENT_BLOCKED' -Name 'systemdLiveValidation' -Message 'Live systemd validation is unavailable on Windows; static template checks only.'
}
else {
    Add-Status -Statuses $statuses -Status 'SKIPPED' -Name 'systemdLiveValidation' -Message 'Live systemd validation was not executed in dry-run mode.'
}

foreach ($status in $statuses) {
    Write-Output "$($status.Status) $($status.Name) $($status.Message)"
}

if (@($statuses | Where-Object { $_.Status -eq 'FAIL' }).Count -gt 0) {
    exit 1
}

exit 0
