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
    dotnet test (Join-Path $repo 'tests/Shared/Infrastructure.Tests/Infrastructure.Tests.csproj') --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Reliability contract tests failed.' }
}

Write-Output 'Reliability platform contract: PASS'
