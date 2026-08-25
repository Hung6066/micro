using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application;

public sealed class ManufacturingReservationStore(IDbContextFactory<ManufacturingDbContext> dbFactory)
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
