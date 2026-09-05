[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$required = @(
    'src/Shared/Messaging/His.Hope.Messaging.Abstractions/EventEnvelope.cs',
    'src/Shared/Messaging/His.Hope.Messaging.Abstractions/EventDeliveryPolicy.cs',
    'src/Shared/Messaging/His.Hope.Messaging.Abstractions/EventSchemaRegistry.cs',
    'src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/RedisLockManager.cs',
    'tests/Shared/Infrastructure.Tests/MessagingReliabilityContractTests.cs'
)

foreach ($path in $required) {
    if (-not (Test-Path (Join-Path $repo $path))) { throw "Missing reliability contract: $path" }
}

if (-not $SkipTests) {
    $project = Join-Path $repo 'tests/Shared/Infrastructure.Tests/Infrastructure.Tests.csproj'
    # Keep this verifier runnable from a clean checkout. The no-restore-only
    # invocation previously failed with NETSDK1004 when assets were absent.
    dotnet restore $project --disable-parallel
    if ($LASTEXITCODE -ne 0) { throw 'Reliability contract restore failed.' }
    dotnet test $project --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Reliability contract tests failed.' }
}

Write-Output 'Reliability platform contract: PASS'
