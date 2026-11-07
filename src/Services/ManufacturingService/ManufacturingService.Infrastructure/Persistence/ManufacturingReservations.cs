using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.Contracts.Commerce;

public sealed class ManufacturingReservationStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingReservationStore
{
    public async Task<(LotReservationDto? Reservation, string? Error)> ReserveAsync(string tenantKey, Guid lotId, CreateLotReservationRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<(LotReservationDto? Reservation, string? Error)>(async () =>
        {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var lot = await db.Lots.SingleOrDefaultAsync(x => x.Id == lotId, cancellationToken);
        if (lot is null) return (null, ManufacturingErrorCodes.LotNotFound);
        var duplicate = await db.LotReservations.SingleOrDefaultAsync(x => x.TenantKey == tenantKey && x.ReferenceType == request.ReferenceType && x.ReferenceId == request.ReferenceId && x.LotId == lotId, cancellationToken);
        if (duplicate is not null)
        {
            if (duplicate.Status == "Reserved" && duplicate.ExpiresAt is { } duplicateExpiry && duplicateExpiry <= now)
                return (null, ManufacturingErrorCodes.ReservationExpired);
            return (ToDto(duplicate), null);
        }
        var reserved = await db.LotReservations.Where(x => x.TenantKey == tenantKey && x.LotId == lotId && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0;
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
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (ToDto(entity), null);
        });
    }

    public async Task<(LotReservationDto? Reservation, string? Error)> ReleaseAsync(string tenantKey, Guid reservationId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.LotReservations.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.ReservationNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        if (entity.Status != "Reserved") return (ToDto(entity), null);
        entity.Status = ManufacturingStatusCodes.Released;
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = entity.LotId, TransactionType = "Unreserve",
            Quantity = entity.Quantity, Uom = entity.Uom, FacilityId = "default", StockStatus = ManufacturingStatusCodes.Released,
            CorrelationId = entity.Id, OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
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
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == ManufacturingStatusCodes.Released && (x.BestBefore == null || x.BestBefore >= today)).ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var reserved = db.LotReservations.AsNoTracking().Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now))
            .GroupBy(x => x.LotId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        return lots.Select(x => new FefoLotDto(x.Id, x.Sku, x.Quantity, reserved.GetValueOrDefault(x.Id), Math.Max(0, x.Quantity - reserved.GetValueOrDefault(x.Id)), x.Uom, x.BestBefore, x.CreatedAt))
            .Where(x => x.AvailableQuantity > 0)
            .OrderBy(x => x.BestBefore.HasValue ? 0 : 1).ThenBy(x => x.BestBefore).ThenBy(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200)).ToList();
    }

    public async Task<(SalesAllocationDto Allocation, string? Error)> AllocateSalesAsync(string tenantKey, string sku, CreateSalesAllocationRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<(SalesAllocationDto Allocation, string? Error)>(async () =>
        {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await AllocateSalesOnDbAsync(db, tenantKey, sku, request, cancellationToken);
        if (result.Error is not null)
            return result;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
        });
    }

    public IReadOnlyList<SalesAllocationDto> GetSalesAllocations(string tenantKey, string? sku, Guid? salesOrderId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.LotReservations.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.ReferenceType == "SalesOrder");
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var lotIds = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Sku == sku.Trim()).Select(x => x.Id).ToArray();
            query = query.Where(x => lotIds.Contains(x.LotId));
        }
        if (salesOrderId is { } orderId && orderId != Guid.Empty) query = query.Where(x => x.ReferenceId == orderId);
        var rows = query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToList();
        var lotsById = db.Lots.AsNoTracking().Where(x => rows.Select(r => r.LotId).Contains(x.Id)).ToDictionary(x => x.Id);
        return rows.GroupBy(x => new { x.ReferenceId, Sku = lotsById.GetValueOrDefault(x.LotId)?.Sku })
            .Where(g => g.Key.Sku is not null)
            .Select(g => new SalesAllocationDto(tenantKey, g.Key.Sku!, g.Key.ReferenceId, g.Sum(x => x.Quantity), g.Where(x => x.Status == "Reserved").Sum(x => x.Quantity), 0, g.Select(ToDto).ToList(), g.Max(x => x.CreatedAt)))
            .Take(Math.Clamp(limit, 1, 200)).ToList();
    }

    public async Task<(IReadOnlyList<SalesAllocationDto> Allocations, string? Error)> AllocateCommerceOrderAsync(CommerceOrderPlacedV1 order, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        var lockKey = order.OrderId.ToString("D");
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // Acquire a session lock before opening the serializable
            // transaction. A transaction advisory lock would be too late: a
            // waiting transaction can already have taken its serializable
            // snapshot and then fail with 40001 after the first delivery
            // commits.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(hashtext({0}))", [lockKey], cancellationToken);
            try
            {
                return await strategy.ExecuteAsync<(IReadOnlyList<SalesAllocationDto> Allocations, string? Error)>(async () =>
                {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                const string eventType = CommerceMessagingContract.OrderPlacedRoutingKey;
                var aggregateId = order.OrderId.ToString();
                if (await db.EventReceipts.AnyAsync(x => x.EventType == eventType && x.AggregateId == aggregateId, cancellationToken))
                    return ([], null);

                if (order.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(order.TenantKey) || order.Lines.Count == 0)
                    return ([], "invalid_commerce_order");

                var allocations = new List<SalesAllocationDto>();
                foreach (var line in order.Lines)
                {
                    var result = await AllocateSalesOnDbAsync(
                        db,
                        order.TenantKey,
                        line.Sku,
                        new CreateSalesAllocationRequest(order.OrderId, line.Quantity),
                        cancellationToken);
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
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return (allocations, null);
                }
                catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // The pre-check above is an optimization, not the concurrency guard.
                    // The unique receipt index is authoritative when two deliveries race.
                    await transaction.RollbackAsync(cancellationToken);
                    return (GetSalesAllocations(order.TenantKey, null, order.OrderId, 200), null);
                }
                });
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(hashtext({0}))", [lockKey], CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<(SalesAllocationDto Allocation, string? Error)> AllocateSalesOnDbAsync(
        ManufacturingDbContext db,
        string tenantKey,
        string sku,
        CreateSalesAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(sku) || request.SalesOrderId == Guid.Empty || request.Quantity <= 0)
            return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, 0, request.Quantity, [], now), ManufacturingErrorCodes.InvalidSalesAllocation);
        var lots = (await db.Lots.Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == ManufacturingStatusCodes.Released && (x.BestBefore == null || x.BestBefore >= DateOnly.FromDateTime(now.UtcDateTime))).ToListAsync(cancellationToken))
            .OrderBy(x => x.BestBefore.HasValue ? 0 : 1).ThenBy(x => x.BestBefore).ThenBy(x => x.CreatedAt).ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var existing = await db.LotReservations.Where(x => x.TenantKey == tenantKey && x.ReferenceType == "SalesOrder" && x.ReferenceId == request.SalesOrderId && lotIds.Contains(x.LotId) && x.Status == "Reserved").ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            var existingAllocated = existing.Sum(x => x.Quantity);
            return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, existingAllocated, Math.Max(0, request.Quantity - existingAllocated), existing.Select(ToDto).ToList(), now), null);
        }
        var reservedByLot = (await db.LotReservations.Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).GroupBy(x => x.LotId).Select(group => new { group.Key, Quantity = group.Sum(y => y.Quantity) }).ToListAsync(cancellationToken)).ToDictionary(x => x.Key, x => x.Quantity);
        var available = lots.Sum(x => Math.Max(0, x.Quantity - reservedByLot.GetValueOrDefault(x.Id)));
        if (available < request.Quantity)
            return (new(tenantKey, sku, request.SalesOrderId, request.Quantity, 0, request.Quantity, [], now), ManufacturingErrorCodes.InsufficientAtp);
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
                OccurredOn = now.UtcDateTime, Status = ManufacturingStatusCodes.Pending
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
