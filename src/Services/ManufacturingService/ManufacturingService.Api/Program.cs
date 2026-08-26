using His.Hope.AspNetCore.Authentication;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
var builder = WebApplication.CreateBuilder(args);
var manufacturingConnection = builder.Configuration.GetConnectionString("ManufacturingDb")
    ?? "Host=localhost;Database=manufacturingdb;Username=postgres;Password=postgres";
builder.Services.AddManufacturingInfrastructure(manufacturingConnection);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ManufacturingDbContext>("manufacturing-db");
builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "ManufacturingService");
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddHisHopeAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseHisHopeServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.Services.MigrateManufacturingDatabase();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

var api = app.MapGroup("/api/v1/manufacturing").RequireAuthorization();

static string? TenantClaim(HttpContext context) =>
    ManufacturingHttpExtensions.ResolveActiveTenant(context);

static bool TenantMatches(HttpContext context, string tenantKey) =>
    ManufacturingHttpExtensions.TenantMatches(context, tenantKey);

static bool TryResolveTenant(HttpContext context, string? requestedTenant, out string tenantKey) =>
    ManufacturingHttpExtensions.TryResolveTenant(context, requestedTenant, out tenantKey);

static IResult ManufacturingProblem(int statusCode, string errorCode) =>
    Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

static IResult ChangeRecipeLifecycle(Guid recipeId, string status, RecipeLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store)
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ChangeRecipeLifecycle(recipeId, tenantKey, status, request);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        "recipe_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "invalid_recipe_actor" or "invalid_recipe_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ when result.Recipe is not null => Results.Ok(result.Recipe),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "recipe_not_found")
    };
}

api.MapGet("/lots/{lotId:guid}/genealogy", (Guid lotId, string? direction, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey) || !store.LotBelongsToTenant(lotId, tenantKey))
        return ManufacturingProblem(StatusCodes.Status404NotFound, "lot_not_found");

    var upstream = !string.Equals(direction, "downstream", StringComparison.OrdinalIgnoreCase);
    return Results.Ok(store.GetGenealogy(lotId, upstream, tenantKey));
});

api.MapGet("/lots", (string? tenantKey, string? sku, string? disposition, int? limit, HttpContext context, IManufacturingProductionStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetLots(scopedTenant, sku, disposition, limit ?? 50));
});

api.MapPost("/lots/{lotId:guid}/disposition", (Guid lotId, LotDispositionRequest request, HttpContext context, IManufacturingProductionStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.SetLotDisposition(lotId, request.Disposition, tenantKey);
    return result.Error switch
    {
        "lot_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_disposition" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        _ => Results.Ok(result.Lot)
    };
});

api.MapGet("/lots/{lotId:guid}/quality-inspections", (Guid lotId, string? tenantKey, int? limit, HttpContext context, IManufacturingProductionStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetQualityInspections(lotId, scopedTenant, limit ?? 25));
});

api.MapGet("/lots/{lotId:guid}/inventory-transactions", (Guid lotId, int? limit, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetInventoryTransactions(lotId, tenantKey, limit ?? 100));
});

api.MapPost("/lots/{lotId:guid}/reservations", (Guid lotId, CreateLotReservationRequest request, HttpContext context, IManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.ReferenceType) || request.ReferenceId == Guid.Empty || request.Quantity <= 0)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_reservation");
    var result = store.Reserve(tenantKey, lotId, request);
    return result.Error switch
    {
        "lot_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "lot_not_released" or "lot_expired" or "reservation_expired" or "invalid_reservation" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        "reservation_exceeds_available" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ => Results.Created($"/api/v1/manufacturing/lots/{lotId}/reservations/{result.Reservation!.Id}", result.Reservation)
    };
});

api.MapPost("/reservations/{reservationId:guid}/release", (Guid reservationId, HttpContext context, IManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.Release(tenantKey, reservationId);
    return result.Error switch
    {
        "reservation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        _ => Results.Ok(result.Reservation)
    };
});

api.MapGet("/products/{sku}/fefo", (string sku, int? limit, HttpContext context, IManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetFefo(tenantKey, sku, limit ?? 50));
});

api.MapPost("/sales/allocations/{sku}", (string sku, CreateSalesAllocationRequest request, HttpContext context, IManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.AllocateSales(tenantKey, sku, request);
    return result.Error switch
    {
        "invalid_sales_allocation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "insufficient_atp" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ => Results.Created($"/api/v1/manufacturing/sales/allocations/{sku}/{request.SalesOrderId}", result.Allocation)
    };
});

