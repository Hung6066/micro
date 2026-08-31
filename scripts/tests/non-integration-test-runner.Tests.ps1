$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runnerPath = Join-Path $repositoryRoot 'scripts\run-non-integration-tests.ps1'
$workflowPath = Join-Path $repositoryRoot '.github\workflows\platform-quality-gates.yml'
$runner = Get-Content -LiteralPath $runnerPath -Raw
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

Write-Output 'Non-integration test runner contract: PASS'
