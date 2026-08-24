[CmdletBinding()]
param(
    [string]$BaseUrl = $(if ($env:E2E_API_URL) { $env:E2E_API_URL } else { 'http://localhost:5000' }),
    [string]$Email = $env:E2E_EMAIL,
    [string]$Password = $env:E2E_PASSWORD,
    [string]$TenantKey = $(if ($env:E2E_TENANT_KEY) { $env:E2E_TENANT_KEY } else { 'manufacturing' })
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($Password)) {
    throw 'MANUFACTURING_AUTH_E2E_FAIL E2E_EMAIL and E2E_PASSWORD are required; keep credentials outside the repository.'
}

$BaseUrl = $BaseUrl.TrimEnd('/')
$ManufacturingUrl = "$BaseUrl/api/v1/manufacturing"
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()

function Invoke-JsonPost([string]$Path, [object]$Payload, [string]$CsrfToken) {
    $response = Invoke-WebRequest `
        -Uri "$ManufacturingUrl$Path" `
        -Method Post `
        -ContentType 'application/json' `
        -Headers @{ 'X-CSRF-Token' = $CsrfToken } `
        -Body ($Payload | ConvertTo-Json -Depth 10) `
        -WebSession $session `
        -UseBasicParsing

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "MANUFACTURING_AUTH_E2E_FAIL POST $Path status=$($response.StatusCode) body=$($response.Content)"
    }

    return $response.Content | ConvertFrom-Json
}

function Invoke-JsonGet([string]$Path) {
    $response = Invoke-WebRequest `
        -Uri "$ManufacturingUrl$Path" `
        -Method Get `
        -WebSession $session `
        -UseBasicParsing

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "MANUFACTURING_AUTH_E2E_FAIL GET $Path status=$($response.StatusCode) body=$($response.Content)"
    }

    return $response.Content | ConvertFrom-Json
}

$loginPayload = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-WebRequest `
    -Uri "$BaseUrl/api/v1/auth/login" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $loginPayload `
    -WebSession $session `
    -UseBasicParsing

if ($login.StatusCode -ne 200) {
    throw "MANUFACTURING_AUTH_E2E_FAIL login status=$($login.StatusCode)"
}

$csrfToken = ($session.Cookies.GetCookies($BaseUrl) | Where-Object Name -eq 'hishop_csrf').Value
if ([string]::IsNullOrWhiteSpace($csrfToken)) {
    throw 'MANUFACTURING_AUTH_E2E_FAIL hishop_csrf cookie was not issued by the login flow.'
}

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$recipe = Invoke-JsonPost '/recipes' @{
    tenantKey = $TenantKey
    productSku = "FG-E2E-$suffix"
    version = 1
    processStep = 'drying'
    outputUom = 'kg'
    targetYieldPercent = 80
    active = $true
    components = @(@{ ingredientSku = "RM-E2E-$suffix"; quantity = 1; uom = 'kg' })
} $csrfToken

$order = Invoke-JsonPost '/production-orders' @{
    orderNumber = "PO-E2E-$suffix"
    productSku = "FG-E2E-$suffix"
    recipeId = $recipe.id
    targetQuantity = 48
    outputUom = 'kg'
} $csrfToken

Invoke-JsonPost "/production-orders/$($order.id)/release" @{} $csrfToken | Out-Null

$lot = Invoke-JsonPost '/lots' @{
    tenantKey = $TenantKey
    sku = "RM-E2E-$suffix"
    quantity = 100
    uom = 'kg'
    disposition = 'Released'
} $csrfToken

$reservation = Invoke-JsonPost "/lots/$($lot.id)/reservations" @{
    referenceType = 'ProductionOrder'
    referenceId = $order.id
    quantity = 60
} $csrfToken

$batch = Invoke-JsonPost '/production-batches' @{
    productionOrderId = $order.id
    batchNumber = "BATCH-E2E-$suffix"
    plannedQuantity = 48
    inputs = @(@{ lotId = $lot.id; reservationId = $reservation.id; quantity = 60 })
} $csrfToken

Invoke-JsonPost "/production-batches/$($batch.id)/start" @{} $csrfToken | Out-Null
Invoke-JsonPost "/production-batches/$($batch.id)/operations" @{
    sequence = 1
    processStep = 'drying'
    operator = 'e2e'
    inputQuantity = 60
    outputQuantity = 48
    required = $true
    qcStatus = 'Pass'
} $csrfToken | Out-Null

$completed = Invoke-JsonPost "/production-batches/$($batch.id)/complete" @{} $csrfToken
Invoke-JsonPost '/quality-inspections' @{
    lotId = $completed.outputLotId
    tenantKey = $TenantKey
    status = 'Pass'
    moisturePercent = 12.5
    inspector = 'qc-e2e'
} $csrfToken | Out-Null

$kpi = Invoke-JsonGet '/dashboard/production-kpis'
if ([int]$kpi.completedBatchCount -lt 1) {
    throw "MANUFACTURING_AUTH_E2E_FAIL completedBatchCount=$($kpi.completedBatchCount)"
}

Write-Output "MANUFACTURING_AUTH_E2E_PASS order=$($order.id) batch=$($batch.id) outputLot=$($completed.outputLotId) kpiCompleted=$($kpi.completedBatchCount)"
