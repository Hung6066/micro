param(
    [switch] $IncludeIntegration,
    [int] $IntegrationTimeoutSeconds = 300,
    [string] $ResultsDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) "his-hope-identity-tests-$([guid]::NewGuid().ToString('N'))")
)

$ErrorActionPreference = 'Stop'
$projects = @(
    'tests/Services/IdentityService/IdentityService.Domain.Tests/IdentityService.Domain.Tests.csproj',
    'tests/Services/IdentityService/IdentityService.Application.Tests/IdentityService.Application.Tests.csproj',
    'tests/Services/IdentityService/IdentityService.Infrastructure.Tests/IdentityService.Infrastructure.Tests.csproj'
)
if ($IncludeIntegration) { $projects += 'tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj' }

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$summary = [System.Collections.Generic.List[object]]::new()
$lockPath = Join-Path (Resolve-Path '.').Path 'artifacts/.identity-test-run.lock'
New-Item -ItemType Directory -Path (Split-Path -Parent $lockPath) -Force | Out-Null
$lockStream = $null
$lockDeadline = (Get-Date).AddSeconds(30)
while ($null -eq $lockStream -and (Get-Date) -lt $lockDeadline) {
    try {
        $lockStream = [System.IO.File]::Open($lockPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        Start-Sleep -Milliseconds 500
    }
}
if ($null -eq $lockStream) {
    throw "Another Identity test run is active; could not acquire workspace lock '$lockPath'."
}

function Remove-NewTestcontainers([string[]] $beforeIds) {
    $afterIds = @(docker ps -aq --filter label=org.testcontainers=true 2>$null)
    $newIds = @($afterIds | Where-Object { $_ -and ($_ -notin $beforeIds) })
    if ($newIds.Count -gt 0) {
        # Remove only containers created by this invocation; never sweep the
        # user's application containers or unrelated Docker workloads.
        docker rm -f @newIds 2>$null | Out-Null
    }
}

try {
foreach ($project in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    dotnet restore $project --disable-parallel
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $project" }

    $trx = Join-Path $ResultsDirectory "$name.trx"
    if ($IncludeIntegration -and $project -like '*IntegrationTests*') {
        $testcontainersBefore = @(docker ps -aq --filter label=org.testcontainers=true 2>$null)
        # The suite owns its Testcontainers instances and each fixture has
        # explicit cleanup. Disabling the Ryuk sidecar avoids a known
        # Windows/Docker Desktop race where concurrent fixtures cancel Ryuk
        # initialization and mark unrelated tests as ResourceReaperException.
        # Callers may override this explicitly when they need Ryuk protection.
        $previousRyukSetting = $env:TESTCONTAINERS_RYUK_DISABLED
        if ([string]::IsNullOrWhiteSpace($previousRyukSetting)) {
            $env:TESTCONTAINERS_RYUK_DISABLED = 'true'
        }
        $exitCode = $null
        try {
            try {
                $process = Start-Process dotnet -ArgumentList @('test', $project, '--no-restore', '--logger', "trx;LogFileName=$name.trx", '--results-directory', $ResultsDirectory) -PassThru -NoNewWindow
                if (-not $process.WaitForExit($IntegrationTimeoutSeconds * 1000)) {
                    & taskkill /PID $process.Id /T /F 2>$null | Out-Null
                    $summary.Add([pscustomobject]@{ Project = $name; Status = 'environment-timeout'; Tests = 0 })
                    continue
                }
                $exitCode = $process.ExitCode
            }
            finally {
                # Cleanup must also run when process startup or waiting throws.
                # The snapshot limits removal to containers created by this invocation.
                Remove-NewTestcontainers $testcontainersBefore
            }
        }
        finally {
            if ($null -eq $previousRyukSetting) { Remove-Item Env:TESTCONTAINERS_RYUK_DISABLED -ErrorAction SilentlyContinue }
            else { $env:TESTCONTAINERS_RYUK_DISABLED = $previousRyukSetting }
        }
    }
    else {
        dotnet test $project --no-restore --logger "trx;LogFileName=$name.trx" --results-directory $ResultsDirectory
        $exitCode = $LASTEXITCODE
    }

    if (-not (Test-Path -LiteralPath $trx)) { throw "TRX result missing: $project" }
    [xml]$document = Get-Content -Raw -LiteralPath $trx
    $results = @($document.SelectNodes("//*[local-name()='UnitTestResult']"))
    $failed = @($results | Where-Object { $_.outcome -ne 'Passed' }).Count
    $status = if ($exitCode -eq 0 -and $results.Count -gt 0 -and $failed -eq 0) { 'pass' } else { 'fail' }
    $summary.Add([pscustomobject]@{ Project = $name; Status = $status; Tests = $results.Count; Failed = $failed })
}

$summary | Format-Table -AutoSize
$hardFailures = @($summary | Where-Object Status -eq 'fail')
if ($hardFailures.Count -gt 0) { throw "Identity test gate failed for $($hardFailures.Project -join ', ')." }
if (@($summary | Where-Object Status -eq 'environment-timeout').Count -gt 0) {
    Write-Warning 'Integration execution timed out; status is environment-timeout, not pass.'
}
Write-Host "Identity test gate completed: $($summary.Tests | Measure-Object -Sum | Select-Object -ExpandProperty Sum) tests recorded."
}
finally {
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
        $lockStream = $null
    }
}
