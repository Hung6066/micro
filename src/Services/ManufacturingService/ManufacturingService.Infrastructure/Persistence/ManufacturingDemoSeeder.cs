using Microsoft.EntityFrameworkCore;

public static class ManufacturingDemoSeeder
{
    public const string TenantKey = "manufacturing";
    private const string LegacyTenantKey = "manufacturing-demo";
    private static DateTimeOffset SeededAt => DateTimeOffset.UtcNow;

    public static void Seed(IDbContextFactory<ManufacturingDbContext> dbFactory)
    {
        using var db = dbFactory.CreateDbContext();
        if (db.AuditEvents.Any(x => x.TenantKey == LegacyTenantKey && x.EntityType == "DemoSeed" && x.Action == "v1"))
            MigrateLegacyTenant(db);

        if (db.AuditEvents.Any(x => x.TenantKey == TenantKey && x.EntityType == "DemoSeed" && x.Action == "v1"))
        {
            var now = DateTime.UtcNow;
            var rawMango = db.Lots.SingleOrDefault(x => x.Id == Id("lot-raw-mango-001"));
            var rawSugar = db.Lots.SingleOrDefault(x => x.Id == Id("lot-raw-sugar-001"));
            var finishedMango = db.Lots.SingleOrDefault(x => x.Id == Id("lot-finished-mango-001"));
            var reservation = db.LotReservations.SingleOrDefault(x => x.Id == Id("reservation-production-001"));
            var machine = db.Machines.SingleOrDefault(x => x.Id == Id("machine-dryer-01"));
            var maintenance = db.MaintenanceWorkOrders.SingleOrDefault(x => x.Id == Id("work-order-dryer-01"));
            var demoInspection = db.QualityInspections.SingleOrDefault(x => x.Id == Id("inspection-001"));
            var demoBatch = db.ProductionBatches.SingleOrDefault(x => x.Id == Id("production-batch-001"));
            if (demoBatch is not null && !db.ProductionBatchCosts.Any(x => x.Id == Id("production-batch-cost-001")))
                db.ProductionBatchCosts.Add(new ManufacturingProductionBatchCostEntity { Id = Id("production-batch-cost-001"), ProductionBatchId = demoBatch.Id, TenantKey = TenantKey, MaterialCost = 1_850_000m, LaborCost = 250_000m, OverheadCost = 100_000m, LossCost = 370_000m, TotalCost = 2_200_000m, CostPerOutputUnit = 27_500m, Currency = "VND", CalculatedAt = DateTimeOffset.UtcNow, CalculatedBy = "costing.demo" });
            if (demoInspection is not null && !db.QualitySamples.Any(x => x.Id == Id("sample-output-001")))
                db.QualitySamples.Add(new ManufacturingQualitySampleEntity { Id = Id("sample-output-001"), InspectionId = demoInspection.Id, LotId = demoInspection.LotId, TenantKey = TenantKey, SampleCode = "SAMPLE-FG-001", CollectedBy = "qa.demo", CollectedAt = demoInspection.InspectedAt, Disposition = "Accepted", DispositionReason = "Passed product inspection", DisposedBy = "qa.demo", DisposedAt = demoInspection.InspectedAt.AddMinutes(5), Location = "QA lab", Notes = "Demo retained sample", CreatedAt = demoInspection.InspectedAt });
            if (!db.InspectionPlanVersions.Any(x => x.Id == Id("inspection-plan-mango-v1")))
                db.InspectionPlanVersions.Add(new ManufacturingInspectionPlanVersionEntity { Id = Id("inspection-plan-mango-v1"), TenantKey = TenantKey, PlanCode = "IP-MANGO", ProductSku = "FG-MANGO-CHILI", Version = 1, SamplingMethod = "Per lot", SamplingFrequency = "Every lot", AcceptanceCriteria = "Moisture <= 12%; sensory pass", Status = "Approved", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30), ApprovedBy = "qa.demo", ApprovedAt = DateTimeOffset.UtcNow.AddDays(-30), CreatedBy = "seed", CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
            if (machine is not null && !db.MachineCalibrations.Any(x => x.Id == Id("calibration-dryer-01")))
                db.MachineCalibrations.Add(new ManufacturingMachineCalibrationEntity { Id = Id("calibration-dryer-01"), MachineId = machine.Id, TenantKey = TenantKey, CalibrationType = "Temperature sensor", CertificateNumber = "CAL-DRY-2026-001", CalibratedAt = DateTimeOffset.UtcNow.AddDays(-30), NextDueAt = DateTimeOffset.UtcNow.AddDays(335), Result = "Pass", Provider = "Nacoms Metrology", EvidenceReference = "cert://demo/calibration-dryer-01", Notes = "Demo calibration record", CreatedBy = "seed", CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
            if (rawMango is not null) rawMango.BestBefore = DateOnly.FromDateTime(now.AddDays(10));
            if (rawSugar is not null) rawSugar.BestBefore = DateOnly.FromDateTime(now.AddDays(180));
            if (finishedMango is not null) finishedMango.BestBefore = DateOnly.FromDateTime(now.AddDays(365));
            if (reservation is not null) reservation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
            if (machine is not null) machine.NextMaintenanceAt = DateTimeOffset.UtcNow.AddDays(30);
            if (maintenance is not null) maintenance.DueAt = DateTimeOffset.UtcNow.AddDays(30);
            if (machine is not null && !db.MaintenancePlans.Any(x => x.Id == Id("maintenance-plan-dryer-01")))
                db.MaintenancePlans.Add(new ManufacturingMaintenancePlanEntity { Id = Id("maintenance-plan-dryer-01"), MachineId = machine.Id, TenantKey = TenantKey, PlanCode = "MP-DRY-30D", MaintenanceType = "Preventive", FrequencyDays = 30, NextDueAt = DateTimeOffset.UtcNow.AddDays(30), Checklist = "Check belts; verify airflow; calibrate sensor", AssignedTo = "maintenance.demo", Active = true, CreatedBy = "seed", CreatedAt = DateTimeOffset.UtcNow });
            db.SaveChanges();
            return;
        }

        using var transaction = db.Database.BeginTransaction();
        var uomKg = Id("uom-kg");
        var uomG = Id("uom-g");
        var uomBox = Id("uom-box");
        db.Uoms.AddRange(
            new ManufacturingUomEntity { Id = uomKg, Code = "kg", Name = "Kilogram", Dimension = "Mass", Active = true, CreatedAt = SeededAt },
            new ManufacturingUomEntity { Id = uomG, Code = "g", Name = "Gram", Dimension = "Mass", Active = true, CreatedAt = SeededAt },
            new ManufacturingUomEntity { Id = uomBox, Code = "box", Name = "Box", Dimension = "Count", Active = true, CreatedAt = SeededAt });
        db.UomConversions.Add(new ManufacturingUomConversionEntity { Id = Id("conversion-kg-g"), FromCode = "kg", ToCode = "g", Factor = 1000, Active = true, CreatedAt = SeededAt });

        var facilityId = Id("facility-main");
        var warehouseId = Id("warehouse-main");
        var locationId = Id("location-raw");
        db.Facilities.Add(new ManufacturingFacilityEntity { Id = facilityId, TenantKey = TenantKey, Code = "FAC-HCM", Name = "Nacoms HCM Factory", Active = true, CreatedAt = SeededAt });
        db.Warehouses.Add(new ManufacturingWarehouseEntity { Id = warehouseId, TenantKey = TenantKey, FacilityId = facilityId, Code = "WH-RAW", Name = "Raw material warehouse", Active = true, CreatedAt = SeededAt });
        db.StorageLocations.Add(new ManufacturingStorageLocationEntity { Id = locationId, TenantKey = TenantKey, WarehouseId = warehouseId, Code = "A-01-01", Name = "Mango receiving bay", Active = true, CreatedAt = SeededAt });

        var rawMangoId = Id("material-mango");
        var rawSugarId = Id("material-sugar");
        var productId = Id("product-dried-mango");
        db.Materials.AddRange(
            new ManufacturingMaterialEntity { Id = rawMangoId, TenantKey = TenantKey, Sku = "RM-MANGO", Name = "Fresh mango", BaseUomCode = "kg", MaterialType = "RawMaterial", Active = true, CreatedAt = SeededAt },
            new ManufacturingMaterialEntity { Id = rawSugarId, TenantKey = TenantKey, Sku = "RM-SUGAR", Name = "Cane sugar", BaseUomCode = "kg", MaterialType = "Ingredient", Active = true, CreatedAt = SeededAt });
        db.Products.Add(new ManufacturingProductEntity { Id = productId, TenantKey = TenantKey, Sku = "FG-MANGO-CHILI", Name = "Dried mango chili", BaseUomCode = "kg", Active = true, CreatedAt = SeededAt });

        var specId = Id("spec-mango");
        var recipeId = Id("recipe-mango-v1");
        db.ProductSpecifications.Add(new ManufacturingProductSpecificationEntity { Id = specId, TenantKey = TenantKey, ProductSku = "FG-MANGO-CHILI", TargetMoisturePercent = 12, Packaging = "500g stand-up pouch", ShelfLifeDays = 365, QcSpec = "Moisture <= 12%; sensory pass", Status = "Approved", ApprovedBy = "qa.demo", ApprovedAt = SeededAt, CreatedAt = SeededAt });
        db.InspectionPlanVersions.Add(new ManufacturingInspectionPlanVersionEntity { Id = Id("inspection-plan-mango-v1"), TenantKey = TenantKey, PlanCode = "IP-MANGO", ProductSku = "FG-MANGO-CHILI", Version = 1, SamplingMethod = "Per lot", SamplingFrequency = "Every lot", AcceptanceCriteria = "Moisture <= 12%; sensory pass", Status = "Approved", EffectiveFrom = SeededAt, ApprovedBy = "qa.demo", ApprovedAt = SeededAt, CreatedBy = "seed", CreatedAt = SeededAt });
        db.Recipes.Add(new ManufacturingRecipeEntity { Id = recipeId, TenantKey = TenantKey, ProductSku = "FG-MANGO-CHILI", Version = 1, ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 80, Active = true, Status = "Approved", EffectiveFrom = SeededAt, ApprovedBy = "qa.demo", ApprovedAt = SeededAt, ProductSpecificationId = specId, CreatedAt = SeededAt, Components = [new ManufacturingRecipeComponentEntity { RecipeId = recipeId, IngredientSku = "RM-MANGO", Quantity = 5, Uom = "kg" }, new ManufacturingRecipeComponentEntity { RecipeId = recipeId, IngredientSku = "RM-SUGAR", Quantity = 0.2m, Uom = "kg" }] });

        var machineId = Id("machine-dryer-01");
        db.Machines.Add(new ManufacturingMachineEntity { Id = machineId, TenantKey = TenantKey, Code = "DRY-01", Name = "Industrial dryer 01", Status = "Available", LastMaintenanceAt = SeededAt.AddDays(-30), NextMaintenanceAt = SeededAt.AddDays(30), Active = true, CreatedAt = SeededAt });
        db.MachineCalibrations.Add(new ManufacturingMachineCalibrationEntity { Id = Id("calibration-dryer-01"), MachineId = machineId, TenantKey = TenantKey, CalibrationType = "Temperature sensor", CertificateNumber = "CAL-DRY-2026-001", CalibratedAt = SeededAt.AddDays(-30), NextDueAt = SeededAt.AddDays(335), Result = "Pass", Provider = "Nacoms Metrology", EvidenceReference = "cert://demo/calibration-dryer-01", Notes = "Demo calibration record", CreatedBy = "seed", CreatedAt = SeededAt.AddDays(-30) });
        db.MachineTelemetry.Add(new ManufacturingMachineTelemetryEntity { Id = Id("telemetry-dryer-01"), EventId = Id("telemetry-event-dryer-01"), MachineId = machineId, TenantKey = TenantKey, Source = "demo-sensor", State = "Running", MeterName = "temperature_c", MeterValue = 62.5m, Sequence = 1, ObservedAt = SeededAt.AddHours(2), ReceivedAt = SeededAt.AddHours(2) });
        db.MaintenanceWorkOrders.Add(new ManufacturingMaintenanceWorkOrderEntity { Id = Id("work-order-dryer-01"), MachineId = machineId, TenantKey = TenantKey, Status = "Open", MaintenanceType = "Preventive", DueAt = SeededAt.AddDays(30), AssignedTo = "maintenance.demo", Notes = "Check belts and airflow", CreatedAt = SeededAt });
        db.MaintenancePlans.Add(new ManufacturingMaintenancePlanEntity { Id = Id("maintenance-plan-dryer-01"), MachineId = machineId, TenantKey = TenantKey, PlanCode = "MP-DRY-30D", MaintenanceType = "Preventive", FrequencyDays = 30, NextDueAt = SeededAt.AddDays(30), Checklist = "Check belts; verify airflow; calibrate sensor", AssignedTo = "maintenance.demo", Active = true, CreatedBy = "seed", CreatedAt = SeededAt });

        var supplierId = Id("supplier-mekong");
        var rfqId = Id("rfq-mango-001");
        db.Suppliers.Add(new ManufacturingSupplierEntity { Id = supplierId, TenantKey = TenantKey, Code = "SUP-MEKONG", Name = "Mekong Fresh Co.", LegalName = "Mekong Fresh Company Limited", TaxIdentificationNumber = "0312345678", ContactName = "Nguyen Van Anh", ContactEmail = "qa@mekongfresh.example", ContactPhone = "+84 28 5555 0101", CountryCode = "VN", Address = "Tien Giang, Vietnam", RiskLevel = "Standard", ApprovalStatus = "Approved", ApprovedBy = "qa.demo", ApprovedAt = SeededAt, LastReviewedAt = SeededAt, CreatedBy = "seed", UpdatedAt = SeededAt, Active = true, CreatedAt = SeededAt });
        db.SupplierMaterialApprovals.AddRange(
            new ManufacturingSupplierMaterialApprovalEntity { Id = Id("supplier-material-mango"), TenantKey = TenantKey, SupplierId = supplierId, MaterialSku = "RM-MANGO", ApprovedUom = "kg", EffectiveFrom = SeededAt.AddDays(-30), Status = "Approved", Notes = "Demo approved raw mango", CreatedAt = SeededAt, CreatedBy = "seed" },
            new ManufacturingSupplierMaterialApprovalEntity { Id = Id("supplier-material-sugar"), TenantKey = TenantKey, SupplierId = supplierId, MaterialSku = "RM-SUGAR", ApprovedUom = "kg", EffectiveFrom = SeededAt.AddDays(-30), Status = "Approved", Notes = "Demo approved sugar", CreatedAt = SeededAt, CreatedBy = "seed" });
        db.SupplierRfqs.Add(new ManufacturingSupplierRfqEntity { Id = rfqId, TenantKey = TenantKey, RfqNumber = "RFQ-2026-001", MaterialSku = "RM-MANGO", Quantity = 1000, Uom = "kg", Status = "Open", NeededBy = SeededAt.AddDays(7), CreatedAt = SeededAt });
        db.SupplierQuotations.Add(new ManufacturingSupplierQuotationEntity { Id = Id("quotation-mekong"), TenantKey = TenantKey, SupplierRfqId = rfqId, SupplierId = supplierId, UnitPrice = 18500, Currency = "VND", LeadTimeDays = 2, Status = "Selected", Notes = "Demo preferred supplier", CreatedAt = SeededAt });
        db.SupplierEvaluations.Add(new ManufacturingSupplierEvaluationEntity { Id = Id("evaluation-mekong"), TenantKey = TenantKey, SupplierId = supplierId, Score = 5, QualityNotes = "Consistent ripeness", DeliveryNotes = "On time", Notes = "Approved demo supplier", EvaluatedBy = "buyer.demo", EvaluatedAt = SeededAt });

        var rawLotId = Id("lot-raw-mango-001");
        var sugarLotId = Id("lot-raw-sugar-001");
        var outputLotId = Id("lot-finished-mango-001");
        db.Lots.AddRange(
            new ManufacturingLotEntity { Id = rawLotId, TenantKey = TenantKey, Sku = "RM-MANGO", Quantity = 500, Uom = "kg", Disposition = "Released", BestBefore = DateOnly.FromDateTime(SeededAt.AddDays(10).Date), LotCode = "LOT-RM-MANGO-001", LotType = "RawMaterial", OriginCountryCode = "VN", ReceivedAt = SeededAt, FacilityCode = "FAC-HCM", StorageLocationCode = "A-01-01", CertificateOfAnalysisReference = "coa://demo/mango-001", SourceLotCode = "MEKONG-LOT-001", QualityStatus = "Passed", CreatedBy = "qa.demo", CreatedAt = SeededAt },
            new ManufacturingLotEntity { Id = sugarLotId, TenantKey = TenantKey, Sku = "RM-SUGAR", Quantity = 50, Uom = "kg", Disposition = "Released", BestBefore = DateOnly.FromDateTime(SeededAt.AddDays(180).Date), LotCode = "LOT-RM-SUGAR-001", LotType = "RawMaterial", OriginCountryCode = "VN", ReceivedAt = SeededAt, FacilityCode = "FAC-HCM", StorageLocationCode = "A-01-01", QualityStatus = "Passed", CreatedBy = "qa.demo", CreatedAt = SeededAt },
            new ManufacturingLotEntity { Id = outputLotId, TenantKey = TenantKey, Sku = "FG-MANGO-CHILI", Quantity = 80, Uom = "kg", Disposition = "Released", BestBefore = DateOnly.FromDateTime(SeededAt.AddDays(365).Date), LotCode = "LOT-FG-MANGO-001", LotType = "FinishedGood", OriginCountryCode = "VN", FacilityCode = "FAC-HCM", QualityStatus = "Passed", CreatedBy = "qa.demo", CreatedAt = SeededAt.AddHours(6) });

        var poId = Id("purchase-order-001");
        var poLineId = Id("purchase-line-001");
        db.PurchaseOrders.Add(new ManufacturingPurchaseOrderEntity { Id = poId, TenantKey = TenantKey, SupplierId = supplierId, OrderNumber = "PO-2026-001", Status = "Received", Currency = "VND", OrderedAt = SeededAt, ExpectedAt = SeededAt.AddDays(2), Lines = [new ManufacturingPurchaseOrderLineEntity { Id = poLineId, PurchaseOrderId = poId, MaterialSku = "RM-MANGO", OrderedQuantity = 100, ReceivedQuantity = 100, Uom = "kg", UnitPrice = 18500 }] });
        db.InboundReceipts.Add(new ManufacturingInboundReceiptEntity { Id = Id("receipt-001"), TenantKey = TenantKey, ReceiptNumber = "GRN-2026-001", PurchaseOrderId = poId, PurchaseOrderLineId = poLineId, LotId = rawLotId, SupplierId = supplierId, SupplierLotCode = "MEKONG-LOT-001", FacilityId = "FAC-HCM", Quantity = 100, Uom = "kg", ReceivedAt = SeededAt.AddHours(1), StorageLocationCode = "A-01-01", DeliveryNoteNumber = "DN-MEKONG-001", CarrierName = "Mekong Logistics", VehicleReference = "51D-00001", CertificateOfAnalysisReference = "coa://demo/mango-001", ReceivedBy = "receiving.demo", AcceptedQuantity = 100, RejectedQuantity = 0 });

        var productionOrderId = Id("production-order-001");
        var batchId = Id("production-batch-001");
        var operationId = Id("operation-001");
        var reservationId = Id("reservation-production-001");
        db.ProductionOrders.Add(new ManufacturingProductionOrderEntity { Id = productionOrderId, TenantKey = TenantKey, OrderNumber = "MO-2026-001", ProductSku = "FG-MANGO-CHILI", RecipeId = recipeId, RecipeVersion = 1, TargetQuantity = 80, OutputUom = "kg", Status = "Released", CreatedAt = SeededAt, ReleasedAt = SeededAt.AddHours(1) });
        db.ProductionBatches.Add(new ManufacturingProductionBatchEntity { Id = batchId, TenantKey = TenantKey, ProductionOrderId = productionOrderId, BatchNumber = "BATCH-2026-001", Status = "Started", PlannedQuantity = 80, ActualOutputQuantity = 80, MachineId = machineId, OutputLotId = outputLotId, CreatedAt = SeededAt.AddHours(1), StartedAt = SeededAt.AddHours(2) });
        db.LotReservations.Add(new ManufacturingLotReservationEntity { Id = reservationId, TenantKey = TenantKey, LotId = rawLotId, ReferenceType = "ProductionBatch", ReferenceId = batchId, Quantity = 100, Uom = "kg", Status = "Reserved", CreatedAt = SeededAt.AddHours(1), ExpiresAt = SeededAt.AddDays(1) });
        db.ProductionBatchInputs.Add(new ManufacturingProductionBatchInputEntity { Id = Id("batch-input-001"), ProductionBatchId = batchId, LotId = rawLotId, ReservationId = reservationId, Quantity = 100 });
        db.OperationExecutions.Add(new ManufacturingOperationExecutionEntity { Id = operationId, ProductionBatchId = batchId, Sequence = 1, ProcessStep = "drying", Operator = "operator.demo", InputQuantity = 100, OutputQuantity = 80, LossQuantity = 20, Status = "Completed", Required = true, QcStatus = "Passed", StartedAt = SeededAt.AddHours(2), CompletedAt = SeededAt.AddHours(6) });
        db.ProductionBatchCosts.Add(new ManufacturingProductionBatchCostEntity { Id = Id("production-batch-cost-001"), ProductionBatchId = batchId, TenantKey = TenantKey, MaterialCost = 1_850_000m, LaborCost = 250_000m, OverheadCost = 100_000m, LossCost = 370_000m, TotalCost = 2_200_000m, CostPerOutputUnit = 27_500m, Currency = "VND", CalculatedAt = SeededAt.AddHours(7), CalculatedBy = "costing.demo" });
        db.LossReviews.Add(new ManufacturingLossReviewEntity { Id = Id("loss-review-001"), TenantKey = TenantKey, ProductionBatchId = batchId, OperationExecutionId = operationId, Decision = "Approved", Reviewer = "qa.demo", Notes = "Expected drying loss", ReviewedAt = SeededAt.AddHours(7) });
        db.Transformations.Add(new ManufacturingTransformationEntity { Id = Id("transformation-001"), TenantKey = TenantKey, ProcessStep = "drying", RecipeId = recipeId, MachineId = machineId, OutputLotId = outputLotId, InputQuantity = 100, OutputQuantity = 80, YieldPercent = 80, LossQuantity = 20, CreatedAt = SeededAt.AddHours(6), Inputs = [new ManufacturingTransformationInputEntity { TransformationId = Id("transformation-001"), LotId = rawLotId, Quantity = 100 }] });

        db.QualityInspections.Add(new ManufacturingQualityInspectionEntity { Id = Id("inspection-001"), LotId = outputLotId, TenantKey = TenantKey, Status = "Approved", MoisturePercent = 11.4m, Inspector = "qa.demo", Notes = "Meets product specification", InspectedAt = SeededAt.AddHours(8) });
        db.QualitySamples.Add(new ManufacturingQualitySampleEntity { Id = Id("sample-output-001"), InspectionId = Id("inspection-001"), LotId = outputLotId, TenantKey = TenantKey, SampleCode = "SAMPLE-FG-001", CollectedBy = "qa.demo", CollectedAt = SeededAt.AddHours(8), Disposition = "Accepted", DispositionReason = "Passed product inspection", DisposedBy = "qa.demo", DisposedAt = SeededAt.AddHours(8).AddMinutes(5), Location = "QA lab", Notes = "Demo retained sample", CreatedAt = SeededAt.AddHours(8) });
        var deviationId = Id("deviation-001");
        db.Deviations.Add(new ManufacturingDeviationEntity { Id = deviationId, TenantKey = TenantKey, ProductionBatchId = batchId, Type = "Quality", Description = "Drying loss above nominal target", Impact = "Yield variance", Status = "Requested", RequestedBy = "operator.demo", CreatedAt = SeededAt.AddHours(7) });
        db.Capas.Add(new ManufacturingCapaEntity { Id = Id("capa-001"), TenantKey = TenantKey, DeviationId = deviationId, SupplierId = supplierId, Title = "Review dryer airflow", ProblemDescription = "Observed yield variance", RootCause = "Airflow calibration drift", CorrectiveAction = "Calibrate airflow sensor", PreventiveAction = "Add weekly calibration check", Owner = "quality.demo", Status = "Open", DueAt = SeededAt.AddDays(14), CreatedAt = SeededAt.AddHours(8) });
        db.SalesForecasts.Add(new ManufacturingSalesForecastEntity { Id = Id("forecast-001"), TenantKey = TenantKey, ProductSku = "FG-MANGO-CHILI", PeriodStart = DateOnly.FromDateTime(SeededAt.Date), PeriodEnd = DateOnly.FromDateTime(SeededAt.AddDays(30).Date), Quantity = 300, Uom = "kg", Source = "Sales", Actor = "sales.demo", Version = 1, CreatedAt = SeededAt });

        db.InventoryTransactions.AddRange(
            new ManufacturingInventoryTransactionEntity { Id = Id("inventory-receipt-001"), TenantKey = TenantKey, LotId = rawLotId, TransactionType = "Receipt", Quantity = 100, Uom = "kg", FacilityId = "FAC-HCM", StockStatus = "Released", CorrelationId = Id("receipt-001"), OccurredAt = SeededAt.AddHours(1) },
            new ManufacturingInventoryTransactionEntity { Id = Id("inventory-reserve-001"), TenantKey = TenantKey, LotId = rawLotId, TransactionType = "Reserve", Quantity = 100, Uom = "kg", FacilityId = "FAC-HCM", StockStatus = "Released", CorrelationId = reservationId, OccurredAt = SeededAt.AddHours(1) },
            new ManufacturingInventoryTransactionEntity { Id = Id("inventory-output-001"), TenantKey = TenantKey, LotId = outputLotId, TransactionType = "ProductionOutput", Quantity = 80, Uom = "kg", FacilityId = "FAC-HCM", StockStatus = "Released", CorrelationId = batchId, OccurredAt = SeededAt.AddHours(6) });
        db.AuditEvents.Add(new ManufacturingAuditEventEntity { Id = Id("demo-seed-marker"), TenantKey = TenantKey, EntityType = "DemoSeed", EntityId = Id("demo-seed"), Action = "v1", Actor = "system", Details = "Complete Manufacturing demo graph", OccurredAt = SeededAt });

        db.SaveChanges();
        transaction.Commit();
    }

    private static Guid Id(string value) => Guid.Parse(value switch
    {
        "uom-kg" => "10000000-0000-0000-0000-000000000001", "uom-g" => "10000000-0000-0000-0000-000000000002", "uom-box" => "10000000-0000-0000-0000-000000000003",
        "conversion-kg-g" => "10000000-0000-0000-0000-000000000004", "facility-main" => "10000000-0000-0000-0000-000000000010", "warehouse-main" => "10000000-0000-0000-0000-000000000011", "location-raw" => "10000000-0000-0000-0000-000000000012",
        "material-mango" => "10000000-0000-0000-0000-000000000020", "material-sugar" => "10000000-0000-0000-0000-000000000021", "product-dried-mango" => "10000000-0000-0000-0000-000000000022", "spec-mango" => "10000000-0000-0000-0000-000000000023", "recipe-mango-v1" => "10000000-0000-0000-0000-000000000024",
        "machine-dryer-01" => "10000000-0000-0000-0000-000000000030", "telemetry-dryer-01" => "10000000-0000-0000-0000-000000000031", "telemetry-event-dryer-01" => "10000000-0000-0000-0000-000000000032", "work-order-dryer-01" => "10000000-0000-0000-0000-000000000033",
        "supplier-mekong" => "10000000-0000-0000-0000-000000000040", "rfq-mango-001" => "10000000-0000-0000-0000-000000000041", "quotation-mekong" => "10000000-0000-0000-0000-000000000042", "evaluation-mekong" => "10000000-0000-0000-0000-000000000043", "supplier-material-mango" => "10000000-0000-0000-0000-000000000044", "supplier-material-sugar" => "10000000-0000-0000-0000-000000000045", "calibration-dryer-01" => "10000000-0000-0000-0000-000000000046", "inspection-plan-mango-v1" => "10000000-0000-0000-0000-000000000047", "sample-output-001" => "10000000-0000-0000-0000-000000000048", "maintenance-plan-dryer-01" => "10000000-0000-0000-0000-000000000049",
        "lot-raw-mango-001" => "10000000-0000-0000-0000-000000000050", "lot-raw-sugar-001" => "10000000-0000-0000-0000-000000000051", "lot-finished-mango-001" => "10000000-0000-0000-0000-000000000052",
        "purchase-order-001" => "10000000-0000-0000-0000-000000000060", "purchase-line-001" => "10000000-0000-0000-0000-000000000061", "receipt-001" => "10000000-0000-0000-0000-000000000062",
        "production-order-001" => "10000000-0000-0000-0000-000000000070", "production-batch-001" => "10000000-0000-0000-0000-000000000071", "operation-001" => "10000000-0000-0000-0000-000000000072", "reservation-production-001" => "10000000-0000-0000-0000-000000000073", "batch-input-001" => "10000000-0000-0000-0000-000000000074", "transformation-001" => "10000000-0000-0000-0000-000000000075", "loss-review-001" => "10000000-0000-0000-0000-000000000076", "production-batch-cost-001" => "10000000-0000-0000-0000-000000000077",
        "inspection-001" => "10000000-0000-0000-0000-000000000080", "deviation-001" => "10000000-0000-0000-0000-000000000081", "capa-001" => "10000000-0000-0000-0000-000000000082", "forecast-001" => "10000000-0000-0000-0000-000000000083",
        "inventory-receipt-001" => "10000000-0000-0000-0000-000000000090", "inventory-reserve-001" => "10000000-0000-0000-0000-000000000091", "inventory-output-001" => "10000000-0000-0000-0000-000000000092", "demo-seed-marker" => "10000000-0000-0000-0000-000000000099", "demo-seed" => "10000000-0000-0000-0000-000000000098",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown demo seed id")
    });

    private static void MigrateLegacyTenant(ManufacturingDbContext db)
    {
        const string oldTenant = LegacyTenantKey;
        const string newTenant = TenantKey;
        var tables = new[]
        {
            "manufacturing_audit_events", "manufacturing_capas", "manufacturing_deviations", "manufacturing_facilities",
            "manufacturing_inbound_receipts", "manufacturing_inventory_transactions", "manufacturing_loss_reviews",
            "manufacturing_lot_reservations", "manufacturing_lots", "manufacturing_machine_downtimes",
            "manufacturing_machine_telemetry", "manufacturing_machines", "manufacturing_maintenance_work_orders",
            "manufacturing_materials", "manufacturing_product_specifications", "manufacturing_production_batches",
            "manufacturing_production_orders", "manufacturing_products", "manufacturing_purchase_orders",
            "manufacturing_quality_inspections", "manufacturing_recipes", "manufacturing_sales_forecasts",
            "manufacturing_storage_locations", "manufacturing_production_batch_costs", "manufacturing_supplier_evaluations", "manufacturing_supplier_quotations",
            "manufacturing_supplier_rfqs", "manufacturing_supplier_certificates", "manufacturing_supplier_material_approvals", "manufacturing_suppliers", "manufacturing_transformations", "manufacturing_warehouses"
        };

        using var transaction = db.Database.BeginTransaction();
        foreach (var table in tables)
        {
#pragma warning disable EF1002 // table names come exclusively from the constant allow-list above.
            db.Database.ExecuteSqlRaw($"UPDATE {table} SET \"TenantKey\" = @p0 WHERE \"TenantKey\" = @p1", newTenant, oldTenant);
#pragma warning restore EF1002
        }
        transaction.Commit();
    }
}