api.MapPost("/quality-inspections", (CreateQualityInspectionRequest request, HttpContext context, IManufacturingLegacyStore store) =>
{
    if (request.LotId == Guid.Empty || string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Inspector))
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_quality_inspection");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreateQualityInspection(request);
    return result.Error switch
    {
        "lot_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "invalid_inspection_status" or "invalid_moisture_percent" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        _ => Results.Created("/api/v1/manufacturing/quality-inspections", result.Inspection)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.QualityInspect))
  .AddEndpointFilter<MobileOperationReplayFilter>();

api.MapGet("/product-specifications", (string? productSku, string? status, int? limit, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetProductSpecifications(tenantKey, productSku, status, limit ?? 50));
});

api.MapPost("/product-specifications", (CreateProductSpecificationRequest request, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreateProductSpecification(request);
    return result.Error is null
        ? Results.Created("/api/v1/manufacturing/product-specifications", result.Specification)
        : ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!);
});

static IResult ChangeProductSpecification(Guid specificationId, string targetStatus, ProductSpecificationLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store)
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ChangeProductSpecificationLifecycle(specificationId, tenantKey, targetStatus, request);
    return result.Error switch
    {
        "product_specification_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_scope_denied" => Results.Forbid(),
        "active_product_specification_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        "invalid_product_specification_actor" or "invalid_product_specification_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ when result.Specification is not null => Results.Ok(result.Specification),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "product_specification_not_found")
    };
}

api.MapPost("/product-specifications/{specificationId:guid}/approve", (Guid specificationId, ProductSpecificationLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeProductSpecification(specificationId, "Approved", request, context, store));
api.MapPost("/product-specifications/{specificationId:guid}/retire", (Guid specificationId, ProductSpecificationLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeProductSpecification(specificationId, "Retired", request, context, store));

api.MapGet("/transformations", (string? tenantKey, string? processStep, int? limit, HttpContext context, IManufacturingProductionStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetTransformationSummaries(scopedTenant, processStep, limit ?? 50));
});

api.MapGet("/recipes", (string? tenantKey, string? productSku, bool? active, int? limit, HttpContext context, IManufacturingLegacyStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetRecipes(scopedTenant, productSku, active, limit ?? 50));
});

