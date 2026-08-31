[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputPath = 'artifacts/evidence/full-test-matrix.json',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    $solution = Join-Path $root 'His.Hope.sln'
    $projects = @(dotnet sln $solution list | Select-String '\.csproj' |
        ForEach-Object { $_.Line.Trim() } | Where-Object { $_ -match '(?i)(Tests?|Test)\\' })
    if ($projects.Count -eq 0) { throw 'No test projects were discovered from the solution.' }

    $results = [System.Collections.Generic.List[object]]::new()
    $totalPassed = 0; $totalSkipped = 0; $totalTests = 0
    $fatalError = $null
    foreach ($relative in $projects) {
        $projectPath = Join-Path $root $relative
        Write-Host "[$($results.Count + 1)/$($projects.Count)] $relative"
        $args = @('test', $projectPath, '--configuration', $Configuration,
            '--no-restore', '-p:TestTfmsInParallel=false')
        if ($NoBuild) { $args += '--no-build' }
        $args += @('--', 'RunConfiguration.MaxCpuCount=1')
        # The Codex host exposes an RTK command shim which can start a second
        # test-results process for plain `dotnet test`. Use the explicit proxy
        # so each project owns exactly one vstest process and Testcontainers
        # never contend across runners.
        $output = @(& rtk proxy dotnet @args 2>&1 | ForEach-Object { $_.ToString() })
        $exitCode = $LASTEXITCODE
        $safeName = ($relative -replace '[^A-Za-z0-9]+', '_').Trim('_')
        $logPath = Join-Path $root "artifacts/evidence/full-test-matrix-$safeName.log"
        [IO.File]::WriteAllLines($logPath, $output, [Text.UTF8Encoding]::new($false))
        $summary = $output | Select-String '^(Passed!|Failed!)\s+- Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)'
        $failed = 0; $passed = 0; $skipped = 0; $total = 0
        if ($summary) {
            $m = [regex]::Match($summary[-1].Line, 'Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)')
            $failed = [int]$m.Groups[1].Value; $passed = [int]$m.Groups[2].Value
            $skipped = [int]$m.Groups[3].Value; $total = [int]$m.Groups[4].Value
        }
        $status = if ($exitCode -ne 0 -or $failed -gt 0) { 'fail' } elseif ($summary -and $skipped -gt 0) { 'pass-with-skips' } else { 'pass' }
        $results.Add([pscustomobject]@{ project = $relative; status = $status; exitCode = $exitCode; failed = $failed; passed = $passed; skipped = $skipped; total = $total; log = $logPath.Substring($root.Length + 1) })
        $totalPassed += $passed; $totalSkipped += $skipped; $totalTests += $total
        if ($status -eq 'fail') {
            $fatalError = "Test project failed: $relative (exit=$exitCode, failed=$failed)."
            break
        }
    }
    $result = [pscustomobject]@{ status = if ($fatalError) { 'fail' } else { 'pass' }; error = $fatalError; projectCount = $results.Count; expectedProjectCount = $projects.Count; passed = $totalPassed; skipped = $totalSkipped; total = $totalTests; projects = @($results); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
    $fullOutput = Join-Path $root $OutputPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
    [IO.File]::WriteAllText($fullOutput, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Output ($result | ConvertTo-Json -Depth 8)
    if ($fatalError) { exit 1 }
} finally { Pop-Location }
