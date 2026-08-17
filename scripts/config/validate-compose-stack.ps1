[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ComposeFile,

    [Parameter(Mandatory)]
    [string]$EnvironmentFile,

    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-EnvironmentFile {
    param([string]$Path)

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

        $values[$trimmed.Substring(0, $separatorIndex).Trim()] = $trimmed.Substring($separatorIndex + 1).Trim()
    }

    return $values
}

function Invoke-RtkCommand {
    param([string[]]$Arguments)

    $output = & rtk @Arguments 2>&1
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output | ForEach-Object { "$_" }) -join [Environment]::NewLine
    }
}

function ConvertFrom-ReferenceValidationJson {
    param([string]$Output)

    $lines = $Output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($lines.Count -lt 1) {
        throw 'Reference validator did not emit any output.'
    }

    return $lines[-1] | ConvertFrom-Json
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composeFilePath = (Resolve-Path -LiteralPath $ComposeFile).Path
$environmentFilePath = (Resolve-Path -LiteralPath $EnvironmentFile).Path
$environmentValues = Read-EnvironmentFile -Path $environmentFilePath
$environmentName = [string]$environmentValues['HIS_HOPE_ENVIRONMENT']

if ($environmentName -notin @('development', 'staging', 'production')) {
    throw "[$environmentFilePath] must include HIS_HOPE_ENVIRONMENT=development|staging|production."
}

$renderScript = Join-Path $repoRoot 'docker\config\compose.runtime.env.ps1'
$contractValidator = Join-Path $repoRoot 'scripts\config\validate-runtime-contract.ps1'
$referenceValidator = Join-Path $repoRoot 'scripts\config\validate-runtime-references.ps1'
$temporaryEnvironmentFile = Join-Path ([System.IO.Path]::GetTempPath()) ("compose-runtime-{0}-{1}.env" -f $environmentName, [guid]::NewGuid().ToString('N'))

try {
    $renderOutput = & pwsh -NoProfile -File $renderScript -Environment $environmentName -OutputFile $temporaryEnvironmentFile 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw (($renderOutput | ForEach-Object { "$_" }) -join [Environment]::NewLine)
    }

    $contractArguments = @('proxy', 'powershell', '-NoProfile', '-File', $contractValidator, '-EnvironmentFile', $environmentFilePath, '-Runtime', 'docker')
    if ($Strict) {
        $contractArguments += '-Strict'
    }

    $contractResult = Invoke-RtkCommand -Arguments $contractArguments
    if ($contractResult.ExitCode -ne 0) {
        Write-Output $contractResult.Output
        exit $contractResult.ExitCode
    }

    $referenceResult = Invoke-RtkCommand -Arguments @(
        'proxy', 'powershell', '-NoProfile', '-File', $referenceValidator,
        '-EnvironmentFile', $environmentFilePath,
        '-Runtime', 'docker',
        '-ComposeFile', $composeFilePath
    )

    $referencePayload = $null
    try {
        $referencePayload = ConvertFrom-ReferenceValidationJson -Output $referenceResult.Output
    }
    catch {
        Write-Output $referenceResult.Output
        exit 1
    }

    if (@($referencePayload.missing).Count -gt 0 -or @($referencePayload.mismatched).Count -gt 0) {
        Write-Output $referenceResult.Output
        exit 1
    }

    $dockerVersionResult = Invoke-RtkCommand -Arguments @('docker', 'compose', 'version')
    if ($dockerVersionResult.ExitCode -ne 0) {
        Write-Output 'ENVIRONMENT_BLOCKED docker compose is unavailable.'
        Write-Output $dockerVersionResult.Output
        exit $dockerVersionResult.ExitCode
    }

    $composeResult = Invoke-RtkCommand -Arguments @('docker', 'compose', '-f', $composeFilePath, '--env-file', $temporaryEnvironmentFile, 'config', '--quiet')
    if ($composeResult.ExitCode -ne 0) {
        Write-Output $composeResult.Output
        exit $composeResult.ExitCode
    }

    Write-Output $contractResult.Output
    Write-Output $referenceResult.Output
    Write-Output "COMPOSE_CONFIG_VALID composeFile=$composeFilePath environment=$environmentName"
    exit 0
}
finally {
    if (Test-Path -LiteralPath $temporaryEnvironmentFile) {
        Remove-Item -LiteralPath $temporaryEnvironmentFile -Force
    }
}
