Set-StrictMode -Version Latest

function Get-TenantPlacementRepoRoot {
    Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

function ConvertFrom-PostgresConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $map = @{}
    foreach ($segment in $ConnectionString.Split(';')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $index = $segment.IndexOf('=')
        if ($index -lt 1) { continue }
        $key = $segment.Substring(0, $index).Trim()
        $value = $segment.Substring($index + 1).Trim()
        $map[$key] = $value
    }
    return $map
}

function Read-TenantPlacementConfig {
    param(
        [string]$PlacementFile = 'config/conglomerate/tenant-placement.v1.json',
        [string]$RepoRoot = (Get-TenantPlacementRepoRoot)
    )

    $path = if ([IO.Path]::IsPathRooted($PlacementFile)) { $PlacementFile } else { Join-Path $RepoRoot $PlacementFile }
    if (-not (Test-Path -LiteralPath $path)) { throw "Tenant placement file not found: $path" }
    $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return [pscustomobject]@{ Path = $path; Config = $config }
}

function Read-TenantPlacementConnectionStrings {
    param(
        [Parameter(Mandatory)]
        [string]$ConnectionStringsFile
    )

    $path = [IO.Path]::GetFullPath($ConnectionStringsFile)
    if (-not (Test-Path -LiteralPath $path)) {
        $repoLocal = Join-Path (Get-TenantPlacementRepoRoot) 'config/conglomerate/tenant-placement.connections.local.json'
        throw @"
Connection strings file not found: $path

Create the file with your dedicated connection, for example:
  Copy-Item config/conglomerate/tenant-placement.connections.local.json.example $repoLocal
  notepad $repoLocal

Or create C:\secure\tenant-placement.connections.json (outside repo):
  New-Item -ItemType Directory -Force C:\secure | Out-Null
  Copy-Item config/conglomerate/tenant-placement.connections.local.json.example C:\secure\tenant-placement.connections.json

Then run Test-TenantPlacementExternalDatabase.ps1 with -ConnectionStringsFile pointing to that file.
Only ManufacturingDb_customer_acme is required; shared ManufacturingDb uses env/docker default.
"@
    }
    $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $map = @{}
    foreach ($property in $document.PSObject.Properties) {
        if ([string]::IsNullOrWhiteSpace($property.Value)) {
            # Shared/default connections (e.g. ManufacturingDb) may be omitted — resolved from runtime env.
            continue
        }
        $map[$property.Name] = [string]$property.Value
    }
    return $map
}

function Get-TenantPlacementServiceMigrationTargets {
    $root = Get-TenantPlacementRepoRoot
    return @{
        manufacturing = @{
            InfrastructureProject = Join-Path $root 'src/Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingService.Infrastructure.csproj'
            StartupProject = Join-Path $root 'src/Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj'
            Context = 'ManufacturingDbContext'
        }
        commerce = @{
            InfrastructureProject = Join-Path $root 'src/Services/CommerceService/CommerceService.Infrastructure/CommerceService.Infrastructure.csproj'
            StartupProject = Join-Path $root 'src/Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj'
            Context = 'CommerceDbContext'
        }
        content = @{
            InfrastructureProject = Join-Path $root 'src/Services/ContentService/ContentService.Infrastructure/ContentService.Infrastructure.csproj'
            StartupProject = Join-Path $root 'src/Services/ContentService/ContentService.Api/ContentService.Api.csproj'
            Context = 'ContentDbContext'
        }
    }
}

