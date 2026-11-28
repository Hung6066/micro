$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scripts = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'scripts') -Recurse -Filter '*.ps1' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' }

$violations = foreach ($script in $scripts) {
    $source = Get-Content -LiteralPath $script.FullName -Raw
    if ($source -match '\?\?') {
        $script.FullName.Substring($repositoryRoot.Length + 1)
    }
}

if ($violations.Count -gt 0) {
    throw "PowerShell 5.1 compatibility violation (null-coalescing operator) in: $($violations -join ', ')"
}

Write-Output "PowerShell compatibility contract: PASS ($($scripts.Count) scripts checked)"
