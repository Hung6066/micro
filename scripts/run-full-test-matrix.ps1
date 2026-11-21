[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputPath = 'artifacts/evidence/full-test-matrix.json',
    [string]$ArtifactsPath = 'artifacts/evidence/full-test-matrix-artifacts',
    [ValidateRange(1, 60)]
    # Integration projects own Testcontainers and may take several minutes
    # to execute serially on Docker Desktop. Keep the ceiling finite, but do
    # not terminate the Identity suite before it can emit its test summary.
    [int]$ProjectTimeoutMinutes = 30,
    [switch]$NoBuild,
    [switch]$Resume,
    [switch]$RequireComplete
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matrixArtifactsPath = if ([IO.Path]::IsPathRooted($ArtifactsPath)) { $ArtifactsPath } else { Join-Path $root $ArtifactsPath }
$previousRepositoryRoot = $env:HIS_HOPE_REPOSITORY_ROOT
$env:HIS_HOPE_REPOSITORY_ROOT = $root
$lockPath = Join-Path $root 'artifacts/evidence/.full-test-matrix.lock'
$lockStream = $null

function Stop-ProcessTree([int]$ProcessId) {
    if ($IsWindows) {
        $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue)
        foreach ($child in $children) { Stop-ProcessTree ([int]$child.ProcessId) }
    }
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

function Invoke-IsolatedTest([string[]]$Arguments, [string]$StdoutPath, [string]$StderrPath) {
    Remove-Item -LiteralPath $StdoutPath, $StderrPath -Force -ErrorAction SilentlyContinue
    $startParameters = @{
        FilePath = 'dotnet'
        ArgumentList = $Arguments
        WorkingDirectory = $root
        RedirectStandardOutput = $StdoutPath
        RedirectStandardError = $StderrPath
        PassThru = $true
    }
    if ($IsWindows) { $startParameters.WindowStyle = 'Hidden' }
    $process = Start-Process @startParameters
    $completed = $process.WaitForExit([int][TimeSpan]::FromMinutes($ProjectTimeoutMinutes).TotalMilliseconds)
    if (-not $completed) {
        Stop-ProcessTree ([int]$process.Id)
        return [pscustomobject]@{ exitCode = 124; timedOut = $true; output = @(
                "Test project exceeded the $ProjectTimeoutMinutes minute timeout."
                if (Test-Path -LiteralPath $StdoutPath) { Get-Content -LiteralPath $StdoutPath }
                if (Test-Path -LiteralPath $StderrPath) { Get-Content -LiteralPath $StderrPath }
            ) }
    }
    $process.WaitForExit()
    return [pscustomobject]@{ exitCode = $process.ExitCode; timedOut = $false; output = @(
            if (Test-Path -LiteralPath $StdoutPath) { Get-Content -LiteralPath $StdoutPath }
            if (Test-Path -LiteralPath $StderrPath) { Get-Content -LiteralPath $StderrPath }
        ) }
}

try {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $lockPath) | Out-Null
    $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
} catch {
    throw "Full test matrix is already running in another process: $lockPath"
}
Push-Location $root
try {
    $solution = Join-Path $root 'His.Hope.sln'
    $projects = @(dotnet sln $solution list | Select-String '\.csproj' |
        ForEach-Object { $_.Line.Trim() } | Where-Object { $_ -match '(?i)(Tests?|Test)[\\/]' })
    if ($projects.Count -eq 0) { throw 'No test projects were discovered from the solution.' }

    $results = [System.Collections.Generic.List[object]]::new()
    $totalPassed = 0; $totalFailed = 0; $totalSkipped = 0; $totalTests = 0
    $fatalError = $null
    $fullOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $root $OutputPath }

    if ($Resume -and (Test-Path -LiteralPath $fullOutput)) {
        try {
            $checkpoint = Get-Content -LiteralPath $fullOutput -Raw | ConvertFrom-Json
            if ($checkpoint.schemaVersion -eq 'full-test-matrix.v1' -and $checkpoint.projects) {
                # Only a project with zero skips is complete. A pass-with-skips
                # result must be retried when the missing external dependency
                # becomes available.
                foreach ($completed in @($checkpoint.projects | Where-Object { $_.status -eq 'pass' })) {
                    $results.Add($completed)
                    $totalPassed += [int]$completed.passed
                    $totalFailed += [int]$completed.failed
                    $totalSkipped += [int]$completed.skipped
                    $totalTests += [int]$completed.total
                }
            }
        } catch {
            $results.Clear()
            $totalPassed = 0; $totalFailed = 0; $totalSkipped = 0; $totalTests = 0
        }
    }
    $completedProjects = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($completed in $results) { [void]$completedProjects.Add([string]$completed.project) }
    $projectsToRun = @($projects | Where-Object { -not $completedProjects.Contains($_) })

    function Write-MatrixCheckpoint {
        $blocked = @($results | Where-Object status -eq 'environment-blocked').Count -gt 0
        $status = if ($results.Count -lt $projects.Count) {
            if ($blocked) { 'environment-blocked' } else { 'in-progress' }
        } elseif ($blocked) { 'environment-blocked' } elseif ($fatalError) { 'fail' } elseif ($totalSkipped -gt 0) { 'pass-with-skips' } else { 'pass' }
        $snapshot = [pscustomobject]@{
            schemaVersion = 'full-test-matrix.v1'
            status = $status
            error = $fatalError
            requireComplete = [bool]$RequireComplete
            projectCount = $results.Count
            expectedProjectCount = $projects.Count
            passed = $totalPassed
            failed = $totalFailed
            skipped = $totalSkipped
            total = $totalTests
            totals = [pscustomobject]@{ passed = $totalPassed; failed = $totalFailed; skipped = $totalSkipped; total = $totalTests }
            projects = @($results)
            generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
        [IO.File]::WriteAllText($fullOutput, ($snapshot | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    }

    foreach ($relative in $projectsToRun) {
        $projectPath = Join-Path $root $relative
        Write-Host "[$($results.Count + 1)/$($projects.Count)] $relative"
        $args = @('test', $projectPath, '--configuration', $Configuration,
            '-p:UseArtifactsOutput=true', "-p:ArtifactsPath=$matrixArtifactsPath", '-p:TestTfmsInParallel=false',
            '-m:1', '-nodeReuse:false')
        if ($NoBuild) { $args += @('--no-build', '--no-restore') }
        $args += @('--', 'RunConfiguration.MaxCpuCount=1')
        # Run each project in a supervised child process. A crashed or hung
        # testhost must produce matrix evidence instead of taking down the
        # runner or leaving an orphaned process holding a project DLL.
        $safeName = ($relative -replace '[^A-Za-z0-9]+', '_').Trim('_')
        $logPath = Join-Path $root "artifacts/evidence/full-test-matrix-$safeName.log"
        $stderrPath = "$logPath.stderr"
        $isolated = Invoke-IsolatedTest $args "$logPath.stdout" $stderrPath
        $output = @($isolated.output | ForEach-Object { $_.ToString() })
        [IO.File]::WriteAllLines($logPath, $output, [Text.UTF8Encoding]::new($false))
        Remove-Item -LiteralPath "$logPath.stdout", $stderrPath -Force -ErrorAction SilentlyContinue
        $launchFailed = $false
        $exitCode = [int]$isolated.exitCode
        if ($isolated.timedOut) { $launchFailed = $true }
        $summary = $output | Select-String '^(Passed!|Failed!)\s+- Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)'
        $failed = 0; $passed = 0; $skipped = 0; $total = 0
        if ($summary) {
            $m = [regex]::Match($summary[-1].Line, 'Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)')
            $failed = [int]$m.Groups[1].Value; $passed = [int]$m.Groups[2].Value
            $skipped = [int]$m.Groups[3].Value; $total = [int]$m.Groups[4].Value
        }
        $environmentBlocked = $launchFailed -or ($exitCode -ne 0 -and $failed -eq 0 -and (
            -not $summary -or
            ($output -match '(?i)test host process crashed|test run aborted|being used by another process|file is locked|MSB302[17]')
        ))
        $status = if ($environmentBlocked) { 'environment-blocked' } elseif ($exitCode -ne 0 -or $failed -gt 0) { 'fail' } elseif ($summary -and $skipped -gt 0) { 'pass-with-skips' } else { 'pass' }
        $results.Add([pscustomobject]@{ project = $relative; status = $status; exitCode = $exitCode; failed = $failed; passed = $passed; skipped = $skipped; total = $total; log = $logPath.Substring($root.Length + 1) })
        $totalPassed += $passed; $totalFailed += $failed; $totalSkipped += $skipped; $totalTests += $total
        Write-MatrixCheckpoint
        if ($status -in @('fail', 'environment-blocked') -and $null -eq $fatalError) {
            $fatalError = if ($status -eq 'environment-blocked') {
                "Test project environment-blocked: $relative (exit=$exitCode, failed=$failed); inspect $($logPath.Substring($root.Length + 1))."
            } else {
                "Test project failed: $relative (exit=$exitCode, failed=$failed)."
            }
        }
    }
    if (-not $fatalError -and $RequireComplete -and $totalSkipped -gt 0) {
        $fatalError = "Full test matrix contains $totalSkipped skipped tests while -RequireComplete is enabled."
    }
    $blocked = @($results | Where-Object status -eq 'environment-blocked').Count -gt 0
    $overallStatus = if ($blocked) { 'environment-blocked' } elseif ($fatalError) { 'fail' } elseif ($totalSkipped -gt 0) { 'pass-with-skips' } else { 'pass' }
    $result = [pscustomobject]@{
        schemaVersion = 'full-test-matrix.v1'
        status = $overallStatus
        error = $fatalError
        requireComplete = [bool]$RequireComplete
        projectCount = $results.Count
        expectedProjectCount = $projects.Count
        passed = $totalPassed
        failed = $totalFailed
        skipped = $totalSkipped
        total = $totalTests
        totals = [pscustomobject]@{ passed = $totalPassed; failed = $totalFailed; skipped = $totalSkipped; total = $totalTests }
        projects = @($results)
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    [IO.File]::WriteAllText($fullOutput, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Output ($result | ConvertTo-Json -Depth 8)
    if ($fatalError) { exit 1 }
} finally {
    Pop-Location
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
        Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
    }
    if ($null -eq $previousRepositoryRoot) {
        Remove-Item Env:HIS_HOPE_REPOSITORY_ROOT -ErrorAction SilentlyContinue
    } else {
        $env:HIS_HOPE_REPOSITORY_ROOT = $previousRepositoryRoot
    }
}
