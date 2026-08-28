using System.Text.Json;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;

public sealed record ManufacturingAutomationRunSummary(
    int ExpiredReservations,
    int HeldExpiredLots,
    int ExpiredSupplierCertificates,
    int ExpiredMaterialApprovals,
    int RetiredRecipes,
    int RetiredInspectionPlans,
    int GeneratedMaintenanceWorkOrders);

public sealed class ManufacturingLifecycleAutomation
{
    private readonly IHisHopeDbContextFactory<ManufacturingDbContext> _dbFactory;

    public ManufacturingLifecycleAutomation(IHisHopeDbContextFactory<ManufacturingDbContext> dbFactory) =>
        _dbFactory = dbFactory;

    public async Task<ManufacturingAutomationRunSummary> RunOnceAsync(
        DateTimeOffset asOf,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var totals = new ManufacturingAutomationRunSummary(0, 0, 0, 0, 0, 0, 0);
        foreach (var connectionName in _dbFactory.GetRegisteredConnectionNames())
        {
            var summary = await RunOnceForConnectionAsync(connectionName, asOf, maxItems, cancellationToken);
            totals = new ManufacturingAutomationRunSummary(
                totals.ExpiredReservations + summary.ExpiredReservations,
                totals.HeldExpiredLots + summary.HeldExpiredLots,
                totals.ExpiredSupplierCertificates + summary.ExpiredSupplierCertificates,
                totals.ExpiredMaterialApprovals + summary.ExpiredMaterialApprovals,
                totals.RetiredRecipes + summary.RetiredRecipes,
                totals.RetiredInspectionPlans + summary.RetiredInspectionPlans,
                totals.GeneratedMaintenanceWorkOrders + summary.GeneratedMaintenanceWorkOrders);
        }

        return totals;
    }

