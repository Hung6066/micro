[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ServiceName,

    [Parameter(Mandatory)]
    [string]$EnvironmentDirectory,

    [Parameter(Mandatory)]
    [string]$SecretDirectory,

    [switch]$SkipServiceLookup
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

function Get-AclValidation {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Kind
    )

    if (-not (Test-IsWindowsHost)) {
        return [pscustomobject]@{
            Status  = 'ENVIRONMENT_BLOCKED'
            Message = "$Kind ACL validation is only available on Windows."
        }
    }

    $icaclsOutput = (& icacls $Path 2>&1) -join [Environment]::NewLine
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{
            Status  = 'FAIL'
            Message = "$Kind ACL inspection failed."
        }
    }

    $hasForbiddenIdentity = $icaclsOutput -match 'Everyone:' -or
        $icaclsOutput -match 'BUILTIN\\Users:' -or
        $icaclsOutput -match 'Authenticated Users:'

    if (-not $hasForbiddenIdentity) {
        return [pscustomobject]@{
            Status  = 'PASS'
            Message = "$Kind ACL is restricted."
        }
    }

    return [pscustomobject]@{
        Status  = 'FAIL'
        Message = "$Kind ACL must disable inheritance and remove broad principals."
    }
}

$environmentFile = Join-Path $EnvironmentDirectory "$ServiceName.env"
if (-not (Test-Path -LiteralPath $environmentFile)) {
    Write-Output "FAIL environmentFile missing path=$environmentFile"
    exit 1
}

if (-not (Test-Path -LiteralPath $SecretDirectory)) {
    Write-Output "FAIL secretDirectory missing path=$SecretDirectory"
    exit 1
}

$values = Read-EnvironmentFile -Path $environmentFile
$errors = [System.Collections.Generic.List[string]]::new()

if (($values['HIS_HOPE_SERVICE_NAME'] | ForEach-Object { [string]$_ }) -ne $ServiceName) {
    $errors.Add('HIS_HOPE_SERVICE_NAME must match the Windows Service name.')
}

foreach ($requiredFileKey in @(
    'SECRET_POSTGRES_PASSWORD_FILE',
    'SECRET_RABBITMQ_PASSWORD_FILE',
    'SECRET_REDIS_PASSWORD_FILE',
    'SECRET_OIDC_CLIENT_SECRET_FILE'
)) {
    if (-not $values.Contains($requiredFileKey)) {
        $errors.Add("Missing required key [$requiredFileKey].")
    }
}

$fileAclResult = Get-AclValidation -Path $environmentFile -Kind 'environment file'
$secretAclResult = Get-AclValidation -Path $SecretDirectory -Kind 'secret directory'

if (-not $SkipServiceLookup) {
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        $errors.Add("Service [$ServiceName] is not installed.")
    }
    elseif ([string]$service.PathName -match 'password|SECRET_[A-Z0-9_]+') {
        $errors.Add("Service [$ServiceName] command line must not include secrets.")
    }
}

if ($fileAclResult.Status -eq 'FAIL') {
    $errors.Add($fileAclResult.Message)
}

if ($secretAclResult.Status -eq 'FAIL') {
    $errors.Add($secretAclResult.Message)
}

Write-Output "$($fileAclResult.Status) fileAcl $($fileAclResult.Message)"
Write-Output "$($secretAclResult.Status) secretAcl $($secretAclResult.Message)"

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Output "FAIL $_" }
    exit 1
}

Write-Output "PASS serviceName Windows service metadata matches rendered environment."
exit 0
