using His.Hope.Authorization;
using His.Hope.CommerceService.Application;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Api;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.Configuration;
using His.Hope.ServiceDefaults;
using His.Hope.Secrets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCommerceServiceHost(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseCommerceServiceHost();

app.MapCommerceWebhookEndpoints();

app.ValidateHisHopeTenantPlacement();

var runCommerceMigrations = builder.Configuration.GetValue("Persistence:RunMigrationsOnStartup", false) ||
    builder.Configuration.GetValue("Persistence:MigrationOnly", false);
if (runCommerceMigrations)
{
    await app.Services.MigrateCommerceDatabaseAsync();
}
else if (!app.Environment.IsDevelopment() &&
         !string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "CommerceService requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
}

if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    return;
}

// Seed only on first startup, then hydrate the process catalog from PostgreSQL.
// Orders and catalog reads therefore remain stable across API restarts/replicas.
var catalogPersistence = app.Services.GetRequiredService<ICommerceCatalogPersistence>();
var catalogStore = app.Services.GetRequiredService<CommerceStore>();
var seedProducts = catalogStore.GetSeedProducts();
foreach (var tenantGroup in seedProducts.GroupBy(product => product.TenantKey, StringComparer.OrdinalIgnoreCase))
{
    var persisted = await catalogPersistence.GetProductsAsync(tenantGroup.Key);
    if (persisted.Count == 0)
    {
        await catalogPersistence.SaveProductsAsync(tenantGroup.Select(ToProductSnapshot).ToArray());
        persisted = await catalogPersistence.GetProductsAsync(tenantGroup.Key);
    }

    var hydrated = persisted.Select(ToProductDto).ToArray();
    catalogStore.ReplaceProducts(seedProducts.Where(product => !string.Equals(product.TenantKey, tenantGroup.Key, StringComparison.OrdinalIgnoreCase)).Concat(hydrated));
    seedProducts = catalogStore.GetSeedProducts();
}

static IResult CommerceProblem(int statusCode, string errorCode) =>
    Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

static CommerceProductSnapshot ToProductSnapshot(ProductDto product) =>
    new(product.Id, product.TenantKey, product.Sku, product.Name, product.Description, product.UnitPrice,
        product.WholesaleUnitPrice, product.MinOrderQty, product.SupportsPrivateLabel, product.SupportsExport);

static ProductDto ToProductDto(CommerceProductSnapshot product) =>
    new(product.Id, product.Sku, product.Name, product.Description, product.UnitPrice, product.WholesaleUnitPrice,
        product.MinOrderQty, product.SupportsPrivateLabel, product.SupportsExport, product.TenantKey);

static async Task<IReadOnlyList<ProductDto>> LoadCatalogAsync(
    ICommerceCatalogPersistence persistence,
    string tenantKey,
    CancellationToken cancellationToken) =>
    (await persistence.GetProductsAsync(tenantKey, cancellationToken)).Select(ToProductDto).ToArray();

var commerce = app.MapGroup("/api/v1/commerce")
    .RequireAuthorization()
    .RequireTenantContext();

commerce.MapGet("/products", async (
    HttpContext context,
    CommerceStore store,
    ICommerceProfilePersistence profilePersistence,
    ICommerceCatalogPersistence catalogPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();

    var portalClass = context.User.GetPortalClass();
    if (string.Equals(portalClass, PortalClassConstants.EndUser, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(userId))
    {
        var profile = await profilePersistence.GetProfileAsync(
            tenantKey,
            userId,
            context.User.GetEmail(),
            context.RequestAborted);
        var products = await LoadCatalogAsync(catalogPersistence, tenantKey, context.RequestAborted);
        return Results.Ok(new { items = store.GetProductsForBuyer(tenantKey, profile.PriceTier, products) });
    }

    var catalog = await LoadCatalogAsync(catalogPersistence, tenantKey, context.RequestAborted);
    return Results.Ok(new { items = store.GetProducts(tenantKey, catalog) });
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerRead,
    AuthorizationPolicyNames.Permissions.CommerceCatalogView);

commerce.MapGet("/products/{productId:guid}", async (
    Guid productId,
    HttpContext context,
    CommerceStore store,
    ICommerceProfilePersistence profilePersistence,
    ICommerceCatalogPersistence catalogPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();

    var priceTier = "standard";
    var portalClass = context.User.GetPortalClass();
    if (string.Equals(portalClass, PortalClassConstants.EndUser, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(userId))
    {
        var profile = await profilePersistence.GetProfileAsync(
            tenantKey,
            userId,
            context.User.GetEmail(),
            context.RequestAborted);
        priceTier = profile.PriceTier;
    }

    var catalog = await LoadCatalogAsync(catalogPersistence, tenantKey, context.RequestAborted);
    var product = store.GetProductForBuyer(tenantKey, priceTier, productId, catalog);
    return product is null
        ? CommerceProblem(StatusCodes.Status404NotFound, "not_found")
        : Results.Ok(product);
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerRead,
    AuthorizationPolicyNames.Permissions.CommerceCatalogView);

commerce.MapCommerceOrderCreationEndpoint();
commerce.MapCommerceOrderQueryEndpoints();
commerce.MapCommerceCustomerEndpoints();

commerce.MapCommerceRfqEndpoints();

app.MapHisHopeHealthEndpoints();
app.Run();