api.MapPost("/recipes", (CreateRecipeRequest request, HttpContext context, IManufacturingLegacyStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.ProductSku) ||
        request.Version <= 0 || string.IsNullOrWhiteSpace(request.ProcessStep) || string.IsNullOrWhiteSpace(request.OutputUom) ||
        request.Components is null || request.Components.Count == 0 || request.Components.Any(x => string.IsNullOrWhiteSpace(x.IngredientSku) || string.IsNullOrWhiteSpace(x.Uom) || x.Quantity <= 0) ||
        request.TargetYieldPercent <= 0 || request.TargetYieldPercent > 100)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_recipe");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/recipes", store.CreateRecipe(request)); }
    catch (InvalidOperationException ex) when (ex.Message == "recipe_version_exists")
    { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
    catch (InvalidOperationException ex) when (ex.Message == "invalid_recipe_status")
    { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
    catch (InvalidOperationException ex) when (ex.Message == "invalid_product_specification")
    { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
});

api.MapPost("/recipes/{recipeId:guid}/submit", (Guid recipeId, RecipeLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeRecipeLifecycle(recipeId, "Submitted", request, context, store));
api.MapPost("/recipes/{recipeId:guid}/approve", (Guid recipeId, RecipeLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeRecipeLifecycle(recipeId, "Approved", request, context, store));
api.MapPost("/recipes/{recipeId:guid}/retire", (Guid recipeId, RecipeLifecycleRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeRecipeLifecycle(recipeId, "Retired", request, context, store));

api.MapGet("/deviations", (Guid? productionBatchId, string? status, int? limit, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetDeviations(tenantKey, productionBatchId, status, limit ?? 50));
});

api.MapPost("/production-batches/{batchId:guid}/deviations", (Guid batchId, CreateDeviationRequest request, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.CreateDeviation(batchId, tenantKey, request);
    return result.Error switch
    {
        "invalid_deviation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_scope_denied" => Results.Forbid(),
        "batch_not_active" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ => Results.Created($"/api/v1/manufacturing/deviations/{result.Deviation!.Id}", result.Deviation)
    };
});

static IResult ChangeDeviation(Guid deviationId, string targetStatus, DeviationActionRequest request, HttpContext context, IManufacturingLegacyStore store)
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ChangeDeviationStatus(deviationId, tenantKey, targetStatus, request);
    return result.Error switch
    {
        "deviation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_deviation_actor" or "invalid_deviation_transition" or "author_cannot_approve_own_deviation" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ when result.Deviation is not null => Results.Ok(result.Deviation),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "deviation_not_found")
    };
}

api.MapPost("/deviations/{deviationId:guid}/approve", (Guid deviationId, DeviationActionRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeDeviation(deviationId, "Approved", request, context, store));
api.MapPost("/deviations/{deviationId:guid}/reject", (Guid deviationId, DeviationActionRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeDeviation(deviationId, "Rejected", request, context, store));
api.MapPost("/deviations/{deviationId:guid}/close", (Guid deviationId, DeviationActionRequest request, HttpContext context, IManufacturingLegacyStore store) =>
    ChangeDeviation(deviationId, "Closed", request, context, store));

api.MapGet("/capas", (string? status, int? limit, HttpContext context, IManufacturingCapaStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); return Results.Ok(store.GetCapas(tenantKey, status, limit ?? 200));
});
api.MapPost("/capas", (CreateCapaRequest request, HttpContext context, IManufacturingCapaStore store) =>
{
    var tenantKey = TenantClaim(context); var actor = context.User.Identity?.Name ?? context.User.FindFirst("sub")?.Value ?? "operator"; if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.CreateCapa(tenantKey, request, actor); return result.Error switch { "invalid_capa" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "supplier_not_found" or "deviation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Created("/api/v1/manufacturing/capas", result.Capa) };
});
api.MapPost("/capas/{capaId:guid}/status", (Guid capaId, UpdateCapaStatusRequest request, HttpContext context, IManufacturingCapaStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateCapaStatus(tenantKey, capaId, request); return result.Error switch { "capa_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "invalid_capa_actor" or "invalid_capa_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!), _ => Results.Ok(result.Capa) };
});
api.MapGet("/supplier-evaluations", (Guid? supplierId, int? limit, HttpContext context, IManufacturingCapaStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); return Results.Ok(store.GetSupplierEvaluations(tenantKey, supplierId, limit ?? 200));
});
api.MapPost("/supplier-evaluations", (CreateSupplierEvaluationRequest request, HttpContext context, IManufacturingCapaStore store) =>
{
    var tenantKey = TenantClaim(context); var actor = context.User.Identity?.Name ?? context.User.FindFirst("sub")?.Value ?? "operator"; if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.CreateSupplierEvaluation(tenantKey, request, actor); return result.Error switch { "invalid_supplier_evaluation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Created("/api/v1/manufacturing/supplier-evaluations", result.Evaluation) };
});

api.MapGet("/machines", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetMachines(scopedTenant, status, limit ?? 50));
});

api.MapPost("/machines", (CreateMachineRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Status))
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_machine");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/machines", store.CreateMachine(request)); }
    catch (InvalidOperationException ex) when (ex.Message == "machine_code_exists")
    { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
});

api.MapPost("/machines/{machineId:guid}/maintenance", (Guid machineId, RecordMaintenanceRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.RecordMaintenance(machineId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        _ when result.Machine is not null => Results.Ok(result.Machine),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
    };
});

api.MapPost("/lots", (CreateLotRequest request, HttpContext context, IManufacturingProductionStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) ||
        request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Uom))
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_lot");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();

    var lot = store.CreateLot(request);
    return Results.Created($"/api/v1/manufacturing/lots/{lot.Id}", lot);
});

api.MapPost("/transformations", (CreateTransformationRequest request, HttpContext context, IManufacturingProductionStore store) =>
{
    if (request.Inputs is null || request.Inputs.Count == 0 || request.OutputQuantity <= 0 ||
        string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.OutputSku) ||
        string.IsNullOrWhiteSpace(request.OutputUom))
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_transformation");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();

    var result = store.CreateTransformation(request);
    return result.Error is not null
        ? (result.Error is "duplicate_input_lot" or "input_lot_not_released" or "input_quantity_exceeds_available" or "reservation_not_found" or "reservation_mismatch" or "reservation_unavailable"
            ? ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!)
            : ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!))
        : Results.Created($"/api/v1/manufacturing/transformations/{result.Transformation!.Id}", result.Transformation);
});

