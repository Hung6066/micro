[CmdletBinding()]
param(
    [string]$TenantKey = 'customer-acme',
    [string]$PlacementFile = 'config/conglomerate/tenant-placement.v1.json',
    [Parameter(Mandatory)]
    [string]$ConnectionStringsFile,
    [string]$SharedConnectionString,
    [switch]$SkipSchemaTest,
    [switch]$SkipSharedRoutingTest
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TenantPlacementOps.Common.ps1')

function Resolve-SharedManufacturingConnectionString {
    param([string]$Explicit)

    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        return $Explicit.Trim()
    }

    foreach ($candidate in @(
            $env:ConnectionStrings__ManufacturingDb,
            $env:MANUFACTURING_TEST_POSTGRES_CONNECTION,
            $env:DATABASE_MANUFACTURING_URL)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return $candidate.Trim()
        }
    }

    return 'Host=localhost;Port=5432;Database=manufacturingdb;Username=postgres;Password=postgres'
}

$root = Get-TenantPlacementRepoRoot
Push-Location $root
try {
    Write-Host "== Validate placement manifest (dedicated connection only required) =="
    ./scripts/tenant-placement/Invoke-TenantPlacementOnboarding.ps1 `
        -TenantKey $TenantKey `
        -PlacementFile $PlacementFile `
        -ConnectionStringsFile $ConnectionStringsFile `
        -Phase Validate `
        -IncludeInactive | Out-String | Write-Host

    $connections = Get-Content -LiteralPath $ConnectionStringsFile -Raw | ConvertFrom-Json
    $dedicatedName = 'ManufacturingDb_customer_acme'
    if (-not $connections.PSObject.Properties.Name.Contains($dedicatedName) -or
        [string]::IsNullOrWhiteSpace([string]$connections.$dedicatedName)) {
        throw "Connection strings file must include non-empty '$dedicatedName'."
    }

    $sharedConnection = Resolve-SharedManufacturingConnectionString -Explicit $SharedConnectionString
    $sharedDatabase = Get-PostgresDatabaseNameFromConnectionString -ConnectionString $sharedConnection
    $dedicatedDatabase = Get-PostgresDatabaseNameFromConnectionString -ConnectionString ([string]$connections.$dedicatedName)

    Write-Host "== Connectivity targets =="
    Write-Host "  ManufacturingDb (shared, from env/default) -> database=$sharedDatabase"
    Write-Host "  $dedicatedName (dedicated, from file) -> database=$dedicatedDatabase"

    $env:TENANT_PLACEMENT_CONNECTIONS_FILE = (Resolve-Path -LiteralPath $ConnectionStringsFile).Path
    $env:TENANT_PLACEMENT_FILE = (Resolve-Path -LiteralPath $PlacementFile).Path
    $env:ConnectionStrings__ManufacturingDb = $sharedConnection
    $env:MANUFACTURING_DB_CUSTOMER_ACME_CONNECTION = [string]$connections.$dedicatedName
    if ($SkipSharedRoutingTest) {
        $env:CUSTOMER_ACME_SKIP_SHARED_ROUTING_TEST = 'true'
    } else {
        Remove-Item Env:CUSTOMER_ACME_SKIP_SHARED_ROUTING_TEST -ErrorAction SilentlyContinue
    }

    Write-Host "== dotnet test (external DB routing) =="
    $filter = if ($SkipSchemaTest) {
        'FullyQualifiedName~Customer_acme_routes_to_external_database'
    } else {
        'FullyQualifiedName~CustomerAcmeExternalDatabaseRoutingTests'
    }

    dotnet test tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/ManufacturingService.Integration.Tests.csproj `
        --filter $filter `
        --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        throw "External database routing tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
