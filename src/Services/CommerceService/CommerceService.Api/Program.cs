using His.Hope.Authorization;
using His.Hope.CommerceService.Application;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Api;
using His.Hope.CommerceService.Api.Middleware;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Security;
using His.Hope.ServiceDefaults;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "CommerceService");
builder.Services.AddCommerceApplication();
builder.Services.AddCommerceInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck(
    "commerce-process",
    () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
    tags: ["live", "ready"]);
builder.Services.AddSingleton<CommerceStore>();
var redis = RedisConnectionFactory.Connect(
    builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"]
        ?? "localhost:6379",
    builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddHisHopeDpopValidation();
His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddHisHopeAuthorization();
builder.Services.AddAuthorizationBuilder().AddCommerceAuthorizationPolicies();

var app = builder.Build();
app.UseHisHopeServiceDefaults();
app.UseDpopAuthorizationSchemeNormalization();
app.UseAuthentication();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseCommerceSecurity();

await app.Services.MigrateCommerceDatabaseAsync();

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

static OrderDto ToOrderDto(CommerceOrderView order) =>
    new(
        order.Id,
        order.TenantKey,
        order.BuyerUserId,
        order.Status,
        order.TotalAmount,
        order.CreatedAt,
        order.Lines.Select(line => new OrderLineDto(
            line.ProductId,
            line.Sku,
            line.Name,
            line.Quantity,
            line.UnitPrice)).ToArray());

static RfqDto ToRfqDto(CommerceRfqSnapshot rfq) =>
    new(rfq.Id, rfq.TenantKey, rfq.BuyerUserId, rfq.Status, rfq.Message, rfq.QuotedTotal, rfq.OperatorNotes,
        rfq.CreatedAt, rfq.RespondedAt, rfq.Lines.Select(line => new RfqLineDto(line.ProductId, line.Quantity, line.Notes)).ToArray());

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

var commerce = app.MapGroup("/api/v1/commerce").RequireAuthorization();

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

commerce.MapGet("/cart", async (
    HttpContext context,
    ICommerceCartPersistence cartPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var cart = await cartPersistence.GetCartAsync(tenantKey, userId, context.RequestAborted);
    return Results.Ok(new CartDto(
        cart.TenantKey,
        cart.Lines.Select(line => new CartLineDto(line.ProductId, line.Quantity)).ToArray()));
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerRead,
    AuthorizationPolicyNames.Permissions.CommerceCatalogView);

commerce.MapPut("/cart", async (
    HttpContext context,
    CommerceStore store,
    ICommerceCartPersistence cartPersistence,
    [FromBody] UpdateCartRequest request) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var cart = store.UpdateCart(tenantKey, userId, request.Lines);
    await cartPersistence.SaveCartAsync(
        new CommerceCartSnapshot(
            cart.TenantKey,
            userId,
            cart.Lines.Select(line => new CommerceCartLineSnapshot(line.ProductId, line.Quantity)).ToArray()),
        context.RequestAborted);
    return Results.Ok(cart);
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerWrite,
    AuthorizationPolicyNames.Permissions.CommerceCatalogView);

commerce.MapPost("/orders", async (
    HttpContext context,
    CommerceStore store,
    ICommerceOrderPersistence orderPersistence,
    ICommerceCartPersistence cartPersistence,
    ICommerceProfilePersistence profilePersistence,
    ICommerceNotificationPersistence notificationPersistence,
    ICommerceCatalogPersistence catalogPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var persistedCart = await cartPersistence.GetCartAsync(tenantKey, userId, context.RequestAborted);
    var persistedProfile = await profilePersistence.GetProfileAsync(
        tenantKey,
        userId,
        context.User.GetEmail(),
        context.RequestAborted);
    var persistedProducts = await LoadCatalogAsync(catalogPersistence, tenantKey, context.RequestAborted);
    var cart = new CartDto(
        persistedCart.TenantKey,
        persistedCart.Lines.Select(line => new CartLineDto(line.ProductId, line.Quantity)).ToArray());
    var order = store.CreateOrder(
        tenantKey,
        userId,
        context.User.GetEmail(),
        cart,
        persistedProfile.PriceTier,
        persistedProducts);
    if (order is null)
        return (IResult)CommerceProblem(StatusCodes.Status400BadRequest, "cart_empty");

    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    var @event = CommerceOrderEventFactory.Create(order, correlationId);
    var snapshot = new CommerceOrderSnapshot(
        order.Id,
        order.TenantKey,
        order.BuyerUserId,
        order.Status,
        order.TotalAmount,
        order.CreatedAt,
        order.Lines.Select(line => new CommerceOrderLineSnapshot(
            line.ProductId,
            line.Sku,
            line.Name,
            line.Quantity,
            line.UnitPrice)).ToArray());

    await orderPersistence.SaveOrderAndOutboxAsync(snapshot, @event, context.RequestAborted);
    var notification = store.CompleteOrder(order);
    await notificationPersistence.SaveNotificationAsync(
        new CommerceNotificationSnapshot(notification.Id, notification.TenantKey, notification.UserId, notification.Title, notification.Message, notification.CreatedAt, notification.IsRead),
        context.RequestAborted);
    await cartPersistence.SaveCartAsync(
        new CommerceCartSnapshot(order.TenantKey, order.BuyerUserId, []),
        context.RequestAborted);
    return Results.Created($"/api/v1/commerce/orders/{order.Id}", order);
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerWrite,
    AuthorizationPolicyNames.Permissions.CommerceOrdersCreate);

var orders = commerce.MapGroup("/orders");

orders.MapGet("/", async (
    HttpContext context,
    ICommerceOrderPersistence orderPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var portalClass = context.User.GetPortalClass();
    var buyerOnly = !string.Equals(portalClass, "operator", StringComparison.OrdinalIgnoreCase);
    var items = await orderPersistence.GetOrdersAsync(
        tenantKey,
        buyerOnly ? userId : null,
        context.RequestAborted);
    return Results.Ok(new { items = items.Select(ToOrderDto).ToArray() });
})
.RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceOrdersView);

orders.MapGet("/{orderId:guid}", async (
    Guid orderId,
    HttpContext context,
    ICommerceOrderPersistence orderPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var order = await orderPersistence.GetOrderAsync(orderId, tenantKey, context.RequestAborted);
    if (order is null)
        return CommerceProblem(StatusCodes.Status404NotFound, "not_found");

    var portalClass = context.User.GetPortalClass();
    if (!string.Equals(portalClass, "operator", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(order.BuyerUserId, userId, StringComparison.OrdinalIgnoreCase))
        return Results.Forbid();

    return Results.Ok(ToOrderDto(order));
})
.RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceOrdersView);

orders.MapPatch("/{orderId:guid}/status", async (
    Guid orderId,
    HttpContext context,
    ICommerceOrderPersistence orderPersistence,
    ICommerceNotificationPersistence notificationPersistence,
    [FromBody] UpdateOrderStatusRequest request) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();

    var order = await orderPersistence.UpdateOrderStatusAsync(
        orderId,
        tenantKey,
        request.Status,
        context.RequestAborted);
    if (order is null)
        return CommerceProblem(StatusCodes.Status404NotFound, "not_found");
    await notificationPersistence.SaveNotificationAsync(
        new CommerceNotificationSnapshot(Guid.NewGuid(), order.TenantKey, order.BuyerUserId, "Order updated",
            $"Order {order.Id.ToString()[..8]} is now {order.Status}.", DateTimeOffset.UtcNow, false),
        context.RequestAborted);
    return Results.Ok(ToOrderDto(order));
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.OperatorFulfill,
    AuthorizationPolicyNames.Permissions.CommerceOrdersUpdate);

commerce.MapGet("/profile", async (
    HttpContext context,
    ICommerceProfilePersistence profilePersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var profile = await profilePersistence.GetProfileAsync(
        tenantKey,
        userId,
        context.User.GetEmail(),
        context.RequestAborted);
    return Results.Ok(profile);
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerRead,
    AuthorizationPolicyNames.Permissions.CommerceProfileManage);

commerce.MapPut("/profile", async (
    HttpContext context,
    CommerceStore store,
    ICommerceProfilePersistence profilePersistence,
    [FromBody] UpdateProfileRequest request) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var profile = store.UpdateProfile(tenantKey, userId, context.User.GetEmail(), request);
    await profilePersistence.SaveProfileAsync(
        new CommerceProfileSnapshot(
            profile.TenantKey,
            profile.UserId,
            profile.DisplayName,
            profile.Email,
            profile.Phone,
            profile.CompanyName,
            profile.PriceTier),
        context.RequestAborted);
    return Results.Ok(profile);
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerWrite,
    AuthorizationPolicyNames.Permissions.CommerceProfileManage);

commerce.MapGet("/notifications", async (
    HttpContext context,
    ICommerceNotificationPersistence notificationPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var items = await notificationPersistence.GetNotificationsAsync(tenantKey, userId, context.RequestAborted);
    return Results.Ok(new { items });
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerRead,
    AuthorizationPolicyNames.Permissions.CommerceNotificationsView);

var rfqs = commerce.MapGroup("/rfqs");

rfqs.MapPost("/", async (
    HttpContext context,
    CommerceStore store,
    ICommerceRfqPersistence rfqPersistence,
    [FromBody] CreateRfqRequest request) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var rfq = store.CreateRfq(tenantKey, userId, request);
    if (rfq is null)
        return CommerceProblem(StatusCodes.Status400BadRequest, "invalid_rfq");
    await rfqPersistence.SaveRfqAsync(new CommerceRfqSnapshot(
        rfq.Id, rfq.TenantKey, rfq.BuyerUserId, rfq.Status, rfq.Message, rfq.QuotedTotal, rfq.OperatorNotes,
        rfq.CreatedAt, rfq.RespondedAt, rfq.Lines.Select(x => new CommerceRfqLineSnapshot(x.ProductId, x.Quantity, x.Notes)).ToArray()), context.RequestAborted);
    return Results.Created($"/api/v1/commerce/rfqs/{rfq.Id}", rfq);
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.BuyerWrite,
    AuthorizationPolicyNames.Permissions.CommerceRfqCreate);

rfqs.MapGet("/", async (
    HttpContext context,
    ICommerceRfqPersistence rfqPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var portalClass = context.User.GetPortalClass();
    var buyerOnly = !string.Equals(portalClass, PortalClassConstants.Operator, StringComparison.OrdinalIgnoreCase);
    var items = await rfqPersistence.GetRfqsAsync(tenantKey, buyerOnly ? userId : null, context.RequestAborted);
    return Results.Ok(new { items = items.Select(ToRfqDto).ToArray() });
})
.RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceRfqView);

rfqs.MapGet("/{rfqId:guid}", async (
    Guid rfqId,
    HttpContext context,
    ICommerceRfqPersistence rfqPersistence) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
    var userId = context.User.GetUserId();
    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
        return Results.Forbid();

    var rfq = await rfqPersistence.GetRfqAsync(rfqId, tenantKey, context.RequestAborted);
    if (rfq is null)
        return CommerceProblem(StatusCodes.Status404NotFound, "not_found");

    var portalClass = context.User.GetPortalClass();
    if (!string.Equals(portalClass, PortalClassConstants.Operator, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(rfq.BuyerUserId, userId, StringComparison.OrdinalIgnoreCase))
        return Results.Forbid();

    return Results.Ok(ToRfqDto(rfq));
})
.RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceRfqView);

rfqs.MapPatch("/{rfqId:guid}/respond", async (
    Guid rfqId,
    HttpContext context,
    ICommerceRfqPersistence rfqPersistence,
    [FromBody] RespondRfqRequest request) =>
{
    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
    if (string.IsNullOrWhiteSpace(tenantKey))
        return Results.Forbid();

    var rfq = await rfqPersistence.UpdateRfqAsync(rfqId, tenantKey, request.Status, request.QuotedTotal, request.OperatorNotes, context.RequestAborted);
    return rfq is null
        ? CommerceProblem(StatusCodes.Status404NotFound, "not_found")
        : Results.Ok(ToRfqDto(rfq));
})
.RequireAuthorization(
    CommerceAuthorizationPolicies.OperatorFulfill,
    AuthorizationPolicyNames.Permissions.CommerceRfqRespond);

app.Run();
