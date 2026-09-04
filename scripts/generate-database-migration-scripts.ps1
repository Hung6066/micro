[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path,
    [string]$OutputDirectory = 'artifacts/database-migrations'
)

$ErrorActionPreference = 'Stop'
function Get-Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($Path))) -replace '-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
$output = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $OutputDirectory))
New-Item -ItemType Directory -Path $output -Force | Out-Null

$contexts = @(
    @{ Name='identity'; Project='src/Services/IdentityService/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj'; Context='IdentityDbContext' },
    @{ Name='appointment'; Project='src/Services/AppointmentService/AppointmentService.Infrastructure/AppointmentService.Infrastructure.csproj'; Context='AppointmentDbContext' },
    @{ Name='clinical'; Project='src/Services/ClinicalService/ClinicalService.Infrastructure/ClinicalService.Infrastructure.csproj'; Context='ClinicalDbContext' },
    @{ Name='lab'; Project='src/Services/LabService/LabService.Infrastructure/LabService.Infrastructure.csproj'; Context='LabDbContext' },
    @{ Name='billing'; Project='src/Services/BillingService/BillingService.Infrastructure/BillingService.Infrastructure.csproj'; Context='BillingDbContext' },
    @{ Name='patient'; Project='src/Services/PatientService/PatientService.Infrastructure/PatientService.Infrastructure.csproj'; Context='PatientDbContext' },
    @{ Name='patient-read'; Project='src/Services/PatientService/PatientService.Infrastructure/PatientService.Infrastructure.csproj'; Context='PatientReadDbContext' },
    @{ Name='pharmacy'; Project='src/Services/PharmacyService/PharmacyService.Infrastructure/PharmacyService.Infrastructure.csproj'; Context='PharmacyDbContext' },
    @{ Name='commerce'; Project='src/Services/CommerceService/CommerceService.Infrastructure/CommerceService.Infrastructure.csproj'; Context='CommerceDbContext'; MigrationDirectory='Persistence/Migrations' },
    @{ Name='content'; Project='src/Services/ContentService/ContentService.Infrastructure/ContentService.Infrastructure.csproj'; Context='ContentDbContext'; MigrationDirectory='Migrations' },
    @{ Name='manufacturing'; Project='src/Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingService.Infrastructure.csproj'; Context='ManufacturingDbContext'; MigrationDirectory='Migrations' }
)

$manifest = [System.Collections.Generic.List[object]]::new()
foreach ($item in $contexts) {
    $file = Join-Path $output "$($item.Name)-idempotent.sql"
    $projectPath = Join-Path $RepositoryRoot $item.Project
    $projectDirectory = Split-Path -Parent $projectPath
    $projectName = [IO.Path]::GetFileNameWithoutExtension($projectPath)
    $depsPath = Join-Path $projectDirectory "bin/Release/net8.0/$projectName.deps.json"
    $efArguments = @(
        'ef', 'migrations', 'script', '--idempotent',
        '--project', $projectPath,
        '--startup-project', (Join-Path $RepositoryRoot $(if ($item.StartupProject) { $item.StartupProject } else { $item.Project })),
        '--context', $item.Context,
        '--configuration', 'Release',
        '--no-color',
        '--output', $file
    )
    $migrationDirectory = Join-Path $projectDirectory $(if ($item.MigrationDirectory) { $item.MigrationDirectory } else { 'Persistence/Migrations' })
    $latestMigrationWrite = if (Test-Path -LiteralPath $migrationDirectory -PathType Container) {
        (Get-ChildItem -LiteralPath $migrationDirectory -Filter '*.cs' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
    } else { [DateTime]::MinValue }
    $assemblyPath = Join-Path $projectDirectory "bin/Release/net8.0/$projectName.dll"
    if ((Test-Path -LiteralPath $depsPath -PathType Leaf) -and
        (Test-Path -LiteralPath $assemblyPath -PathType Leaf) -and
        (Get-Item -LiteralPath $assemblyPath).LastWriteTimeUtc -ge $latestMigrationWrite) {
        $efArguments += '--no-build'
    } else {
        Write-Warning "Release output is missing or older than migration source for $($item.Context); rebuilding before script generation."
    }
    dotnet @efArguments
    if ($LASTEXITCODE -ne 0) { throw "Migration script generation failed for $($item.Context)." }
    $hash = Get-Sha256 $file
    $manifest.Add([pscustomobject]@{ name = $item.Name; context = $item.Context; script = (Split-Path -Leaf $file); sha256 = $hash })
    Write-Output "Generated $file"
}

$manifestPath = Join-Path $output 'migration-manifest.json'
$manifestDocument = [pscustomobject]@{
    format = 'his-hope-ef-migration-manifest-v1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    contexts = @($manifest)
    warning = 'Generated SQL must run once under the migration/deployer identity; API replicas must keep Persistence:RunMigrationsOnStartup=false.'
}
[IO.File]::WriteAllText($manifestPath, ($manifestDocument | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Output "Generated $manifestPath"
