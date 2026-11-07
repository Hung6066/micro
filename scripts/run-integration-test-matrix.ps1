[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ResultsDirectory = 'artifacts/evidence/integration-matrix',
    # The repository compose stack maps PostgreSQL to host port 5433. Keep an
    # explicit override for CI/customer environments, but make the local
    # matrix deterministic instead of falling back to an unrelated port 5432.
    [string]$ContentDatabaseUrl = $(
        $configuredContentDatabaseUrl = [Environment]::GetEnvironmentVariable('DATABASE_CONTENT_URL')
        if ([string]::IsNullOrWhiteSpace($configuredContentDatabaseUrl)) {
            'Host=localhost;Port=5433;Database=contentdb;Username=postgres;Password=postgres'
        } else {
            $configuredContentDatabaseUrl
        }
    )
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
$resultsPath = if ([IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory
} else {
    Join-Path $RepositoryRoot $ResultsDirectory
}
New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null

$originalContentDatabaseUrl = $env:DATABASE_CONTENT_URL

$projects = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'tests') -Recurse -Filter '*.csproj' -File |
    Where-Object { $_.BaseName -match 'Integration' -and $_.BaseName -ne 'IntegrationTestBase' } |
    Sort-Object FullName)
if ($projects.Count -eq 0) { throw 'Integration matrix contains no test projects.' }

$records = [System.Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $safeName = [IO.Path]::GetFileNameWithoutExtension($project.Name)
    $trxName = "$safeName.trx"
    $trxPath = Join-Path $resultsPath $trxName
    $projectExitCode = 0

    # Never attribute results from an earlier run to the current matrix. This is
    # especially important when a test process fails before creating a new TRX.
    Remove-Item -LiteralPath $trxPath -Force -ErrorAction SilentlyContinue

    # ContentService integration tests use DATABASE_CONTENT_URL as a PostgreSQL
    # connection string. BFF projects use the same setting name for an HTTP
    # service endpoint, so never leak the database override into other tests.
    if ($safeName -eq 'ContentService.Integration.Tests') {
        if ([string]::IsNullOrWhiteSpace($ContentDatabaseUrl)) {
            Remove-Item Env:DATABASE_CONTENT_URL -ErrorAction SilentlyContinue
        } else {
            $env:DATABASE_CONTENT_URL = $ContentDatabaseUrl
        }
    } elseif ($null -eq $originalContentDatabaseUrl) {
        Remove-Item Env:DATABASE_CONTENT_URL -ErrorAction SilentlyContinue
    } else {
        $env:DATABASE_CONTENT_URL = $originalContentDatabaseUrl
    }

    Push-Location $RepositoryRoot
    try {
        & dotnet restore $project.FullName -warnAsError:NU1605 -warnAsError:NU1901 -warnAsError:NU1902 -warnAsError:NU1903 -warnAsError:NU1904 | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Integration test restore failed for '$($project.FullName)'." }
        & dotnet build $project.FullName --configuration Release --no-restore --nologo | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Integration test build failed for '$($project.FullName)'." }
        & dotnet test $project.FullName --configuration Release --no-build --no-restore --nologo `
            --logger "trx;LogFileName=$trxName" --results-directory $resultsPath `
            -- RunConfiguration.MaxCpuCount=1 | Out-Host
        $projectExitCode = $LASTEXITCODE
    } catch {
        $projectExitCode = if ($LASTEXITCODE -ne 0) { $LASTEXITCODE } else { 1 }
        Write-Warning $_.Exception.Message
    } finally {
        Pop-Location
    }

    $total = 0
    $passed = 0
    $failed = 0
    $skipped = 0
    if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
        [xml]$document = Get-Content -LiteralPath $trxPath -Raw
        $testResults = @($document.TestRun.Results.UnitTestResult)
        $counters = $document.TestRun.ResultSummary.Counters
        if ($testResults.Count -gt 0) {
            $total = $testResults.Count
            $passed = @($testResults | Where-Object outcome -eq 'Passed').Count
            $failed = @($testResults | Where-Object outcome -eq 'Failed').Count
            $skipped = @($testResults | Where-Object { $_.outcome -in @('Skipped', 'NotExecuted') }).Count
        } elseif ($null -ne $counters) {
            $total = [int]$counters.total
            $passed = [int]$counters.passed
            $failed = [int]$counters.failed
            $skipped = [int]$counters.notExecuted
        }
    }

    $status = if ($projectExitCode -ne 0 -or $failed -gt 0) { 'fail' }
        elseif ($skipped -gt 0 -or $total -eq 0) { 'environment-blocked' }
        else { 'pass' }
    $records.Add([pscustomobject]@{
        project = $safeName
        path = $project.FullName.Substring($RepositoryRoot.Length).TrimStart('\', '/')
        exitCode = $projectExitCode
        total = $total
        passed = $passed
        failed = $failed
        skipped = $skipped
        status = $status
    })
    Write-Host "${safeName}: status=$status total=$total passed=$passed failed=$failed skipped=$skipped exit=$projectExitCode"
}

if ($null -eq $originalContentDatabaseUrl) {
    Remove-Item Env:DATABASE_CONTENT_URL -ErrorAction SilentlyContinue
} else {
    $env:DATABASE_CONTENT_URL = $originalContentDatabaseUrl
}

$failedProjects = @($records | Where-Object status -eq 'fail')
$blockedProjects = @($records | Where-Object status -eq 'environment-blocked')
$result = [pscustomobject]@{
    schemaVersion = 'integration-test-matrix.v1'
    status = if ($failedProjects.Count -gt 0) { 'fail' } elseif ($blockedProjects.Count -gt 0) { 'environment-blocked' } else { 'pass' }
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    projectCount = $records.Count
    totals = [pscustomobject]@{
        total = [int](($records | Measure-Object total -Sum).Sum)
        passed = [int](($records | Measure-Object passed -Sum).Sum)
        failed = [int](($records | Measure-Object failed -Sum).Sum)
        skipped = [int](($records | Measure-Object skipped -Sum).Sum)
    }
    projects = @($records)
}
$evidencePath = Join-Path $resultsPath 'integration-test-matrix.json'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8
Write-Host "Integration matrix status=$($result.status) projects=$($result.projectCount) total=$($result.totals.total) passed=$($result.totals.passed) failed=$($result.totals.failed) skipped=$($result.totals.skipped)"

if ($failedProjects.Count -gt 0) { exit 80 }
if ($blockedProjects.Count -gt 0) { exit 70 }
