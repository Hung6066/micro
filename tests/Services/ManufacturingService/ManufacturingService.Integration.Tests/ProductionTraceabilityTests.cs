using FluentAssertions;
using His.Hope.Contracts.Manufacturing;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Persistence.Tenancy;
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
    public async Task Lot_traceability_profile_and_disposition_history_are_persisted()
    {
        const string tenant = "tenant-enterprise-lot";
        var store = new PostgresManufacturingStore(dbFactory);
        var lot = store.CreateLot(new CreateLotRequest(
            tenant, "RM-MANGO", 120, "kg", "Quarantined", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            LotCode: "LOT-ENTERPRISE-001", LotType: "RawMaterial", OriginCountryCode: "VN",
            ManufacturedOn: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), FacilityCode: "FAC-HCM",
            StorageLocationCode: "A-01-01", CertificateOfAnalysisReference: "coa://enterprise/001",
            SourceLotCode: "FARM-MEKONG-001", RecordedBy: "receiving-operator"));

        lot.LotCode.Should().Be("LOT-ENTERPRISE-001");
        lot.LotType.Should().Be("RawMaterial");
        lot.QualityStatus.Should().Be("Pending");

        var released = store.SetLotDisposition(lot.Id, "Released", tenant, "qa-approver", "incoming_qc_pass", "coa://enterprise/001");
        released.Error.Should().BeNull();

        var history = store.GetLotStatusHistory(lot.Id, tenant, 10);
        history.Should().ContainSingle();
        history[0].FromDisposition.Should().Be("Quarantined");
        history[0].ToDisposition.Should().Be("Released");
        history[0].Actor.Should().Be("qa-approver");
        history[0].ReasonCode.Should().Be("incoming_qc_pass");

        await using var verify = await dbFactory.CreateDbContextAsync();
        var persisted = await verify.Lots.SingleAsync(x => x.Id == lot.Id);
        persisted.CertificateOfAnalysisReference.Should().Be("coa://enterprise/001");
        persisted.QualityStatus.Should().Be("Passed");
    }

    [Fact]
    public void OeeReportsInsufficientDataInsteadOfInventingRate()
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

        var history = store.GetDeviationStatusHistory(tenant, created.Deviation.Id);
        history.Should().HaveCount(3);
        history[0].ToStatus.Should().Be("Requested");
        history[0].Actor.Should().Be("operator-1");
        history[1].ToStatus.Should().Be("Approved");
        history[1].Actor.Should().Be("qa-1");
        history[2].ToStatus.Should().Be("Closed");
        history[2].Actor.Should().Be("supervisor-1");
    }

    [Fact]
    public async Task ProductionBatchStatusHistory_is_persisted_on_create_and_transition()
    {
        const string tenant = "tenant-batch-history";
        var recipeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Recipes.Add(new ManufacturingRecipeEntity
            {
                Id = recipeId, TenantKey = tenant, ProductSku = "FG-HIST", Version = 1,
                ProcessStep = "mixing", OutputUom = "kg", TargetYieldPercent = 85, Active = true,
                Status = "Approved", CreatedAt = DateTimeOffset.UtcNow
            });
            db.ProductionOrders.Add(new ManufacturingProductionOrderEntity
            {
                Id = orderId, TenantKey = tenant, OrderNumber = "PO-HIST-001", ProductSku = "FG-HIST",
                RecipeId = recipeId, RecipeVersion = 1, TargetQuantity = 100, OutputUom = "kg",
                Status = "Released", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var productionStore = new ManufacturingProductionStore(dbFactory);
        var created = productionStore.CreateBatch(tenant, new CreateProductionBatchRequest(orderId, "B-HIST-001", 100, null, null));
        created.Error.Should().BeNull();

        var started = productionStore.ChangeBatchStatus(tenant, created.Batch!.Id, "Started");
        started.Error.Should().BeNull();

        var history = productionStore.GetBatchStatusHistory(tenant, created.Batch.Id);
        history.Should().HaveCount(2);
        history[0].FromStatus.Should().Be("");
        history[0].ToStatus.Should().Be("Created");
        history[1].FromStatus.Should().Be("Created");
        history[1].ToStatus.Should().Be("Started");
    }

    [Fact]
    public async Task PurchaseOrderStatusHistory_is_persisted_on_create_and_approval()
    {
        const string tenant = "tenant-po-history";
        var supplierId = Guid.NewGuid();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Uoms.Add(new ManufacturingUomEntity { Code = "kg", Name = "Kilogram", CreatedAt = DateTimeOffset.UtcNow });
            db.Suppliers.Add(new ManufacturingSupplierEntity
            {
                Id = supplierId, TenantKey = tenant, Code = "SUP-HIST", Name = "History Supplier", Active = true,
                ApprovalStatus = "Approved", RiskLevel = "Low", CreatedAt = DateTimeOffset.UtcNow
            });
            db.Materials.Add(new ManufacturingMaterialEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenant, Sku = "RM-HIST", Name = "History Material",
                BaseUomCode = "kg", Active = true, CreatedAt = DateTimeOffset.UtcNow
            });
            db.SupplierMaterialApprovals.Add(new ManufacturingSupplierMaterialApprovalEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenant, SupplierId = supplierId, MaterialSku = "RM-HIST",
                ApprovedUom = "kg", EffectiveFrom = DateTimeOffset.UtcNow, Status = "Approved",
                CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system"
            });
            await db.SaveChangesAsync();
        }

        var procurementStore = new ManufacturingProcurementStore(dbFactory);
        var created = procurementStore.CreatePurchaseOrder(new CreatePurchaseOrderRequest(
            tenant, supplierId, "PO-HIST-001", "VND",
            [new PurchaseOrderLineRequest("RM-HIST", 10, "kg", 1000)],
            "Draft"));
        created.Error.Should().BeNull();

        var approved = procurementStore.UpdatePurchaseOrderStatus(tenant, created.Order!.Id, "Approved");
        approved.Error.Should().BeNull();

        var history = procurementStore.GetPurchaseOrderStatusHistory(tenant, created.Order.Id);
        history.Should().HaveCount(2);
        history[0].ToStatus.Should().Be("Draft");
        history[1].FromStatus.Should().Be("Draft");
        history[1].ToStatus.Should().Be("Approved");
    }

    [Fact]
    public async Task CrossEntityWorkflow_links_purchase_order_to_inbound_lot_and_batch()
    {
        const string tenant = "tenant-cross-workflow";
        var supplierId = Guid.NewGuid();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Uoms.Add(new ManufacturingUomEntity { Code = "kg", Name = "Kilogram", CreatedAt = DateTimeOffset.UtcNow });
            db.Suppliers.Add(new ManufacturingSupplierEntity
            {
                Id = supplierId, TenantKey = tenant, Code = "SUP-XWF", Name = "Cross Workflow Supplier", Active = true,
                ApprovalStatus = "Approved", RiskLevel = "Low", CreatedAt = DateTimeOffset.UtcNow
            });
            db.Materials.Add(new ManufacturingMaterialEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenant, Sku = "RM-XWF", Name = "Cross Workflow Material",
                BaseUomCode = "kg", Active = true, CreatedAt = DateTimeOffset.UtcNow
            });
            db.SupplierMaterialApprovals.Add(new ManufacturingSupplierMaterialApprovalEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenant, SupplierId = supplierId, MaterialSku = "RM-XWF",
                ApprovedUom = "kg", EffectiveFrom = DateTimeOffset.UtcNow, Status = "Approved",
                CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system"
            });
            await db.SaveChangesAsync();
        }

        var procurementStore = new ManufacturingProcurementStore(dbFactory);
        var legacyStore = new PostgresManufacturingStore(dbFactory);

        var created = procurementStore.CreatePurchaseOrder(new CreatePurchaseOrderRequest(
            tenant, supplierId, "PO-XWF-001", "VND",
            [new PurchaseOrderLineRequest("RM-XWF", 10, "kg", 1000)],
            "Draft"));
        created.Error.Should().BeNull();
        procurementStore.UpdatePurchaseOrderStatus(tenant, created.Order!.Id, "Approved").Error.Should().BeNull();

        var receipt = procurementStore.ReceiveInboundLot(tenant, new ReceiveInboundLotRequest(
            created.Order.Id,
            created.Order.Lines[0].Id,
            "RM-XWF",
            "RCPT-XWF-001",
            "SUP-LOT-XWF",
            "default",
            10,
            ReceivedBy: "receiver"));
        receipt.Error.Should().BeNull();

        var lotId = receipt.Receipt!.LotId;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.QualityInspections.Add(new ManufacturingQualityInspectionEntity
            {
                Id = Guid.NewGuid(), LotId = lotId, TenantKey = tenant, Status = "Pass",
                MoisturePercent = 10, Inspector = "qa-1", InspectedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var trace = legacyStore.GetCrossEntityWorkflow(tenant, "purchase-order", created.Order.Id);
        trace.Should().NotBeNull();
        trace!.Steps.Should().Contain(x => x.EntityType == "purchase-order");
        trace.Steps.Should().Contain(x => x.EntityType == "lot");
        trace.Steps.Should().Contain(x => x.EntityType == "quality-inspection");
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

    [Fact]
    public async Task LifecycleAutomationExpiresAndHoldsRecordsIdempotently()
    {
        const string tenant = "tenant-lifecycle-automation";
        var now = DateTimeOffset.UtcNow;
        var supplierId = Guid.NewGuid();
        var expiredLotId = Guid.NewGuid();
        var activeLotId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var machineDueId = Guid.NewGuid();
        var expiredRecipeId = Guid.NewGuid();
        var expiredInspectionPlanId = Guid.NewGuid();

        await using (var setup = await dbFactory.CreateDbContextAsync())
        {
            setup.Suppliers.Add(new ManufacturingSupplierEntity { Id = supplierId, TenantKey = tenant, Code = "SUP-AUTO", Name = "Automation supplier", LegalName = "Automation supplier", Active = true, CreatedAt = now });
            setup.SupplierCertificates.Add(new ManufacturingSupplierCertificateEntity { Id = Guid.NewGuid(), TenantKey = tenant, SupplierId = supplierId, CertificateType = "HACCP", CertificateNumber = "CERT-AUTO", Issuer = "QA", IssuedAt = now.AddYears(-1), ExpiresAt = now.AddMinutes(-1), Status = "Active", CreatedAt = now.AddYears(-1) });
            setup.SupplierMaterialApprovals.Add(new ManufacturingSupplierMaterialApprovalEntity { Id = Guid.NewGuid(), TenantKey = tenant, SupplierId = supplierId, MaterialSku = "RM-AUTO", ApprovedUom = "kg", EffectiveFrom = now.AddYears(-1), EffectiveTo = now.AddMinutes(-1), Status = "Approved", CreatedAt = now.AddYears(-1) });
            setup.Recipes.Add(new ManufacturingRecipeEntity { Id = expiredRecipeId, TenantKey = tenant, ProductSku = "FG-AUTO-EXPIRED", Version = 1, ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 80, Active = true, Status = "Approved", EffectiveFrom = now.AddYears(-1), EffectiveTo = now.AddMinutes(-1), CreatedAt = now.AddYears(-1) });
            setup.InspectionPlanVersions.Add(new ManufacturingInspectionPlanVersionEntity { Id = expiredInspectionPlanId, TenantKey = tenant, PlanCode = "IP-AUTO", ProductSku = "FG-AUTO-EXPIRED", Version = 1, SamplingMethod = "Per lot", SamplingFrequency = "Every lot", AcceptanceCriteria = "Pass", Status = "Approved", EffectiveFrom = now.AddYears(-1), EffectiveTo = now.AddMinutes(-1), CreatedAt = now.AddYears(-1) });
            setup.Lots.AddRange(
                new ManufacturingLotEntity { Id = expiredLotId, TenantKey = tenant, Sku = "FG-AUTO-EXPIRED", Quantity = 10, Uom = "kg", Disposition = "Released", BestBefore = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)), LotCode = "LOT-AUTO-EXPIRED", LotType = "FinishedGood", QualityStatus = "Passed", CreatedAt = now },
                new ManufacturingLotEntity { Id = activeLotId, TenantKey = tenant, Sku = "FG-AUTO-ACTIVE", Quantity = 10, Uom = "kg", Disposition = "Released", LotCode = "LOT-AUTO-ACTIVE", LotType = "FinishedGood", QualityStatus = "Passed", CreatedAt = now });
            setup.LotReservations.AddRange(
                new ManufacturingLotReservationEntity { Id = Guid.NewGuid(), TenantKey = tenant, LotId = expiredLotId, ReferenceType = "SalesOrder", ReferenceId = Guid.NewGuid(), Quantity = 2, Uom = "kg", Status = "Reserved", CreatedAt = now.AddDays(-1), ExpiresAt = now.AddDays(1) },
                new ManufacturingLotReservationEntity { Id = Guid.NewGuid(), TenantKey = tenant, LotId = activeLotId, ReferenceType = "SalesOrder", ReferenceId = Guid.NewGuid(), Quantity = 2, Uom = "kg", Status = "Reserved", CreatedAt = now.AddDays(-1), ExpiresAt = now.AddMinutes(-1) });
            setup.Machines.Add(new ManufacturingMachineEntity { Id = machineId, TenantKey = tenant, Code = "M-AUTO", Name = "Automation machine", Status = "Available", Active = true, CreatedAt = now });
            setup.Machines.Add(new ManufacturingMachineEntity { Id = machineDueId, TenantKey = tenant, Code = "M-DUE", Name = "Due machine", Status = "Available", Active = true, NextMaintenanceAt = now.AddMinutes(-1), CreatedAt = now });
            setup.MaintenancePlans.Add(new ManufacturingMaintenancePlanEntity { Id = Guid.NewGuid(), TenantKey = tenant, MachineId = machineId, PlanCode = "MP-AUTO", MaintenanceType = "Preventive", FrequencyDays = 30, NextDueAt = now.AddMinutes(-1), Active = true, CreatedAt = now });
            await setup.SaveChangesAsync();
        }

        var automation = new ManufacturingLifecycleAutomation(new SingleConnectionDbContextFactory<ManufacturingDbContext>(dbFactory, "ManufacturingDb"));
        var first = await automation.RunOnceAsync(now, 100);
        first.Should().Be(new ManufacturingAutomationRunSummary(1, 1, 1, 1, 1, 1, 2));
        var second = await automation.RunOnceAsync(now.AddMinutes(1), 100);
        second.Should().Be(new ManufacturingAutomationRunSummary(0, 0, 0, 0, 0, 0, 0));

        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.Lots.SingleAsync(x => x.Id == expiredLotId)).Disposition.Should().Be("Hold");
        (await verify.Lots.SingleAsync(x => x.Id == expiredLotId)).QualityStatus.Should().Be("Expired");
        (await verify.LotReservations.Where(x => x.TenantKey == tenant).Select(x => x.Status).ToListAsync()).Should().BeEquivalentTo(["Cancelled", "Expired"]);
        (await verify.SupplierCertificates.SingleAsync(x => x.TenantKey == tenant)).Status.Should().Be("Expired");
        (await verify.SupplierMaterialApprovals.SingleAsync(x => x.TenantKey == tenant)).Status.Should().Be("Expired");
        (await verify.Recipes.SingleAsync(x => x.Id == expiredRecipeId)).Status.Should().Be("Retired");
        (await verify.Recipes.SingleAsync(x => x.Id == expiredRecipeId)).Active.Should().BeFalse();
        (await verify.InspectionPlanVersions.SingleAsync(x => x.Id == expiredInspectionPlanId)).Status.Should().Be("Retired");
        (await verify.MaintenanceWorkOrders.CountAsync(x => x.TenantKey == tenant && x.Status == "Open")).Should().Be(2);
        (await verify.OutboxMessages.Where(x => x.Content.Contains(tenant)).Select(x => x.Type).ToListAsync()).Should().Contain(new[]
        {
            "Manufacturing.InventoryReservationExpired.v1", "Manufacturing.LotDispositionChanged.v1",
            "Manufacturing.SupplierCertificateExpired.v1", "Manufacturing.SupplierMaterialApprovalExpired.v1",
            "Manufacturing.RecipeVersionRetired.v1", "Manufacturing.InspectionPlanVersionRetired.v1",
            "Manufacturing.MaintenanceWorkOrderCreated.v1"
        });
    }

    [Fact]
    public async Task LifecycleAutomationProcessesConcurrentRunsOnce()
    {
        const string tenant = "tenant-lifecycle-concurrent";
        var certificateId = Guid.NewGuid();
        var concurrentSupplierId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var setup = await dbFactory.CreateDbContextAsync())
        {
            setup.Suppliers.Add(new ManufacturingSupplierEntity
            {
                Id = concurrentSupplierId, TenantKey = tenant, Code = "SUP-CONCURRENT", Name = "Concurrent supplier",
                LegalName = "Concurrent supplier", Active = true, CreatedAt = now
            });
            setup.SupplierCertificates.Add(new ManufacturingSupplierCertificateEntity
            {
                Id = certificateId, TenantKey = tenant, SupplierId = concurrentSupplierId, CertificateType = "HACCP",
                CertificateNumber = "CERT-CONCURRENT", Issuer = "QA", IssuedAt = now.AddYears(-1),
                ExpiresAt = now.AddMinutes(-1), Status = "Active", CreatedAt = now.AddYears(-1)
            });
            await setup.SaveChangesAsync();
        }

        var manufacturingDbFactory = new SingleConnectionDbContextFactory<ManufacturingDbContext>(dbFactory, "ManufacturingDb");
        var first = new ManufacturingLifecycleAutomation(manufacturingDbFactory).RunOnceAsync(now, 100);
        var second = new ManufacturingLifecycleAutomation(manufacturingDbFactory).RunOnceAsync(now, 100);
        var results = await Task.WhenAll(first, second);

        results.Sum(x => x.ExpiredSupplierCertificates).Should().Be(1);
        await using var verify = await dbFactory.CreateDbContextAsync();
        (await verify.SupplierCertificates.SingleAsync(x => x.Id == certificateId)).Status.Should().Be("Expired");
    }

    [Fact]
    public async Task MlTrainingDataCaptureAndDatasetQualityIsTenantScoped()
    {
        const string tenant = "tenant-ml-training";
        var recipeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        await using (var setup = await dbFactory.CreateDbContextAsync())
        {
            setup.Recipes.Add(new ManufacturingRecipeEntity { Id = recipeId, TenantKey = tenant, ProductSku = "FG-ML", Version = 1, ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 80, Active = true, Status = "Approved", CreatedAt = DateTimeOffset.UtcNow });
            setup.ProductionOrders.Add(new ManufacturingProductionOrderEntity { Id = orderId, TenantKey = tenant, OrderNumber = "PO-ML-001", ProductSku = "FG-ML", RecipeId = recipeId, RecipeVersion = 1, TargetQuantity = 100, OutputUom = "kg", Status = "InProgress", CreatedAt = DateTimeOffset.UtcNow });
            setup.ProductionBatches.Add(new ManufacturingProductionBatchEntity { Id = batchId, TenantKey = tenant, ProductionOrderId = orderId, BatchNumber = "B-ML-001", Status = "Started", PlannedQuantity = 100, CreatedAt = DateTimeOffset.UtcNow });
            await setup.SaveChangesAsync();
        }

        var store = new ManufacturingMlDataStore(dbFactory);
        var measurement = store.RecordOperationMeasurement(tenant, "operator-ml", new RecordOperationMeasurementRequest(batchId, null, null, null, "temperature", 62.5m, "C", DateTimeOffset.UtcNow));
        measurement.Error.Should().BeNull();
        store.GetOperationMeasurements(tenant, batchId, 10).Should().ContainSingle();

        var actual = store.RecordSalesActual(tenant, "sales-ml", new RecordSalesActualRequest("FG-ML", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 42, "kg", "online", "VN"));
        actual.Error.Should().BeNull();
        store.GetSalesActuals(tenant, "FG-ML", 10).Should().ContainSingle();

        var snapshotRequest = new MlFeatureSnapshotRequest("yield-v1", "production_batch", batchId, DateTimeOffset.UtcNow, "{\"input_kg\":100,\"temperature_c\":62.5}", "{\"yield_percent\":80}", "[\"measurement-1\"]");
        var snapshot = store.CreateFeatureSnapshot(tenant, "ml-pipeline", snapshotRequest);
        snapshot.Error.Should().BeNull();
        store.CreateFeatureSnapshot(tenant, "ml-pipeline", snapshotRequest).Error.Should().Be("ml_feature_snapshot_exists");
        store.CreateFeatureSnapshot(tenant, "ml-pipeline", snapshotRequest with { EntityId = Guid.NewGuid(), FeaturesJson = "not-json" }).Error.Should().Be("invalid_ml_json");

        var quality = store.GetDatasetQuality(tenant, "yield-v1");
        quality.RowCount.Should().Be(1);
        quality.LabeledRowCount.Should().Be(1);
        quality.Warnings.Should().Contain("split_unassigned");
    }

    private sealed class TestDbContextFactory(DbContextOptions<ManufacturingDbContext> options) : IDbContextFactory<ManufacturingDbContext>
    {
        public ManufacturingDbContext CreateDbContext() => new(options);
        public Task<ManufacturingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ManufacturingDbContext(options));
    }
}