api.MapGet("/products/{sku}/availability", (string sku, string? tenantKey, HttpContext context, IManufacturingProductionStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetAvailability(scopedTenant, sku));
});

api.MapGet("/dashboard/manufacturing-summary", (string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetDashboardSummary(scopedTenant));
});

api.MapGet("/machines/{machineId:guid}/telemetry", (Guid machineId, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetMachineTelemetry(machineId, tenantKey, limit ?? 50));
});

api.MapPost("/machines/{machineId:guid}/telemetry", (Guid machineId, RecordMachineTelemetryRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.RecordMachineTelemetry(machineId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_machine_telemetry" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        _ when result.Telemetry is not null && result.Duplicate => Results.Ok(result.Telemetry),
        _ when result.Telemetry is not null => Results.Created($"/api/v1/manufacturing/machines/{machineId}/telemetry/{result.Telemetry.EventId}", result.Telemetry),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
    };
});

api.MapGet("/maintenance-work-orders", (Guid? machineId, string? status, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetMaintenanceWorkOrders(tenantKey, machineId, status, limit ?? 50));
});

api.MapPost("/maintenance-work-orders/generate", (GenerateMaintenanceWorkOrdersRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var asOf = request.AsOf ?? DateTimeOffset.UtcNow;
    if (asOf > DateTimeOffset.UtcNow.AddMinutes(5)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_generation_time");
    return Results.Ok(store.GenerateDueMaintenanceWorkOrders(tenantKey, asOf));
});

api.MapPost("/machines/{machineId:guid}/maintenance-work-orders", (Guid machineId, CreateMaintenanceWorkOrderRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.CreateMaintenanceWorkOrder(machineId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_maintenance_work_order" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "maintenance_work_order_open" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ when result.WorkOrder is not null => Results.Created($"/api/v1/manufacturing/maintenance-work-orders/{result.WorkOrder.Id}", result.WorkOrder),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
    };
});

api.MapPost("/machines/{machineId:guid}/maintenance-work-orders/{workOrderId:guid}/complete", (Guid machineId, Guid workOrderId, CompleteMaintenanceWorkOrderRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.CompleteMaintenanceWorkOrder(machineId, workOrderId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_maintenance_completion" or "maintenance_work_order_not_open" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        "maintenance_work_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        _ when result.WorkOrder is not null => Results.Ok(result.WorkOrder),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
    };
}).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.MaintenanceComplete))
  .AddEndpointFilter<MobileOperationReplayFilter>();

api.MapGet("/machine-downtimes", (Guid? machineId, string? status, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetDowntimes(tenantKey, machineId, status, limit ?? 50));
});

api.MapPost("/machines/{machineId:guid}/downtimes", (Guid machineId, CreateDowntimeRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.CreateDowntime(machineId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_downtime" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "machine_downtime_open" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ when result.Downtime is not null => Results.Created($"/api/v1/manufacturing/machines/{machineId}/downtimes/{result.Downtime.Id}", result.Downtime),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
    };
});

api.MapPost("/machines/{machineId:guid}/downtimes/{downtimeId:guid}/resolve", (Guid machineId, Guid downtimeId, ResolveDowntimeRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ResolveDowntime(machineId, downtimeId, request, tenantKey);
    return result.Error switch
    {
        "tenant_scope_denied" => Results.Forbid(),
        "invalid_downtime_end" or "downtime_not_open" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        "downtime_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        _ when result.Downtime is not null => Results.Ok(result.Downtime),
        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
    };
});

api.MapGet("/dashboard/production-kpis", (string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetProductionKpis(scopedTenant));
});

api.MapGet("/dashboard/machine-health", (string? tenantKey, int? dueWithinDays, HttpContext context, IManufacturingDashboardStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetMachineHealth(scopedTenant, dueWithinDays ?? 7));
});

api.MapGet("/dashboard/oee", (Guid? machineId, string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetOee(scopedTenant, machineId));
});

api.MapGet("/dashboard/production-costs", (string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetProductionCosts(scopedTenant));
});

api.MapGet("/dashboard/exceptions", (string? tenantKey, int? expiryWithinDays, int? downtimeThresholdHours, HttpContext context, IManufacturingDashboardStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetExecutiveExceptions(scopedTenant, expiryWithinDays ?? 7, downtimeThresholdHours ?? 4));
});