function Get-TenantPlacementDedicatedBindings {
    param(
        [Parameter(Mandatory)]
        $PlacementConfig,
        [Parameter(Mandatory)]
        [string]$TenantKey,
        [switch]$IncludeInactive
    )

    $entry = @($PlacementConfig.placements | Where-Object { $_.tenantKey -eq $TenantKey }) | Select-Object -First 1
    if (-not $entry) { throw "Tenant placement entry not found for '$TenantKey'." }
    if ($entry.tier -ne 'dedicated') { throw "Tenant '$TenantKey' is not tier=dedicated." }
    if (-not $IncludeInactive -and -not $entry.active) {
        throw "Tenant '$TenantKey' dedicated placement is inactive. Pass -IncludeInactive to operate on inactive entries."
    }

    $bindings = @()
    foreach ($serviceName in $entry.services.PSObject.Properties.Name) {
        $binding = $entry.services.$serviceName
        if ([string]::IsNullOrWhiteSpace($binding.connectionName)) {
            throw "Dedicated placement '$TenantKey' service '$serviceName' is missing connectionName."
        }
        $bindings += [pscustomobject]@{
            TenantKey = [string]$entry.tenantKey
            Tier = [string]$entry.tier
            Active = [bool]$entry.active
            DataRegion = $entry.dataRegion
            Reason = $entry.reason
            ServiceName = [string]$serviceName
            ConnectionName = [string]$binding.connectionName
        }
    }

    if ($bindings.Count -eq 0) {
        throw "Dedicated placement '$TenantKey' has no service bindings."
    }

    return $bindings
}

function Get-PostgresDatabaseNameFromConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $parts = ConvertFrom-PostgresConnectionString -ConnectionString $ConnectionString
    $database = $parts['Database']
    if ([string]::IsNullOrWhiteSpace($database)) { throw 'Connection string is missing Database.' }
    return $database
}

function Get-PostgresAdminConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $parts = ConvertFrom-PostgresConnectionString -ConnectionString $ConnectionString
    $parts['Database'] = 'postgres'
    return ($parts.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ';'
}

function Invoke-PostgresSql {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [Parameter(Mandatory)][string]$Sql,
        [string]$PsqlPath = 'psql'
    )

    # psql/libpq uses different option names from Npgsql/ADO.NET connection strings.
    # Keep the password out of the command line because external credentials may contain
    # shell-significant characters (and command lines are observable by other processes).
    $parts = ConvertFrom-PostgresConnectionString -ConnectionString $ConnectionString
    $passwordEntry = $parts.GetEnumerator() | Where-Object { $_.Key -match '^(?i:Password|Pwd)$' } | Select-Object -First 1
    $previousPassword = $env:PGPASSWORD
    try {
        if ($passwordEntry) { $env:PGPASSWORD = [string]$passwordEntry.Value }
        $psqlParts = foreach ($entry in $parts.GetEnumerator()) {
            if ($entry.Key -match '^(?i:Password|Pwd)$') { continue }
            $key = [string]$entry.Key
            if ($key -match '^(?i:Database)$') { $key = 'dbname' }
            elseif ($key -match '^(?i:Username|User Id)$') { $key = 'user' }
            elseif ($key -match '^(?i:Host|Server)$') { $key = 'host' }
            elseif ($key -match '^(?i:Port)$') { $key = 'port' }
            elseif ($key -match '^(?i:SSL Mode)$') { $key = 'sslmode' }
            "$key=$($entry.Value)"
        }
        # libpq keyword/value strings are whitespace-delimited (unlike ADO.NET strings).
        $psqlConnectionString = $psqlParts -join ' '
        $output = & $PsqlPath "--dbname=$psqlConnectionString" -v ON_ERROR_STOP=1 -Atc $Sql 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed: $output"
        }
        return [string]$output
    }
    finally {
        if ($null -eq $previousPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
        else { $env:PGPASSWORD = $previousPassword }
    }
}

