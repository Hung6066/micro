[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ServiceName,

    [Parameter(Mandatory)]
    [string]$EnvironmentFile,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [string]$ServiceAccount
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

function Get-ServiceInventory {
    param([Parameter(Mandatory)][string]$ContractPath)

    $contract = Get-Content -LiteralPath $ContractPath -Raw | ConvertFrom-Json
    $inventory = [ordered]@{}
    foreach ($endpoint in $contract.serviceEndpoints) {
        $logicalName = [string]$endpoint.logicalName
        $healthPath = switch ($logicalName) {
            'api-gateway' { '/health' }
            default { '/health/ready' }
        }

        $inventory[$logicalName] = [ordered]@{
            ServiceUrlKey = [string]$endpoint.key
            Host          = [string]$endpoint.runtimes.vm.host
            Port          = [int]$endpoint.runtimes.vm.port
            HealthPath    = $healthPath
        }
    }

    return $inventory
}

function Test-SecretValueKey {
    param([Parameter(Mandatory)][string]$Key)

    return $Key.StartsWith('SECRET_') -and -not $Key.EndsWith('_REF')
}

function ConvertTo-SecretFileKey {
    param([Parameter(Mandatory)][string]$SecretKey)

    return '{0}_FILE' -f $SecretKey
}

function ConvertTo-SecretLeafName {
    param([Parameter(Mandatory)][string]$SecretKey)

    return $SecretKey.Substring('SECRET_'.Length).ToLowerInvariant().Replace('_', '-')
}

function Protect-RenderedFileAcl {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-IsWindowsHost)) {
        return
    }

    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $icaclsOutput = & icacls $Path /inheritance:r /grant:r "BUILTIN\Administrators:(F)" "NT AUTHORITY\SYSTEM:(F)" "${currentUser}:(M)" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "icacls failed to protect [$Path]: $($icaclsOutput -join ' ')"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$contractPath = Join-Path $repoRoot 'config\runtime-contract.v1.json'
$inventory = Get-ServiceInventory -ContractPath $contractPath

if (-not $inventory.Contains($ServiceName)) {
    throw "Unknown VM service name [$ServiceName]."
}

$environmentValues = Read-EnvironmentFile -Path $EnvironmentFile
$service = $inventory[$ServiceName]

if ([string]::IsNullOrWhiteSpace($ServiceAccount)) {
    $ServiceAccount = "his-hope-$ServiceName"
}

$secretDirectory = "/etc/his-hope/secrets/$ServiceName"
$windowsSecretDirectory = "C:\ProgramData\HisHope\$ServiceName\secrets"
$renderedValues = [ordered]@{}

foreach ($entry in $environmentValues.GetEnumerator()) {
    if (Test-SecretValueKey -Key $entry.Key) {
        continue
    }

    $renderedValues[$entry.Key] = [string]$entry.Value
}

$renderedValues['HIS_HOPE_SERVICE_NAME'] = $ServiceName
$renderedValues['HIS_HOPE_SERVICE_ACCOUNT'] = $ServiceAccount
$renderedValues['HIS_HOPE_VM_INTERNAL_HOST'] = [string]$service.Host
$renderedValues['HIS_HOPE_VM_INTERNAL_PORT'] = [string]$service.Port
$renderedValues['HIS_HOPE_VM_HEALTHCHECK_URL'] = "http://$($service.Host):$($service.Port)$($service.HealthPath)"
$renderedValues['HIS_HOPE_SECRET_DIRECTORY'] = $secretDirectory
$renderedValues['HIS_HOPE_WINDOWS_SECRET_DIRECTORY'] = $windowsSecretDirectory
$renderedValues['HIS_HOPE_LINUX_ENV_MODE'] = '0640'

foreach ($secretKey in @(
    'SECRET_POSTGRES_PASSWORD',
    'SECRET_RABBITMQ_PASSWORD',
    'SECRET_REDIS_PASSWORD',
    'SECRET_OIDC_CLIENT_SECRET'
)) {
    $renderedValues[(ConvertTo-SecretFileKey -SecretKey $secretKey)] = '{0}/{1}' -f $secretDirectory, (ConvertTo-SecretLeafName -SecretKey $secretKey)
}

$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$outputPath = Join-Path $OutputDirectory "$ServiceName.env"
$lines = foreach ($entry in $renderedValues.GetEnumerator()) {
    '{0}={1}' -f $entry.Key, $entry.Value
}

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Protect-RenderedFileAcl -Path $outputPath

$result = [pscustomobject]@{
    ServiceName            = $ServiceName
    OutputPath             = $outputPath
    LinuxMode              = '0640'
    ServiceAccount         = $ServiceAccount
    SecretDirectory        = $secretDirectory
    WindowsSecretDirectory = $windowsSecretDirectory
    HealthcheckUrl         = $renderedValues['HIS_HOPE_VM_HEALTHCHECK_URL']
}

Write-Output "VM_RUNTIME_ENV_RENDERED service=$ServiceName output=$outputPath linuxMode=0640"
$result
exit 0
