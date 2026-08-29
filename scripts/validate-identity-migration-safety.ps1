[CmdletBinding()]
param(
    [string]$MigrationsPath = "src/Services/IdentityService/IdentityService.Infrastructure/Persistence/Migrations"
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$path = if ([IO.Path]::IsPathRooted($MigrationsPath)) { $MigrationsPath } else { Join-Path $root $MigrationsPath }
if (-not (Test-Path -LiteralPath $path -PathType Container)) {
    throw "Identity migrations path not found: $path"
}

$criticalTables = @(
    'asp_net_users', 'asp_net_user_claims', 'asp_net_user_roles',
    'iam_permission_set_assignments', 'iam_group_memberships',
    'openiddict_tokens', 'audit_logs', 'security_events'
)
$unsafe = [System.Collections.Generic.List[string]]::new()
$checked = 0
$protectedIndexes = @('ix_security_events_timestamp_brin', 'ix_audit_logs_timestamp_brin')

Get-ChildItem -LiteralPath $path -Filter '*.cs' -File |
    Where-Object { $_.Name -notlike '*.Designer.cs' } |
    ForEach-Object {
        $checked++
        $content = Get-Content -LiteralPath $_.FullName -Raw
        $up = ($content -split 'protected override void Down', 2)[0]
        foreach ($table in $criticalTables) {
            $quoted = [regex]::Escape('"' + $table + '"')
            if ($up -match ('DropTable\s*\([\s\S]{0,300}?name:\s*' + $quoted) -or
                $up -match ('DropColumn\s*\([\s\S]{0,300}?table:\s*' + $quoted) -or
                $up -match ('RenameTable\s*\([\s\S]{0,300}?name:\s*' + $quoted)) {
                $unsafe.Add("$($_.Name): destructive operation detected for $table")
            }
        }
        foreach ($indexName in $protectedIndexes) {
            if ($up -match ('DropIndex\s*\([\s\S]{0,300}?name:\s*["'']?' + [regex]::Escape($indexName))) {
                $unsafe.Add("$($_.Name): protected time-series index $indexName must not be dropped by a migration")
            }
        }
    }

if ($unsafe.Count -gt 0) {
    $unsafe | ForEach-Object { Write-Error $_ }
    exit 80
}

$scaleMigration = Get-ChildItem -LiteralPath $path -Filter '20260829040337_AddTimeSeriesScaleIndexes.cs' -File | Select-Object -First 1
if ($null -eq $scaleMigration) {
    throw 'Identity migration safety failed: additive time-series scale migration is missing.'
}
$scaleContent = Get-Content -LiteralPath $scaleMigration.FullName -Raw
foreach ($indexName in @('ix_security_events_timestamp_brin', 'ix_audit_logs_timestamp_brin')) {
    if ($scaleContent -notmatch [regex]::Escape($indexName) -or $scaleContent -notmatch 'CREATE INDEX IF NOT EXISTS') {
        throw "Identity migration safety failed: additive BRIN index $indexName is missing."
    }
}
if ($scaleContent -notmatch 'USING BRIN') {
    throw 'Identity migration safety failed: time-series indexes must use BRIN.'
}
if ($scaleContent -notmatch 'timestamp\);') {
    throw 'Identity migration safety failed: raw SQL index statements must be terminated.'
}

$contextPath = Join-Path (Split-Path -Parent $path) 'IdentityDbContext.cs'
$snapshotPath = Join-Path $path 'IdentityDbContextModelSnapshot.cs'
if (-not (Test-Path -LiteralPath $contextPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $snapshotPath -PathType Leaf)) {
    throw 'Identity migration safety failed: DbContext or model snapshot is missing.'
}
$contextContent = Get-Content -LiteralPath $contextPath -Raw
$snapshotContent = Get-Content -LiteralPath $snapshotPath -Raw
foreach ($indexName in @('ix_asp_net_users_created_at_id', 'ix_asp_net_users_active_created_at_id')) {
    if ($contextContent -notmatch [regex]::Escape($indexName)) {
        throw "Identity migration safety failed: DbContext declaration for $indexName is missing."
    }
    if ($snapshotContent -notmatch [regex]::Escape($indexName)) {
        throw "Identity migration safety failed: EF model snapshot declaration for $indexName is missing."
    }
    $migration = Get-ChildItem -LiteralPath $path -Filter '*.cs' -File |
        Where-Object { $_.Name -notlike '*.Designer.cs' } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw } |
        Where-Object { $_ -match [regex]::Escape($indexName) }
    if ($null -eq $migration) {
        throw "Identity migration safety failed: migration operation for $indexName is missing."
    }
}

Write-Output "Identity migration safety PASS: checked $checked migration files; no destructive operation targets critical identity tables."
