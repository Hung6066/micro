using His.Hope.AspNetCore.Authentication;
var builder = WebApplication.CreateBuilder(args);
var manufacturingConnection = builder.Configuration.GetConnectionString("ManufacturingDb")
    ?? "Host=localhost;Database=manufacturingdb;Username=postgres;Password=postgres";
builder.Services.AddManufacturingInfrastructure(manufacturingConnection);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ManufacturingDbContext>("manufacturing-db");
builder.Services.AddProblemDetails();
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.Services.MigrateManufacturingDatabase();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

var api = app.MapGroup("/api/v1/manufacturing").RequireAuthorization();

static string? TenantClaim(HttpContext context) =>
    context.User.FindFirst("tenant_id")?.Value ?? context.User.FindFirst("tenant")?.Value;

static bool TenantMatches(HttpContext context, string tenantKey) =>
    !string.IsNullOrWhiteSpace(TenantClaim(context)) &&
    string.Equals(TenantClaim(context), tenantKey, StringComparison.OrdinalIgnoreCase);

static bool TryResolveTenant(HttpContext context, string? requestedTenant, out string tenantKey)
{
    tenantKey = TenantClaim(context) ?? string.Empty;
    if (string.IsNullOrWhiteSpace(tenantKey)) return false;
    return string.IsNullOrWhiteSpace(requestedTenant) ||
        string.Equals(tenantKey, requestedTenant, StringComparison.OrdinalIgnoreCase);
}

api.MapGet("/lots/{lotId:guid}/genealogy", (Guid lotId, string? direction, HttpContext context, PostgresManufacturingStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey) || !store.LotBelongsToTenant(lotId, tenantKey))
        return Results.NotFound(new { error = "lot_not_found", lotId });

    var upstream = !string.Equals(direction, "downstream", StringComparison.OrdinalIgnoreCase);
    return Results.Ok(store.GetGenealogy(lotId, upstream, tenantKey));
});

api.MapGet("/lots", (string? tenantKey, string? sku, string? disposition, int? limit, HttpContext context, PostgresManufacturingStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetLots(scopedTenant, sku, disposition, limit ?? 50));
});

api.MapPost("/lots/{lotId:guid}/disposition", (Guid lotId, LotDispositionRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.SetLotDisposition(lotId, request.Disposition, tenantKey);
    return result.Error switch
    {
        "lot_not_found" => Results.NotFound(new { error = result.Error, lotId }),
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_disposition" => Results.BadRequest(new { error = result.Error }),
        _ => Results.Ok(result.Lot)
    };
});

api.MapGet("/lots/{lotId:guid}/quality-inspections", (Guid lotId, string? tenantKey, int? limit, HttpContext context, PostgresManufacturingStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetQualityInspections(lotId, scopedTenant, limit ?? 25));
});

api.MapGet("/lots/{lotId:guid}/inventory-transactions", (Guid lotId, int? limit, HttpContext context, PostgresManufacturingStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetInventoryTransactions(lotId, tenantKey, limit ?? 100));
});

api.MapPost("/lots/{lotId:guid}/reservations", (Guid lotId, CreateLotReservationRequest request, HttpContext context, ManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.ReferenceType) || request.ReferenceId == Guid.Empty || request.Quantity <= 0)
        return Results.BadRequest(new { error = "invalid_reservation" });
    var result = store.Reserve(tenantKey, lotId, request);
    return result.Error switch
    {
        "lot_not_found" => Results.NotFound(new { error = result.Error, lotId }),
        "tenant_mismatch" => Results.Forbid(),
        "lot_not_released" or "lot_expired" or "reservation_expired" or "invalid_reservation" => Results.UnprocessableEntity(new { error = result.Error }),
        "reservation_exceeds_available" => Results.Conflict(new { error = result.Error }),
        _ => Results.Created($"/api/v1/manufacturing/lots/{lotId}/reservations/{result.Reservation!.Id}", result.Reservation)
    };
});

api.MapPost("/reservations/{reservationId:guid}/release", (Guid reservationId, HttpContext context, ManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.Release(tenantKey, reservationId);
    return result.Error switch
    {
        "reservation_not_found" => Results.NotFound(new { error = result.Error, reservationId }),
        "tenant_mismatch" => Results.Forbid(),
        _ => Results.Ok(result.Reservation)
    };
});

