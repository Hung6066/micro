using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Domain;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Persistence.Querying;
using System.Text.Json;
using System.Data;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.AspNetCore.Tenancy;

public sealed partial class PostgresManufacturingStore
{
    public RecipeDto CreateRecipe(CreateRecipeRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Recipes.Any(x => x.TenantKey == request.TenantKey && x.ProductSku == request.ProductSku && x.Version == request.Version))
            Guard.Against.Conflict(true, "recipe_version_exists");
        var status = string.IsNullOrWhiteSpace(request.Status) ? ManufacturingStatusCodes.Approved : request.Status.Trim();
        if (status is not (ManufacturingStatusCodes.Draft or ManufacturingStatusCodes.Submitted or ManufacturingStatusCodes.Approved or "Retired")) throw new InvalidOperationException(ManufacturingErrorCodes.InvalidRecipeStatus);
        ManufacturingProductSpecificationEntity? specification = null;
        if (request.ProductSpecificationId.HasValue)
        {
            specification = db.ProductSpecifications.SingleOrDefault(x => x.Id == request.ProductSpecificationId.Value);
            if (specification is null || !specification.TenantKey.Equals(request.TenantKey.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !specification.ProductSku.Equals(request.ProductSku.Trim(), StringComparison.OrdinalIgnoreCase) || specification.Status != ManufacturingStatusCodes.Approved)
                throw new InvalidOperationException(ManufacturingErrorCodes.InvalidProductSpecification);
        }
        var entity = new ManufacturingRecipeEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), ProductSku = request.ProductSku.Trim(),
            Version = request.Version, ProcessStep = request.ProcessStep.Trim(), OutputUom = request.OutputUom.Trim(),
            TargetYieldPercent = request.TargetYieldPercent, Active = request.Active && status == ManufacturingStatusCodes.Approved, Status = status,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            ProductSpecificationId = specification?.Id,
            ApprovedAt = status == ManufacturingStatusCodes.Approved ? DateTimeOffset.UtcNow : null, CreatedAt = DateTimeOffset.UtcNow,
            Components = request.Components!.Select(x => new ManufacturingRecipeComponentEntity
            {
                IngredientSku = x.IngredientSku.Trim(), Quantity = x.Quantity, Uom = x.Uom.Trim()
            }).ToList()
        };
        db.Recipes.Add(entity);
        db.SaveChanges();
        return ToDto(entity);
    }

    public IReadOnlyList<RecipeDto> GetRecipes(string? productSku, bool? active, int limit, int page = 1) =>
        GetRecipes(HisHopeTenantScope.Current ?? throw new InvalidOperationException("Tenant context is required."), productSku, active, limit, page);

    public IReadOnlyList<RecipeDto> GetRecipes(string? tenantKey, string? productSku, bool? active, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Recipes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(productSku)) query = query.Where(x => x.ProductSku == productSku);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.TagUseCase("Manufacturing.Recipes.GetRecipes")
            .Include(x => x.Components).OrderByDescending(x => x.ProductSku).ThenByDescending(x => x.Version)
            .ApplyPage(page, limit).AsEnumerable().Select(ToDto).ToList();
    }

    public (RecipeDto? Recipe, string? Error) ChangeRecipeLifecycle(Guid recipeId, string tenantKey, string targetStatus, RecipeLifecycleRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Recipes.Include(x => x.Components).SingleOrDefault(x => x.Id == recipeId);
        if (entity is null) return (null, ManufacturingErrorCodes.RecipeNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_recipe_actor");
        var valid = (entity.Status, targetStatus) switch
        {
            (ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.Submitted) => true,
            (ManufacturingStatusCodes.Submitted, ManufacturingStatusCodes.Approved) => true,
            (ManufacturingStatusCodes.Approved, "Retired") => true,
            _ => false
        };
        if (!valid) return (null, "invalid_recipe_transition");
        entity.Status = targetStatus;
        entity.Active = targetStatus == ManufacturingStatusCodes.Approved;
        if (targetStatus == ManufacturingStatusCodes.Approved)
        {
            entity.ApprovedBy = request.Actor.Trim();
            entity.ApprovedAt = DateTimeOffset.UtcNow;
            entity.EffectiveFrom = request.EffectiveFrom ?? entity.EffectiveFrom ?? DateTimeOffset.UtcNow;
            entity.EffectiveTo = request.EffectiveTo ?? entity.EffectiveTo;
        }
        var eventId = Guid.NewGuid();
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = eventId, Type = $"Manufacturing.Recipe{targetStatus}v1",
            Content = JsonSerializer.Serialize(new { eventId, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = entity.Id, recipeId = entity.Id, tenantKey = entity.TenantKey, status = entity.Status, actor = request.Actor }),
            OccurredOn = DateTime.UtcNow, Status = ManufacturingStatusCodes.Pending
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public async Task<MachineDto> CreateMachineAsync(CreateMachineRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Machines.AnyAsync(x => x.TenantKey == request.TenantKey && x.Code == request.Code, cancellationToken))
            Guard.Against.Conflict(true, "machine_code_exists");
        var entity = new ManufacturingMachineEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Code = request.Code.Trim(), Name = request.Name.Trim(),
            Status = request.Status.Trim(), LastMaintenanceAt = request.LastMaintenanceAt, NextMaintenanceAt = request.NextMaintenanceAt,
            Active = request.Active, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Machines.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<(MachineDto? Machine, string? Error)> UpdateMachineAsync(Guid machineId, UpdateMachineRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); var entity = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId && x.TenantKey == tenantKey, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Status)) return (null, ManufacturingErrorCodes.InvalidMachine);
        if (await db.Machines.AnyAsync(x => x.Id != machineId && x.TenantKey == tenantKey && x.Code == request.Code.Trim(), cancellationToken)) return (null, "machine_code_exists");
        entity.Code = request.Code.Trim(); entity.Name = request.Name.Trim(); entity.Status = request.Status.Trim(); entity.NextMaintenanceAt = request.NextMaintenanceAt; entity.Active = request.Active; await db.SaveChangesAsync(cancellationToken); return (ToDto(entity), null);
    }

    public IReadOnlyList<MachineDto> GetMachines(string? tenantKey, string? status, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Machines.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.TagUseCase("Manufacturing.Maintenance.GetMachines")
            .OrderBy(x => x.Code).ApplyPage(page, limit).AsEnumerable().Select(ToDto).ToList();
    }

    public (MachineCalibrationDto? Calibration, string? Error) CreateMachineCalibration(Guid machineId, CreateMachineCalibrationRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (string.IsNullOrWhiteSpace(request.CalibrationType) || string.IsNullOrWhiteSpace(request.CertificateNumber) || request.CalibratedAt == default || request.NextDueAt <= request.CalibratedAt || string.IsNullOrWhiteSpace(request.Result)) return (null, "invalid_machine_calibration");
        if (db.MachineCalibrations.Any(x => x.TenantKey == tenantKey && x.MachineId == machineId && x.CertificateNumber == request.CertificateNumber.Trim())) return (null, ManufacturingErrorCodes.MachineCalibrationExists);
        var entity = new ManufacturingMachineCalibrationEntity
        {
            Id = Guid.NewGuid(), MachineId = machineId, TenantKey = tenantKey,
            CalibrationType = request.CalibrationType.Trim(), CertificateNumber = request.CertificateNumber.Trim(),
            CalibratedAt = request.CalibratedAt, NextDueAt = request.NextDueAt, Result = request.Result.Trim(),
            Provider = string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider.Trim(),
            EvidenceReference = string.IsNullOrWhiteSpace(request.EvidenceReference) ? null : request.EvidenceReference.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.MachineCalibrations.Add(entity);
        if (machine.NextMaintenanceAt is null || machine.NextMaintenanceAt > entity.NextDueAt) machine.NextMaintenanceAt = entity.NextDueAt;
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<MachineCalibrationDto> GetMachineCalibrations(Guid machineId, string tenantKey, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.MachineCalibrations.AsNoTracking().Where(x => x.MachineId == machineId && x.TenantKey == tenantKey)
            .OrderByDescending(x => x.CalibratedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public async Task<(MachineTelemetryDto? Telemetry, string? Error, bool Duplicate)> RecordMachineTelemetryAsync(Guid machineId, RecordMachineTelemetryRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound, false);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied, false);
        if (request.EventId == Guid.Empty || request.ObservedAt == default || request.ObservedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
            string.IsNullOrWhiteSpace(request.Source) || (string.IsNullOrWhiteSpace(request.State) && string.IsNullOrWhiteSpace(request.MeterName)))
            return (null, "invalid_machine_telemetry", false);

        var existing = await db.MachineTelemetry.SingleOrDefaultAsync(x => x.TenantKey == tenantKey && x.EventId == request.EventId, cancellationToken);
        if (existing is not null) return (ToDto(existing), null, true);

        var entity = new ManufacturingMachineTelemetryEntity
        {
            Id = Guid.NewGuid(), EventId = request.EventId, MachineId = machineId, TenantKey = tenantKey,
            Source = request.Source.Trim(), State = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim(),
            MeterName = string.IsNullOrWhiteSpace(request.MeterName) ? null : request.MeterName.Trim(), MeterValue = request.MeterValue,
            Sequence = request.Sequence, ObservedAt = request.ObservedAt, ReceivedAt = DateTimeOffset.UtcNow
        };
        db.MachineTelemetry.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MachineTelemetryRecorded.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.EventId, schemaVersion = 1, occurredAt = entity.ObservedAt, receivedAt = entity.ReceivedAt, correlationId = entity.Id, facilityId = "default", machineId, tenantKey, source = entity.Source, state = entity.State, meterName = entity.MeterName, meterValue = entity.MeterValue, sequence = entity.Sequence }),
            OccurredOn = entity.ReceivedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending, RetryCount = 0
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null, false);
    }

    public IReadOnlyList<MachineTelemetryDto> GetMachineTelemetry(Guid machineId, string tenantKey, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.MachineTelemetry.AsNoTracking()
            .Where(x => x.MachineId == machineId && x.TenantKey == tenantKey)
            .OrderByDescending(x => x.ObservedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .AsEnumerable().Select(ToDto).ToList();
    }

    public async Task<(MachineDto? Machine, string? Error)> RecordMaintenanceAsync(Guid machineId, RecordMaintenanceRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        entity.LastMaintenanceAt = request.MaintainedAt;
        entity.NextMaintenanceAt = request.NextMaintenanceAt;
        entity.Status = request.Status.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<(MaintenanceWorkOrderDto? WorkOrder, string? Error)> CreateMaintenanceWorkOrderAsync(Guid machineId, CreateMaintenanceWorkOrderRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (request.DueAt == default || string.IsNullOrWhiteSpace(request.MaintenanceType)) return (null, "invalid_maintenance_work_order");
        var existing = await db.MaintenanceWorkOrders.AnyAsync(x => x.MachineId == machineId && x.Status == "Open", cancellationToken);
        if (existing) return (null, "maintenance_work_order_open");
        var entity = new ManufacturingMaintenanceWorkOrderEntity
        {
            Id = Guid.NewGuid(), MachineId = machineId, TenantKey = tenantKey, Status = "Open",
            MaintenanceType = request.MaintenanceType.Trim(), DueAt = request.DueAt,
            AssignedTo = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.MaintenanceWorkOrders.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MaintenanceWorkOrderCreated.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CreatedAt, correlationId = entity.Id, facilityId = "default", machineId, tenantKey, dueAt = entity.DueAt, maintenanceType = entity.MaintenanceType }),
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending, RetryCount = 0
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<(MaintenanceWorkOrderDto? WorkOrder, string? Error)> CompleteMaintenanceWorkOrderAsync(Guid machineId, Guid workOrderId, CompleteMaintenanceWorkOrderRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        var entity = await db.MaintenanceWorkOrders.SingleOrDefaultAsync(x => x.Id == workOrderId && x.MachineId == machineId && x.TenantKey == tenantKey, cancellationToken);
        if (entity is null) return (null, "maintenance_work_order_not_found");
        if (entity.Status != "Open") return (null, "maintenance_work_order_not_open");
        if (string.IsNullOrWhiteSpace(request.Technician) || request.CompletedAt == default || request.CompletedAt < entity.CreatedAt)
            return (null, "invalid_maintenance_completion");
        entity.Status = ManufacturingStatusCodes.Completed;
        entity.Technician = request.Technician.Trim();
        entity.CompletedAt = request.CompletedAt;
        entity.Evidence = string.IsNullOrWhiteSpace(request.Evidence) ? null : request.Evidence.Trim();
        machine.LastMaintenanceAt = request.CompletedAt;
        machine.NextMaintenanceAt = request.NextMaintenanceAt;
        if (machine.Status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)) machine.Status = "Available";
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MaintenanceWorkOrderCompleted.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = request.CompletedAt, correlationId = entity.Id, facilityId = "default", machineId, tenantKey, technician = entity.Technician, nextMaintenanceAt = machine.NextMaintenanceAt }),
            OccurredOn = request.CompletedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending, RetryCount = 0
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public IReadOnlyList<MaintenanceWorkOrderDto> GetMaintenanceWorkOrders(string tenantKey, Guid? machineId, string? status, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.MaintenanceWorkOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (machineId.HasValue) query = query.Where(x => x.MachineId == machineId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.TagUseCase("Manufacturing.Maintenance.GetWorkOrders")
            .OrderBy(x => x.DueAt).ApplyPage(page, limit).ToList().Select(ToDto).ToList();
    }

    public (MaintenancePlanDto? Plan, string? Error) CreateMaintenancePlan(Guid machineId, CreateMaintenancePlanRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (string.IsNullOrWhiteSpace(request.PlanCode) || string.IsNullOrWhiteSpace(request.MaintenanceType) || request.FrequencyDays <= 0 || request.NextDueAt == default) return (null, ManufacturingErrorCodes.InvalidMaintenancePlan);
        if (db.MaintenancePlans.Any(x => x.TenantKey == tenantKey && x.MachineId == machineId && x.PlanCode == request.PlanCode.Trim())) return (null, "maintenance_plan_exists");
        var entity = new ManufacturingMaintenancePlanEntity { Id = Guid.NewGuid(), MachineId = machineId, TenantKey = tenantKey, PlanCode = request.PlanCode.Trim(), MaintenanceType = request.MaintenanceType.Trim(), FrequencyDays = request.FrequencyDays, NextDueAt = request.NextDueAt, Checklist = string.IsNullOrWhiteSpace(request.Checklist) ? null : request.Checklist.Trim(), AssignedTo = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim(), Active = request.Active, CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        db.MaintenancePlans.Add(entity); db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<MaintenancePlanDto> GetMaintenancePlans(string tenantKey, Guid? machineId, bool? active, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.MaintenancePlans.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (machineId.HasValue) query = query.Where(x => x.MachineId == machineId.Value);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.TagUseCase("Manufacturing.Maintenance.GetPlans")
            .OrderBy(x => x.NextDueAt).ApplyPage(page, limit).AsEnumerable().Select(ToDto).ToList();
    }

    public IReadOnlyList<MaintenanceWorkOrderDto> GenerateDueMaintenanceWorkOrders(string tenantKey, DateTimeOffset asOf)
    {
        using var db = dbFactory.CreateDbContext();
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.Execute(() =>
        {
        using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var dueMachines = db.Machines.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Active && x.NextMaintenanceAt != null && x.NextMaintenanceAt <= asOf)
            .ToList();
        var duePlans = db.MaintenancePlans
            .Where(x => x.TenantKey == tenantKey && x.Active && x.NextDueAt <= asOf)
            .ToList();
        var candidateMachineIds = duePlans.Select(x => x.MachineId)
            .Concat(dueMachines.Select(x => x.Id))
            .Distinct()
            .ToArray();
        var openMachineIds = db.MaintenanceWorkOrders.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Status == "Open" && candidateMachineIds.Contains(x.MachineId))
            .Select(x => x.MachineId)
            .ToHashSet();
        var created = new List<ManufacturingMaintenanceWorkOrderEntity>();
        foreach (var plan in duePlans)
        {
            if (openMachineIds.Contains(plan.MachineId)) continue;
            var entity = new ManufacturingMaintenanceWorkOrderEntity
            {
                Id = Guid.NewGuid(), MachineId = plan.MachineId, TenantKey = tenantKey, Status = "Open",
                MaintenanceType = plan.MaintenanceType, DueAt = plan.NextDueAt,
                AssignedTo = plan.AssignedTo, Notes = plan.Checklist, CreatedAt = asOf
            };
            db.MaintenanceWorkOrders.Add(entity);
            plan.LastGeneratedAt = asOf;
            plan.NextDueAt = plan.NextDueAt.AddDays(plan.FrequencyDays);
            AddMaintenanceWorkOrderOutbox(db, entity, "Manufacturing.MaintenanceWorkOrderCreated.v1");
            created.Add(entity);
            openMachineIds.Add(plan.MachineId);
        }
        foreach (var machine in dueMachines)
        {
            if (openMachineIds.Contains(machine.Id)) continue;
            var entity = new ManufacturingMaintenanceWorkOrderEntity
            {
                Id = Guid.NewGuid(), MachineId = machine.Id, TenantKey = tenantKey, Status = "Open",
                MaintenanceType = "Preventive", DueAt = machine.NextMaintenanceAt!.Value,
                Notes = "Generated from machine maintenance schedule", CreatedAt = asOf
            };
            db.MaintenanceWorkOrders.Add(entity);
            AddMaintenanceWorkOrderOutbox(db, entity, "Manufacturing.MaintenanceWorkOrderCreated.v1");
            created.Add(entity);
            openMachineIds.Add(machine.Id);
        }
        db.SaveChanges();
        transaction.Commit();
        return created.Select(ToDto).ToList();
        });
    }

    public async Task<(DowntimeDto? Downtime, string? Error)> CreateDowntimeAsync(Guid machineId, CreateDowntimeRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.StartedAt > DateTimeOffset.UtcNow.AddMinutes(5)) return (null, ManufacturingErrorCodes.InvalidDowntime);
        if (await db.MachineDowntimes.AnyAsync(x => x.MachineId == machineId && x.Status == "Open", cancellationToken)) return (null, "machine_downtime_open");
        var entity = new ManufacturingMachineDowntimeEntity
        {
            Id = Guid.NewGuid(), MachineId = machineId, TenantKey = tenantKey, Reason = request.Reason.Trim(), Status = "Open",
            ProductionBatchId = request.ProductionBatchId, OperationExecutionId = request.OperationExecutionId,
            StartedAt = request.StartedAt, Notes = request.Notes?.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        machine.Status = "Maintenance";
        db.MachineDowntimes.Add(entity);
        AddDowntimeOutbox(db, entity, "Manufacturing.MachineDowntimeOpened.v1");
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<(DowntimeDto? Downtime, string? Error)> ResolveDowntimeAsync(Guid machineId, Guid downtimeId, ResolveDowntimeRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
        if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        var entity = await db.MachineDowntimes.SingleOrDefaultAsync(x => x.Id == downtimeId && x.MachineId == machineId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.DowntimeNotFound);
        if (entity.Status != "Open") return (null, ManufacturingErrorCodes.DowntimeNotOpen);
        if (request.EndedAt < entity.StartedAt || request.EndedAt > DateTimeOffset.UtcNow.AddMinutes(5)) return (null, "invalid_downtime_end");
        entity.Status = ManufacturingStatusCodes.Closed;
        entity.EndedAt = request.EndedAt;
        if (!string.IsNullOrWhiteSpace(request.Notes)) entity.Notes = request.Notes.Trim();
        machine.Status = machine.Active ? "Available" : "Inactive";
        AddDowntimeOutbox(db, entity, "Manufacturing.MachineDowntimeClosed.v1");
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public IReadOnlyList<DowntimeDto> GetDowntimes(string tenantKey, Guid? machineId, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.MachineDowntimes.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (machineId.HasValue) query = query.Where(x => x.MachineId == machineId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.StartedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    private static void AddDowntimeOutbox(ManufacturingDbContext db, ManufacturingMachineDowntimeEntity entity, string type)
    {
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = type,
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.StartedAt, correlationId = entity.Id, machineId = entity.MachineId, tenantKey = entity.TenantKey, reason = entity.Reason, status = entity.Status, startedAt = entity.StartedAt, endedAt = entity.EndedAt }),
            OccurredOn = DateTime.UtcNow, Status = ManufacturingStatusCodes.Pending
        });
    }

    private static void AddMaintenanceWorkOrderOutbox(ManufacturingDbContext db, ManufacturingMaintenanceWorkOrderEntity entity, string type)
    {
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = type,
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CreatedAt, correlationId = entity.Id, facilityId = "default", machineId = entity.MachineId, tenantKey = entity.TenantKey, dueAt = entity.DueAt, maintenanceType = entity.MaintenanceType }),
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending, RetryCount = 0
        });
    }
}
