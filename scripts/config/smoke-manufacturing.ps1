[CmdletBinding()]
param(
    [string]$ComposeFile = 'docker/docker-compose.yml',
    [string]$ManufacturingContainer = 'his-hope-manufacturing',
    [string]$PostgresContainer = 'his-hope-postgres',
    [string]$RabbitHost = 'http://127.0.0.1:15672',
    [string]$RabbitUser = 'admin',
    [string]$RabbitPassword = 'admin',
    [string]$BuyerUrl = 'http://127.0.0.1:4205',
    [string]$OperatorUrl = 'http://127.0.0.1:4300',
    [string]$GatewayUrl = 'http://127.0.0.1:5000',
    [switch]$SkipBuyerIntegration
)

Add-Type -AssemblyName System.Net.Http

function Get-HttpStatusCode([string] $Uri) {
    try {
        return [int](Invoke-WebRequest -Uri $Uri -UseBasicParsing -ErrorAction Stop).StatusCode
    }
    catch {
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            return [int]$_.Exception.Response.StatusCode.value__
        }
        throw
    }
}

function Get-PostStatusCode([string] $Uri, [string] $Body) {
    try {
        return [int](Invoke-WebRequest -Uri $Uri -Method Post -ContentType 'application/json' -Body $Body -UseBasicParsing -ErrorAction Stop).StatusCode
    }
    catch {
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            return [int]$_.Exception.Response.StatusCode.value__
        }
        throw
    }
}

function Get-HttpResult([string] $Uri) {
    $client = [System.Net.Http.HttpClient]::new()
    try {
        $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            ContentType = [string]$response.Content.Headers.ContentType
            Content = $content
        }
    }
    finally {
        $client.Dispose()
    }
}

function Assert-ProblemDetails([string] $Name, [string] $Uri) {
    $response = Get-HttpResult $Uri
    $contentType = $response.ContentType
    if ($response.StatusCode -ne 401 -or $contentType -notmatch 'application/problem\+json') {
        throw "MANUFACTURING_SMOKE_FAIL $Name expected=401/problem+json actual=$($response.StatusCode)/$contentType"
    }
    $problem = ([string]$response.Content) | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$problem.errorCode)) {
        throw "MANUFACTURING_SMOKE_FAIL $Name missing-errorCode"
    }
    Write-Output "MANUFACTURING_SMOKE_PASS $Name status=$($response.StatusCode) errorCode=$($problem.errorCode)"
}

function Invoke-PsqlQuery([string] $Sql) {
    return ($Sql | & docker exec -i $PostgresContainer psql -U postgres -d manufacturingdb -tA -f - | Out-String)
}

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

$readyStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/health/ready'
if ($readyStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL readiness-endpoint status=$readyStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS readiness-endpoint status=$readyStatus"

$operatorHealthStatus = Get-HttpStatusCode "$OperatorUrl/health"
if ($operatorHealthStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL operator-health expected=200 actual=$operatorHealthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS operator-health status=$operatorHealthStatus"

$operatorRootStatus = Get-HttpStatusCode "$OperatorUrl/"
if ($operatorRootStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL operator-root expected=200 actual=$operatorRootStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS operator-root status=$operatorRootStatus"

$operatorRoutes = @('/dashboard', '/inventory/lots', '/production', '/procurement', '/recipes', '/product-specifications', '/quality-inspections', '/deviations', '/forecast', '/sales-allocation', '/maintenance', '/orders', '/users')
foreach ($route in $operatorRoutes) {
    $routeStatus = Get-HttpStatusCode "$OperatorUrl$route"
    if ($routeStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL operator-route route=$route expected=200 actual=$routeStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS operator-route route=$route status=$routeStatus"
}

$operatorApiStatus = Get-HttpStatusCode "$OperatorUrl/api/v1/manufacturing/suppliers"
if ($operatorApiStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL operator-api-protection expected=401 actual=$operatorApiStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS operator-api-protection status=$operatorApiStatus"
Assert-ProblemDetails 'manufacturing-error-contract' 'http://127.0.0.1:5050/api/v1/manufacturing/recipes'
Assert-ProblemDetails 'commerce-error-contract' 'http://127.0.0.1:5015/api/v1/commerce/products'

$lossReviewProtectionStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/production-batches/00000000-0000-0000-0000-000000000001/operations/00000000-0000-0000-0000-000000000002/loss-review'
if ($lossReviewProtectionStatus -notin @(401, 405)) { throw "MANUFACTURING_SMOKE_FAIL loss-review-method expected=401-or-405 actual=$lossReviewProtectionStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS loss-review-method status=$lossReviewProtectionStatus"
$lossReviewAuthStatus = Get-PostStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/production-batches/00000000-0000-0000-0000-000000000001/operations/00000000-0000-0000-0000-000000000002/loss-review' '{"decision":"Approved","reviewer":"smoke"}'
if ($lossReviewAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL loss-review-auth expected=401 actual=$lossReviewAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS loss-review-auth status=$lossReviewAuthStatus"

$protectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/events/receipts'
if ($protectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL protected-endpoint expected=401 actual=$protectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS protected-endpoint status=$protectedStatus"

$procurementProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/suppliers'
if ($procurementProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL procurement-protection expected=401 actual=$procurementProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS procurement-protection status=$procurementProtectedStatus"

$ledgerProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/lots/00000000-0000-0000-0000-000000000001/inventory-transactions'
if ($ledgerProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL ledger-protection expected=401 actual=$ledgerProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS ledger-protection status=$ledgerProtectedStatus"

$reservationProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/products/FX-MANGO-SOFT/fefo'
if ($reservationProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL reservation-protection expected=401 actual=$reservationProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS reservation-protection status=$reservationProtectedStatus"

$productionProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/production-orders'
if ($productionProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL production-protection expected=401 actual=$productionProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS production-protection status=$productionProtectedStatus"

$downtimeProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/machine-downtimes'
if ($downtimeProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL downtime-protection expected=401 actual=$downtimeProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS downtime-protection status=$downtimeProtectedStatus"

$dashboardProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/dashboard/manufacturing-summary'
if ($dashboardProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL dashboard-protection expected=401 actual=$dashboardProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS dashboard-protection status=$dashboardProtectedStatus"

$costProjectionProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/dashboard/cost-projection?productSku=FX-MANGO-SOFT&plannedQuantity=100'
if ($costProjectionProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL cost-projection-protection expected=401 actual=$costProjectionProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS cost-projection-protection status=$costProjectionProtectedStatus"

$exceptionsProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/dashboard/exceptions'
if ($exceptionsProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL exceptions-protection expected=401 actual=$exceptionsProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS exceptions-protection status=$exceptionsProtectedStatus"

$requirementsProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/planning/material-requirements'
if ($requirementsProtectedStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL material-requirements-protection expected=401 actual=$requirementsProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS material-requirements-protection status=$requirementsProtectedStatus"

$salesAllocationProtectedStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/sales/allocations/FX-MANGO-SOFT'
if ($salesAllocationProtectedStatus -notin @(401, 405)) { throw "MANUFACTURING_SMOKE_FAIL sales-allocation-method expected=401-or-405 actual=$salesAllocationProtectedStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS sales-allocation-method status=$salesAllocationProtectedStatus"

$maintenancePlannerAuthStatus = Get-PostStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/maintenance-work-orders/generate' '{}'
if ($maintenancePlannerAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL maintenance-planner-auth expected=401 actual=$maintenancePlannerAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS maintenance-planner-auth status=$maintenancePlannerAuthStatus"
$telemetryAuthStatus = Get-PostStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/machines/00000000-0000-0000-0000-000000000001/telemetry' '{"eventId":"00000000-0000-0000-0000-000000000002","observedAt":"2026-01-01T00:00:00Z","source":"smoke","meterName":"temperature_c","meterValue":1}'
if ($telemetryAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL telemetry-auth expected=401 actual=$telemetryAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS telemetry-auth status=$telemetryAuthStatus"
$oeeAuthStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/dashboard/oee'
if ($oeeAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL oee-auth expected=401 actual=$oeeAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS oee-auth status=$oeeAuthStatus"
$deviationAuthStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/deviations'
if ($deviationAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL deviation-auth expected=401 actual=$deviationAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS deviation-auth status=$deviationAuthStatus"
$productSpecificationAuthStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/product-specifications'
if ($productSpecificationAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL product-specification-auth expected=401 actual=$productSpecificationAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS product-specification-auth status=$productSpecificationAuthStatus"
$salesForecastAuthStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/sales/forecasts'
if ($salesForecastAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL sales-forecast-auth expected=401 actual=$salesForecastAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS sales-forecast-auth status=$salesForecastAuthStatus"
$forecastRequirementsAuthStatus = Get-HttpStatusCode 'http://127.0.0.1:5050/api/v1/manufacturing/planning/forecast-material-requirements/00000000-0000-0000-0000-000000000001'
if ($forecastRequirementsAuthStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL forecast-requirements-auth expected=401 actual=$forecastRequirementsAuthStatus" }
Write-Output "MANUFACTURING_SMOKE_PASS forecast-requirements-auth status=$forecastRequirementsAuthStatus"

if (-not $SkipBuyerIntegration) {
    $buyerRootStatus = Get-HttpStatusCode "$BuyerUrl/"
    if ($buyerRootStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL buyer-root expected=200 actual=$buyerRootStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS buyer-root status=$buyerRootStatus"
    $buyerRoutes = @('/home', '/catalog', '/cart', '/orders', '/profile', '/notifications')
    foreach ($route in $buyerRoutes) {
        $routeStatus = Get-HttpStatusCode "$BuyerUrl$route"
        if ($routeStatus -ne 200) { throw "MANUFACTURING_SMOKE_FAIL buyer-route route=$route expected=200 actual=$routeStatus" }
        Write-Output "MANUFACTURING_SMOKE_PASS buyer-route route=$route status=$routeStatus"
    }

    $buyerApiStatus = Get-HttpStatusCode "$BuyerUrl/api/v1/manufacturing/products/FX-MANGO-SOFT/availability"
    if ($buyerApiStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL buyer-api-protection expected=401 actual=$buyerApiStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS buyer-api-protection status=$buyerApiStatus"

    $gatewayApiStatus = Get-HttpStatusCode "$GatewayUrl/api/v1/manufacturing/products/FX-MANGO-SOFT/availability"
    if ($gatewayApiStatus -ne 401) { throw "MANUFACTURING_SMOKE_FAIL gateway-api-protection expected=401 actual=$gatewayApiStatus" }
    Write-Output "MANUFACTURING_SMOKE_PASS gateway-api-protection status=$gatewayApiStatus"
}

$migrationSql = 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";'
$migrationOutput = Invoke-PsqlQuery $migrationSql
if ($LASTEXITCODE -ne 0 -or $migrationOutput -notmatch 'AddTransformationInvariants' -or $migrationOutput -notmatch 'AddManufacturingRecipes' -or $migrationOutput -notmatch 'AddRecipeComponents' -or $migrationOutput -notmatch 'LinkTransformationsToRecipes' -or $migrationOutput -notmatch 'AddManufacturingMachines' -or $migrationOutput -notmatch 'LinkTransformationsToMachines' -or $migrationOutput -notmatch 'AddQualityInspections' -or $migrationOutput -notmatch 'AddProcurementInbound' -or $migrationOutput -notmatch 'AddInventoryLedger' -or $migrationOutput -notmatch 'AddLotReservations' -or $migrationOutput -notmatch 'AddProductionBatchOrchestration' -or $migrationOutput -notmatch 'AddProductionOutputLot' -or $migrationOutput -notmatch 'AddProductionBatchInputs' -or $migrationOutput -notmatch 'AddMachineDowntimes' -or $migrationOutput -notmatch 'AddRecipeLifecycle' -or $migrationOutput -notmatch 'AddLossReviews' -or $migrationOutput -notmatch 'AddMaintenanceWorkOrders' -or $migrationOutput -notmatch 'AddMachineTelemetry' -or $migrationOutput -notmatch 'AddManufacturingDeviations' -or $migrationOutput -notmatch 'AddProductSpecifications' -or $migrationOutput -notmatch 'LinkRecipesToProductSpecifications' -or $migrationOutput -notmatch 'AddSalesForecasts') {
    throw 'MANUFACTURING_SMOKE_FAIL database-migrations missing=AddSalesForecasts'
}
Write-Output 'MANUFACTURING_SMOKE_PASS database-migrations ...AddProductSpecifications,LinkRecipesToProductSpecifications,AddSalesForecasts'

$constraintSql = "select count(*) from pg_constraint where conname like 'CK_manufacturing_transformations%' or conname in ('CK_manufacturing_recipes_yield_range','CK_manufacturing_recipe_components_quantity_positive','CK_manufacturing_quality_moisture_range','CK_manufacturing_po_lines_quantity_positive');"
$constraintCount = (Invoke-PsqlQuery $constraintSql).Trim()
if ($LASTEXITCODE -ne 0 -or [int]$constraintCount -lt 7) {
    throw "MANUFACTURING_SMOKE_FAIL database-invariants expected_at_least=7 actual=$constraintCount"
}
Write-Output "MANUFACTURING_SMOKE_PASS database-invariants count=$constraintCount"

$procurementSql = "select count(*) from information_schema.tables where table_schema = 'public' and table_name in ('manufacturing_suppliers','manufacturing_purchase_orders','manufacturing_purchase_order_lines','manufacturing_inbound_receipts','manufacturing_inventory_transactions','manufacturing_lot_reservations','manufacturing_production_orders','manufacturing_production_batches','manufacturing_production_batch_inputs','manufacturing_operation_executions','manufacturing_loss_reviews','manufacturing_maintenance_work_orders','manufacturing_machine_telemetry','manufacturing_deviations','manufacturing_product_specifications','manufacturing_sales_forecasts');"
$procurementTableCount = (Invoke-PsqlQuery $procurementSql).Trim()
if ($LASTEXITCODE -ne 0 -or [int]$procurementTableCount -ne 16) {
    throw "MANUFACTURING_SMOKE_FAIL manufacturing-domain-tables expected=16 actual=$procurementTableCount"
}
Write-Output "MANUFACTURING_SMOKE_PASS manufacturing-domain-tables count=$procurementTableCount"

$outputLotColumnSql = "select count(*) from information_schema.columns where table_schema = 'public' and table_name = 'manufacturing_production_batches' and column_name = 'output_lot_id';"
$outputLotColumnCount = (Invoke-PsqlQuery $outputLotColumnSql).Trim()
if ($LASTEXITCODE -ne 0 -or [int]$outputLotColumnCount -ne 1) {
    throw "MANUFACTURING_SMOKE_FAIL production-output-lot-column expected=1 actual=$outputLotColumnCount"
}
Write-Output "MANUFACTURING_SMOKE_PASS production-output-lot-column count=$outputLotColumnCount"

$downtimeTableSql = "select count(*) from information_schema.tables where table_schema = 'public' and table_name = 'manufacturing_machine_downtimes';"
$downtimeTableCount = (Invoke-PsqlQuery $downtimeTableSql).Trim()
if ($LASTEXITCODE -ne 0 -or [int]$downtimeTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL machine-downtime-table expected=1 actual=$downtimeTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS machine-downtime-table count=$downtimeTableCount"

$lossReviewTableSql = "select count(*) from information_schema.tables where table_schema = 'public' and table_name = 'manufacturing_loss_reviews';"
$lossReviewTableCount = (Invoke-PsqlQuery $lossReviewTableSql).Trim()
if ($LASTEXITCODE -ne 0 -or [int]$lossReviewTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL loss-review-table expected=1 actual=$lossReviewTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS loss-review-table count=$lossReviewTableCount"
$maintenanceWorkOrderTableCount = Invoke-PsqlQuery 'select count(*) from information_schema.tables where table_schema = ''public'' and table_name = ''manufacturing_maintenance_work_orders'';'
if ($LASTEXITCODE -ne 0 -or [int]$maintenanceWorkOrderTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL maintenance-work-order-table expected=1 actual=$maintenanceWorkOrderTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS maintenance-work-order-table count=$maintenanceWorkOrderTableCount"
$telemetryTableCount = Invoke-PsqlQuery 'select count(*) from information_schema.tables where table_schema = ''public'' and table_name = ''manufacturing_machine_telemetry'';'
if ($LASTEXITCODE -ne 0 -or [int]$telemetryTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL machine-telemetry-table expected=1 actual=$telemetryTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS machine-telemetry-table count=$telemetryTableCount"
$deviationTableCount = Invoke-PsqlQuery 'select count(*) from information_schema.tables where table_schema = ''public'' and table_name = ''manufacturing_deviations'';'
if ($LASTEXITCODE -ne 0 -or [int]$deviationTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL deviation-table expected=1 actual=$deviationTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS deviation-table count=$deviationTableCount"
$productSpecificationTableCount = Invoke-PsqlQuery 'select count(*) from information_schema.tables where table_schema = ''public'' and table_name = ''manufacturing_product_specifications'';'
if ($LASTEXITCODE -ne 0 -or [int]$productSpecificationTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL product-specification-table expected=1 actual=$productSpecificationTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS product-specification-table count=$productSpecificationTableCount"
$recipeSpecificationColumnCount = Invoke-PsqlQuery 'select count(*) from information_schema.columns where table_schema = ''public'' and table_name = ''manufacturing_recipes'' and column_name = ''product_specification_id'';'
if ($LASTEXITCODE -ne 0 -or [int]$recipeSpecificationColumnCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL recipe-product-specification-column expected=1 actual=$recipeSpecificationColumnCount" }
Write-Output "MANUFACTURING_SMOKE_PASS recipe-product-specification-column count=$recipeSpecificationColumnCount"
$salesForecastTableCount = Invoke-PsqlQuery 'select count(*) from information_schema.tables where table_schema = ''public'' and table_name = ''manufacturing_sales_forecasts'';'
if ($LASTEXITCODE -ne 0 -or [int]$salesForecastTableCount -ne 1) { throw "MANUFACTURING_SMOKE_FAIL sales-forecast-table expected=1 actual=$salesForecastTableCount" }
Write-Output "MANUFACTURING_SMOKE_PASS sales-forecast-table count=$salesForecastTableCount"

$queueUri = "$RabbitHost/api/queues/%2F/manufacturing.analytics.v1"
$rabbitAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$RabbitUser`:$RabbitPassword"))
$rabbitHeaders = @{ Authorization = "Basic $rabbitAuth" }
$queue = $null
for ($attempt = 1; $attempt -le 15; $attempt++) {
    try { $queue = Invoke-RestMethod -Uri $queueUri -Headers $rabbitHeaders } catch { $queue = $null }
    if ($queue -and $queue.consumers -ge 1 -and $queue.messages_unacknowledged -eq 0) { break }
    Start-Sleep -Seconds 2
}
if ($queue.durable -ne $true -or $queue.consumers -lt 1 -or $queue.messages_unacknowledged -ne 0) {
    throw "MANUFACTURING_SMOKE_FAIL rabbit-consumer durable=$($queue.durable) consumers=$($queue.consumers) unack=$($queue.messages_unacknowledged)"
}
Write-Output "MANUFACTURING_SMOKE_PASS rabbit-consumer consumers=$($queue.consumers) unack=$($queue.messages_unacknowledged)"

Write-Output 'MANUFACTURING_SMOKE_PASS all-checks'
