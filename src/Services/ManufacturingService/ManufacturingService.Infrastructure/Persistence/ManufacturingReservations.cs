using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.Contracts.Commerce;

public sealed class ManufacturingReservationStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingReservationStore
{
    public (LotReservationDto? Reservation, string? Error) Reserve(string tenantKey, Guid lotId, CreateLotReservationRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var now = DateTimeOffset.UtcNow;
        var lot = db.Lots.SingleOrDefault(x => x.Id == lotId);
        if (lot is null) return (null, "lot_not_found");
        var duplicate = db.LotReservations.SingleOrDefault(x => x.TenantKey == tenantKey && x.ReferenceType == request.ReferenceType && x.ReferenceId == request.ReferenceId && x.LotId == lotId);
        if (duplicate is not null)
        {
            if (duplicate.Status == "Reserved" && duplicate.ExpiresAt is { } duplicateExpiry && duplicateExpiry <= now)
                return (null, "reservation_expired");
            return (ToDto(duplicate), null);
        }
        var reserved = db.LotReservations.Where(x => x.TenantKey == tenantKey && x.LotId == lotId && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).Sum(x => (decimal?)x.Quantity) ?? 0;
        var policyError = ReservationPolicy.Validate(new ReservationValidationInput(
            tenantKey,
            lot.TenantKey,
            lot.Disposition,
            lot.BestBefore,
            DateOnly.FromDateTime(DateTime.UtcNow),
            request.ReferenceId,
            request.ReferenceType,
            request.Quantity,
            reserved,
            lot.Quantity));
        if (policyError is not null) return (null, policyError);
        var entity = new ManufacturingLotReservationEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = lotId, ReferenceType = request.ReferenceType.Trim(),
            ReferenceId = request.ReferenceId, Quantity = request.Quantity, Uom = lot.Uom, Status = "Reserved",
            CreatedAt = now, ExpiresAt = request.ExpiresAt
        };
        db.LotReservations.Add(entity);
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = lotId, TransactionType = "Reserve",
            Quantity = entity.Quantity, Uom = entity.Uom, FacilityId = request.FacilityId?.Trim() ?? "default",
            StockStatus = lot.Disposition, CorrelationId = entity.Id, OccurredAt = entity.CreatedAt
        });
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.InventoryReserved.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CreatedAt, correlationId = entity.Id, facilityId = request.FacilityId ?? "default", reservationId = entity.Id, lotId, tenantKey, quantity = entity.Quantity, referenceType = entity.ReferenceType, referenceId = entity.ReferenceId }),
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = "Pending"
        });
        db.SaveChanges();
        transaction.Commit();
        return (ToDto(entity), null);
    }

    public (LotReservationDto? Reservation, string? Error) Release(string tenantKey, Guid reservationId)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.LotReservations.SingleOrDefault(x => x.Id == reservationId);
        if (entity is null) return (null, "reservation_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        if (entity.Status != "Reserved") return (ToDto(entity), null);
        entity.Status = "Released";
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = entity.LotId, TransactionType = "Unreserve",
            Quantity = entity.Quantity, Uom = entity.Uom, FacilityId = "default", StockStatus = "Released",
            CorrelationId = entity.Id, OccurredAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<LotReservationDto> GetReservations(string tenantKey, Guid lotId, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.LotReservations.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.LotId == lotId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        return query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToList().Select(ToDto).ToList();
    }

    public IReadOnlyList<FefoLotDto> GetFefo(string tenantKey, string sku, int limit)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var db = dbFactory.CreateDbContext();
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == "Released" && (x.BestBefore == null || x.BestBefore >= today)).ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var reserved = db.LotReservations.AsNoTracking().Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now))
            .GroupBy(x => x.LotId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        return lots.Select(x => new FefoLotDto(x.Id, x.Sku, x.Quantity, reserved.GetValueOrDefault(x.Id), Math.Max(0, x.Quantity - reserved.GetValueOrDefault(x.Id)), x.Uom, x.BestBefore, x.CreatedAt))
            .Where(x => x.AvailableQuantity > 0)
            .OrderBy(x => x.BestBefore.HasValue ? 0 : 1).ThenBy(x => x.BestBefore).ThenBy(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200)).ToList();
    }

    public (SalesAllocationDto Allocation, string? Error) AllocateSales(string tenantKey, string sku, CreateSalesAllocationRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var result = AllocateSalesOnDb(db, tenantKey, sku, request);
        if (result.Error is not null)
            return result;
        db.SaveChanges();
        transaction.Commit();
        return result;
    }

    public (IReadOnlyList<SalesAllocationDto> Allocations, string? Error) AllocateCommerceOrder(CommerceOrderPlacedV1 order)
    {
        using var db = dbFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        const string eventType = "Commerce.OrderPlaced.v1";
        var aggregateId = order.OrderId.ToString();
        if (db.EventReceipts.Any(x => x.EventType == eventType && x.AggregateId == aggregateId))
            return ([], null);

        if (order.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(order.TenantKey) || order.Lines.Count == 0)
            return ([], "invalid_commerce_order");

        var allocations = new List<SalesAllocationDto>();
        foreach (var line in order.Lines)
        {
            var result = AllocateSalesOnDb(
                db,
                order.TenantKey,
                line.Sku,
                new CreateSalesAllocationRequest(order.OrderId, line.Quantity));
            if (result.Error is not null)
                return ([], result.Error);
            allocations.Add(result.Allocation);
        }

        db.EventReceipts.Add(new ManufacturingEventReceiptEntity
        {
            Id = order.EventId,
            EventType = eventType,
            AggregateId = aggregateId,
            Content = JsonSerializer.Serialize(order),
            ReceivedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        transaction.Commit();
        return (allocations, null);
    }

    private static (SalesAllocationDto Allocation, string? Error) AllocateSalesOnDb(
        ManufacturingDbContext db,
        string tenantKey,
        string sku,
        CreateSalesAllocationRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(sku) || request.SalesOrderId == Guid.Empty || request.Quantity <= 0)
            return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, 0, request.Quantity, [], now), "invalid_sales_allocation");
        var lots = db.Lots.Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == "Released" && (x.BestBefore == null || x.BestBefore >= DateOnly.FromDateTime(now.UtcDateTime))).ToList()
            .OrderBy(x => x.BestBefore.HasValue ? 0 : 1).ThenBy(x => x.BestBefore).ThenBy(x => x.CreatedAt).ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var existing = db.LotReservations.Where(x => x.TenantKey == tenantKey && x.ReferenceType == "SalesOrder" && x.ReferenceId == request.SalesOrderId && lotIds.Contains(x.LotId) && x.Status == "Reserved").ToList();
        if (existing.Count > 0)
        {
            var existingAllocated = existing.Sum(x => x.Quantity);
            return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, existingAllocated, Math.Max(0, request.Quantity - existingAllocated), existing.Select(ToDto).ToList(), now), null);
        }
        var reservedByLot = db.LotReservations.Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).GroupBy(x => x.LotId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var available = lots.Sum(x => Math.Max(0, x.Quantity - reservedByLot.GetValueOrDefault(x.Id)));
        if (available < request.Quantity)
            return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, 0, request.Quantity, [], now), "insufficient_atp");
        var reservations = new List<ManufacturingLotReservationEntity>();
        var remaining = request.Quantity;
        foreach (var lot in lots)
        {
            var lotAvailable = Math.Max(0, lot.Quantity - reservedByLot.GetValueOrDefault(lot.Id));
            var quantity = Math.Min(remaining, lotAvailable);
            if (quantity <= 0) continue;
            var reservation = new ManufacturingLotReservationEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = lot.Id, ReferenceType = "SalesOrder", ReferenceId = request.SalesOrderId,
                Quantity = quantity, Uom = lot.Uom, Status = "Reserved", CreatedAt = now, ExpiresAt = request.ExpiresAt
            };
            reservations.Add(reservation);
            db.LotReservations.Add(reservation);
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = lot.Id, TransactionType = "Reserve", Quantity = quantity, Uom = lot.Uom,
                FacilityId = request.FacilityId?.Trim() ?? "default", StockStatus = lot.Disposition, CorrelationId = reservation.Id, OccurredAt = now
            });
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.SalesAllocationCreated.v1",
                Content = JsonSerializer.Serialize(new { eventId = reservation.Id, schemaVersion = 1, occurredAt = now, correlationId = reservation.Id, facilityId = request.FacilityId ?? "default", reservationId = reservation.Id, lotId = lot.Id, tenantKey, sku, quantity, salesOrderId = request.SalesOrderId }),
                OccurredOn = now.UtcDateTime, Status = "Pending"
            });
            remaining -= quantity;
            if (remaining <= 0) break;
        }
        return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, request.Quantity, 0, reservations.Select(ToDto).ToList(), now), null);
    }

    private static LotReservationDto ToDto(ManufacturingLotReservationEntity x) => new(x.Id, x.TenantKey, x.LotId, x.ReferenceType, x.ReferenceId, x.Quantity, x.Uom, x.Status, x.CreatedAt, x.ExpiresAt);
}

public sealed class ManufacturingLotReservationEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid LotId { get; set; }
    public string ReferenceType { get; set; } = "";
    public Guid ReferenceId { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string Status { get; set; } = "Reserved";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
