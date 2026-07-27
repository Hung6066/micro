param(
    [string] $TestRoot = 'tests/Contract',
    [string] $ResultsDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) "his-hope-contract-tests-$([guid]::NewGuid().ToString('N'))")
)

$ErrorActionPreference = 'Stop'
$projects = @(Get-ChildItem -LiteralPath $TestRoot -Filter '*.csproj' -Recurse -File | Sort-Object FullName)
if ($projects.Count -eq 0) { throw "No contract test projects found under '$TestRoot'." }

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$totalTests = 0
foreach ($project in $projects) {
    $safeName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
    $logFile = "contract-$safeName.trx"
    dotnet restore $project.FullName -warnAsError:NU1605 -warnAsError:NU1901 -warnAsError:NU1902 -warnAsError:NU1903 -warnAsError:NU1904
    if ($LASTEXITCODE -ne 0) { throw "Contract test restore failed for '$($project.FullName)'." }
    dotnet test $project.FullName --no-restore --logger "trx;LogFileName=$logFile" --results-directory $ResultsDirectory
    if ($LASTEXITCODE -ne 0) { throw "Contract tests failed for '$($project.FullName)'." }

    $trx = Join-Path $ResultsDirectory $logFile
    if (-not (Test-Path -LiteralPath $trx)) { throw "Contract test result file was not produced for '$($project.FullName)'." }
    [xml] $document = Get-Content -Raw -LiteralPath $trx
    $results = @($document.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($results.Count -eq 0) { throw "Contract test project '$($project.FullName)' executed zero tests." }
    $totalTests += $results.Count
    Write-Host "${safeName}: $($results.Count) tests executed"
}

if ($totalTests -eq 0) { throw 'Contract test gate executed zero tests.' }
Write-Host "Contract test gate passed: $totalTests tests executed across $($projects.Count) projects."