api.MapGet("/products/{sku}/fefo", (string sku, int? limit, HttpContext context, ManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetFefo(tenantKey, sku, limit ?? 50));
});

api.MapPost("/quality-inspections", (CreateQualityInspectionRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    if (request.LotId == Guid.Empty || string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Inspector))
        return Results.BadRequest(new { error = "invalid_quality_inspection" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreateQualityInspection(request);
    return result.Error switch
    {
        "lot_not_found" => Results.NotFound(new { error = result.Error }),
        "tenant_mismatch" => Results.BadRequest(new { error = result.Error }),
        "invalid_inspection_status" or "invalid_moisture_percent" => Results.BadRequest(new { error = result.Error }),
        _ => Results.Created("/api/v1/manufacturing/quality-inspections", result.Inspection)
    };
});

api.MapGet("/transformations", (string? tenantKey, string? processStep, int? limit, HttpContext context, PostgresManufacturingStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetTransformationSummaries(scopedTenant, processStep, limit ?? 50));
});

api.MapGet("/recipes", (string? tenantKey, string? productSku, bool? active, int? limit, HttpContext context, PostgresManufacturingStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetRecipes(scopedTenant, productSku, active, limit ?? 50));
});

api.MapPost("/recipes", (CreateRecipeRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.ProductSku) ||
        request.Version <= 0 || string.IsNullOrWhiteSpace(request.ProcessStep) || string.IsNullOrWhiteSpace(request.OutputUom) ||
        request.Components is null || request.Components.Count == 0 || request.Components.Any(x => string.IsNullOrWhiteSpace(x.IngredientSku) || string.IsNullOrWhiteSpace(x.Uom) || x.Quantity <= 0) ||
        request.TargetYieldPercent <= 0 || request.TargetYieldPercent > 100)
        return Results.BadRequest(new { error = "invalid_recipe" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/recipes", store.CreateRecipe(request)); }
    catch (InvalidOperationException ex) when (ex.Message == "recipe_version_exists")
    { return Results.Conflict(new { error = ex.Message }); }
});

api.MapGet("/machines", (string? tenantKey, string? status, int? limit, HttpContext context, PostgresManufacturingStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetMachines(scopedTenant, status, limit ?? 50));
});

api.MapPost("/machines", (CreateMachineRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Status))
        return Results.BadRequest(new { error = "invalid_machine" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/machines", store.CreateMachine(request)); }
    catch (InvalidOperationException ex) when (ex.Message == "machine_code_exists")
    { return Results.Conflict(new { error = ex.Message }); }
});

api.MapPost("/machines/{machineId:guid}/maintenance", (Guid machineId, RecordMaintenanceRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.RecordMaintenance(machineId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        _ when result.Machine is not null => Results.Ok(result.Machine),
        _ => Results.NotFound(new { error = "machine_not_found", machineId })
    };
});

api.MapPost("/lots", (CreateLotRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) ||
        request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Uom))
        return Results.BadRequest(new { error = "invalid_lot", message = "tenantKey, sku, quantity and uom are required" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();

    var lot = store.CreateLot(request);
    return Results.Created($"/api/v1/manufacturing/lots/{lot.Id}", lot);
});

api.MapPost("/transformations", (CreateTransformationRequest request, HttpContext context, PostgresManufacturingStore store) =>
{
    if (request.Inputs is null || request.Inputs.Count == 0 || request.OutputQuantity <= 0 ||
        string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.OutputSku) ||
        string.IsNullOrWhiteSpace(request.OutputUom))
        return Results.BadRequest(new { error = "invalid_transformation" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();

    var result = store.CreateTransformation(request);
    return result.Error is not null
        ? result.Error is "duplicate_input_lot" or "input_lot_not_released" or "input_quantity_exceeds_available" or "reservation_not_found" or "reservation_mismatch" or "reservation_unavailable"
            ? Results.UnprocessableEntity(new { error = result.Error })
            : Results.BadRequest(new { error = result.Error })
        : Results.Created($"/api/v1/manufacturing/transformations/{result.Transformation!.Id}", result.Transformation);
});

api.MapGet("/products/{sku}/availability", (string sku, string? tenantKey, HttpContext context, PostgresManufacturingStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetAvailability(scopedTenant, sku));
});

api.MapGet("/dashboard/manufacturing-summary", (HttpContext context, PostgresManufacturingStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey) ? Results.Forbid() : Results.Ok(store.GetDashboardSummary(tenantKey));
});

