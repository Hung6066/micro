using His.Hope.Authorization;

using His.Hope.CommerceService.Api;

using His.Hope.CommerceService.Api.Security;

using His.Hope.Infrastructure.Caching;

using His.Hope.Infrastructure.Security;

using His.Hope.ServiceDefaults;

using His.Hope.SharedKernel.Authorization;

using Microsoft.AspNetCore.Mvc;

using StackExchange.Redis;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "CommerceService");

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

His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(

    builder.Services,

    builder.Configuration,

    options => options.Audience = builder.Configuration["Jwt:Audience"] ?? "commerce-api");

builder.Services.AddHisHopeAuthorization();



var app = builder.Build();

app.UseHisHopeServiceDefaults();

app.UseDpopAuthorizationSchemeNormalization();

app.UseAuthentication();

app.UseDpopAccessTokenValidation();

app.UseAuthorization();

app.UseCommerceRateLimiting();

app.UseCommerceSecurity();



var commerce = app.MapGroup("/api/v1/commerce").RequireAuthorization();



commerce.MapGet("/session", (HttpContext context) =>
{
    var portalClass = CommercePortalGuard.GetPortalClass(context.User);
    if (string.IsNullOrWhiteSpace(portalClass))
        return Results.Forbid();

    return Results.Ok(new
    {
        portalClass,
        tenantId = context.User.GetTokenTenant(),
        clientId = CommercePortalGuard.GetClientId(context.User),
        userId = context.User.GetUserId(),
    });
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceCatalogView);



commerce.MapGet("/products", (

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    if (string.IsNullOrWhiteSpace(tenantKey))

        return Results.Forbid();



    return Results.Ok(new { items = store.GetProducts(tenantKey) });

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceCatalogView);



commerce.MapGet("/cart", (

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    return Results.Ok(store.GetCart(tenantKey, userId));

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceCatalogView);



commerce.MapPut("/cart", (

    HttpContext context,

    CommerceStore store,

    [FromBody] UpdateCartRequest request) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    return Results.Ok(store.UpdateCart(tenantKey, userId, request.Lines));

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceCatalogView);



commerce.MapPost("/orders", (

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    var order = store.CreateOrder(tenantKey, userId);

    return order is null

        ? Results.BadRequest(new { error = "cart_empty" })

        : Results.Created($"/api/v1/commerce/orders/{order.Id}", order);

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceOrdersCreate);



commerce.MapGet("/orders", (

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    var buyerOnly = !CommercePortalGuard.IsOperator(context.User);

    var orders = store.GetOrders(tenantKey, buyerOnly ? userId : null);

    return Results.Ok(new { items = orders });

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceOrdersView);



commerce.MapGet("/orders/{orderId:guid}", (

    Guid orderId,

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    var order = store.GetOrder(orderId, tenantKey);

    if (order is null)

        return Results.NotFound();



    if (!CommercePortalGuard.IsOperator(context.User) &&

        !string.Equals(order.BuyerUserId, userId, StringComparison.OrdinalIgnoreCase))

        return Results.Forbid();



    return Results.Ok(order);

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceOrdersView);



commerce.MapPatch("/orders/{orderId:guid}/status", (

    Guid orderId,

    HttpContext context,

    CommerceStore store,

    [FromBody] UpdateOrderStatusRequest request) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    if (string.IsNullOrWhiteSpace(tenantKey))

        return Results.Forbid();



    var order = store.UpdateOrderStatus(orderId, tenantKey, request.Status);

    return order is null ? Results.NotFound() : Results.Ok(order);

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceOrdersUpdate);



commerce.MapGet("/profile", (

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    return Results.Ok(store.GetProfile(tenantKey, userId, context.User.GetEmail()));

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceProfileManage);



commerce.MapPut("/profile", (

    HttpContext context,

    CommerceStore store,

    [FromBody] UpdateProfileRequest request) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    return Results.Ok(store.UpdateProfile(tenantKey, userId, context.User.GetEmail(), request));

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceProfileManage);



commerce.MapGet("/notifications", (

    HttpContext context,

    CommerceStore store) =>

{

    var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);

    var userId = context.User.GetUserId();

    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))

        return Results.Forbid();



    return Results.Ok(new { items = store.GetNotifications(tenantKey, userId) });

}).RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceNotificationsView);



app.Run();


