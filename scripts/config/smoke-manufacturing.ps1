[CmdletBinding()]
param(
    [string]$ComposeFile = 'docker/docker-compose.yml',
    [string]$ManufacturingContainer = 'his-hope-manufacturing',
    [string]$PostgresContainer = 'his-hope-postgres',
    [string]$RabbitHost = 'http://127.0.0.1:15672',
    [string]$RabbitUser = 'admin',
    [string]$RabbitPassword = 'admin',
    [string]$BuyerUrl = 'http://127.0.0.1:4205',
    [string]$GatewayUrl = 'http://127.0.0.1:5000',
    [switch]$SkipBuyerIntegration
)

$ErrorActionPreference = 'Stop'

function Assert-CommandSuccess([string]$Name, [scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "MANUFACTURING_SMOKE_FAIL $Name exit_code=$LASTEXITCODE"
    }
    Write-Output "MANUFACTURING_SMOKE_PASS $Name"
}

Assert-CommandSuccess 'compose-config' { docker compose -f $ComposeFile config --quiet }

$health = ''
for ($attempt = 1; $attempt -le 15; $attempt++) {
    $health = (& docker inspect -f '{{.State.Health.Status}}' $ManufacturingContainer).Trim()
    if ($LASTEXITCODE -eq 0 -and $health -eq 'healthy') { break }
    Start-Sleep -Seconds 2
}
if ($LASTEXITCODE -ne 0 -or $health -ne 'healthy') {
    throw "MANUFACTURING_SMOKE_FAIL manufacturing-health actual=$health"
}
Write-Output "MANUFACTURING_SMOKE_PASS manufacturing-health status=$health"

$healthStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/health' -UseBasicParsing).StatusCode
if ($healthStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL health-endpoint status=$healthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS health-endpoint status=$healthStatus"

$readyStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/health/ready' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($readyStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL readiness-endpoint status=$readyStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS readiness-endpoint status=$readyStatus"

$protectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/events/receipts' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($protectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL protected-endpoint expected=401 actual=$protectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS protected-endpoint status=$protectedStatus"

$procurementProtectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/suppliers' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($procurementProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL procurement-protection expected=401 actual=$procurementProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS procurement-protection status=$procurementProtectedStatus"

$ledgerProtectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/lots/00000000-0000-0000-0000-000000000001/inventory-transactions' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($ledgerProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL ledger-protection expected=401 actual=$ledgerProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS ledger-protection status=$ledgerProtectedStatus"

$reservationProtectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/products/FX-MANGO-SOFT/fefo' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($reservationProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL reservation-protection expected=401 actual=$reservationProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS reservation-protection status=$reservationProtectedStatus"

$productionProtectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/production-orders' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($productionProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL production-protection expected=401 actual=$productionProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS production-protection status=$productionProtectedStatus"

$dashboardProtectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/dashboard/manufacturing-summary' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($dashboardProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL dashboard-protection expected=401 actual=$dashboardProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS dashboard-protection status=$dashboardProtectedStatus"

$costProjectionProtectedStatus = (Invoke-WebRequest -Uri 'http://127.0.0.1:5050/api/v1/manufacturing/dashboard/cost-projection?productSku=FX-MANGO-SOFT&plannedQuantity=100' -UseBasicParsing -SkipHttpErrorCheck).StatusCode
if ($costProjectionProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL cost-projection-protection expected=401 actual=$costProjectionProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS cost-projection-protection status=$costProjectionProtectedStatus"

if (-not $SkipBuyerIntegration) {
    $buyerRootStatus = (Invoke-WebRequest -Uri "$BuyerUrl/" -UseBasicParsing -SkipHttpErrorCheck).StatusCode
    if ($buyerRootStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL buyer-root expected=200 actual=$buyerRootStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS buyer-root status=$buyerRootStatus"

    $buyerApiStatus = (Invoke-WebRequest -Uri "$BuyerUrl/api/v1/manufacturing/products/FX-MANGO-SOFT/availability" -UseBasicParsing -SkipHttpErrorCheck).StatusCode
    if ($buyerApiStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL buyer-api-protection expected=401 actual=$buyerApiStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS buyer-api-protection status=$buyerApiStatus"

    $gatewayApiStatus = (Invoke-WebRequest -Uri "$GatewayUrl/api/v1/manufacturing/products/FX-MANGO-SOFT/availability" -UseBasicParsing -SkipHttpErrorCheck).StatusCode
    if ($gatewayApiStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL gateway-api-protection expected=401 actual=$gatewayApiStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS gateway-api-protection status=$gatewayApiStatus"
}

$migrationSql = 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";'
$migrationOutput = (& docker exec $PostgresContainer psql -U postgres -d manufacturingdb -tA -c $migrationSql | Out-String)
if ($LASTEXITCODE -ne 0 -or $migrationOutput -notmatch 'AddTransformationInvariants' -or $migrationOutput -notmatch 'AddManufacturingRecipes' -or $migrationOutput -notmatch 'AddRecipeComponents' -or $migrationOutput -notmatch 'LinkTransformationsToRecipes' -or $migrationOutput -notmatch 'AddManufacturingMachines' -or $migrationOutput -notmatch 'LinkTransformationsToMachines' -or $migrationOutput -notmatch 'AddQualityInspections' -or $migrationOutput -notmatch 'AddProcurementInbound' -or $migrationOutput -notmatch 'AddInventoryLedger' -or $migrationOutput -notmatch 'AddLotReservations' -or $migrationOutput -notmatch 'AddProductionBatchOrchestration') {
    throw 'MANUFACTURING_SMOKE_FAIL database-migrations missing=AddTransformationInvariants,AddManufacturingRecipes,AddRecipeComponents,LinkTransformationsToRecipes,AddManufacturingMachines,LinkTransformationsToMachines,AddQualityInspections,AddProcurementInbound,AddInventoryLedger,AddLotReservations,AddProductionBatchOrchestration'
}
Write-Output 'MANUFACTURING_SMOKE_PASS database-migrations AddTransformationInvariants,AddManufacturingRecipes,AddRecipeComponents,LinkTransformationsToRecipes,AddManufacturingMachines,LinkTransformationsToMachines,AddQualityInspections,AddProcurementInbound,AddInventoryLedger,AddLotReservations,AddProductionBatchOrchestration'

$constraintSql = "select count(*) from pg_constraint where conname like 'CK_manufacturing_transformations%' or conname in ('CK_manufacturing_recipes_yield_range','CK_manufacturing_recipe_components_quantity_positive','CK_manufacturing_quality_moisture_range','CK_manufacturing_po_lines_quantity_positive');"
$constraintCount = ((& docker exec $PostgresContainer psql -U postgres -d manufacturingdb -tA -c $constraintSql).Trim())
if ($LASTEXITCODE -ne 0 -or [int]$constraintCount -lt 7) {
    throw "MANUFACTURING_SMOKE_FAIL database-invariants expected_at_least=7 actual=$constraintCount"
}
Write-Output "MANUFACTURING_SMOKE_PASS database-invariants count=$constraintCount"

$procurementSql = "select count(*) from information_schema.tables where table_schema = 'public' and table_name in ('manufacturing_suppliers','manufacturing_purchase_orders','manufacturing_purchase_order_lines','manufacturing_inbound_receipts','manufacturing_inventory_transactions','manufacturing_lot_reservations','manufacturing_production_orders','manufacturing_production_batches','manufacturing_operation_executions');"
$procurementTableCount = ((& docker exec $PostgresContainer psql -U postgres -d manufacturingdb -tA -c $procurementSql).Trim())
if ($LASTEXITCODE -ne 0 -or [int]$procurementTableCount -ne 9) {
    throw "MANUFACTURING_SMOKE_FAIL manufacturing-domain-tables expected=9 actual=$procurementTableCount"
}
Write-Output "MANUFACTURING_SMOKE_PASS manufacturing-domain-tables count=$procurementTableCount"

$queueUri = "$RabbitHost/api/queues/%2F/manufacturing.analytics.v1"
$rabbitCredential = New-Object PSCredential($RabbitUser, (ConvertTo-SecureString $RabbitPassword -AsPlainText -Force))
$queue = $null
for ($attempt = 1; $attempt -le 15; $attempt++) {
    try { $queue = Invoke-RestMethod -Uri $queueUri -Authentication Basic -AllowUnencryptedAuthentication -Credential $rabbitCredential } catch { $queue = $null }
    if ($queue -and $queue.consumers -ge 1 -and $queue.messages_unacknowledged -eq 0) { break }
    Start-Sleep -Seconds 2
}
if ($queue.durable -ne $true -or $queue.consumers -lt 1 -or $queue.messages_unacknowledged -ne 0) {
    throw "MANUFACTURING_SMOKE_FAIL rabbit-consumer durable=$($queue.durable) consumers=$($queue.consumers) unack=$($queue.messages_unacknowledged)"
}
Write-Output "MANUFACTURING_SMOKE_PASS rabbit-consumer consumers=$($queue.consumers) unack=$($queue.messages_unacknowledged)"

Write-Output 'MANUFACTURING_SMOKE_PASS all-checks'
