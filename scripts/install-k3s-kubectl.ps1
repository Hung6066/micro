[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Version = 'v1.35.5',
    [ValidateSet('amd64')]
    [string]$Architecture = 'amd64',
    [string]$DestinationDirectory = '.runtime/toolchain',
    [string]$ExpectedSha256 = '5d8b15772199f652286ca8a17ba683cb453dcbbba40dc948f71fec81d9e9ca30'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -ne 'v1.35.5' -or $Architecture -ne 'amd64') {
    throw 'This repository gate currently permits only the reviewed kubectl v1.35.5 Windows amd64 artifact.'
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$directory = [IO.Path]::GetFullPath((Join-Path $root $DestinationDirectory))
$target = Join-Path $directory 'kubectl.exe'
$uri = "https://dl.k8s.io/release/$Version/bin/windows/$Architecture/kubectl.exe"

New-Item -ItemType Directory -Path $directory -Force | Out-Null
if (-not $PSCmdlet.ShouldProcess($target, "Download and verify $Version")) { return }

$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(120)
try {
    $bytes = $client.GetByteArrayAsync($uri).GetAwaiter().GetResult()
} finally {
    $client.Dispose()
}
[IO.File]::WriteAllBytes($target, $bytes)

$actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $ExpectedSha256.ToLowerInvariant()) {
    Remove-Item -LiteralPath $target -Force
    throw "kubectl checksum mismatch: expected $ExpectedSha256, got $actual"
}

Write-Output "kubectl $Version installed and checksum verified: $actual"