api.MapGet("/sales/forecasts", (string? productSku, int? limit, HttpContext context, IManufacturingDashboardStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetSalesForecasts(tenantKey, productSku, limit ?? 50));
});

api.MapPost("/sales/forecasts", (CreateSalesForecastRequest request, HttpContext context, IManufacturingDashboardStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/sales/forecasts", store.CreateSalesForecast(tenantKey, request)); }
    catch (InvalidOperationException ex) when (ex.Message == "invalid_sales_forecast") { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
    catch (InvalidOperationException ex) when (ex.Message == "forecast_version_exists") { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
});

api.MapGet("/planning/forecast-material-requirements/{forecastId:guid}", (Guid forecastId, HttpContext context, IManufacturingDashboardStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.GetSalesForecastMaterialRequirements(tenantKey, forecastId);
    return result.Error switch
    {
        "forecast_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "approved_recipe_not_found" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ => Results.Ok(result.Requirements)
    };
});

api.MapGet("/planning/material-requirements", (Guid? productionOrderId, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetMaterialRequirements(tenantKey, productionOrderId));
});

api.MapGet("/dashboard/cost-projection", (string productSku, int? recipeVersion, decimal? plannedQuantity, HttpContext context, IManufacturingDashboardStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(productSku) || !plannedQuantity.HasValue || plannedQuantity <= 0)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_cost_projection");

    var projection = store.GetCostProjection(tenantKey, productSku, recipeVersion, plannedQuantity.Value);
    return projection is null
        ? ManufacturingProblem(StatusCodes.Status404NotFound, "recipe_not_found")
        : Results.Ok(projection);
});

api.MapGet("/events/receipts", (string? eventType, int? limit, IManufacturingLegacyStore store) =>
    Results.Ok(store.GetEventReceipts(eventType, limit ?? 25)));

api.MapGet("/suppliers", (string? tenantKey, bool? active, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetSuppliers(scopedTenant, active, limit ?? 100));
});

api.MapPost("/suppliers", (CreateSupplierRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_supplier");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    try { return Results.Created("/api/v1/manufacturing/suppliers", store.CreateSupplier(request)); }
    catch (InvalidOperationException ex) when (ex.Message == "supplier_code_exists")
    { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
});

api.MapGet("/purchase-orders", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetPurchaseOrders(scopedTenant, status, limit ?? 100));
});

api.MapGet("/facilities", (string? tenantKey, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetFacilities(scopedTenant, active, limit ?? 100));
});

api.MapPost("/facilities", (CreateFacilityRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_facility");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreateFacility(request);
    return result.Error is null ? Results.Created("/api/v1/manufacturing/facilities", result.Facility) : ManufacturingProblem(StatusCodes.Status409Conflict, result.Error);
});

api.MapGet("/lots/{lotId:guid}/reservations", (Guid lotId, string? status, int? limit, HttpContext context, IManufacturingReservationStore store) =>
{
    var tenantKey = TenantClaim(context);
    return string.IsNullOrWhiteSpace(tenantKey)
        ? Results.Forbid()
        : Results.Ok(store.GetReservations(tenantKey, lotId, status, limit ?? 100));
});
api.MapPatch("/machines/{machineId:guid}", (Guid machineId, UpdateMachineRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateMachine(machineId, request, tenantKey);
    return result.Error switch { "machine_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "machine_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Machine) };
});
api.MapPatch("/facilities/{facilityId:guid}", (Guid facilityId, UpdateFacilityRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateFacility(tenantKey, facilityId, request);
    return result.Error switch { "facility_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "facility_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Facility) };
});

api.MapGet("/warehouses", (string? tenantKey, Guid? facilityId, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetWarehouses(scopedTenant, facilityId, active, limit ?? 100));
});

api.MapPost("/warehouses", (CreateWarehouseRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || request.FacilityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_warehouse");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreateWarehouse(request);
    return result.Error switch { "facility_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "warehouse_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/warehouses", result.Warehouse) };
});
api.MapPatch("/warehouses/{warehouseId:guid}", (Guid warehouseId, UpdateWarehouseRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateWarehouse(tenantKey, warehouseId, request);
    return result.Error switch { "warehouse_not_found" or "facility_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "warehouse_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Warehouse) };
});

