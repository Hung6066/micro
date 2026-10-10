using System.Data;
using System.Text.Json;
using His.Hope.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed partial class PostgresManufacturingStore
{
    public async Task<(LotDto? Lot, string? Error)> SetLotDispositionAsync(Guid lotId, string disposition, string tenantKey, string? actor = null, string? reasonCode = null, string? evidenceReference = null, DateTimeOffset? expectedUpdatedAt = null, CancellationToken cancellationToken = default)
    {
        var normalized = disposition.Trim();
        if (!AllowedLotDispositions.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return (null, ManufacturingErrorCodes.InvalidDisposition);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<(LotDto? Lot, string? Error)>(async () =>
        {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lot = await db.Lots.SingleOrDefaultAsync(x => x.Id == lotId, cancellationToken);
        if (lot is null) return (null, ManufacturingErrorCodes.LotNotFound);
        if (!lot.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (expectedUpdatedAt.HasValue && lot.UpdatedAt != expectedUpdatedAt)
            return (ToDto(lot), ManufacturingErrorCodes.ConcurrencyConflict);
        if (lot.Disposition.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return (ToDto(lot), null);

        var now = DateTimeOffset.UtcNow;
        var held = normalized is "Quarantined" or ManufacturingStatusCodes.Rejected or "Hold";
        var activeReservations = held
            ? await db.LotReservations.Where(x => x.TenantKey == lot.TenantKey && x.LotId == lot.Id && x.Status == "Reserved").ToListAsync(cancellationToken)
            : [];
        var previousDisposition = lot.Disposition;
        lot.Disposition = normalized;
        lot.QualityStatus = normalized.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase) ? "Passed" : lot.QualityStatus;
        lot.UpdatedAt = now;
        var dispositionEventId = Guid.NewGuid();
        db.LotStatusHistory.Add(new ManufacturingLotStatusHistoryEntity
        {
            Id = Guid.NewGuid(), LotId = lot.Id, TenantKey = lot.TenantKey, FromDisposition = previousDisposition,
            ToDisposition = normalized, Actor = actor?.Trim() ?? "system", ReasonCode = reasonCode?.Trim(),
            EvidenceReference = evidenceReference?.Trim(), CorrelationId = dispositionEventId, OccurredAt = now
        });
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id,
            TransactionType = normalized.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase) ? "Release" : "Hold",
            Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
            CorrelationId = dispositionEventId, OccurredAt = now
        });
        foreach (var reservation in activeReservations)
        {
            reservation.Status = ManufacturingStatusCodes.Cancelled;
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id, TransactionType = "Unreserve",
                Quantity = reservation.Quantity, Uom = reservation.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                CorrelationId = reservation.Id, OccurredAt = now
            });
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.InventoryReservationCancelled.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = reservation.Id, schemaVersion = 1, occurredAt = now, correlationId = reservation.Id,
                    facilityId = "default", reservationId = reservation.Id, lotId = lot.Id, tenantKey = lot.TenantKey,
                    quantity = reservation.Quantity, reason = "lot_disposition_changed", disposition = lot.Disposition
                }),
                OccurredOn = now.UtcDateTime, Status = ManufacturingStatusCodes.Pending
            });
        }
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = dispositionEventId, Type = "Manufacturing.LotDispositionChanged.v1",
            Content = JsonSerializer.Serialize(new
            {
                eventId = dispositionEventId, schemaVersion = 1, occurredAt = now, correlationId = dispositionEventId,
                facilityId = (string?)null, lotId = lot.Id, tenantKey = lot.TenantKey, disposition = lot.Disposition,
                cancelledReservationCount = activeReservations.Count
            }),
            OccurredOn = now.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (ToDto(lot), null);
        });
    }

    private static readonly string[] AllowedLotDispositions = [ManufacturingStatusCodes.Released, "Quarantined", ManufacturingStatusCodes.Rejected, "Hold", "Consumed"];
}
