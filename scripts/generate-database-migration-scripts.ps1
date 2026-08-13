[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path,
    [string]$OutputDirectory = 'artifacts/database-migrations'
)

$ErrorActionPreference = 'Stop'
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $OutputDirectory))
}
New-Item -ItemType Directory -Path $output -Force | Out-Null

$contexts = @(
    @{ Name='identity'; Project='src/Services/IdentityService/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj'; Context='IdentityDbContext' },
    @{ Name='appointment'; Project='src/Services/AppointmentService/AppointmentService.Infrastructure/AppointmentService.Infrastructure.csproj'; Context='AppointmentDbContext' },
    @{ Name='clinical'; Project='src/Services/ClinicalService/ClinicalService.Infrastructure/ClinicalService.Infrastructure.csproj'; Context='ClinicalDbContext' },
    @{ Name='lab'; Project='src/Services/LabService/LabService.Infrastructure/LabService.Infrastructure.csproj'; Context='LabDbContext' },
    @{ Name='billing'; Project='src/Services/BillingService/BillingService.Infrastructure/BillingService.Infrastructure.csproj'; Context='BillingDbContext' },
    @{ Name='patient'; Project='src/Services/PatientService/PatientService.Infrastructure/PatientService.Infrastructure.csproj'; Context='PatientDbContext' },
    @{ Name='patient-read'; Project='src/Services/PatientService/PatientService.Infrastructure/PatientService.Infrastructure.csproj'; Context='PatientReadDbContext' },
    @{ Name='pharmacy'; Project='src/Services/PharmacyService/PharmacyService.Infrastructure/PharmacyService.Infrastructure.csproj'; Context='PharmacyDbContext' }
)

$projects = $contexts.Project | Select-Object -Unique
foreach ($project in $projects) {
    $projectPath = Join-Path $RepositoryRoot $project
    dotnet restore $projectPath
    if ($LASTEXITCODE -ne 0) { throw "Migration project restore failed for $project." }

    dotnet build $projectPath --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Migration project build failed for $project." }
}

$manifest = [System.Collections.Generic.List[object]]::new()
foreach ($item in $contexts) {
    $file = Join-Path $output "$($item.Name)-idempotent.sql"
    dotnet ef migrations script --idempotent --project (Join-Path $RepositoryRoot $item.Project) --startup-project (Join-Path $RepositoryRoot $item.Project) --context $item.Context --configuration Release --no-build --no-color --output $file
    if ($LASTEXITCODE -ne 0) { throw "Migration script generation failed for $($item.Context)." }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
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