api.MapGet("/storage-locations", (string? tenantKey, Guid? warehouseId, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetStorageLocations(scopedTenant, warehouseId, active, limit ?? 200));
});

api.MapPost("/storage-locations", (CreateStorageLocationRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || request.WarehouseId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_storage_location");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreateStorageLocation(request);
    return result.Error switch { "warehouse_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "location_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/storage-locations", result.Location) };
});
api.MapPatch("/storage-locations/{locationId:guid}", (Guid locationId, UpdateStorageLocationRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateStorageLocation(tenantKey, locationId, request);
    return result.Error switch { "storage_location_not_found" or "warehouse_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "location_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Location) };
});

api.MapGet("/uoms", (bool? active, IManufacturingMasterDataStore store) => Results.Ok(store.GetUoms(active, 200)));
api.MapPost("/uoms", (CreateUomRequest request, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Dimension)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_uom");
    var result = store.CreateUom(request);
    return result.Error is null ? Results.Created("/api/v1/manufacturing/uoms", result.Uom) : ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!);
});
api.MapPatch("/uoms/{uomId:guid}", (Guid uomId, UpdateUomRequest request, IManufacturingMasterDataStore store) =>
{
    var result = store.UpdateUom(uomId, request); return result.Error switch { "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "uom_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Uom) };
});
api.MapGet("/uom-conversions", (bool? active, IManufacturingMasterDataStore store) => Results.Ok(store.GetUomConversions(active, 500)));
api.MapPost("/uom-conversions", (CreateUomConversionRequest request, IManufacturingMasterDataStore store) =>
{
    var result = store.CreateUomConversion(request);
    return result.Error switch { "invalid_uom_conversion" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "uom_conversion_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/uom-conversions", result.Conversion) };
});
api.MapPatch("/uom-conversions/{conversionId:guid}", (Guid conversionId, UpdateUomConversionRequest request, IManufacturingMasterDataStore store) =>
{
    var result = store.UpdateUomConversion(conversionId, request); return result.Error switch { "uom_conversion_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "uom_conversion_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Conversion) };
});

api.MapGet("/materials", (string? tenantKey, string? materialType, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid(); return Results.Ok(store.GetMaterials(scopedTenant, materialType, active, limit ?? 500));
});
api.MapPost("/materials", (CreateMaterialRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUomCode)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_material"); if (!TenantMatches(context, request.TenantKey)) return Results.Forbid(); var result = store.CreateMaterial(request); return result.Error switch { "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "material_sku_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/materials", result.Material) };
});
api.MapPatch("/materials/{materialId:guid}", (Guid materialId, UpdateMaterialRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateMaterial(tenantKey, materialId, request);
    return result.Error switch { "material_not_found" or "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Ok(result.Material) };
});
api.MapGet("/products", (string? tenantKey, bool? active, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid(); return Results.Ok(store.GetProducts(scopedTenant, active, limit ?? 500));
});
api.MapPost("/products", (CreateProductRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUomCode)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_product"); if (!TenantMatches(context, request.TenantKey)) return Results.Forbid(); var result = store.CreateProduct(request); return result.Error switch { "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "product_sku_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/products", result.Product) };
});
api.MapPatch("/products/{productId:guid}", (Guid productId, UpdateProductRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateProduct(tenantKey, productId, request);
    return result.Error switch { "product_not_found" or "uom_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Ok(result.Product) };
});

api.MapGet("/supplier-rfqs", (string? status, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); return Results.Ok(store.GetSupplierRfqs(tenantKey, status, limit ?? 200));
});
api.MapPost("/supplier-rfqs", (CreateSupplierRfqRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || !TenantMatches(context, request.TenantKey)) return Results.Forbid(); var result = store.CreateSupplierRfq(request); return result.Error switch { "invalid_supplier_rfq" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "supplier_rfq_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/supplier-rfqs", result.Rfq) };
});
api.MapPost("/supplier-rfqs/{rfqId:guid}/quotations", (Guid rfqId, CreateSupplierQuotationRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey) || request.SupplierRfqId != rfqId) return Results.Forbid(); var result = store.CreateSupplierQuotation(tenantKey, request); return result.Error switch { "supplier_rfq_not_found" or "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "invalid_supplier_quotation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "supplier_quotation_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created($"/api/v1/manufacturing/supplier-rfqs/{rfqId}/quotations", result.Quotation) };
});
api.MapGet("/supplier-rfqs/{rfqId:guid}/quotations", (Guid rfqId, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetSupplierQuotations(tenantKey, rfqId, 200));
});