api.MapGet("/dashboard/cost-projection", (string productSku, int? recipeVersion, decimal? plannedQuantity, HttpContext context, PostgresManufacturingStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(productSku) || !plannedQuantity.HasValue || plannedQuantity <= 0)
        return Results.BadRequest(new { error = "invalid_cost_projection" });

    var projection = store.GetCostProjection(tenantKey, productSku, recipeVersion, plannedQuantity.Value);
    return projection is null
        ? Results.NotFound(new { error = "recipe_not_found", productSku, recipeVersion })
        : Results.Ok(projection);
});

api.MapGet("/events/receipts", (string? eventType, int? limit, PostgresManufacturingStore store) =>
    Results.Ok(store.GetEventReceipts(eventType, limit ?? 25)));

api.MapGet("/suppliers", (bool? active, int? limit, HttpContext context, ManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetSuppliers(tenantKey, active, limit ?? 100));
});

api.MapPost("/suppliers", (CreateSupplierRequest request, HttpContext context, ManufacturingProcurementStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "invalid_supplier" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/suppliers", store.CreateSupplier(request)); }
    catch (InvalidOperationException ex) when (ex.Message == "supplier_code_exists")
    { return Results.Conflict(new { error = ex.Message }); }
});

api.MapGet("/purchase-orders", (string? status, int? limit, HttpContext context, ManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetPurchaseOrders(tenantKey, status, limit ?? 100));
});

api.MapPost("/purchase-orders", (CreatePurchaseOrderRequest request, HttpContext context, ManufacturingProcurementStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.OrderNumber) ||
        string.IsNullOrWhiteSpace(request.Currency) || request.SupplierId == Guid.Empty || request.Lines is null || request.Lines.Count == 0 ||
        request.Lines.Any(x => string.IsNullOrWhiteSpace(x.MaterialSku) || string.IsNullOrWhiteSpace(x.Uom) || x.OrderedQuantity <= 0 || x.UnitPrice < 0))
        return Results.BadRequest(new { error = "invalid_purchase_order" });
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreatePurchaseOrder(request);
    return result.Error switch
    {
        "supplier_not_found" or "supplier_inactive" => Results.NotFound(new { error = result.Error }),
        "tenant_mismatch" => Results.Forbid(),
        "purchase_order_exists" => Results.Conflict(new { error = result.Error }),
        "invalid_purchase_order_status" => Results.BadRequest(new { error = result.Error }),
        _ => Results.Created("/api/v1/manufacturing/purchase-orders", result.Order)
    };
});

api.MapPost("/purchase-orders/{purchaseOrderId:guid}/receipts", (Guid purchaseOrderId, ReceiveInboundLotRequest request, HttpContext context, ManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (request.PurchaseOrderId != purchaseOrderId || string.IsNullOrWhiteSpace(request.MaterialSku) || string.IsNullOrWhiteSpace(request.ReceiptNumber) ||
        string.IsNullOrWhiteSpace(request.SupplierLotCode) || string.IsNullOrWhiteSpace(request.FacilityId) || request.PurchaseOrderLineId == Guid.Empty)
        return Results.BadRequest(new { error = "invalid_inbound_receipt" });
    var result = store.ReceiveInboundLot(tenantKey, request);
    return result.Error switch
    {
        "purchase_order_not_found" or "purchase_order_line_not_found" => Results.NotFound(new { error = result.Error }),
        "tenant_mismatch" => Results.Forbid(),
        "invalid_receipt_quantity" or "material_mismatch" or "purchase_order_not_receivable" => Results.BadRequest(new { error = result.Error }),
        "receipt_number_exists" or "supplier_lot_exists" => Results.Conflict(new { error = result.Error }),
        "over_receipt" => Results.UnprocessableEntity(new { error = result.Error }),
        _ => Results.Created($"/api/v1/manufacturing/lots/{result.Receipt!.LotId}/inbound-receipt", result.Receipt)
    };
});

api.MapGet("/production-orders", (string? status, int? limit, HttpContext context, ManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey) ? Results.Forbid() : Results.Ok(store.GetOrders(tenantKey, status, limit ?? 100));
});

