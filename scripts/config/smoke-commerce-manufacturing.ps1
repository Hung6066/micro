[CmdletBinding()]
param(
    [string]$RabbitContainer = "his-hope-rabbitmq",
    [string]$PostgresContainer = "his-hope-postgres",
    [string]$RabbitUser = "admin",
    [string]$RabbitPassword = "admin",
    [string]$TenantKey = "customer-factory-x",
    [string]$Sku = "FX-MANGO-SOFT",
    [int]$Quantity = 1
)

$ErrorActionPreference = "Stop"
$orderId = [guid]::NewGuid()
$eventId = [guid]::NewGuid()

function Invoke-RabbitPublish([guid]$EventId) {
    $payload = @{ 
        EventId = $EventId
        SchemaVersion = 1
        OccurredAt = [DateTime]::UtcNow.ToString("o")
        OrderId = $orderId
        TenantKey = $TenantKey
        BuyerUserId = "runtime-smoke"
        TotalAmount = 0
        Lines = @(@{ ProductId = [guid]::NewGuid(); Sku = $Sku; Quantity = $Quantity; UnitPrice = 0 })
        CorrelationId = $orderId
        CausationId = $null
    } | ConvertTo-Json -Compress -Depth 5

    $credentials = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$RabbitUser`:$RabbitPassword"))
    $headers = @{ Authorization = "Basic $credentials" }
    $request = @{
        properties = @{ delivery_mode = 2; content_type = "application/json"; type = "Commerce.OrderPlaced.v1" }
        routing_key = "Commerce.OrderPlaced.v1"
        payload = $payload
        payload_encoding = "string"
    } | ConvertTo-Json -Compress -Depth 8
    Invoke-RestMethod -Method Post -Uri "http://localhost:15672/api/exchanges/%2F/his-hope.manufacturing/publish" -Headers $headers -ContentType "application/json" -Body $request | Out-Host
}

function Get-AllocationState {
    $sql = @"
select count(*) || ',' || coalesce(sum("Quantity"),0) from manufacturing_lot_reservations where "ReferenceId"='$orderId';
select count(*) from manufacturing_event_receipts where "AggregateId"='$orderId';
"@
    ($sql | & docker exec -i $PostgresContainer psql -U postgres -d manufacturingdb -t -A) -join "`n"
}

Invoke-RabbitPublish $eventId
$first = $null
for ($i = 0; $i -lt 20; $i++) {
    $first = Get-AllocationState
    if ($first -match "1,$Quantity" -and $first -match "`n1") { break }
    Start-Sleep -Seconds 1
}
if ($first -notmatch "1,$Quantity" -or $first -notmatch "`n1") {
    throw "Commerce.OrderPlaced.v1 was not allocated: $first"
}

Invoke-RabbitPublish ([guid]::NewGuid())
Start-Sleep -Seconds 2
$replay = Get-AllocationState
if ($replay -notmatch "1,$Quantity" -or $replay -notmatch "`n1") {
    throw "Duplicate replay changed allocation state: $replay"
}

Write-Output "PASS Commerce -> RabbitMQ -> Manufacturing allocation; duplicate replay remained idempotent for order $orderId."