api.MapPatch("/suppliers/{supplierId:guid}", (Guid supplierId, UpdateSupplierRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_supplier");
    var result = store.UpdateSupplier(tenantKey, supplierId, request);
    return result.Error switch
    {
        "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "supplier_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ => Results.Ok(result.Supplier)
    };
});

api.MapGet("/inbound-receipts", (Guid? purchaseOrderId, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    return Results.Ok(store.GetInboundReceipts(tenantKey, purchaseOrderId, limit ?? 100));
});

api.MapPost("/purchase-orders", (CreatePurchaseOrderRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.OrderNumber) ||
        string.IsNullOrWhiteSpace(request.Currency) || request.SupplierId == Guid.Empty || request.Lines is null || request.Lines.Count == 0 ||
        request.Lines.Any(x => string.IsNullOrWhiteSpace(x.MaterialSku) || string.IsNullOrWhiteSpace(x.Uom) || x.OrderedQuantity <= 0 || x.UnitPrice < 0))
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_purchase_order");
    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
    var result = store.CreatePurchaseOrder(request);
    return result.Error switch
    {
        "supplier_not_found" or "supplier_inactive" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "purchase_order_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        "invalid_purchase_order_status" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        _ => Results.Created("/api/v1/manufacturing/purchase-orders", result.Order)
    };
});

api.MapPut("/purchase-orders/{purchaseOrderId:guid}", (Guid purchaseOrderId, UpdatePurchaseOrderRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.UpdatePurchaseOrder(tenantKey, purchaseOrderId, request);
    return result.Error switch
    {
        "purchase_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "purchase_order_not_editable" or "invalid_purchase_order" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "purchase_order_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ => Results.Ok(result.Order)
    };
});

api.MapPost("/purchase-orders/{purchaseOrderId:guid}/status", (Guid purchaseOrderId, UpdatePurchaseOrderStatusRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Status)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_purchase_order_status");
    var result = store.UpdatePurchaseOrderStatus(tenantKey, purchaseOrderId, request.Status);
    return result.Error switch
    {
        "purchase_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "invalid_purchase_order_transition" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        _ => Results.Ok(result.Order)
    };
});

api.MapPost("/purchase-orders/{purchaseOrderId:guid}/receipts", (Guid purchaseOrderId, ReceiveInboundLotRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (request.PurchaseOrderId != purchaseOrderId || string.IsNullOrWhiteSpace(request.MaterialSku) || string.IsNullOrWhiteSpace(request.ReceiptNumber) ||
        string.IsNullOrWhiteSpace(request.SupplierLotCode) || string.IsNullOrWhiteSpace(request.FacilityId) || request.PurchaseOrderLineId == Guid.Empty)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_inbound_receipt");
    var result = store.ReceiveInboundLot(tenantKey, request);
    return result.Error switch
    {
        "purchase_order_not_found" or "purchase_order_line_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "invalid_receipt_quantity" or "material_mismatch" or "purchase_order_not_receivable" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "receipt_number_exists" or "supplier_lot_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        "over_receipt" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ => Results.Created($"/api/v1/manufacturing/lots/{result.Receipt!.LotId}/inbound-receipt", result.Receipt)
    };
});

api.MapGet("/production-orders", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetOrders(scopedTenant, status, limit ?? 100));
});

api.MapPost("/production-orders", (CreateProductionOrderRequest request, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.ProductSku) || string.IsNullOrWhiteSpace(request.OutputUom) || request.RecipeId == Guid.Empty || request.TargetQuantity <= 0)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_production_order");
    var result = store.CreateOrder(tenantKey, request);
    return result.Error switch
    {
        "invalid_production_order" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "production_order_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        "recipe_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "recipe_unavailable" or "recipe_product_mismatch" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ => Results.Created("/api/v1/manufacturing/production-orders", result.Order)
    };
});

