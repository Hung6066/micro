[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ServiceName,
    [Parameter(Mandatory)] [string]$ExecutablePath,
    [string]$EnvironmentFile = "C:\ProgramData\HisHope\$ServiceName.env"
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ExecutablePath)) { throw "Executable not found: $ExecutablePath" }
if (-not (Test-Path -LiteralPath $EnvironmentFile)) { throw "Runtime environment not found: $EnvironmentFile" }
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) { Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue; sc.exe delete $ServiceName | Out-Null }
New-Service -Name $ServiceName -BinaryPathName "`"$ExecutablePath`"" -DisplayName "His.Hope $ServiceName" -StartupType Automatic | Out-Null
Write-Output "WINDOWS_SERVICE_REGISTERED $ServiceName"