function Ensure-PostgresDatabase {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [string]$PsqlPath = 'psql',
        [switch]$WhatIf
    )

    $databaseName = Get-PostgresDatabaseNameFromConnectionString -ConnectionString $ConnectionString
    if ($databaseName -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,62}$') {
        throw "Unsafe database name '$databaseName'."
    }

    $adminConnection = Get-PostgresAdminConnectionString -ConnectionString $ConnectionString
    $exists = Invoke-PostgresSql -ConnectionString $adminConnection -Sql "SELECT 1 FROM pg_database WHERE datname = '$databaseName'" -PsqlPath $PsqlPath
    if (-not [string]::IsNullOrWhiteSpace($exists)) {
        return [pscustomobject]@{ Database = $databaseName; Created = $false; Message = 'already exists' }
    }

    if ($WhatIf) {
        return [pscustomobject]@{ Database = $databaseName; Created = $false; Message = 'would create' }
    }

    Invoke-PostgresSql -ConnectionString $adminConnection -Sql "CREATE DATABASE $databaseName" -PsqlPath $PsqlPath | Out-Null
    return [pscustomobject]@{ Database = $databaseName; Created = $true; Message = 'created' }
}

function Invoke-TenantPlacementEfMigrate {
    param(
        [Parameter(Mandatory)][string]$ServiceName,
        [Parameter(Mandatory)][string]$ConnectionString,
        [switch]$WhatIf
    )

    $targets = Get-TenantPlacementServiceMigrationTargets
    if (-not $targets.ContainsKey($ServiceName)) {
        throw "Unknown service '$ServiceName'. Supported: $($targets.Keys -join ', ')."
    }

    $target = $targets[$ServiceName]
    foreach ($requiredPath in @($target.InfrastructureProject, $target.StartupProject)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Migration project not found: $requiredPath"
        }
    }

    if ($WhatIf) {
        return [pscustomobject]@{
            ServiceName = $ServiceName
            Context = $target.Context
            Migrated = $false
            Message = 'would run dotnet ef database update'
        }
    }

    $connectionEnvironmentName = switch ($ServiceName) {
        'manufacturing' { 'ConnectionStrings__ManufacturingDb' }
        'commerce' { 'ConnectionStrings__CommerceDb' }
        'content' { 'ConnectionStrings__ContentDb' }
        default { throw "No default connection environment mapping exists for service '$ServiceName'." }
    }

    $previousConnectionEnvironmentValue = [Environment]::GetEnvironmentVariable(
        $connectionEnvironmentName,
        [EnvironmentVariableTarget]::Process)
    try {
        # EF receives --connection for the target database, while the startup
        # host still needs its named connection during design-time DI creation.
        # Keep the value process-scoped so it is not persisted or exposed in the
        # command line, and restore any caller-provided value after migration.
        [Environment]::SetEnvironmentVariable(
            $connectionEnvironmentName,
            $ConnectionString,
            [EnvironmentVariableTarget]::Process)

        & dotnet ef database update `
            --project $target.InfrastructureProject `
            --startup-project $target.StartupProject `
            --context $target.Context `
            --connection $ConnectionString
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef database update failed for service '$ServiceName'."
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            $connectionEnvironmentName,
            $previousConnectionEnvironmentValue,
            [EnvironmentVariableTarget]::Process)
    }

    return [pscustomobject]@{
        ServiceName = $ServiceName
        Context = $target.Context
        Migrated = $true
        Message = 'completed'
    }
}

function Invoke-TenantPlacementDatabaseBackup {
    param(
        [Parameter(Mandatory)][string]$ConnectionName,
        [Parameter(Mandatory)][string]$ConnectionString,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [string]$PgDumpPath = 'pg_dump'
    )

    $databaseName = Get-PostgresDatabaseNameFromConnectionString -ConnectionString $ConnectionString
    $resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
    $stamp = Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ'
    $safeConnectionName = ($ConnectionName -replace '[^\w\-]', '_')
    $file = Join-Path $resolvedOutput "$safeConnectionName-$stamp.dump"

    & $PgDumpPath --format=custom --no-owner --no-acl --dbname=$ConnectionString --file=$file
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed for connection '$ConnectionName'." }

    $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
    $metadata = [pscustomobject]@{
        ConnectionName = $ConnectionName
        Database = $databaseName
        File = $file
        Sha256 = $hash.Hash
        CreatedAtUtc = $stamp
    }
    $metadata | ConvertTo-Json | Set-Content -LiteralPath "$file.sha256.json" -Encoding utf8
    return $metadata
}