api.MapPost("/production-orders/{orderId:guid}/release", (Guid orderId, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ReleaseOrder(tenantKey, orderId);
    return result.Error switch
    {
        "production_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        _ => Results.Ok(result.Order)
    };
});
api.MapPost("/supplier-quotations/{quotationId:guid}/status", (Guid quotationId, UpdateSupplierQuotationStatusRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateSupplierQuotationStatus(tenantKey, quotationId, request.Status);
    return result.Error switch { "supplier_quotation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "invalid_supplier_quotation_status" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), _ => Results.Ok(result.Quotation) };
});
api.MapPost("/production-orders/{orderId:guid}/cancel", (Guid orderId, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.CancelOrder(tenantKey, orderId);
    return result.Error switch { "production_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "production_order_not_cancellable" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Order) };
});

api.MapGet("/production-batches", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
    return Results.Ok(store.GetBatches(scopedTenant, status, limit ?? 100));
});

api.MapPost("/production-batches", (CreateProductionBatchRequest request, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (request.ProductionOrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.BatchNumber) || request.PlannedQuantity is <= 0 ||
        request.Inputs?.Any(x => x.LotId == Guid.Empty || x.ReservationId == Guid.Empty || x.Quantity <= 0) == true)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_production_batch");
    var result = store.CreateBatch(tenantKey, request);
    return result.Error switch
    {
        "production_order_not_found" or "machine_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "invalid_production_batch" or "production_order_not_released" or "machine_unavailable" or "input_lot_not_released" or "input_reservation_unavailable" or "input_reservation_mismatch" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        "production_batch_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ => Results.Created("/api/v1/manufacturing/production-batches", result.Batch)
    };
});

api.MapPost("/production-batches/{batchId:guid}/start", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Started", context, store))
    .RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.ProductionExecute))
    .AddEndpointFilter<MobileOperationReplayFilter>();
api.MapPost("/production-batches/{batchId:guid}/pause", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Paused", context, store));
api.MapPost("/production-batches/{batchId:guid}/resume", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Started", context, store));
api.MapPost("/production-batches/{batchId:guid}/complete", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Completed", context, store));
api.MapPost("/production-batches/{batchId:guid}/cancel", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.CancelBatch(tenantKey, batchId);
    return result.Error switch { "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "production_batch_not_cancellable" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Batch) };
});

api.MapPost("/production-batches/{batchId:guid}/operations", (Guid batchId, RecordOperationRequest request, HttpContext context, IManufacturingProductionOrderStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    if (request.Sequence < 0 || string.IsNullOrWhiteSpace(request.ProcessStep) || string.IsNullOrWhiteSpace(request.Operator) || request.InputQuantity <= 0 || request.OutputQuantity < 0)
        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_operation_measurement");
    var result = store.RecordOperation(tenantKey, batchId, request);
    return result.Error switch
    {
        "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "batch_not_started" or "invalid_operation_measurement" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        "operation_sequence_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        _ => Results.Created($"/api/v1/manufacturing/production-batches/{batchId}/operations", result.Operation)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.ProductionExecute))
  .AddEndpointFilter<MobileOperationReplayFilter>();

api.MapPost("/purchase-orders/{purchaseOrderId:guid}/receipts/batch", (Guid purchaseOrderId, ReceiveInboundBatchRequest request, HttpContext context, IManufacturingProcurementStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ReceiveInboundBatch(tenantKey, purchaseOrderId, request);
    return result.Error switch
    {
        "invalid_inbound_batch" or "invalid_inbound_receipt" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "purchase_order_not_found" or "purchase_order_line_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "receipt_number_exists" or "supplier_lot_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
        "over_receipt" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ => Results.Ok(result.Receipts)
    };
});

api.MapPost("/production-batches/{batchId:guid}/operations/{operationId:guid}/loss-review", (Guid batchId, Guid operationId, LossReviewRequest request, HttpContext context, IManufacturingLegacyStore store) =>
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ReviewLoss(tenantKey, batchId, operationId, request);
    return result.Error switch
    {
        "invalid_loss_review" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
        "production_batch_not_found" or "operation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        _ => Results.Ok(result.Review)
    };
});

app.Run();

static IResult BatchStatusResult(Guid batchId, string targetStatus, HttpContext context, IManufacturingProductionOrderStore store)
{
    var tenantKey = TenantClaim(context);
    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
    var result = store.ChangeBatchStatus(tenantKey, batchId, targetStatus);
    return result.Error switch
    {
        "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
        "tenant_mismatch" => Results.Forbid(),
        "required_operation_incomplete" or "quality_gate_incomplete" or "machine_unavailable" or "invalid_batch_transition" or "input_reservation_unavailable" or "input_quantity_insufficient" or "loss_review_required" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
        _ => Results.Ok(result.Batch)
    };
}
