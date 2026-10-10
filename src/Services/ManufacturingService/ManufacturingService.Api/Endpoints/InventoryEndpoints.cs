using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Persistence.Querying;
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
                
                api.MapGet("/lots/{lotId:guid}/genealogy", (Guid lotId, string? direction, HttpContext context, IManufacturingTraceabilityReadRepository store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || !store.LotBelongsToTenant(lotId, tenantKey))
                        return ManufacturingProblem(StatusCodes.Status404NotFound, ManufacturingErrorCodes.LotNotFound);
                
                    var upstream = !string.Equals(direction, "downstream", StringComparison.OrdinalIgnoreCase);
                    return Results.Ok(store.GetGenealogy(lotId, upstream, tenantKey));
                });

                api.MapGet("/lots/{lotId:guid}/recall-impact", (Guid lotId, int? maxLots, HttpContext context, IManufacturingTraceabilityReadRepository store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || !store.LotBelongsToTenant(lotId, tenantKey))
                        return ManufacturingProblem(StatusCodes.Status404NotFound, ManufacturingErrorCodes.LotNotFound);
                    return Results.Ok(store.GetRecallImpact(lotId, tenantKey, Math.Clamp(maxLots ?? 500, 1, 5000)));
                });

                api.MapGet("/traceability/epcis", async (DateTimeOffset? from, DateTimeOffset? to, int? limit, int? page, HttpContext context, IManufacturingTraceabilityReadRepository store, CancellationToken cancellationToken) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey)
                        ? Results.Forbid()
                        : Results.Ok(await store.GetEpcisEventsAsync(from, to, Math.Clamp(limit ?? HisHopePaginationDefaults.ExportDefaultPageSize, 1, HisHopePaginationDefaults.ExportMaxPageSize), page ?? HisHopePaginationDefaults.FirstPage, cancellationToken));
                });

                api.MapGet("/lots", async (string? sku, string? disposition, int? limit, int? page, HttpContext context, IManufacturingProductionStore store, CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(TenantClaim(context))) return Results.Forbid();
                    return Results.Ok(await store.GetLotsAsync(sku, disposition, limit ?? HisHopePaginationDefaults.DefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage, cancellationToken));
                });

                api.MapGet("/lots/{lotId:guid}/status-history", (Guid lotId, int? limit, int? page, HttpContext context, IManufacturingProductionStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetLotStatusHistory(lotId, tenantKey, limit ?? HisHopePaginationDefaults.DefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage));
                });

                api.MapPost("/lots/{lotId:guid}/disposition", async (Guid lotId, LotDispositionRequest request, HttpContext context, IManufacturingProductionStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.SetLotDispositionAsync(lotId, request.Disposition, tenantKey, request.Actor, request.ReasonCode, request.EvidenceReference, request.ExpectedUpdatedAt, context.RequestAborted);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.LotNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                        ManufacturingErrorCodes.InvalidDisposition => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        ManufacturingErrorCodes.ConcurrencyConflict => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Ok(result.Lot)
                    };
                });

                api.MapGet("/lots/{lotId:guid}/inventory-transactions", async (Guid lotId, int? limit, int? page, HttpContext context, IManufacturingTraceabilityReadRepository store, CancellationToken cancellationToken) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey)
                        ? Results.Forbid()
                        : Results.Ok(await store.GetInventoryTransactionsAsync(lotId, tenantKey, limit ?? HisHopePaginationDefaults.SmallDefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage, cancellationToken));
                });

                api.MapPost("/lots/{lotId:guid}/reservations", async (Guid lotId, CreateLotReservationRequest request, HttpContext context, IManufacturingReservationStore store, CancellationToken cancellationToken) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (string.IsNullOrWhiteSpace(request.ReferenceType) || request.ReferenceId == Guid.Empty || request.Quantity <= 0)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_reservation");
                    var result = await store.ReserveAsync(tenantKey, lotId, request, cancellationToken);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.LotNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantMismatch => Results.Forbid(),
                        ManufacturingErrorCodes.LotNotReleased or ManufacturingErrorCodes.LotExpired or ManufacturingErrorCodes.ReservationExpired or "invalid_reservation" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        ManufacturingErrorCodes.ReservationExceedsAvailable => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/lots/{lotId}/reservations/{result.Reservation!.Id}", result.Reservation)
                    };
                });

                api.MapPost("/reservations/{reservationId:guid}/release", async (Guid reservationId, HttpContext context, IManufacturingReservationStore store, CancellationToken cancellationToken) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.ReleaseAsync(tenantKey, reservationId, cancellationToken);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.ReservationNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantMismatch => Results.Forbid(),
                        _ => Results.Ok(result.Reservation)
                    };
                });

                api.MapPost("/sales/allocations/{sku}", async (string sku, CreateSalesAllocationRequest request, HttpContext context, IManufacturingReservationStore store, CancellationToken cancellationToken) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.AllocateSalesAsync(tenantKey, sku, request, cancellationToken);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.InvalidSalesAllocation => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        ManufacturingErrorCodes.InsufficientAtp => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/sales/allocations/{sku}/{request.SalesOrderId}", result.Allocation)
                    };
                });

                api.MapGet("/sales/allocations", (string? sku, Guid? salesOrderId, int? limit, HttpContext context, IManufacturingReservationStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey) ? Results.Forbid() : Results.Ok(store.GetSalesAllocations(tenantKey, sku, salesOrderId, limit ?? 100));
                });

                api.MapPost("/lots", async (CreateLotRequest request, HttpContext context, IManufacturingProductionStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Sku) ||
                        request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Uom))
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, ManufacturingErrorCodes.InvalidLot);
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                
                    var lot = await store.CreateLotAsync(request, context.RequestAborted);
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
