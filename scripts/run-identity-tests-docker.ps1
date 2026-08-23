[CmdletBinding()]
param(
    [string]$Network = 'docker_default',
    [string]$Filter,
    [switch]$CollectCoverage,
    [string]$ResultsDirectory = 'tests/IdentityService/IdentityService.IntegrationTests/TestResults/docker-run',
    [string]$LockPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$project = 'tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj'
$repo = (Resolve-Path '.').Path
# Prefer the repository-scoped Docker credentials used by CI/test runs. This
# avoids inheriting a host Docker config that may be unreadable in the
# sandboxed runner and keeps the invocation self-contained.
$repoDockerConfig = Join-Path $repo '.docker-test-config'
if (Test-Path (Join-Path $repoDockerConfig 'config.json')) {
    $env:DOCKER_CONFIG = $repoDockerConfig
}
$homeRoot = if ($env:USERPROFILE) { $env:USERPROFILE } else { $env:HOME }
if ([string]::IsNullOrWhiteSpace($homeRoot)) { throw 'Neither USERPROFILE nor HOME is available for the NuGet cache mount.' }
$nuget = Join-Path $homeRoot '.nuget/packages'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$postgres = "identity-docker-pg-$suffix"
$redis = "identity-docker-redis-$suffix"
$runner = "identity-docker-runner-$suffix"
$testNetwork = $Network
$networkCreated = $false
$lockPath = if ([string]::IsNullOrWhiteSpace($LockPath)) {
    Join-Path $repo 'artifacts/.identity-test-run.lock'
} elseif ([IO.Path]::IsPathRooted($LockPath)) {
    $LockPath
} else {
    Join-Path $repo $LockPath
}
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

function Invoke-Docker([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker command failed with exit code ${LASTEXITCODE}: docker $($Arguments -join ' ')"
    }
}

try {
    # Docker's built-in bridge network does not provide container-name DNS.
    # Use an isolated user-defined network for bridge/CI callers, while
    # preserving an explicitly supplied application network when it exists.
    $networkExists = (& docker network inspect $testNetwork 2>$null)
    if ($LASTEXITCODE -ne 0 -or $testNetwork -in @('bridge', 'host', 'none')) {
        $testNetwork = "identity-docker-net-$suffix"
        Invoke-Docker @('network', 'create', $testNetwork) | Out-Null
        $networkCreated = $true
    }

    Invoke-Docker @('run', '-d', '--name', $postgres, '--network', $testNetwork,
        '-e', 'POSTGRES_DB=hishopetest', '-e', 'POSTGRES_USER=testuser',
        '-e', 'POSTGRES_PASSWORD=testpass123', 'postgres:16-alpine')
    Invoke-Docker @('run', '-d', '--name', $redis, '--network', $testNetwork, 'redis:7-alpine')

    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        $pgReady = (& docker exec $postgres pg_isready -U testuser -d hishopetest 2>$null)
        $redisReady = (& docker exec $redis redis-cli ping 2>$null)
        if ($pgReady -match 'accepting connections' -and $redisReady -match 'PONG') {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'PostgreSQL/Redis did not become ready within 120 seconds.' }

    # The repository can contain Windows-generated project.assets.json files
    # with machine-local fallback folders (for example DevExpress offline
    # paths).  The Linux runner must use an isolated NuGet config so those
    # paths cannot leak into the Docker validation gate.
    # Explicitly clear fallback folders as well as package sources. A Docker
    # SDK can still merge machine-level NuGet fallback configuration when only
    # packageSources are supplied; that leaks Windows-only paths such as the
    # DevExpress offline folder into project.assets.json and breaks Linux builds.
    $nugetConfig = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>'))
    # Keep the validation scope unchanged, but serialize MSBuild graph work.
    # Docker Desktop runners share memory with the host; the default project
    # graph parallelism can be OOM-killed (exit 137) while compiling the full
    # Identity integration suite. Test execution itself remains unchanged.
    $testCommand = "export DOTNET_CLI_TELEMETRY_OPTOUT=1 NUGET_PACKAGES=/root/.nuget/packages NUGET_FALLBACK_PACKAGES= && echo $nugetConfig | base64 -d > /tmp/nuget.docker.config && find /src -type d -name obj -prune -exec rm -rf {} + && dotnet restore $project --disable-parallel --force --force-evaluate --configfile /tmp/nuget.docker.config -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:RestoreFallbackFolders= -p:RestoreAdditionalProjectFallbackFolders= && dotnet test $project --no-restore --logger 'console;verbosity=minimal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false"
    if ($Filter) { $testCommand += " --filter '$Filter'" }
    if ($CollectCoverage) { $testCommand += " --collect:'XPlat Code Coverage' --results-directory /src/$ResultsDirectory" }

    # Run the test container detached. Docker Desktop can emit an
    # "unexpected EOF" while a long foreground process streams output even
    # though the container itself is healthy; polling its state avoids that
    # transport race and lets us preserve the actual test exit code.
    # The repository is mounted back into the runner workspace. Match the
    # GitHub runner uid/gid so generated obj/bin files remain writable by the
    # subsequent host-side dotnet test steps.
    $dockerUserArguments = @()
    if (Get-Command id -ErrorAction SilentlyContinue) {
        $runnerUid = (& id -u).Trim()
        $runnerGid = (& id -g).Trim()
        $dockerUserArguments = @('--user', "${runnerUid}:${runnerGid}")
    }
    Invoke-Docker (@('run', '-d', '--name', $runner) + $dockerUserArguments + @('--memory', '6g', '--memory-swap', '6g', '--network', $testNetwork,
        '-v', "${repo}:/src", '-v', "${nuget}:/root/.nuget/packages", '-w', '/src',
        '-e', "IDENTITY_TEST_POSTGRES_CONNECTION=Host=$postgres;Port=5432;Database=hishopetest;Username=testuser;Password=testpass123",
        '-e', "IDENTITY_TEST_REDIS_CONNECTION=${redis}:6379",
        'mcr.microsoft.com/dotnet/sdk:8.0', 'bash', '-lc', $testCommand))

    $finished = $false
    for ($attempt = 0; $attempt -lt 1800; $attempt++) {
        $state = (& docker inspect --format '{{.State.Status}} {{.State.ExitCode}}' $runner 2>$null)
        if ($LASTEXITCODE -ne 0) { throw "Unable to inspect test runner container '$runner'." }
        if ($state -match '^(exited|dead)\s+(\d+)$') {
            $finished = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $finished) { throw 'Identity integration runner exceeded the 30-minute timeout.' }

    & docker logs $runner
    $runnerExitCode = [int]((& docker inspect --format '{{.State.ExitCode}}' $runner).Trim())
    if ($runnerExitCode -ne 0) {
        throw "Identity integration tests failed with exit code $runnerExitCode."
    }
}
finally {
    # Remove only this invocation's exact containers. Application containers
    # and unrelated Testcontainers workloads are intentionally untouched.
    $cleanupPreference = $ErrorActionPreference
    try {
        # Docker Desktop may reap a disposable resource concurrently. Cleanup
        # must remain best-effort and must never turn a completed test run into
        # a false failure.
        $ErrorActionPreference = 'SilentlyContinue'
        foreach ($name in @($runner, $postgres, $redis)) {
            & docker rm -f $name 2>$null | Out-Null
        }
        if ($networkCreated) {
            & docker network rm $testNetwork 2>$null | Out-Null
        }
        if ($null -ne $lockStream) {
            $lockStream.Dispose()
            $lockStream = $null
        }
    }
    finally {
        $ErrorActionPreference = $cleanupPreference
    }
}
