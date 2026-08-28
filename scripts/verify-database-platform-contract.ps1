[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Description) {
    $fullPath = Join-Path $RepositoryRoot $Path
    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($content -notmatch [regex]::Escape($Pattern)) {
        throw "Database platform contract failed: $Description ($Path)"
    }
}

Assert-Contains 'src/Shared/Persistence/His.Hope.Persistence/HisHopeDatabaseOptions.cs' 'EnableRetryOnFailure' 'shared PostgreSQL retry policy exists'
Assert-Contains 'src/Shared/Persistence/His.Hope.Persistence/HisHopeDatabaseOptions.cs' 'MaxPoolSize' 'shared connection pool policy exists'
Assert-Contains 'src/Shared/Persistence/His.Hope.Persistence/MigrationRunner.cs' 'pg_advisory_lock' 'migration runner serializes replicas'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs' 'UseHisHopeNpgsql' 'identity uses shared database policy'
Assert-Contains 'docker/docker-compose.yml' 'pg_stat_statements' 'local PostgreSQL query telemetry is enabled'
Assert-Contains 'docker/init-multiple-dbs.sh' 'CREATE EXTENSION IF NOT EXISTS pg_stat_statements' 'database initialization enables query telemetry'
Assert-Contains 'docker/prometheus.yml' "targets: ['postgres-exporter:9187']" 'PostgreSQL exporter is scraped'
Assert-Contains 'docker/prometheus.yml' "targets: ['redis-exporter:9121']" 'Redis exporter is scraped'
Assert-Contains 'k8s/base/postgres.yaml' 'replicas: 1' 'plain PostgreSQL is not falsely scaled as independent replicas'
Assert-Contains 'k8s/infrastructure/pgbouncer-configmap.yaml' 'postgres.his-hope.svc.cluster.local port=5432' 'PgBouncer points to PostgreSQL'
Assert-Contains 'admin-app/src/app/app.routes.ts' 'database-platform' 'Angular Manager exposes database platform screen'

$migrationFiles = Get-ChildItem (Join-Path $RepositoryRoot 'src/Services') -Recurse -Filter '*OptimizeReadIndexes.cs'
if ($migrationFiles.Count -lt 6) {
    throw "Database platform contract failed: expected six read-index migrations, found $($migrationFiles.Count)"
}

foreach ($migration in $migrationFiles) {
    $content = Get-Content -LiteralPath $migration.FullName -Raw
    # EF migrations may express indexes either through the strongly typed
    # migrationBuilder.CreateIndex API or through idempotent SQL (for legacy
    # quoted identifiers / cross-version compatibility). Both are valid index
    # operations and must satisfy this contract.
    if ($content -notmatch 'CreateIndex|CREATE\s+INDEX') {
        throw "Database platform contract failed: migration has no index operation ($($migration.FullName))"
    }
}

Write-Output "Database platform contract passed: $($migrationFiles.Count) read-index migrations and shared runtime policy verified."