api.MapPost("/production-orders", (CreateProductionOrderRequest request, HttpContext context, ManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.ProductSku) || string.IsNullOrWhiteSpace(request.OutputUom) || request.RecipeId == Guid.Empty || request.TargetQuantity <= 0)
        return Results.BadRequest(new { error = "invalid_production_order" });
    var result = store.CreateOrder(tenantKey, request);
    return result.Error switch
    {
        "invalid_production_order" => Results.BadRequest(new { error = result.Error }),
        "production_order_exists" => Results.Conflict(new { error = result.Error }),
        "recipe_not_found" => Results.NotFound(new { error = result.Error }),
        "recipe_unavailable" or "recipe_product_mismatch" => Results.UnprocessableEntity(new { error = result.Error }),
        _ => Results.Created("/api/v1/manufacturing/production-orders", result.Order)
    };
});

api.MapPost("/production-orders/{orderId:guid}/release", (Guid orderId, HttpContext context, ManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ReleaseOrder(tenantKey, orderId);
    return result.Error switch
    {
        "production_order_not_found" => Results.NotFound(new { error = result.Error, orderId }),
        "tenant_mismatch" => Results.Forbid(),
        _ => Results.Ok(result.Order)
    };
});

api.MapGet("/production-batches", (string? status, int? limit, HttpContext context, ManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey) ? Results.Forbid() : Results.Ok(store.GetBatches(tenantKey, status, limit ?? 100));
});

api.MapPost("/production-batches", (CreateProductionBatchRequest request, HttpContext context, ManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (request.ProductionOrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.BatchNumber) || request.PlannedQuantity is <= 0)
        return Results.BadRequest(new { error = "invalid_production_batch" });
    var result = store.CreateBatch(tenantKey, request);
    return result.Error switch
    {
        "production_order_not_found" or "machine_not_found" => Results.NotFound(new { error = result.Error }),
        "tenant_mismatch" => Results.Forbid(),
        "invalid_production_batch" or "production_order_not_released" or "machine_unavailable" => Results.UnprocessableEntity(new { error = result.Error }),
        "production_batch_exists" => Results.Conflict(new { error = result.Error }),
        _ => Results.Created("/api/v1/manufacturing/production-batches", result.Batch)
    };
});

api.MapPost("/production-batches/{batchId:guid}/start", (Guid batchId, HttpContext context, ManufacturingProductionStore store) => BatchStatusResult(batchId, "Started", context, store));
api.MapPost("/production-batches/{batchId:guid}/pause", (Guid batchId, HttpContext context, ManufacturingProductionStore store) => BatchStatusResult(batchId, "Paused", context, store));
api.MapPost("/production-batches/{batchId:guid}/resume", (Guid batchId, HttpContext context, ManufacturingProductionStore store) => BatchStatusResult(batchId, "Started", context, store));
api.MapPost("/production-batches/{batchId:guid}/complete", (Guid batchId, HttpContext context, ManufacturingProductionStore store) => BatchStatusResult(batchId, "Completed", context, store));

api.MapPost("/production-batches/{batchId:guid}/operations", (Guid batchId, RecordOperationRequest request, HttpContext context, ManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (request.Sequence < 0 || string.IsNullOrWhiteSpace(request.ProcessStep) || string.IsNullOrWhiteSpace(request.Operator) || request.InputQuantity <= 0 || request.OutputQuantity < 0)
        return Results.BadRequest(new { error = "invalid_operation_measurement" });
    var result = store.RecordOperation(tenantKey, batchId, request);
    return result.Error switch
    {
        "production_batch_not_found" => Results.NotFound(new { error = result.Error }),
        "tenant_mismatch" => Results.Forbid(),
        "batch_not_started" or "invalid_operation_measurement" => Results.UnprocessableEntity(new { error = result.Error }),
        "operation_sequence_exists" => Results.Conflict(new { error = result.Error }),
        _ => Results.Created($"/api/v1/manufacturing/production-batches/{batchId}/operations", result.Operation)
    };
});

app.Run();

static IResult BatchStatusResult(Guid batchId, string targetStatus, HttpContext context, ManufacturingProductionStore store)
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ChangeBatchStatus(tenantKey, batchId, targetStatus);
    return result.Error switch
    {
        "production_batch_not_found" => Results.NotFound(new { error = result.Error, batchId }),
        "tenant_mismatch" => Results.Forbid(),
        "required_operation_incomplete" or "quality_gate_incomplete" or "machine_unavailable" or "invalid_batch_transition" => Results.UnprocessableEntity(new { error = result.Error }),
        _ => Results.Ok(result.Batch)
    };
}


