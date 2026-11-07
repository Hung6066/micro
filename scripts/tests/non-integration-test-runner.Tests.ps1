$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runnerPath = Join-Path $repositoryRoot 'scripts\run-non-integration-tests.ps1'
$matrixRunnerPath = Join-Path $repositoryRoot 'scripts\run-full-test-matrix.ps1'
$workflowPath = Join-Path $repositoryRoot '.github\workflows\platform-quality-gates.yml'
$runner = Get-Content -LiteralPath $runnerPath -Raw
$matrixRunner = Get-Content -LiteralPath $matrixRunnerPath -Raw
$workflow = Get-Content -LiteralPath $workflowPath -Raw

foreach ($required in @(
    "Get-ChildItem -LiteralPath `$TestRoot -Recurse -Filter '*.csproj' -File",
    'IntegrationTestBase',
    'dotnet restore $project.FullName',
    'dotnet test $project.FullName --no-restore --configuration Release',
    'Non-integration test gate passed'
)) {
    if (-not $runner.Contains($required)) {
        throw "Non-integration runner is missing required contract: $required"
    }
}

if (-not $runner.Contains('(?:\.Integration(?:\.Tests|Tests)?|IntegrationTestBase)')) {
    throw 'Non-integration runner must exclude integration projects by project path.'
}

if ($workflow -match 'FullyQualifiedName!~Integration') {
    throw 'Platform quality workflow must not use the incomplete FullyQualifiedName integration filter.'
}
if ($workflow -notmatch 'run-non-integration-tests\.ps1') {
    throw 'Platform quality workflow must invoke the dedicated non-integration test runner.'
}

if ($matrixRunner -notmatch "\(Tests\?\|Test\)\[\\\\/\]" -or
    -not $matrixRunner.Contains("FilePath = 'dotnet'") -or
    -not $matrixRunner.Contains('.full-test-matrix.lock') -or
    -not $matrixRunner.Contains('[IO.FileShare]::None') -or
    -not $matrixRunner.Contains('RequireComplete') -or
    -not $matrixRunner.Contains('environment-blocked') -or
    -not $matrixRunner.Contains('UseArtifactsOutput') -or
    -not $matrixRunner.Contains('ArtifactsPath') -or
    -not $matrixRunner.Contains('launchFailed') -or
    -not $matrixRunner.Contains('HIS_HOPE_REPOSITORY_ROOT') -or
    -not $matrixRunner.Contains('ProjectTimeoutMinutes') -or
    -not $matrixRunner.Contains('WaitForExit')) {
    throw 'Full test matrix runner must support Windows/Linux paths, supervised dotnet children, isolated output, exclusive execution, environment-blocked classification, and a fail-closed complete mode.'
}
if ($workflow -notmatch 'full-service-integration-matrix' -or
    $workflow -notmatch 'dotnet build His\.Hope\.sln[^\r\n]*--disable-parallel' -or
    $workflow -notmatch 'UseArtifactsOutput' -or
    $workflow -notmatch 'ArtifactsPath' -or
    $workflow -notmatch 'run-full-test-matrix\.ps1[^\r\n]*-ArtifactsPath[^\r\n]*-NoBuild[^\r\n]*RequireComplete') {
    throw 'Platform quality workflow must build once into isolated output and invoke the full service integration matrix in no-build complete mode.'
}

if ($workflow -notmatch 'needs:\s*full-service-integration-matrix' -or
    $workflow -notmatch 'actions/download-artifact@' -or
    $workflow -notmatch 'name:\s*full-service-integration-matrix' -or
    $workflow -notmatch 'validate-enterprise-production-phases\.ps1[^\r\n]*-IntegrationMatrixPath\s+artifacts/evidence/full-test-matrix\.json') {
    throw 'Enterprise production gates are not wired to fresh full matrix evidence.'
}

if ($matrixRunner -notmatch "schemaVersion\s*=\s*'full-test-matrix\.v1'" -or
    $matrixRunner -notmatch 'totals\s*=\s*\[pscustomobject\]' -or
    $matrixRunner -match "if \(\$status -in @\('fail', 'environment-blocked'\)\)\s*\{[\s\S]*?\bbreak\b") {
    throw 'Full matrix evidence does not expose the enterprise validator schema or complete all projects.'
}

$enterpriseValidator = Get-Content -LiteralPath (Join-Path $repositoryRoot 'scripts\validate-enterprise-production-phases.ps1') -Raw
if ($enterpriseValidator -notmatch 'if \(\$SkipServiceIntegrationMatrix\)') {
    throw 'Enterprise validator still couples service matrix skipping to integration-test skipping.'
}

Write-Output 'Non-integration test runner contract: PASS'
