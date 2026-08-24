using FluentAssertions;
using His.Hope.Contracts.Manufacturing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class ProductionTraceabilityTests : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private IDbContextFactory<ManufacturingDbContext> dbFactory = null!;

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("MANUFACTURING_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("manufacturingtest")
                .WithUsername("testuser")
                .WithPassword("testpass123!")
                .WithCleanUp(true)
                .Build();
            await container.StartAsync();
            connection = container.GetConnectionString();
        }

        var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(ManufacturingDbContext).Assembly.GetName().Name))
            .Options;
        dbFactory = new TestDbContextFactory(options);
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
    }

    [Fact]
    public async Task OeeReportsInsufficientDataInsteadOfInventingRate()
    {
        var oee = new PostgresManufacturingStore(dbFactory).GetOee("tenant-oee-empty", null);

        oee.Status.Should().Be("insufficient-data");
        oee.OeePercent.Should().BeNull();
        oee.PerformancePercent.Should().BeNull();
        oee.IdealRatePerMinute.Should().BeNull();
        oee.MissingMetrics.Should().Contain(new[] { "planned_production_time", "run_time", "good_reject_count", "ideal_rate" });
    }

    [Fact]
    public async Task DeviationRequiresIndependentApprovalAndAuditsLifecycle()
    {
        const string tenant = "tenant-deviation";
        var recipeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Recipes.Add(new ManufacturingRecipeEntity
            {
                Id = recipeId, TenantKey = tenant, ProductSku = "FG-DEV", Version = 1,
                ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 85, Active = true,
                Status = "Approved", CreatedAt = DateTimeOffset.UtcNow
            });
            db.ProductionOrders.Add(new ManufacturingProductionOrderEntity
            {
                Id = orderId, TenantKey = tenant, OrderNumber = "PO-DEV-001", ProductSku = "FG-DEV",
                RecipeId = recipeId, RecipeVersion = 1, TargetQuantity = 100, OutputUom = "kg",
                Status = "InProgress", CreatedAt = DateTimeOffset.UtcNow
            });
            db.ProductionBatches.Add(new ManufacturingProductionBatchEntity
            {
                Id = batchId, TenantKey = tenant, ProductionOrderId = orderId, BatchNumber = "B-DEV-001",
                Status = "Started", PlannedQuantity = 100, CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var store = new PostgresManufacturingStore(dbFactory);
        var created = store.CreateDeviation(batchId, tenant,
            new CreateDeviationRequest("ingredient-substitution", "Use supplier lot B", "May change moisture profile", "operator-1"));
        created.Error.Should().BeNull();
        created.Deviation!.Status.Should().Be("Requested");

        var selfApproval = store.ChangeDeviationStatus(created.Deviation.Id, tenant, "Approved", new DeviationActionRequest("operator-1"));
        selfApproval.Error.Should().Be("author_cannot_approve_own_deviation");

        var approved = store.ChangeDeviationStatus(created.Deviation.Id, tenant, "Approved", new DeviationActionRequest("qa-1", "QA accepted equivalent material"));
        approved.Error.Should().BeNull();
        approved.Deviation!.ApprovedBy.Should().Be("qa-1");
        var closed = store.ChangeDeviationStatus(created.Deviation.Id, tenant, "Closed", new DeviationActionRequest("supervisor-1", "Applied and verified"));
        closed.Error.Should().BeNull();
        closed.Deviation!.Status.Should().Be("Closed");

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.Deviations.SingleAsync(x => x.Id == created.Deviation.Id)).Status.Should().Be("Closed");
        (await verify.OutboxMessages.CountAsync(x => x.Content.Contains(created.Deviation.Id.ToString()) && x.Type.StartsWith("Manufacturing.Deviation"))).Should().Be(3);
    }

    [Fact]
    public async Task ProductSpecificationRequiresValidLifecycleAndPreventsTwoActiveSpecs()
    {
        const string tenant = "tenant-product-spec";
        var store = new PostgresManufacturingStore(dbFactory);
        var invalid = store.CreateProductSpecification(new CreateProductSpecificationRequest(tenant, "FG-SPEC", 101, "Pouch", 180, "Moisture <= 12%"));
        invalid.Error.Should().Be("invalid_product_specification");

        var created = store.CreateProductSpecification(new CreateProductSpecificationRequest(tenant, "FG-SPEC", 12, "Pouch", 180, "Moisture <= 12%"));
        created.Error.Should().BeNull();
        created.Specification!.Status.Should().Be("Draft");
        store.ChangeProductSpecificationLifecycle(created.Specification.Id, tenant, "Approved", new ProductSpecificationLifecycleRequest("qa-1")).Error.Should().BeNull();

        var second = store.CreateProductSpecification(new CreateProductSpecificationRequest(tenant, "FG-SPEC", 10, "Pouch", 180, "Moisture <= 10%"));
        second.Error.Should().BeNull();
        var duplicateApproval = store.ChangeProductSpecificationLifecycle(second.Specification!.Id, tenant, "Approved", new ProductSpecificationLifecycleRequest("qa-2"));
        duplicateApproval.Error.Should().Be("active_product_specification_exists");

        var retired = store.ChangeProductSpecificationLifecycle(created.Specification.Id, tenant, "Retired", new ProductSpecificationLifecycleRequest("qa-1"));
        retired.Error.Should().BeNull();
        store.ChangeProductSpecificationLifecycle(second.Specification.Id, tenant, "Approved", new ProductSpecificationLifecycleRequest("qa-2")).Error.Should().BeNull();

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.ProductSpecifications.CountAsync(x => x.TenantKey == tenant)).Should().Be(2);
        (await verify.OutboxMessages.CountAsync(x => x.Type.StartsWith("Manufacturing.ProductSpecification"))).Should().Be(5);
    }

    [Fact]
    public async Task RecipeCanReferenceOnlyApprovedMatchingProductSpecification()
    {
        const string tenant = "tenant-recipe-spec";
        var store = new PostgresManufacturingStore(dbFactory);
        var draft = store.CreateProductSpecification(new CreateProductSpecificationRequest(tenant, "FG-TRACE", 12, "Pouch", 180, "Moisture <= 12%"));
        draft.Error.Should().BeNull();
        var specificationId = draft.Specification!.Id;

        Action createWithDraft = () => store.CreateRecipe(new CreateRecipeRequest(
            tenant, "FG-TRACE", 1, "drying", "kg", 85,
            [new RecipeComponentRequest("RM-MANGO", 2, "kg")], ProductSpecificationId: specificationId));
        createWithDraft.Should().Throw<InvalidOperationException>().WithMessage("invalid_product_specification");

        store.ChangeProductSpecificationLifecycle(specificationId, tenant, "Approved", new ProductSpecificationLifecycleRequest("qa-1")).Error.Should().BeNull();
        var recipe = store.CreateRecipe(new CreateRecipeRequest(
            tenant, "FG-TRACE", 1, "drying", "kg", 85,
            [new RecipeComponentRequest("RM-MANGO", 2, "kg")], ProductSpecificationId: specificationId));
        recipe.ProductSpecificationId.Should().Be(specificationId);

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.Recipes.SingleAsync(x => x.Id == recipe.Id)).ProductSpecificationId.Should().Be(specificationId);
    }

    [Fact]
    public async Task SalesForecastIsVersionedAndProjectsMaterialShortageWithoutDuplicateReplay()
    {
        const string tenant = "tenant-sales-forecast";
        var recipeId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Recipes.Add(new ManufacturingRecipeEntity
            {
                Id = recipeId, TenantKey = tenant, ProductSku = "FG-FORECAST", Version = 1,
                ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 85, Active = true,
                Status = "Approved", CreatedAt = DateTimeOffset.UtcNow,
                Components = [new ManufacturingRecipeComponentEntity { IngredientSku = "RM-MANGO", Quantity = 2, Uom = "kg" }]
            });
            db.Lots.Add(new ManufacturingLotEntity
            {
                Id = lotId, TenantKey = tenant, Sku = "RM-MANGO", Quantity = 5, Uom = "kg", Disposition = "Released", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var store = new PostgresManufacturingStore(dbFactory);
        var request = new CreateSalesForecastRequest("FG-FORECAST", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 10, "kg", "Sales", "planner-1", 1);
        var forecast = store.CreateSalesForecast(tenant, request);
        forecast.Version.Should().Be(1);
        Action duplicate = () => store.CreateSalesForecast(tenant, request);
        duplicate.Should().Throw<InvalidOperationException>().WithMessage("forecast_version_exists");

        var projection = store.GetSalesForecastMaterialRequirements(tenant, forecast.Id);
        projection.Error.Should().BeNull();
        projection.Requirements.Should().ContainSingle(x => x.MaterialSku == "RM-MANGO" && x.RequiredQuantity == 20 && x.AvailableQuantity == 5 && x.ShortageQuantity == 15);

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.SalesForecasts.CountAsync(x => x.TenantKey == tenant)).Should().Be(1);
        (await verify.OutboxMessages.CountAsync(x => x.Type == "Manufacturing.SalesForecastChanged.v1" && x.Content.Contains(forecast.Id.ToString()))).Should().Be(1);
    }

    [Fact]
    public async Task CompletingBatchConsumesReservationAndCreatesTraceableOutput()
    {
        const string tenant = "integration-tenant";
        var recipeId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Recipes.Add(new ManufacturingRecipeEntity
            {
                Id = recipeId, TenantKey = tenant, ProductSku = "FG-MANGO", Version = 1,
                ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 85, Active = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.Lots.Add(new ManufacturingLotEntity
            {
                Id = lotId, TenantKey = tenant, Sku = "RM-MANGO", Quantity = 100,
                Uom = "kg", Disposition = "Released", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var production = new ManufacturingProductionStore(dbFactory);
        var orderResult = production.CreateOrder(tenant, new CreateProductionOrderRequest("PO-INT-001", "FG-MANGO", recipeId, 48, "kg"));
        orderResult.Error.Should().BeNull();
        orderResult.Order.Should().NotBeNull();
        var order = orderResult.Order!;
        production.ReleaseOrder(tenant, order.Id).Error.Should().BeNull();

        var reservations = new ManufacturingReservationStore(dbFactory);
        var reservationResult = reservations.Reserve(tenant, lotId, new CreateLotReservationRequest("ProductionOrder", order.Id, 60));
        reservationResult.Error.Should().BeNull();
        reservationResult.Reservation.Should().NotBeNull();
        var reservation = reservationResult.Reservation!;

        var availability = new PostgresManufacturingStore(dbFactory).GetAvailability(tenant, "RM-MANGO");
        availability.ReleasedQuantity.Should().Be(100);
        availability.ReservedQuantity.Should().Be(60);
        availability.AvailableToPromiseQuantity.Should().Be(40);

        var batchResult = production.CreateBatch(tenant, new CreateProductionBatchRequest(
            order.Id, "BATCH-INT-001", 48, null,
            [new ProductionInputRequest(lotId, reservation.Id, 60)]));
        batchResult.Error.Should().BeNull();
        batchResult.Batch.Should().NotBeNull();
        var batch = batchResult.Batch!;

        production.ChangeBatchStatus(tenant, batch.Id, "Started").Error.Should().BeNull();
        production.RecordOperation(tenant, batch.Id, new RecordOperationRequest(1, "drying", "operator-1", 60, 48, true, "Pass")).Error.Should().BeNull();
        var blockedCompletion = production.ChangeBatchStatus(tenant, batch.Id, "Completed");
        blockedCompletion.Error.Should().Be("loss_review_required");
        blockedCompletion.Batch.Should().BeNull();
        await using (var pending = await dbFactory.CreateDbContextAsync())
        {
            var operation = await pending.OperationExecutions.SingleAsync(x => x.ProductionBatchId == batch.Id);
            var review = new PostgresManufacturingStore(dbFactory).ReviewLoss(tenant, batch.Id, operation.Id, new LossReviewRequest("Approved", "supervisor-1", "Expected moisture loss"));
            review.Error.Should().BeNull();
            review.Review!.Decision.Should().Be("Approved");
        }
        var completed = production.ChangeBatchStatus(tenant, batch.Id, "Completed");
        completed.Error.Should().BeNull();
        completed.Batch!.OutputLotId.Should().NotBeNull();
        completed.Batch.Inputs.Should().ContainSingle(x => x.LotId == lotId && x.ReservationId == reservation.Id && x.Quantity == 60);

        await using var verify = await dbFactory.CreateDbContextAsync();
        var outputLot = await verify.Lots.SingleAsync(x => x.Id == completed.Batch.OutputLotId);
        outputLot.Quantity.Should().Be(48);
        (await verify.Lots.SingleAsync(x => x.Id == lotId)).Quantity.Should().Be(40);
        (await verify.LotReservations.SingleAsync(x => x.Id == reservation.Id)).Status.Should().Be("Consumed");
        var transformation = await verify.Transformations.Include(x => x.Inputs).SingleAsync(x => x.OutputLotId == outputLot.Id);
        transformation.Inputs.Should().ContainSingle(x => x.LotId == lotId && x.Quantity == 60);
        (await verify.InventoryTransactions.CountAsync(x => x.CorrelationId == transformation.Id && x.TransactionType == "Issue")).Should().Be(1);
        (await verify.InventoryTransactions.CountAsync(x => x.CorrelationId == transformation.Id && x.TransactionType == "Produce")).Should().Be(1);
        (await verify.OutboxMessages.AnyAsync(x => x.Type == "Manufacturing.ProductionOutputLotCreated.v1" && x.Content.Contains(outputLot.Id.ToString()))).Should().BeTrue();
        (await verify.OutboxMessages.AnyAsync(x => x.Type == "Manufacturing.LossThresholdExceeded.v1" && x.Content.Contains(batch.Id.ToString()))).Should().BeTrue();
        new PostgresManufacturingStore(dbFactory).GetExecutiveExceptions(tenant, 7, 4)
            .Should().NotContain(x => x.Code == "loss_threshold");
        await using var reviewed = await dbFactory.CreateDbContextAsync();
        (await reviewed.LossReviews.SingleAsync(x => x.ProductionBatchId == batch.Id)).Reviewer.Should().Be("supervisor-1");
    }

    [Fact]
    public async Task HoldingLotCancelsActiveReservationsAndRemovesAtp()
    {
        const string tenant = "tenant-lot-hold";
        var lotId = Guid.NewGuid();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Lots.Add(new ManufacturingLotEntity
            {
                Id = lotId, TenantKey = tenant, Sku = "FG-HOLD", Quantity = 50,
                Uom = "kg", Disposition = "Released", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var reservations = new ManufacturingReservationStore(dbFactory);
        var reservationResult = reservations.Reserve(tenant, lotId,
            new CreateLotReservationRequest("SalesOrder", Guid.NewGuid(), 30));
        reservationResult.Error.Should().BeNull();
        var reservationId = reservationResult.Reservation!.Id;

        var store = new PostgresManufacturingStore(dbFactory);
        var held = store.SetLotDisposition(lotId, "Hold", tenant);
        held.Error.Should().BeNull();
        held.Lot!.Disposition.Should().Be("Hold");
        store.SetLotDisposition(lotId, "Hold", tenant).Error.Should().BeNull();

        var availability = store.GetAvailability(tenant, "FG-HOLD");
        availability.ReleasedQuantity.Should().Be(0);
        availability.ReservedQuantity.Should().Be(0);
        availability.AvailableToPromiseQuantity.Should().Be(0);

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.LotReservations.SingleAsync(x => x.Id == reservationId)).Status.Should().Be("Cancelled");
        (await verify.InventoryTransactions.CountAsync(x => x.LotId == lotId && x.TransactionType == "Unreserve")).Should().Be(1);
        (await verify.OutboxMessages.CountAsync(x => x.Type == "Manufacturing.InventoryReservationCancelled.v1" && x.Content.Contains(reservationId.ToString()))).Should().Be(1);
        (await verify.OutboxMessages.CountAsync(x => x.Type == "Manufacturing.LotDispositionChanged.v1" && x.Content.Contains("cancelledReservationCount\":1"))).Should().Be(1);
    }

    [Fact]
    public async Task MachineDowntimeLocksMachineUntilResolved()
    {
        const string tenant = "tenant-maintenance";
        var store = new PostgresManufacturingStore(dbFactory);
        var machine = store.CreateMachine(new CreateMachineRequest(tenant, "DRY-01", "Dryer 01"));

        var opened = store.CreateDowntime(machine.Id, new CreateDowntimeRequest("Unplanned vibration", DateTimeOffset.UtcNow.AddMinutes(-5)), tenant);
        opened.Error.Should().BeNull();
        opened.Downtime!.Status.Should().Be("Open");

        store.CreateDowntime(machine.Id, new CreateDowntimeRequest("duplicate", DateTimeOffset.UtcNow.AddMinutes(-1)), tenant)
            .Error.Should().Be("machine_downtime_open");
        store.GetMachines(tenant, null, 10).Single().Status.Should().Be("Maintenance");

        var closed = store.ResolveDowntime(machine.Id, opened.Downtime.Id, new ResolveDowntimeRequest(DateTimeOffset.UtcNow), tenant);
        closed.Error.Should().BeNull();
        closed.Downtime!.Status.Should().Be("Closed");
        store.GetMachines(tenant, null, 10).Single().Status.Should().Be("Available");

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.MachineDowntimes.SingleAsync(x => x.Id == opened.Downtime.Id)).EndedAt.Should().NotBeNull();
        (await verify.OutboxMessages.CountAsync(x => x.Type.StartsWith("Manufacturing.MachineDowntime"))).Should().Be(2);
    }

    [Fact]
    public async Task PreventiveMaintenanceWorkOrderCompletesAndSchedulesNextService()
    {
        const string tenant = "tenant-pm";
        var store = new PostgresManufacturingStore(dbFactory);
        var machine = store.CreateMachine(new CreateMachineRequest(tenant, "OV-01", "Oven 01", "Maintenance"));
        var dueAt = DateTimeOffset.UtcNow.AddDays(1);

        var created = store.CreateMaintenanceWorkOrder(machine.Id,
            new CreateMaintenanceWorkOrderRequest(dueAt, "Preventive", "tech-1", "Inspect belts"), tenant);
        created.Error.Should().BeNull();
        created.WorkOrder!.Status.Should().Be("Open");
        store.CreateMaintenanceWorkOrder(machine.Id, new CreateMaintenanceWorkOrderRequest(dueAt), tenant)
            .Error.Should().Be("maintenance_work_order_open");

        var nextService = dueAt.AddDays(30);
        var completed = store.CompleteMaintenanceWorkOrder(machine.Id, created.WorkOrder.Id,
            new CompleteMaintenanceWorkOrderRequest("tech-1", DateTimeOffset.UtcNow, nextService, "photo://pm-1"), tenant);
        completed.Error.Should().BeNull();
        completed.WorkOrder!.Status.Should().Be("Completed");
        store.GetMachines(tenant, null, 10).Single().Status.Should().Be("Available");
        store.GetMachines(tenant, null, 10).Single().NextMaintenanceAt.Should().BeCloseTo(nextService, TimeSpan.FromMilliseconds(10));

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.MaintenanceWorkOrders.SingleAsync(x => x.Id == created.WorkOrder.Id)).Evidence.Should().Be("photo://pm-1");
        (await verify.OutboxMessages.CountAsync(x => x.Type.StartsWith("Manufacturing.MaintenanceWorkOrder"))).Should().Be(2);
    }

    [Fact]
    public async Task MaintenancePlannerGeneratesDueWorkOrderIdempotently()
    {
        const string tenant = "tenant-pm-planner";
        var asOf = DateTimeOffset.UtcNow;
        var store = new PostgresManufacturingStore(dbFactory);
        var machine = store.CreateMachine(new CreateMachineRequest(tenant, "PM-01", "Planner Dryer", "Available", null, asOf.AddMinutes(-1)));

        var first = store.GenerateDueMaintenanceWorkOrders(tenant, asOf);
        first.Should().ContainSingle(x => x.MachineId == machine.Id && x.Status == "Open");
        var second = store.GenerateDueMaintenanceWorkOrders(tenant, asOf.AddMinutes(1));
        second.Should().BeEmpty();
        store.GetMaintenanceWorkOrders(tenant, machine.Id, "Open", 10).Should().ContainSingle();

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.OutboxMessages.CountAsync(x => x.Type == "Manufacturing.MaintenanceWorkOrderCreated.v1" && x.Content.Contains(machine.Id.ToString()))).Should().Be(1);
    }

    [Fact]
    public async Task MachineTelemetryIsIdempotentAndOutOfOrderSafe()
    {
        const string tenant = "tenant-telemetry";
        var store = new PostgresManufacturingStore(dbFactory);
        var machine = store.CreateMachine(new CreateMachineRequest(tenant, "TEL-01", "Telemetry Dryer"));
        var observedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var eventId = Guid.NewGuid();
        var request = new RecordMachineTelemetryRequest(eventId, observedAt, "opcua", "Running", "temperature_c", 72.5m, 20);

        var first = store.RecordMachineTelemetry(machine.Id, request, tenant);
        first.Error.Should().BeNull();
        first.Duplicate.Should().BeFalse();
        var duplicate = store.RecordMachineTelemetry(machine.Id, request with { MeterValue = 73m }, tenant);
        duplicate.Error.Should().BeNull();
        duplicate.Duplicate.Should().BeTrue();
        duplicate.Telemetry!.MeterValue.Should().Be(72.5m);

        var older = store.RecordMachineTelemetry(machine.Id,
            new RecordMachineTelemetryRequest(Guid.NewGuid(), observedAt.AddMinutes(-10), "opcua", "Stopped", "temperature_c", 10m, 19), tenant);
        older.Error.Should().BeNull();
        store.GetMachines(tenant, null, 10).Single().Status.Should().Be("Available");
        store.GetMachineTelemetry(machine.Id, tenant, 10).Should().HaveCount(2);

        var fault = store.RecordMachineTelemetry(machine.Id,
            new RecordMachineTelemetryRequest(Guid.NewGuid(), observedAt.AddMinutes(1), "opcua", "Fault", "temperature_c", 99m, 21), tenant);
        fault.Error.Should().BeNull();
        store.GetExecutiveExceptions(tenant, 7, 4).Should().Contain(x => x.Code == "machine_telemetry_fault" && x.EntityId == machine.Id);
        var recovered = store.RecordMachineTelemetry(machine.Id,
            new RecordMachineTelemetryRequest(Guid.NewGuid(), observedAt.AddMinutes(2), "opcua", "Running", "temperature_c", 70m, 22), tenant);
        recovered.Error.Should().BeNull();
        store.GetExecutiveExceptions(tenant, 7, 4).Should().NotContain(x => x.Code == "machine_telemetry_fault" && x.EntityId == machine.Id);

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.MachineTelemetry.CountAsync(x => x.MachineId == machine.Id)).Should().Be(4);
        (await verify.OutboxMessages.CountAsync(x => x.Type == "Manufacturing.MachineTelemetryRecorded.v1" && x.Content.Contains(machine.Id.ToString()))).Should().Be(4);
    }

    [Fact]
    public async Task RecipeLifecycleRequiresApprovalBeforeProductionUse()
    {
        const string tenant = "tenant-recipe";
        var store = new PostgresManufacturingStore(dbFactory);
        var recipe = store.CreateRecipe(new CreateRecipeRequest(
            tenant, "FG-RECIPE", 1, "drying", "kg", 85,
            [new RecipeComponentRequest("RM-RECIPE", 1, "kg")], true, "Draft"));
        recipe.Status.Should().Be("Draft");

        store.ChangeRecipeLifecycle(recipe.Id, tenant, "Approved", new RecipeLifecycleRequest("qa-user"))
            .Error.Should().Be("invalid_recipe_transition");
        var submitted = store.ChangeRecipeLifecycle(recipe.Id, tenant, "Submitted", new RecipeLifecycleRequest("rd-user"));
        submitted.Error.Should().BeNull();
        var approved = store.ChangeRecipeLifecycle(recipe.Id, tenant, "Approved", new RecipeLifecycleRequest("qa-user"));
        approved.Error.Should().BeNull();
        approved.Recipe!.Status.Should().Be("Approved");
        approved.Recipe.Active.Should().BeTrue();
        var order = new ManufacturingProductionStore(dbFactory).CreateOrder(tenant,
            new CreateProductionOrderRequest("PO-REQ-001", "FG-RECIPE", recipe.Id, 48, "kg"));
        order.Error.Should().BeNull();
        var requirements = store.GetMaterialRequirements(tenant, order.Order!.Id);
        requirements.Should().ContainSingle(x => x.MaterialSku == "RM-RECIPE" && x.RequiredQuantity == 48 && x.ShortageQuantity == 48);

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.Recipes.SingleAsync(x => x.Id == recipe.Id)).ApprovedBy.Should().Be("qa-user");
        (await verify.OutboxMessages.CountAsync(x => x.Type.Contains("Recipe"))).Should().Be(2);
    }

    [Fact]
    public void ExecutiveExceptionsExposeHeldLotsAndPendingRecipeApproval()
    {
        const string tenant = "tenant-executive";
        var store = new PostgresManufacturingStore(dbFactory);
        store.CreateLot(new CreateLotRequest(tenant, "FG-HELD", 20, "kg", "Hold"));
        store.CreateRecipe(new CreateRecipeRequest(tenant, "FG-APPROVAL", 1, "drying", "kg", 85,
            [new RecipeComponentRequest("RM-APPROVAL", 1, "kg")], true, "Submitted"));

        var exceptions = store.GetExecutiveExceptions(tenant, 7, 4);
        exceptions.Should().Contain(x => x.Code == "lot_hold" && x.Severity == "High");
        exceptions.Should().Contain(x => x.Code == "recipe_approval" && x.Severity == "Medium");
    }

    [Fact]
    public async Task SalesAllocationReservesLotsInFefoOrderAndRejectsShortage()
    {
        const string tenant = "tenant-sales";
        var store = new PostgresManufacturingStore(dbFactory);
        var first = store.CreateLot(new CreateLotRequest(tenant, "FG-SALES", 30, "kg", "Released", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))));
        var second = store.CreateLot(new CreateLotRequest(tenant, "FG-SALES", 40, "kg", "Released", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))));
        var sales = new ManufacturingReservationStore(dbFactory);
        var allocation = sales.AllocateSales(tenant, "FG-SALES", new CreateSalesAllocationRequest(Guid.NewGuid(), 50));
        allocation.Error.Should().BeNull();
        allocation.Allocation.AllocatedQuantity.Should().Be(50);
        allocation.Allocation.Reservations.Should().HaveCount(2);
        allocation.Allocation.Reservations[0].LotId.Should().Be(first.Id);
        allocation.Allocation.Reservations[1].LotId.Should().Be(second.Id);

        var shortage = sales.AllocateSales(tenant, "FG-SALES", new CreateSalesAllocationRequest(Guid.NewGuid(), 100));
        shortage.Error.Should().Be("insufficient_atp");
        shortage.Allocation.ShortageQuantity.Should().Be(100);
        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.LotReservations.CountAsync(x => x.TenantKey == tenant && x.Status == "Reserved")).Should().Be(2);
    }

    private sealed class TestDbContextFactory(DbContextOptions<ManufacturingDbContext> options) : IDbContextFactory<ManufacturingDbContext>
    {
        public ManufacturingDbContext CreateDbContext() => new(options);
        public Task<ManufacturingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ManufacturingDbContext(options));
    }
}
