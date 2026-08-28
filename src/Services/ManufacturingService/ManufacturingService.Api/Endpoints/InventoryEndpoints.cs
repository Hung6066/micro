using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder;
using static ManufacturingEndpointHelpers;

internal static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/lots/{lotId:guid}/genealogy", (Guid lotId, string? direction, HttpContext context, IManufacturingTraceabilityStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || !store.LotBelongsToTenant(lotId, tenantKey))
                        return ManufacturingProblem(StatusCodes.Status404NotFound, "lot_not_found");
                
                    var upstream = !string.Equals(direction, "downstream", StringComparison.OrdinalIgnoreCase);
                    return Results.Ok(store.GetGenealogy(lotId, upstream, tenantKey));
                });

                api.MapGet("/lots/{lotId:guid}/recall-impact", (Guid lotId, int? maxLots, HttpContext context, IManufacturingTraceabilityStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || !store.LotBelongsToTenant(lotId, tenantKey))
                        return ManufacturingProblem(StatusCodes.Status404NotFound, "lot_not_found");
                    return Results.Ok(store.GetRecallImpact(lotId, tenantKey, Math.Clamp(maxLots ?? 500, 1, 5000)));
                });

                api.MapGet("/traceability/epcis", (DateTimeOffset? from, DateTimeOffset? to, int? limit, HttpContext context, IManufacturingTraceabilityStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey)
                        ? Results.Forbid()
                        : Results.Ok(store.GetEpcisEvents(tenantKey, from, to, Math.Clamp(limit ?? 500, 1, 5000)));
                });

                api.MapGet("/lots", (string? tenantKey, string? sku, string? disposition, int? limit, HttpContext context, IManufacturingProductionStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetLots(scopedTenant, sku, disposition, limit ?? 50));
                });

                api.MapGet("/lots/{lotId:guid}/status-history", (Guid lotId, int? limit, HttpContext context, IManufacturingProductionStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetLotStatusHistory(lotId, tenantKey, limit ?? 50));
                });

                api.MapPost("/lots/{lotId:guid}/disposition", (Guid lotId, LotDispositionRequest request, HttpContext context, IManufacturingProductionStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.SetLotDisposition(lotId, request.Disposition, tenantKey, request.Actor, request.ReasonCode, request.EvidenceReference, request.ExpectedUpdatedAt);
                    return result.Error switch
                    {
                        "lot_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_disposition" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "concurrency_conflict" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Ok(result.Lot)
                    };
                });

                api.MapGet("/lots/{lotId:guid}/inventory-transactions", (Guid lotId, int? limit, HttpContext context, IManufacturingTraceabilityStore store) =>
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

                api.MapGet("/sales/allocations", (string? sku, Guid? salesOrderId, int? limit, HttpContext context, IManufacturingReservationStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey) ? Results.Forbid() : Results.Ok(store.GetSalesAllocations(tenantKey, sku, salesOrderId, limit ?? 100));
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

                api.MapGet("/lots/{lotId:guid}/reservations", (Guid lotId, string? status, int? limit, HttpContext context, IManufacturingReservationStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey)
                        ? Results.Forbid()
                        : Results.Ok(store.GetReservations(tenantKey, lotId, status, limit ?? 100));
                });

        return api;
    }
}




