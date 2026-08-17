[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Namespace,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$rendered = @(kubectl kustomize $Path --load-restrictor LoadRestrictionsNone 2>&1)
if ($LASTEXITCODE -ne 0) { throw "GitOps plane render failed: $Path`n$($rendered -join "`n")" }
$text = $rendered -join "`n"
if ([string]::IsNullOrWhiteSpace($text)) { throw "GitOps plane rendered empty: $Path" }
$images = @([regex]::Matches($text, '(?m)^\s*image:\s*(?<image>[^\s]+)') | ForEach-Object { $_.Groups['image'].Value })
$unpinned = @($images | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
if ($unpinned.Count -gt 0) { throw "Unpinned images in ${Path}: $($unpinned -join ', ')" }
$namespaces = @([regex]::Matches($text, '(?m)^\s*namespace:\s*(?<namespace>[^\s]+)') | ForEach-Object { $_.Groups['namespace'].Value })
if ($namespaces -notcontains $Namespace) { throw "Expected namespace [$Namespace] is absent from ${Path}." }
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($OutputPath), $text, [System.Text.UTF8Encoding]::new($false))
}
Write-Output "GitOps plane PASS: path=$Path namespace=$Namespace images=$($images.Count)"
