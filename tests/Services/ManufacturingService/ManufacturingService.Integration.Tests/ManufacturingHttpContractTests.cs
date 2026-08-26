using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using His.Hope.SharedKernel.Authorization;
using Xunit;

public sealed class ManufacturingHttpContractTests : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private string connection = string.Empty;

    public async Task InitializeAsync()
    {
        connection = Environment.GetEnvironmentVariable("MANUFACTURING_TEST_POSTGRES_CONNECTION") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connection))
        {
            container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("manufacturinghttp")
                .WithUsername("testuser")
                .WithPassword("testpass123!")
                .WithCleanUp(true)
                .Build();
            await container.StartAsync();
            connection = container.GetConnectionString();
        }

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:ManufacturingDb", connection);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddAuthentication(TestAuthHandler.TestScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.TestScheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
                });
            });
        });
        client = factory.CreateClient();
    }

    [Fact]
    public async Task DemoSeed_creates_relationship_complete_graph_for_operator_features()
    {
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
        ManufacturingDemoSeeder.Seed(dbFactory);
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Products.Count(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey).Should().Be(1);
        var recipe = await db.Recipes.Include(x => x.Components).SingleAsync(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey && x.ProductSku == "FG-MANGO-CHILI");
        recipe.Status.Should().Be("Approved");
        recipe.Components.Should().HaveCount(2);
        var batch = await db.ProductionBatches.SingleAsync(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey && x.BatchNumber == "BATCH-2026-001");
        db.ProductionOrders.Any(x => x.Id == batch.ProductionOrderId && x.RecipeId == recipe.Id).Should().BeTrue();
        db.ProductionBatchInputs.Any(x => x.ProductionBatchId == batch.Id && x.ReservationId != Guid.Empty).Should().BeTrue();
        db.OperationExecutions.Any(x => x.ProductionBatchId == batch.Id && x.LossQuantity > 0).Should().BeTrue();
        db.LossReviews.Any(x => x.ProductionBatchId == batch.Id).Should().BeTrue();
        db.QualityInspections.Any(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey && x.Status == "Approved").Should().BeTrue();
        db.InboundReceipts.Any(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey).Should().BeTrue();
        db.SupplierQuotations.Any(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey && x.Status == "Selected").Should().BeTrue();
        db.Capas.Any(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey && x.DeviationId != null).Should().BeTrue();
        db.SalesForecasts.Any(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey).Should().BeTrue();
        db.AuditEvents.Count(x => x.TenantKey == ManufacturingDemoSeeder.TenantKey && x.EntityType == "DemoSeed").Should().Be(1);
    }

    [Fact]
    public async Task PurchaseOrder_list_returns_orders_with_supplier_without_concurrent_database_reader()
    {
        var supplierResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new
        {
            tenantKey = "http-integration-tenant", code = "SUP-LIST", name = "List supplier", active = true
        });
        supplierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplierId = (await ReadJson(supplierResponse)).GetProperty("id").GetGuid();

        var orderResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/purchase-orders", new
        {
            tenantKey = "http-integration-tenant", orderNumber = "PO-LIST", supplierId, status = "Approved",
            currency = "VND", lines = new[] { new { materialSku = "RM-LIST", orderedQuantity = 5m, uom = "kg", unitPrice = 100m } }
        });
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetAsync("/api/v1/manufacturing/purchase-orders?tenantKey=http-integration-tenant");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(listResponse)).GetArrayLength().Should().Be(1);
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        factory.Dispose();
        if (container is not null) await container.DisposeAsync();
    }

    [Fact]
    public async Task ProductionWorkflow_IsAvailableThroughAuthenticatedHttpContract()
    {
        const string tenant = "http-integration-tenant";
        var recipeId = Guid.NewGuid();

        var recipeResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/recipes", new
        {
            tenantKey = tenant, productSku = "FG-HTTP-MANGO", version = 1, processStep = "drying",
            outputUom = "kg", targetYieldPercent = 80, active = true,
            components = new[] { new { ingredientSku = "RM-HTTP-MANGO", quantity = 1m, uom = "kg" } }
        });
        recipeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        recipeId = (await ReadJson(recipeResponse)).GetProperty("id").GetGuid();

        var orderResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/production-orders", new
        {
            orderNumber = "PO-HTTP-001", productSku = "FG-HTTP-MANGO", recipeId, targetQuantity = 48, outputUom = "kg"
        });
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdOrder = await ReadJson(orderResponse);
        createdOrder.GetProperty("status").GetString().Should().Be("Planned");
        var orderId = createdOrder.GetProperty("id").GetGuid();
        var releaseResponse = await client.PostAsync($"/api/v1/manufacturing/production-orders/{orderId}/release", null);
        releaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(releaseResponse)).GetProperty("status").GetString().Should().Be("Released");
        var listedOrdersResponse = await client.GetAsync("/api/v1/manufacturing/production-orders");
        listedOrdersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(listedOrdersResponse)).GetArrayLength().Should().Be(1);

        var lotResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/lots", new
        {
            tenantKey = tenant, sku = "RM-HTTP-MANGO", quantity = 100, uom = "kg", disposition = "Released"
        });
        lotResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var lotId = (await ReadJson(lotResponse)).GetProperty("id").GetGuid();

        var reservationResponse = await client.PostAsJsonAsync($"/api/v1/manufacturing/lots/{lotId}/reservations", new
        {
            referenceType = "ProductionOrder", referenceId = orderId, quantity = 60
        });
        reservationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservationId = (await ReadJson(reservationResponse)).GetProperty("id").GetGuid();
        var reservationsResponse = await client.GetAsync($"/api/v1/manufacturing/lots/{lotId}/reservations?status=Reserved");
        reservationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(reservationsResponse)).GetArrayLength().Should().Be(1);

        var batchResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/production-batches", new
        {
            productionOrderId = orderId, batchNumber = "BATCH-HTTP-001", plannedQuantity = 48,
            inputs = new[] { new { lotId, reservationId, quantity = 60 } }
        });
        batchResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBatch = await ReadJson(batchResponse);
        createdBatch.GetProperty("status").GetString().Should().Be("Created");
        var batchId = createdBatch.GetProperty("id").GetGuid();

        var startedBatch = await client.PostAsync($"/api/v1/manufacturing/production-batches/{batchId}/start", null);
        startedBatch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(startedBatch)).GetProperty("status").GetString().Should().Be("Started");
        var pausedBatch = await client.PostAsync($"/api/v1/manufacturing/production-batches/{batchId}/pause", null);
        pausedBatch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(pausedBatch)).GetProperty("status").GetString().Should().Be("Paused");
        var resumedBatch = await client.PostAsync($"/api/v1/manufacturing/production-batches/{batchId}/resume", null);
        resumedBatch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(resumedBatch)).GetProperty("status").GetString().Should().Be("Started");

        var deviationCreate = await client.PostAsJsonAsync($"/api/v1/manufacturing/production-batches/{batchId}/deviations", new
        {
            type = "Quality", description = "Moisture sample requires review", impact = "Hold release", requestedBy = "operator"
        });
        deviationCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var deviationId = (await ReadJson(deviationCreate)).GetProperty("id").GetGuid();
        var deviationApprove = await client.PostAsJsonAsync($"/api/v1/manufacturing/deviations/{deviationId}/approve", new { actor = "qc-reviewer", notes = "Reviewed sample" });
        deviationApprove.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(deviationApprove)).GetProperty("status").GetString().Should().Be("Approved");
        var deviationClose = await client.PostAsJsonAsync($"/api/v1/manufacturing/deviations/{deviationId}/close", new { actor = "qc-reviewer", notes = "Corrective action complete" });
        deviationClose.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(deviationClose)).GetProperty("status").GetString().Should().Be("Closed");

        const string operationId = "f99a2b3e-1c5d-4c4e-8d0d-111111111111";
        var operationPayload = JsonContent.Create(new
        {
            sequence = 1, processStep = "drying", @operator = "http-operator", inputQuantity = 60,
            outputQuantity = 48, required = true, qcStatus = "Pass"
        });
        using var operationRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/manufacturing/production-batches/{batchId}/operations")
        {
            Content = operationPayload
        };
        operationRequest.Headers.Add("X-HisHope-Operation-Id", operationId);
        var operationResponse = await client.SendAsync(operationRequest);
        operationResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/manufacturing/production-batches/{batchId}/operations")
        {
            Content = JsonContent.Create(new
            {
                sequence = 1, processStep = "drying", @operator = "http-operator", inputQuantity = 60,
                outputQuantity = 48, required = true, qcStatus = "Pass"
            })
        };
        replayRequest.Headers.Add("X-HisHope-Operation-Id", operationId);
        var replayResponse = await client.SendAsync(replayRequest);
          replayResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replayResponse.Headers.GetValues("X-HisHope-Operation-Replay").Single().Should().Be("true");

        var completeResponse = await client.PostAsync($"/api/v1/manufacturing/production-batches/{batchId}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await ReadJson(completeResponse);
        completed.GetProperty("outputLotId").GetGuid().Should().NotBeEmpty();
        completed.GetProperty("inputs").GetArrayLength().Should().Be(1);
        var outputLotId = completed.GetProperty("outputLotId").GetGuid();
        var quarantinedAvailability = await client.GetAsync("/api/v1/manufacturing/products/FG-HTTP-MANGO/availability");
        quarantinedAvailability.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(quarantinedAvailability)).GetProperty("releasedQuantity").GetDecimal().Should().Be(0m);
        var kpiResponse = await client.GetAsync("/api/v1/manufacturing/dashboard/production-kpis");
        kpiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var kpi = await ReadJson(kpiResponse);
        kpi.GetProperty("completedBatchCount").GetInt32().Should().Be(1);
        kpi.GetProperty("actualOutputQuantity").GetDecimal().Should().Be(48);
        kpi.GetProperty("totalInputQuantity").GetDecimal().Should().Be(60);
        var machineResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/machines", new
        {
            tenantKey = tenant, code = "M-HTTP-001", name = "HTTP Dryer", status = "Available",
            active = true, nextMaintenanceAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        machineResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var machineHealthResponse = await client.GetAsync("/api/v1/manufacturing/dashboard/machine-health?dueWithinDays=7");
        machineHealthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var machineHealth = await ReadJson(machineHealthResponse);
        machineHealth.GetProperty("totalMachineCount").GetInt32().Should().Be(1);
        machineHealth.GetProperty("availableMachineCount").GetInt32().Should().Be(1);
        machineHealth.GetProperty("dueWithinDaysCount").GetInt32().Should().Be(1);
        var productionCostResponse = await client.GetAsync("/api/v1/manufacturing/dashboard/production-costs");
        productionCostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var productionCost = await ReadJson(productionCostResponse);
        productionCost.GetProperty("completedBatchCount").GetInt32().Should().Be(1);
        productionCost.GetProperty("estimatedMaterialCost").GetDecimal().Should().Be(0);
        productionCost.GetProperty("missingPriceSkus").GetArrayLength().Should().Be(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
        await using var context = await db.CreateDbContextAsync();
        outputLotId = completed.GetProperty("outputLotId").GetGuid();
        (await context.Lots.SingleAsync(x => x.Id == outputLotId)).Disposition.Should().Be("Quarantined");
        (await context.QualityInspections.CountAsync(x => x.LotId == outputLotId && x.Status == "Pending")).Should().Be(1);
        (await context.Lots.SingleAsync(x => x.Id == lotId)).Quantity.Should().Be(40);
        (await context.LotReservations.SingleAsync(x => x.Id == reservationId)).Status.Should().Be("Consumed");
        (await context.InventoryTransactions.CountAsync(x => x.TransactionType == "Issue" && x.LotId == lotId)).Should().Be(1);

        var inspectionResponse = await client.PostAsJsonAsync("/api/v1/manufacturing/quality-inspections", new
        {
            lotId = outputLotId, tenantKey = tenant, status = "Pass", moisturePercent = 12.5m, inspector = "qc-http"
        });
        inspectionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        await context.Entry(await context.Lots.SingleAsync(x => x.Id == outputLotId)).ReloadAsync();
        (await context.Lots.SingleAsync(x => x.Id == outputLotId)).Disposition.Should().Be("Released");
        (await context.QualityInspections.CountAsync(x => x.LotId == outputLotId && x.Status == "Pending")).Should().Be(0);
        (await context.QualityInspections.CountAsync(x => x.LotId == outputLotId && x.Status == "Pass")).Should().Be(1);
        var releasedAvailability = await client.GetAsync("/api/v1/manufacturing/products/FG-HTTP-MANGO/availability");
        releasedAvailability.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(releasedAvailability)).GetProperty("availableToPromiseQuantity").GetDecimal().Should().Be(48m);
        var salesAllocation = await client.PostAsJsonAsync("/api/v1/manufacturing/sales/allocations/FG-HTTP-MANGO", new
        {
            salesOrderId = Guid.NewGuid(), quantity = 20m
        });
        salesAllocation.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Invalid_recipe_uses_shared_problem_details_contract()
    {
        var response = await client.PostAsJsonAsync("/api/v1/manufacturing/recipes", new
        {
            tenantKey = "http-integration-tenant",
            productSku = "",
            version = 0,
            processStep = "",
            outputUom = "",
            targetYieldPercent = 0,
            components = Array.Empty<object>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await ReadJson(response);
        problem.GetProperty("errorCode").GetString().Should().Be("invalid_recipe");
        problem.GetProperty("status").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task Insufficient_sales_allocation_uses_shared_problem_details_contract()
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/manufacturing/sales/allocations/FG-NOT-IN-STOCK",
            new { salesOrderId = Guid.NewGuid(), quantity = 10m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await ReadJson(response);
        problem.GetProperty("errorCode").GetString().Should().Be("insufficient_atp");
        problem.GetProperty("status").GetInt32().Should().Be(422);
    }

    [Fact]
    public async Task Quality_inspection_is_available_through_authenticated_http_contract()
    {
        var lot = await client.PostAsJsonAsync("/api/v1/manufacturing/lots", new
        {
            tenantKey = "http-integration-tenant", sku = "RM-QC-MANGO", quantity = 250m, uom = "kg", disposition = "Quarantined"
        });
        lot.StatusCode.Should().Be(HttpStatusCode.Created);
        var lotId = (await ReadJson(lot)).GetProperty("id").GetGuid();

        var inspection = await client.PostAsJsonAsync("/api/v1/manufacturing/quality-inspections", new
        {
            lotId, tenantKey = "http-integration-tenant", status = "Pass", moisturePercent = 11.5m,
            inspector = "qc-inspector", notes = "Within specification"
        });
        inspection.StatusCode.Should().Be(HttpStatusCode.Created);
        var inspectionJson = await ReadJson(inspection);
        inspectionJson.GetProperty("status").GetString().Should().Be("Pass");
        inspectionJson.GetProperty("moisturePercent").GetDecimal().Should().Be(11.5m);

        var history = await client.GetAsync($"/api/v1/manufacturing/lots/{lotId}/quality-inspections");
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(history)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Lot_disposition_and_genealogy_are_available_through_authenticated_http_contract()
    {
        var lot = await client.PostAsJsonAsync("/api/v1/manufacturing/lots", new
        {
            tenantKey = "http-integration-tenant", sku = "FG-TRACEABILITY", quantity = 80m, uom = "kg", disposition = "Quarantined"
        });
        lot.StatusCode.Should().Be(HttpStatusCode.Created);
        var lotId = (await ReadJson(lot)).GetProperty("id").GetGuid();

        var released = await client.PostAsJsonAsync($"/api/v1/manufacturing/lots/{lotId}/disposition", new { disposition = "Released" });
        released.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(released)).GetProperty("disposition").GetString().Should().Be("Released");

        var genealogy = await client.GetAsync($"/api/v1/manufacturing/lots/{lotId}/genealogy?direction=upstream");
        genealogy.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await ReadJson(genealogy);
        result.GetProperty("lot").GetProperty("id").GetGuid().Should().Be(lotId);
        result.GetProperty("relations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Sales_allocation_returns_atp_and_reservation_contract()
    {
        var lot = await client.PostAsJsonAsync("/api/v1/manufacturing/lots", new
        {
            tenantKey = "http-integration-tenant", sku = "FG-ATP-SALES", quantity = 120m, uom = "kg", disposition = "Released"
        });
        lot.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderId = Guid.NewGuid();

        var availability = await client.GetAsync("/api/v1/manufacturing/products/FG-ATP-SALES/availability");
        availability.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(availability)).GetProperty("availableToPromiseQuantity").GetDecimal().Should().Be(120m);

        var allocation = await client.PostAsJsonAsync("/api/v1/manufacturing/sales/allocations/FG-ATP-SALES", new { salesOrderId = orderId, quantity = 40m });
        allocation.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await ReadJson(allocation);
        result.GetProperty("allocatedQuantity").GetDecimal().Should().Be(40m);
        result.GetProperty("shortageQuantity").GetDecimal().Should().Be(0m);
        result.GetProperty("reservations").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Machine_downtime_can_be_opened_and_resolved_through_authenticated_http_contract()
    {
        var machine = await client.PostAsJsonAsync("/api/v1/manufacturing/machines", new
        {
            tenantKey = "http-integration-tenant", code = "M-DOWNTIME", name = "Dryer downtime test", status = "Available", active = true
        });
        machine.StatusCode.Should().Be(HttpStatusCode.Created);
        var machineId = (await ReadJson(machine)).GetProperty("id").GetGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var endedAt = DateTimeOffset.UtcNow;

        var opened = await client.PostAsJsonAsync($"/api/v1/manufacturing/machines/{machineId}/downtimes", new
        {
            reason = "Overheat", startedAt, notes = "Temperature alarm"
        });
        opened.StatusCode.Should().Be(HttpStatusCode.Created);
        var openedJson = await ReadJson(opened);
        var downtimeId = openedJson.GetProperty("id").GetGuid();
        openedJson.GetProperty("status").GetString().Should().Be("Open");

        var resolved = await client.PostAsJsonAsync($"/api/v1/manufacturing/machines/{machineId}/downtimes/{downtimeId}/resolve", new { endedAt });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(resolved)).GetProperty("status").GetString().Should().Be("Closed");
    }

    [Fact]
    public async Task Material_and_product_master_data_requires_valid_uom()
    {
        var uom = await client.PostAsJsonAsync("/api/v1/manufacturing/uoms", new { code = "KG", name = "Kilogram", dimension = "mass", active = true });
        uom.StatusCode.Should().Be(HttpStatusCode.Created);

        var material = await client.PostAsJsonAsync("/api/v1/manufacturing/materials", new
        {
            tenantKey = "http-integration-tenant", sku = "RM-MASTER", name = "Master mango", baseUomCode = "kg", materialType = "RawMaterial", active = true
        });
        material.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await client.PostAsJsonAsync("/api/v1/manufacturing/products", new
        {
            tenantKey = "http-integration-tenant", sku = "FG-MASTER", name = "Master dried mango", baseUomCode = "kg", active = true
        });
        product.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Uom_conversion_and_supplier_capa_evaluation_workflow_is_tenant_scoped()
    {
        var kg = await client.PostAsJsonAsync("/api/v1/manufacturing/uoms", new { code = "KG", name = "Kilogram", dimension = "mass", active = true });
        kg.StatusCode.Should().Be(HttpStatusCode.Created);
        var g = await client.PostAsJsonAsync("/api/v1/manufacturing/uoms", new { code = "G", name = "Gram", dimension = "mass", active = true });
        g.StatusCode.Should().Be(HttpStatusCode.Created);
        var conversion = await client.PostAsJsonAsync("/api/v1/manufacturing/uom-conversions", new { fromCode = "kg", toCode = "g", factor = 1000m, active = true });
        conversion.StatusCode.Should().Be(HttpStatusCode.Created);
        var conversions = await client.GetAsync("/api/v1/manufacturing/uom-conversions");
        conversions.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(conversions)).GetArrayLength().Should().Be(1);

        var supplier = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new { tenantKey = "http-integration-tenant", code = "SUP-CAPA", name = "CAPA supplier", active = true });
        var supplierId = (await ReadJson(supplier)).GetProperty("id").GetGuid();
        var evaluation = await client.PostAsJsonAsync("/api/v1/manufacturing/supplier-evaluations", new { supplierId, score = 4, qualityNotes = "Good quality", deliveryNotes = "On time", notes = "Annual review" });
        evaluation.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(evaluation)).GetProperty("score").GetInt32().Should().Be(4);

        var capa = await client.PostAsJsonAsync("/api/v1/manufacturing/capas", new { supplierId, title = "Supplier moisture deviation", problemDescription = "Lot moisture above limit", rootCause = "Drying variance", correctiveAction = "Rework lot", preventiveAction = "Add incoming moisture gate", owner = "quality-owner" });
        capa.StatusCode.Should().Be(HttpStatusCode.Created);
        var capaJson = await ReadJson(capa); var capaId = capaJson.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/manufacturing/capas/{capaId}/status", new { status = "InProgress", actor = "quality-owner" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/v1/manufacturing/capas/{capaId}/status", new { status = "Verified", actor = "quality-reviewer" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = await client.PostAsJsonAsync($"/api/v1/manufacturing/capas/{capaId}/status", new { status = "Closed", actor = "quality-reviewer" });
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(closed)).GetProperty("status").GetString().Should().Be("Closed");
    }

    [Fact]
    public async Task Facility_warehouse_and_storage_location_master_data_is_tenant_scoped()
    {
        var facility = await client.PostAsJsonAsync("/api/v1/manufacturing/facilities", new
        {
            tenantKey = "http-integration-tenant", code = "FAC-MASTER", name = "Main facility", active = true
        });
        facility.StatusCode.Should().Be(HttpStatusCode.Created);
        var facilityId = (await ReadJson(facility)).GetProperty("id").GetGuid();

        var warehouse = await client.PostAsJsonAsync("/api/v1/manufacturing/warehouses", new
        {
            tenantKey = "http-integration-tenant", facilityId, code = "WH-RAW", name = "Raw material warehouse", active = true
        });
        warehouse.StatusCode.Should().Be(HttpStatusCode.Created);
        var warehouseId = (await ReadJson(warehouse)).GetProperty("id").GetGuid();

        var location = await client.PostAsJsonAsync("/api/v1/manufacturing/storage-locations", new
        {
            tenantKey = "http-integration-tenant", warehouseId, code = "A-01", name = "Rack A01", active = true
        });
        location.StatusCode.Should().Be(HttpStatusCode.Created);

        var locations = await client.GetAsync($"/api/v1/manufacturing/storage-locations?warehouseId={warehouseId}");
        locations.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(locations)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Supplier_rfq_and_quotation_are_tenant_scoped_and_unique()
    {
        var supplier = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new
        {
            tenantKey = "http-integration-tenant", code = "SUP-RFQ", name = "RFQ supplier", active = true
        });
        supplier.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplierId = (await ReadJson(supplier)).GetProperty("id").GetGuid();

        var rfq = await client.PostAsJsonAsync("/api/v1/manufacturing/supplier-rfqs", new
        {
            tenantKey = "http-integration-tenant", rfqNumber = "RFQ-001", materialSku = "RM-RFQ-MANGO",
            quantity = 100m, uom = "kg", neededBy = DateTimeOffset.UtcNow.AddDays(14)
        });
        rfq.StatusCode.Should().Be(HttpStatusCode.Created);
        var rfqJson = await ReadJson(rfq);
        var rfqId = rfqJson.GetProperty("id").GetGuid();
        rfqJson.GetProperty("status").GetString().Should().Be("Open");

        var quotation = await client.PostAsJsonAsync($"/api/v1/manufacturing/supplier-rfqs/{rfqId}/quotations", new
        {
            supplierRfqId = rfqId, supplierId, unitPrice = 12500m, currency = "vnd", leadTimeDays = 5, notes = "Harvest lot"
        });
        quotation.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(quotation)).GetProperty("currency").GetString().Should().Be("VND");

        var duplicate = await client.PostAsJsonAsync($"/api/v1/manufacturing/supplier-rfqs/{rfqId}/quotations", new
        {
            supplierRfqId = rfqId, supplierId, unitPrice = 13000m, currency = "VND", leadTimeDays = 7
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var list = await client.GetAsync("/api/v1/manufacturing/supplier-rfqs?status=Open");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(list)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Purchase_order_inbound_receipt_creates_traceable_lot_through_authenticated_http_contract()
    {
        var supplier = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new
        {
            tenantKey = "http-integration-tenant", code = "SUP-RECEIPT", name = "Receipt supplier", active = true
        });
        supplier.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplierId = (await ReadJson(supplier)).GetProperty("id").GetGuid();

        var updatedSupplier = await client.PatchAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}", new
        {
            code = "SUP-RECEIPT-UPDATED", name = "Receipt supplier updated", active = true
        });
        updatedSupplier.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedSupplierJson = await ReadJson(updatedSupplier);
        updatedSupplierJson.GetProperty("code").GetString().Should().Be("SUP-RECEIPT-UPDATED");

        var purchaseOrder = await client.PostAsJsonAsync("/api/v1/manufacturing/purchase-orders", new
        {
            tenantKey = "http-integration-tenant", orderNumber = "PO-RECEIPT", supplierId, status = "Approved",
            currency = "VND", lines = new[] { new { materialSku = "RM-RECEIPT", orderedQuantity = 25m, uom = "kg", unitPrice = 12000m } }
        });
        purchaseOrder.StatusCode.Should().Be(HttpStatusCode.Created);
        var purchaseOrderJson = await ReadJson(purchaseOrder);
        var purchaseOrderId = purchaseOrderJson.GetProperty("id").GetGuid();
        var purchaseOrderLineId = purchaseOrderJson.GetProperty("lines")[0].GetProperty("id").GetGuid();

        var receipt = await client.PostAsJsonAsync($"/api/v1/manufacturing/purchase-orders/{purchaseOrderId}/receipts", new
        {
            purchaseOrderId, purchaseOrderLineId, materialSku = "RM-RECEIPT", receiptNumber = "GRN-RECEIPT",
            supplierLotCode = "SUPLOT-RECEIPT", facilityId = "FAC-01", quantity = 10m,
            expiryDate = "2027-08-25", receivedAt = DateTimeOffset.UtcNow
        });
        receipt.StatusCode.Should().Be(HttpStatusCode.Created);
        var receiptJson = await ReadJson(receipt);
        receiptJson.GetProperty("receiptNumber").GetString().Should().Be("GRN-RECEIPT");
        receiptJson.GetProperty("quantity").GetDecimal().Should().Be(10m);
        receiptJson.GetProperty("lotId").GetGuid().Should().NotBe(Guid.Empty);
        receiptJson.GetProperty("disposition").GetString().Should().Be("Quarantined");

        var receipts = await client.GetAsync($"/api/v1/manufacturing/inbound-receipts?purchaseOrderId={purchaseOrderId}");
        receipts.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(receipts)).GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var draftOrder = await client.PostAsJsonAsync("/api/v1/manufacturing/purchase-orders", new
        {
            tenantKey = "http-integration-tenant", orderNumber = "PO-STATUS", supplierId, status = "Draft",
            currency = "VND", lines = new[] { new { materialSku = "RM-STATUS", orderedQuantity = 2m, uom = "kg", unitPrice = 1m } }
        });
        draftOrder.StatusCode.Should().Be(HttpStatusCode.Created);
        var draftOrderId = (await ReadJson(draftOrder)).GetProperty("id").GetGuid();
        var editedDraft = await client.PutAsJsonAsync($"/api/v1/manufacturing/purchase-orders/{draftOrderId}", new
        {
            supplierId, orderNumber = "PO-STATUS-EDITED", currency = "vnd",
            lines = new[] { new { materialSku = "RM-STATUS", orderedQuantity = 3m, uom = "kg", unitPrice = 2m } }
        });
        editedDraft.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(editedDraft)).GetProperty("orderNumber").GetString().Should().Be("PO-STATUS-EDITED");
        var approved = await client.PostAsJsonAsync($"/api/v1/manufacturing/purchase-orders/{draftOrderId}/status", new { status = "Approved", actor = "operator" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(approved)).GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task Purchase_order_batch_receipts_support_multiple_lines_and_supplier_lots()
    {
        var supplier = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new { tenantKey = "http-integration-tenant", code = "SUP-BATCH", name = "Batch supplier", active = true });
        var supplierId = (await ReadJson(supplier)).GetProperty("id").GetGuid();
        var order = await client.PostAsJsonAsync("/api/v1/manufacturing/purchase-orders", new
        {
            tenantKey = "http-integration-tenant", orderNumber = "PO-BATCH", supplierId, status = "Approved", currency = "VND",
            lines = new[] { new { materialSku = "RM-BATCH-A", orderedQuantity = 10m, uom = "kg", unitPrice = 1m }, new { materialSku = "RM-BATCH-B", orderedQuantity = 20m, uom = "kg", unitPrice = 2m } }
        });
        var orderJson = await ReadJson(order);
        var orderId = orderJson.GetProperty("id").GetGuid();
        var lineA = orderJson.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var lineB = orderJson.GetProperty("lines")[1].GetProperty("id").GetGuid();
        var batch = await client.PostAsJsonAsync($"/api/v1/manufacturing/purchase-orders/{orderId}/receipts/batch", new
        {
            receipts = new[]
            {
                new { purchaseOrderId = orderId, purchaseOrderLineId = lineA, materialSku = "RM-BATCH-A", receiptNumber = "GRN-BATCH-A", supplierLotCode = "LOT-A", facilityId = "FAC-01", quantity = 10m },
                new { purchaseOrderId = orderId, purchaseOrderLineId = lineB, materialSku = "RM-BATCH-B", receiptNumber = "GRN-BATCH-B", supplierLotCode = "LOT-B", facilityId = "FAC-01", quantity = 20m }
            }
        });
        batch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(batch)).GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Recipe_lifecycle_is_available_through_authenticated_http_contract()
    {
        var create = await client.PostAsJsonAsync("/api/v1/manufacturing/recipes", new
        {
            tenantKey = "http-integration-tenant", productSku = "FG-RECIPE-LIFECYCLE", version = 1,
            processStep = "drying", outputUom = "kg", targetYieldPercent = 82,
            active = false, status = "Draft",
            components = new[] { new { ingredientSku = "RM-RECIPE-MANGO", quantity = 1m, uom = "kg" } }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipeId = (await ReadJson(create)).GetProperty("id").GetGuid();

        var submit = await client.PostAsJsonAsync($"/api/v1/manufacturing/recipes/{recipeId}/submit", new { actor = "recipe-reviewer" });
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(submit)).GetProperty("status").GetString().Should().Be("Submitted");

        var approve = await client.PostAsJsonAsync($"/api/v1/manufacturing/recipes/{recipeId}/approve", new { actor = "recipe-approver" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await ReadJson(approve);
        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("active").GetBoolean().Should().BeTrue();

        var retire = await client.PostAsJsonAsync($"/api/v1/manufacturing/recipes/{recipeId}/retire", new { actor = "recipe-owner" });
        retire.StatusCode.Should().Be(HttpStatusCode.OK);
        var retired = await ReadJson(retire);
        retired.GetProperty("status").GetString().Should().Be("Retired");
        retired.GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Product_specification_lifecycle_is_available_through_authenticated_http_contract()
    {
        var create = await client.PostAsJsonAsync("/api/v1/manufacturing/product-specifications", new
        {
            tenantKey = "http-integration-tenant", productSku = "FG-SPEC-LIFECYCLE", targetMoisturePercent = 12.5m,
            packaging = "250g pouch", shelfLifeDays = 180, qcSpec = "Moisture <= 12.5%; seal intact"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var specificationId = (await ReadJson(create)).GetProperty("id").GetGuid();

        var approve = await client.PostAsJsonAsync($"/api/v1/manufacturing/product-specifications/{specificationId}/approve", new { actor = "qc-approver" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await ReadJson(approve);
        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("approvedBy").GetString().Should().Be("qc-approver");

        var retire = await client.PostAsJsonAsync($"/api/v1/manufacturing/product-specifications/{specificationId}/retire", new { actor = "qc-owner" });
        retire.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(retire)).GetProperty("status").GetString().Should().Be("Retired");
    }

    [Fact]
    public async Task Sales_forecast_calculates_material_requirements_from_approved_recipe()
    {
        var recipe = await client.PostAsJsonAsync("/api/v1/manufacturing/recipes", new
        {
            tenantKey = "http-integration-tenant", productSku = "FG-FORECAST-LIFECYCLE", version = 1,
            processStep = "drying", outputUom = "kg", targetYieldPercent = 80,
            active = false, status = "Draft",
            components = new[] { new { ingredientSku = "RM-FORECAST-MANGO", quantity = 1.25m, uom = "kg" } }
        });
        recipe.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipeId = (await ReadJson(recipe)).GetProperty("id").GetGuid();
        var submit = await client.PostAsJsonAsync($"/api/v1/manufacturing/recipes/{recipeId}/submit", new { actor = "forecast-reviewer" });
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var approve = await client.PostAsJsonAsync($"/api/v1/manufacturing/recipes/{recipeId}/approve", new { actor = "forecast-approver" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var forecast = await client.PostAsJsonAsync("/api/v1/manufacturing/sales/forecasts", new
        {
            productSku = "FG-FORECAST-LIFECYCLE", periodStart = "2026-08-25", periodEnd = "2026-09-24",
            quantity = 100m, uom = "kg", source = "sales", actor = "planner", version = 1
        });
        forecast.StatusCode.Should().Be(HttpStatusCode.Created);
        var forecastId = (await ReadJson(forecast)).GetProperty("id").GetGuid();

        var requirements = await client.GetAsync($"/api/v1/manufacturing/planning/forecast-material-requirements/{forecastId}");
        requirements.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadJson(requirements);
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("materialSku").GetString().Should().Be("RM-FORECAST-MANGO");
        items[0].GetProperty("requiredQuantity").GetDecimal().Should().Be(125m);
    }

    private static async Task<System.Text.Json.JsonElement> ReadJson(HttpResponseMessage response) =>
        System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string TestScheme = "ManufacturingIntegration";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim("sub", "manufacturing-http-test"),
                new Claim("tenant_id", "http-integration-tenant"),
                new Claim("portal_class", "operator"),
                new Claim("permissions", HisHopePermissions.Manufacturing.ProductionExecute),
                new Claim("permissions", HisHopePermissions.Manufacturing.QualityInspect),
                new Claim("permissions", HisHopePermissions.Manufacturing.MaintenanceComplete)
            };
            var identity = new ClaimsIdentity(claims, TestScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
        }
    }
}
