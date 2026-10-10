[CmdletBinding()]
param(
    [string]$TestRoot = 'tests'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projects = @(Get-ChildItem -LiteralPath $TestRoot -Recurse -Filter '*.csproj' -File |
    Where-Object { $_.FullName -notmatch '(?i)(?:\.Integration(?:\.Tests|Tests)?|IntegrationTestBase)[\\/]?' } |
    Sort-Object FullName)

if ($projects.Count -eq 0) {
    throw "No non-integration test projects found under '$TestRoot'."
}

$totalProjects = 0
foreach ($project in $projects) {
    Write-Host "Running non-integration tests: $($project.FullName)"
    dotnet restore $project.FullName -warnAsError:NU1605 -warnAsError:NU1901 -warnAsError:NU1902 -warnAsError:NU1903 -warnAsError:NU1904
    if ($LASTEXITCODE -ne 0) {
        throw "Non-integration test restore failed for '$($project.FullName)'."
    }
    # Some non-integration test projects are intentionally not part of the
    # solution; build the test project after restoring its own graph.
    dotnet test $project.FullName --no-restore --configuration Release --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) {
        throw "Non-integration tests failed for '$($project.FullName)'."
    }
    $totalProjects++
}

Write-Host "Non-integration test gate passed: $totalProjects projects executed; integration projects excluded by project path."