    private async Task<ManufacturingAutomationRunSummary> RunOnceForConnectionAsync(
        string connectionName,
        DateTimeOffset asOf,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maxItems, 1, 1_000);
        await using var db = await _dbFactory.CreateDbContextForConnectionAsync(connectionName, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var acquired = await db.Database.SqlQueryRaw<bool>(
                "SELECT pg_try_advisory_xact_lock(hashtext('his-hope:manufacturing:lifecycle-automation')) AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (!acquired)
            return new(0, 0, 0, 0, 0, 0, 0);

        var now = asOf.ToUniversalTime();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var expiredReservations = await ExpireReservationsAsync(db, now, limit, cancellationToken);
        var heldLots = await HoldExpiredLotsAsync(db, now, today, limit, cancellationToken);
        var expiredCertificates = await ExpireSupplierCertificatesAsync(db, now, limit, cancellationToken);
        var expiredApprovals = await ExpireMaterialApprovalsAsync(db, now, limit, cancellationToken);
        var retiredRecipes = await RetireExpiredRecipesAsync(db, now, limit, cancellationToken);
        var retiredInspectionPlans = await RetireExpiredInspectionPlansAsync(db, now, limit, cancellationToken);
        var workOrders = await GenerateDueMaintenanceWorkOrdersAsync(db, now, limit, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(expiredReservations, heldLots, expiredCertificates, expiredApprovals, retiredRecipes, retiredInspectionPlans, workOrders);
    }

    private static async Task<int> ExpireReservationsAsync(ManufacturingDbContext db, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var reservations = await db.LotReservations
            .Where(x => x.Status == "Reserved" && x.ExpiresAt != null && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).Take(limit).ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            reservation.Status = "Expired";
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = reservation.TenantKey, LotId = reservation.LotId,
                TransactionType = "Unreserve", Quantity = reservation.Quantity, Uom = reservation.Uom,
                FacilityId = "default", StockStatus = "Released", CorrelationId = reservation.Id, OccurredAt = now
            });
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                Type = "Manufacturing.InventoryReservationExpired.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = reservation.Id, schemaVersion = 1, occurredAt = now, correlationId = reservation.Id,
                    facilityId = "default", reservationId = reservation.Id, lotId = reservation.LotId,
                    tenantKey = reservation.TenantKey, quantity = reservation.Quantity,
                    referenceType = reservation.ReferenceType, referenceId = reservation.ReferenceId
                }),
                OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
            });
        }

        return reservations.Count;
    }

    private static async Task<int> HoldExpiredLotsAsync(ManufacturingDbContext db, DateTimeOffset now, DateOnly today, int limit, CancellationToken cancellationToken)
    {
        var lots = await db.Lots
            .Where(x => x.Disposition == "Released" && x.BestBefore != null && x.BestBefore < today)
            .OrderBy(x => x.BestBefore).Take(limit).ToListAsync(cancellationToken);
        foreach (var lot in lots)
        {
            var previousDisposition = lot.Disposition;
            var dispositionEventId = Guid.NewGuid();
            lot.Disposition = "Hold";
            lot.QualityStatus = "Expired";
            lot.UpdatedAt = now;
            db.LotStatusHistory.Add(new ManufacturingLotStatusHistoryEntity
            {
                Id = Guid.NewGuid(), LotId = lot.Id, TenantKey = lot.TenantKey,
                FromDisposition = previousDisposition, ToDisposition = lot.Disposition,
                Actor = "lifecycle-automation", ReasonCode = "best_before_expired",
                CorrelationId = dispositionEventId, OccurredAt = now
            });
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id, TransactionType = "Hold",
                Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                CorrelationId = dispositionEventId, OccurredAt = now
            });

            var activeReservations = await db.LotReservations
                .Where(x => x.TenantKey == lot.TenantKey && x.LotId == lot.Id && x.Status == "Reserved")
                .ToListAsync(cancellationToken);
            foreach (var reservation in activeReservations)
            {
                reservation.Status = "Cancelled";
                db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
                {
                    Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id, TransactionType = "Unreserve",
                    Quantity = reservation.Quantity, Uom = reservation.Uom, FacilityId = "default",
                    StockStatus = lot.Disposition, CorrelationId = reservation.Id, OccurredAt = now
                });
                db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
                {
                    Id = Guid.NewGuid(), Type = "Manufacturing.InventoryReservationCancelled.v1",
                    Content = JsonSerializer.Serialize(new
                    {
                        eventId = reservation.Id, schemaVersion = 1, occurredAt = now, correlationId = reservation.Id,
                        facilityId = "default", reservationId = reservation.Id, lotId = lot.Id,
                        tenantKey = lot.TenantKey, quantity = reservation.Quantity,
                        reason = "lot_disposition_changed", disposition = lot.Disposition
                    }),
                    OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
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
                OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
            });
        }

        return lots.Count;
    }

    private static async Task<int> ExpireSupplierCertificatesAsync(ManufacturingDbContext db, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var certificates = await db.SupplierCertificates
            .Where(x => x.Status == "Active" && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).Take(limit).ToListAsync(cancellationToken);
        foreach (var certificate in certificates)
        {
            certificate.Status = "Expired";
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.SupplierCertificateExpired.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = certificate.Id, schemaVersion = 1, occurredAt = now, correlationId = certificate.Id,
                    certificateId = certificate.Id, tenantKey = certificate.TenantKey, supplierId = certificate.SupplierId
                }),
                OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
            });
        }

        return certificates.Count;
    }

    private static async Task<int> ExpireMaterialApprovalsAsync(ManufacturingDbContext db, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var approvals = await db.SupplierMaterialApprovals
            .Where(x => x.Status == "Approved" && x.EffectiveTo != null && x.EffectiveTo <= now)
            .OrderBy(x => x.EffectiveTo).Take(limit).ToListAsync(cancellationToken);
        foreach (var approval in approvals)
        {
            approval.Status = "Expired";
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.SupplierMaterialApprovalExpired.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = approval.Id, schemaVersion = 1, occurredAt = now, correlationId = approval.Id,
                    approvalId = approval.Id, tenantKey = approval.TenantKey, supplierId = approval.SupplierId,
                    materialSku = approval.MaterialSku
                }),
                OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
            });
        }

        return approvals.Count;
    }

    private static async Task<int> RetireExpiredRecipesAsync(ManufacturingDbContext db, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var recipes = await db.Recipes
            .Where(x => x.Status == "Approved" && x.EffectiveTo != null && x.EffectiveTo <= now)
            .OrderBy(x => x.EffectiveTo).Take(limit).ToListAsync(cancellationToken);
        foreach (var recipe in recipes)
        {
            recipe.Status = "Retired";
            recipe.Active = false;
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.RecipeVersionRetired.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = recipe.Id, schemaVersion = 1, occurredAt = now, correlationId = recipe.Id,
                    recipeId = recipe.Id, tenantKey = recipe.TenantKey, productSku = recipe.ProductSku, version = recipe.Version
                }),
                OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
            });
        }

        return recipes.Count;
    }

    private static async Task<int> RetireExpiredInspectionPlansAsync(ManufacturingDbContext db, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var plans = await db.InspectionPlanVersions
            .Where(x => x.Status == "Approved" && x.EffectiveTo != null && x.EffectiveTo <= now)
            .OrderBy(x => x.EffectiveTo).Take(limit).ToListAsync(cancellationToken);
        foreach (var plan in plans)
        {
            plan.Status = "Retired";
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.InspectionPlanVersionRetired.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = plan.Id, schemaVersion = 1, occurredAt = now, correlationId = plan.Id,
                    planId = plan.Id, tenantKey = plan.TenantKey, planCode = plan.PlanCode, version = plan.Version
                }),
                OccurredOn = now.UtcDateTime, Status = "Pending", RetryCount = 0
            });
        }

        return plans.Count;
    }

    private static async Task<int> GenerateDueMaintenanceWorkOrdersAsync(ManufacturingDbContext db, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        var generated = 0;
        var duePlans = await db.MaintenancePlans
            .Where(x => x.Active && x.NextDueAt <= now)
            .OrderBy(x => x.NextDueAt).Take(limit).ToListAsync(cancellationToken);
        foreach (var plan in duePlans)
        {
            if (generated >= limit) break;
            if (await db.MaintenanceWorkOrders.AnyAsync(
                    x => x.TenantKey == plan.TenantKey && x.MachineId == plan.MachineId && x.Status == "Open",
                    cancellationToken))
                continue;

            var entity = new ManufacturingMaintenanceWorkOrderEntity
            {
                Id = Guid.NewGuid(), MachineId = plan.MachineId, TenantKey = plan.TenantKey, Status = "Open",
                MaintenanceType = plan.MaintenanceType, DueAt = plan.NextDueAt,
                AssignedTo = plan.AssignedTo, Notes = plan.Checklist, CreatedAt = now
            };
            db.MaintenanceWorkOrders.Add(entity);
            plan.LastGeneratedAt = now;
            plan.NextDueAt = plan.NextDueAt.AddDays(plan.FrequencyDays);
            AddMaintenanceWorkOrderOutbox(db, entity);
            generated++;
        }

        var dueMachines = await db.Machines
            .Where(x => x.Active && x.NextMaintenanceAt != null && x.NextMaintenanceAt <= now)
            .OrderBy(x => x.NextMaintenanceAt).Take(limit).ToListAsync(cancellationToken);
        foreach (var machine in dueMachines)
        {
            if (generated >= limit) break;
            if (await db.MaintenanceWorkOrders.AnyAsync(
                    x => x.TenantKey == machine.TenantKey && x.MachineId == machine.Id && x.Status == "Open",
                    cancellationToken))
                continue;

            var entity = new ManufacturingMaintenanceWorkOrderEntity
            {
                Id = Guid.NewGuid(), MachineId = machine.Id, TenantKey = machine.TenantKey, Status = "Open",
                MaintenanceType = "Preventive", DueAt = machine.NextMaintenanceAt!.Value,
                Notes = "Generated from machine maintenance schedule", CreatedAt = now
            };
            db.MaintenanceWorkOrders.Add(entity);
            AddMaintenanceWorkOrderOutbox(db, entity);
            generated++;
        }

        return generated;
    }

    private static void AddMaintenanceWorkOrderOutbox(ManufacturingDbContext db, ManufacturingMaintenanceWorkOrderEntity entity) =>
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MaintenanceWorkOrderCreated.v1",
            Content = JsonSerializer.Serialize(new
            {
                eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CreatedAt, correlationId = entity.Id,
                facilityId = "default", machineId = entity.MachineId, tenantKey = entity.TenantKey,
                dueAt = entity.DueAt, maintenanceType = entity.MaintenanceType
            }),
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = "Pending", RetryCount = 0
        });
}
