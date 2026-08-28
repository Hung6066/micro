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
            builder.UseSetting("ConnectionStrings:ManufacturingDb_customer_acme", connection);
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
        using (var scope = factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO manufacturing_uoms (id, code, name, dimension, active, created_at)
                VALUES ({Guid.NewGuid()}, {"kg-http"}, {"Kilogram (HTTP tests)"}, {"Mass"}, {true}, {DateTimeOffset.UtcNow})
                ON CONFLICT (code) DO NOTHING
                """);
            var skus = new[] { "RM-LIST", "RM-RECEIPT", "RM-STATUS", "RM-BATCH-A", "RM-BATCH-B" };
            var existing = await db.Materials
                .Where(x => x.TenantKey == "http-integration-tenant" && skus.Contains(x.Sku))
                .Select(x => x.Sku)
                .ToListAsync();
            foreach (var sku in skus.Where(x => !existing.Contains(x)))
            {
                db.Materials.Add(new ManufacturingMaterialEntity
                {
                    Id = Guid.NewGuid(), TenantKey = "http-integration-tenant", Sku = sku,
                    Name = $"{sku} material", BaseUomCode = "kg-http", MaterialType = "RawMaterial", Active = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }
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
        var batchCost = await db.ProductionBatchCosts.SingleAsync(x => x.ProductionBatchId == batch.Id && x.TenantKey == ManufacturingDemoSeeder.TenantKey);
        batchCost.TotalCost.Should().Be(2_200_000m);
        batchCost.CostPerOutputUnit.Should().Be(27_500m);
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
        var unapprovedOrder = await client.PostAsJsonAsync("/api/v1/manufacturing/purchase-orders", new
        {
            tenantKey = "http-integration-tenant", orderNumber = "PO-LIST-UNAPPROVED", supplierId, status = "Approved",
            currency = "VND", lines = new[] { new { materialSku = "RM-LIST", orderedQuantity = 5m, uom = "kg", unitPrice = 100m } }
        });
        unapprovedOrder.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJson(unapprovedOrder)).GetProperty("errorCode").GetString().Should().Be("supplier_not_approved");
        var supplierApproved = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "PendingApproval" });
        supplierApproved.StatusCode.Should().Be(HttpStatusCode.OK);
        supplierApproved = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "Approved" });
        supplierApproved.StatusCode.Should().Be(HttpStatusCode.OK);

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

    [Fact]
    public async Task Every_tenant_scoped_mutation_creates_a_shared_audit_event()
    {
        var response = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new
        {
            tenantKey = "http-integration-tenant", code = "SUP-AUDIT", name = "Audited supplier", active = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplierId = (await ReadJson(response)).GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var audit = await db.AuditEvents.SingleAsync(x =>
            x.TenantKey == "http-integration-tenant"
            && x.EntityType == nameof(ManufacturingSupplierEntity)
            && x.EntityId == supplierId
            && x.Action == "Created");

        audit.Actor.Should().NotBeNullOrWhiteSpace();
        audit.Details.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PurchaseOrder_rejects_unknown_or_duplicate_material_lines()
    {
        var supplier = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new
        {
            tenantKey = "http-integration-tenant", code = "SUP-MATERIAL-GUARD", name = "Material guard supplier", active = true
        });
        var supplierId = (await ReadJson(supplier)).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "PendingApproval" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "Approved" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var unknown = await client.PostAsJsonAsync("/api/v1/manufacturing/purchase-orders", new
        {
            tenantKey = "http-integration-tenant", orderNumber = "PO-UNKNOWN-MATERIAL", supplierId, status = "Draft", currency = "VND",
            lines = new[] { new { materialSku = "RM-NOT-CATALOGUED", orderedQuantity = 1m, uom = "kg", unitPrice = 1m } }
        });
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadJson(unknown)).GetProperty("errorCode").GetString().Should().Be("material_not_found");

    }

    [Fact]
    public async Task Supplier_certificate_is_tenant_scoped_and_validated()
    {
        var supplier = await client.PostAsJsonAsync("/api/v1/manufacturing/suppliers", new
        {
            tenantKey = "http-integration-tenant", code = "SUP-CERT", name = "Certified supplier", active = true
        });
        supplier.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplierId = (await ReadJson(supplier)).GetProperty("id").GetGuid();

        var invalid = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/certificates", new
        {
            certificateType = "HACCP", certificateNumber = "HACCP-INVALID", issuer = "Auditor",
            issuedAt = "2026-08-01T00:00:00Z", expiresAt = "2026-07-01T00:00:00Z"
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/certificates", new
        {
            certificateType = "HACCP", certificateNumber = "HACCP-001", issuer = "Auditor",
            issuedAt = "2026-08-01T00:00:00Z", expiresAt = "2027-08-01T00:00:00Z", evidenceReference = "s3://evidence/haccp-001"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(created)).GetProperty("status").GetString().Should().Be("Active");

        var list = await client.GetAsync($"/api/v1/manufacturing/suppliers/{supplierId}/certificates");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(list)).GetArrayLength().Should().Be(1);

        var missingMaterial = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/material-approvals", new
        {
            materialSku = "RM-NOT-CATALOGUED", approvedUom = "kg", effectiveFrom = "2026-08-01T00:00:00Z"
        });
        missingMaterial.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var approval = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/material-approvals", new
        {
            materialSku = "RM-LIST", approvedUom = "kg", effectiveFrom = "2026-08-01T00:00:00Z", notes = "Approved raw material"
        });
        approval.StatusCode.Should().Be(HttpStatusCode.Created);
        var approvals = await client.GetAsync($"/api/v1/manufacturing/suppliers/{supplierId}/material-approvals");
        approvals.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(approvals)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Operator_tenant_selector_accepts_only_claimed_membership()
    {
        var selected = await client.GetAsync("/api/v1/manufacturing/production-batches?tenantKey=selector-tenant");
        selected.StatusCode.Should().Be(HttpStatusCode.OK);

        using var canonical = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manufacturing/production-batches");
        canonical.Headers.Add("X-HisHope-Tenant", "selector-tenant");
        var canonicalResponse = await client.SendAsync(canonical);
        canonicalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var preferred = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manufacturing/production-batches?tenantKey=unclaimed-tenant");
        preferred.Headers.Add("X-HisHope-Tenant", "selector-tenant");
        var preferredResponse = await client.SendAsync(preferred);
        preferredResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var denied = await client.GetAsync("/api/v1/manufacturing/production-batches?tenantKey=unclaimed-tenant");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Command_body_can_omit_tenant_key_when_canonical_header_is_present()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/manufacturing/suppliers")
        {
            Content = JsonContent.Create(new { code = "SUP-CONTEXT-ONLY", name = "Context-only supplier", active = true })
        };
        request.Headers.Add("X-HisHope-Tenant", "http-integration-tenant");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(response)).GetProperty("tenantKey").GetString().Should().Be("http-integration-tenant");
    }

    [Fact]
    public async Task Batch_cost_recalculation_requires_cost_permission()
    {
        var response = await client.PostAsJsonAsync($"/api/v1/manufacturing/production-batches/{Guid.NewGuid()}/cost", new { laborCost = 1m, overheadCost = 1m, currency = "VND" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        operationRequest.Headers.Add("Idempotency-Key", operationId);
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
        replayRequest.Headers.Add("Idempotency-Key", operationId);
        var replayResponse = await client.SendAsync(replayRequest);
          replayResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replayResponse.Headers.GetValues("X-HisHope-Operation-Replay").Single().Should().Be("true");
        replayResponse.Headers.GetValues("Idempotency-Replayed").Single().Should().Be("true");

        var completeResponse = await client.PostAsync($"/api/v1/manufacturing/production-batches/{batchId}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await ReadJson(completeResponse);
        completed.GetProperty("outputLotId").GetGuid().Should().NotBeEmpty();
        completed.GetProperty("inputs").GetArrayLength().Should().Be(1);
        var outputLotId = completed.GetProperty("outputLotId").GetGuid();
        var recallImpact = await client.GetAsync($"/api/v1/manufacturing/lots/{outputLotId}/recall-impact");
        recallImpact.StatusCode.Should().Be(HttpStatusCode.OK);
        var recall = await ReadJson(recallImpact);
        recall.GetProperty("rootLotId").GetGuid().Should().Be(outputLotId);
        recall.GetProperty("impactedLotCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var epcisResponse = await client.GetAsync("/api/v1/manufacturing/traceability/epcis?limit=100");
        epcisResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var epcis = await ReadJson(epcisResponse);
        epcis.GetProperty("type").GetString().Should().Be("EPCISDocument");
        epcis.GetProperty("specVersion").GetString().Should().Be("2.0");
        epcis.GetProperty("events").GetArrayLength().Should().BeGreaterThan(0);
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
            lotId = outputLotId, tenantKey = tenant, status = "Pass", moisturePercent = 12.5m, inspector = "qc-http", specificationReference = "SPEC-FG-001",
            results = new[] { new { testCode = "MOISTURE", testName = "Moisture", measuredValue = 12.5m, uom = "%", result = "Pass", lowerLimit = 0m, upperLimit = 15m, method = "AOAC", evidenceReference = "coa://qc-http" } }
        });
        inspectionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var inspectionJson = await ReadJson(inspectionResponse);
        inspectionJson.GetProperty("specificationReference").GetString().Should().Be("SPEC-FG-001");
        inspectionJson.GetProperty("results")[0].GetProperty("testCode").GetString().Should().Be("MOISTURE");
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
    public async Task Maintenance_plan_generates_due_work_order_and_rolls_forward()
    {
        var machine = await client.PostAsJsonAsync("/api/v1/manufacturing/machines", new { tenantKey = "http-integration-tenant", code = "M-PLAN", name = "Planned maintenance machine", status = "Available", active = true });
        machine.StatusCode.Should().Be(HttpStatusCode.Created);
        var machineId = (await ReadJson(machine)).GetProperty("id").GetGuid();
        var dueAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var plan = await client.PostAsJsonAsync($"/api/v1/manufacturing/machines/{machineId}/maintenance-plans", new { machineId, planCode = "MP-HTTP-30D", maintenanceType = "Preventive", frequencyDays = 30, nextDueAt = dueAt, checklist = "Inspect belts", assignedTo = "maintenance" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created);
        var generated = await client.PostAsJsonAsync("/api/v1/manufacturing/maintenance-work-orders/generate", new { asOf = DateTimeOffset.UtcNow });
        generated.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await ReadJson(generated);
        orders.EnumerateArray().Should().Contain(x => x.GetProperty("machineId").GetGuid() == machineId && x.GetProperty("notes").GetString() == "Inspect belts");
        var listed = await client.GetAsync($"/api/v1/manufacturing/maintenance-plans?machineId={machineId}&active=true");
        var nextDue = (await ReadJson(listed)).EnumerateArray().Single().GetProperty("nextDueAt").GetDateTimeOffset();
        nextDue.Should().BeAfter(dueAt);
    }

    [Fact]
    public async Task Machine_calibration_is_tenant_scoped_and_idempotently_unique_by_certificate()
    {
        var machine = await client.PostAsJsonAsync("/api/v1/manufacturing/machines", new
        {
            tenantKey = "http-integration-tenant", code = "M-CALIBRATION", name = "Calibration test machine", status = "Available", active = true
        });
        machine.StatusCode.Should().Be(HttpStatusCode.Created);
        var machineId = (await ReadJson(machine)).GetProperty("id").GetGuid();
        var calibratedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var nextDueAt = DateTimeOffset.UtcNow.AddDays(364);
        var request = new { calibrationType = "Temperature", certificateNumber = "CAL-HTTP-001", calibratedAt, nextDueAt, result = "Pass", provider = "Metrology lab", evidenceReference = "cert://http/001", createdBy = "qa" };

        var created = await client.PostAsJsonAsync($"/api/v1/manufacturing/machines/{machineId}/calibrations", request);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(created)).GetProperty("result").GetString().Should().Be("Pass");

        var duplicate = await client.PostAsJsonAsync($"/api/v1/manufacturing/machines/{machineId}/calibrations", request);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var listed = await client.GetAsync($"/api/v1/manufacturing/machines/{machineId}/calibrations");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(listed)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Inspection_plan_version_follows_controlled_lifecycle()
    {
        var created = await client.PostAsJsonAsync("/api/v1/manufacturing/inspection-plan-versions", new
        {
            tenantKey = "http-integration-tenant", planCode = "IP-HTTP", productSku = "FG-PLAN", version = 1,
            samplingMethod = "Per lot", samplingFrequency = "Every lot", acceptanceCriteria = "Moisture <= 12%", createdBy = "qa"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var planId = (await ReadJson(created)).GetProperty("id").GetGuid();
        var submitted = await client.PostAsJsonAsync($"/api/v1/manufacturing/inspection-plan-versions/{planId}/status?status=Submitted", new { actor = "qa" });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await client.PostAsJsonAsync($"/api/v1/manufacturing/inspection-plan-versions/{planId}/status?status=Approved", new { actor = "qa", effectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1) });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(approved)).GetProperty("status").GetString().Should().Be("Approved");
        var listed = await client.GetAsync("/api/v1/manufacturing/inspection-plan-versions?productSku=FG-PLAN&status=Approved");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(listed)).GetArrayLength().Should().Be(1);

        var lot = await client.PostAsJsonAsync("/api/v1/manufacturing/lots", new { tenantKey = "http-integration-tenant", sku = "FG-PLAN", quantity = 10m, uom = "kg", disposition = "Quarantined" });
        lot.StatusCode.Should().Be(HttpStatusCode.Created);
        var lotId = (await ReadJson(lot)).GetProperty("id").GetGuid();
        var inspection = await client.PostAsJsonAsync("/api/v1/manufacturing/quality-inspections", new { lotId, tenantKey = "http-integration-tenant", status = "Pass", moisturePercent = 10m, inspector = "qa", inspectionPlanVersionId = planId });
        inspection.StatusCode.Should().Be(HttpStatusCode.Created);
        var inspectionJson = await ReadJson(inspection);
        inspectionJson.GetProperty("inspectionPlanVersionId").GetGuid().Should().Be(planId);
        var sample = await client.PostAsJsonAsync("/api/v1/manufacturing/quality-samples", new { inspectionId = inspectionJson.GetProperty("id").GetGuid(), sampleCode = "SAMPLE-001", collectedBy = "qa", location = "QA lab" });
        sample.StatusCode.Should().Be(HttpStatusCode.Created);
        var sampleId = (await ReadJson(sample)).GetProperty("id").GetGuid();
        var disposition = await client.PostAsJsonAsync($"/api/v1/manufacturing/quality-samples/{sampleId}/disposition", new { disposition = "Accepted", actor = "qa", reason = "All tests passed" });
        disposition.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJson(disposition)).GetProperty("disposition").GetString().Should().Be("Accepted");
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

        var supplierPendingApproval = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "PendingApproval" });
        supplierPendingApproval.StatusCode.Should().Be(HttpStatusCode.OK);
        var supplierApproved = await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "Approved" });
        supplierApproved.StatusCode.Should().Be(HttpStatusCode.OK);

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
            expiryDate = "2027-08-25", receivedAt = DateTimeOffset.UtcNow,
            traceabilityLotCode = "LOT-HTTP-RECEIPT-001", originCountryCode = "VN", manufacturedOn = "2026-08-20",
            storageLocationCode = "A-01-01", deliveryNoteNumber = "DN-HTTP-001", carrierName = "Nacoms Logistics",
            vehicleReference = "51D-12345", temperatureOnReceiptC = 8.5m,
            certificateOfAnalysisReference = "coa://http/receipt-001", receivedBy = "receiving-operator",
            acceptedQuantity = 10m, rejectedQuantity = 0m
        });
        receipt.StatusCode.Should().Be(HttpStatusCode.Created);
        var receiptJson = await ReadJson(receipt);
        receiptJson.GetProperty("receiptNumber").GetString().Should().Be("GRN-RECEIPT");
        receiptJson.GetProperty("quantity").GetDecimal().Should().Be(10m);
        receiptJson.GetProperty("lotId").GetGuid().Should().NotBe(Guid.Empty);
        receiptJson.GetProperty("disposition").GetString().Should().Be("Quarantined");
        receiptJson.GetProperty("lotCode").GetString().Should().Be("LOT-HTTP-RECEIPT-001");
        receiptJson.GetProperty("certificateOfAnalysisReference").GetString().Should().Be("coa://http/receipt-001");
        receiptJson.GetProperty("acceptedQuantity").GetDecimal().Should().Be(10m);

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
        (await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "PendingApproval" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/v1/manufacturing/suppliers/{supplierId}/approval", new { status = "Approved" })).StatusCode.Should().Be(HttpStatusCode.OK);
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

    [Fact]
    public async Task Ml_training_data_routes_capture_and_export_tenant_scoped_records()
    {
        var recipeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            db.Recipes.Add(new ManufacturingRecipeEntity { Id = recipeId, TenantKey = "http-integration-tenant", ProductSku = "FG-ML-HTTP", Version = 1, ProcessStep = "drying", OutputUom = "kg", TargetYieldPercent = 80, Active = true, Status = "Approved", CreatedAt = DateTimeOffset.UtcNow });
            db.ProductionOrders.Add(new ManufacturingProductionOrderEntity { Id = orderId, TenantKey = "http-integration-tenant", OrderNumber = "PO-ML-HTTP", ProductSku = "FG-ML-HTTP", RecipeId = recipeId, RecipeVersion = 1, TargetQuantity = 100, OutputUom = "kg", Status = "InProgress", CreatedAt = DateTimeOffset.UtcNow });
            db.ProductionBatches.Add(new ManufacturingProductionBatchEntity { Id = batchId, TenantKey = "http-integration-tenant", ProductionOrderId = orderId, BatchNumber = "B-ML-HTTP", Status = "Started", PlannedQuantity = 100, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var measurement = await client.PostAsJsonAsync($"/api/v1/manufacturing/production-batches/{batchId}/measurements", new
        {
            productionBatchId = batchId, measurementType = "temperature", value = 62.5m, uom = "C", measuredAt = DateTimeOffset.UtcNow
        });
        measurement.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJson(measurement)).GetProperty("productionBatchId").GetGuid().Should().Be(batchId);

        var actual = await client.PostAsJsonAsync("/api/v1/manufacturing/sales/actuals", new
        {
            productSku = "FG-ML-HTTP", periodStart = "2026-01-01", periodEnd = "2026-01-31", quantity = 42m, uom = "kg", channel = "online"
        });
        actual.StatusCode.Should().Be(HttpStatusCode.Created);

        var snapshot = await client.PostAsJsonAsync("/api/v1/manufacturing/ml/datasets/yield-v1/snapshots", new
        {
            datasetKey = "yield-v1", entityType = "production_batch", entityId = batchId, asOf = DateTimeOffset.UtcNow,
            featuresJson = "{\"input_kg\":100}", labelJson = "{\"yield_percent\":80}", split = "train", schemaVersion = 1
        });
        snapshot.StatusCode.Should().Be(HttpStatusCode.Created);

        var quality = await client.GetAsync("/api/v1/manufacturing/ml/datasets/yield-v1/quality");
        quality.StatusCode.Should().Be(HttpStatusCode.OK);
        var qualityJson = await ReadJson(quality);
        qualityJson.GetProperty("rowCount").GetInt32().Should().Be(1);
        qualityJson.GetProperty("labeledRowCount").GetInt32().Should().Be(1);

        var denied = await client.GetAsync("/api/v1/manufacturing/ml/datasets/other-tenant/quality?tenantKey=unclaimed-tenant");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
                new Claim("tenant_membership", "http-integration-tenant"),
                new Claim("tenant_membership", "selector-tenant"),
                new Claim("portal_class", "operator"),
                new Claim("permissions", HisHopePermissions.Manufacturing.ProductionExecute),
                new Claim("permissions", HisHopePermissions.Manufacturing.QualityInspect),
                new Claim("permissions", HisHopePermissions.Manufacturing.QualityApprove),
                new Claim("permissions", HisHopePermissions.Manufacturing.RecipeApprove),
                new Claim("permissions", HisHopePermissions.Manufacturing.SpecificationApprove),
                new Claim("permissions", HisHopePermissions.Manufacturing.MaintenanceComplete)
            };
            var identity = new ClaimsIdentity(claims, TestScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
        }
    }
}
