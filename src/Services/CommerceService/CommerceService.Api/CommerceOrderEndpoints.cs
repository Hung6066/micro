using His.Hope.Authorization;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.CommerceService.Api;

internal static class CommerceOrderEndpoints
{
    public static void MapCommerceOrderQueryEndpoints(this RouteGroupBuilder commerce)
    {
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
            UpdateOrderStatusRequest request) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
            if (string.IsNullOrWhiteSpace(tenantKey))
                return Results.Forbid();

            var existingOrder = await orderPersistence.GetOrderAsync(orderId, tenantKey, context.RequestAborted);
            if (existingOrder is null)
                return CommerceProblem(StatusCodes.Status404NotFound, "not_found");

            var normalizedStatus = CommerceOrderStatusPolicy.Normalize(request.Status);
            if (!CommerceOrderStatusPolicy.CanTransition(existingOrder.Status, normalizedStatus))
                return CommerceProblem(StatusCodes.Status409Conflict, "invalid_status_transition");

            var order = await orderPersistence.UpdateOrderStatusAsync(
                orderId,
                tenantKey,
                request.Status,
                existingOrder.Status,
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
    }

    public static void MapCommerceOrderCreationEndpoint(this RouteGroupBuilder commerce)
    {
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
            var persistedProducts = await catalogPersistence.GetProductsAsync(tenantKey, context.RequestAborted);
            var orderAggregate = CommerceOrderAggregate.Create(
                tenantKey,
                userId,
                persistedCart,
                persistedProfile,
                persistedProducts);
            if (orderAggregate is null)
                return (IResult)Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "cart_empty" });

            var orderSnapshot = orderAggregate.Snapshot;
            var order = new OrderDto(
                orderSnapshot.Id,
                orderSnapshot.TenantKey,
                orderSnapshot.BuyerUserId,
                orderSnapshot.Status,
                orderSnapshot.TotalAmount,
                orderSnapshot.CreatedAt,
                orderSnapshot.Lines.Select(line => new OrderLineDto(
                    line.ProductId,
                    line.Sku,
                    line.Name,
                    line.Quantity,
                    line.UnitPrice)).ToArray());

            var correlationId = context.Request.Headers[His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Headers.CorrelationId].FirstOrDefault();
            var @event = CommerceOrderEventFactory.Create(order, correlationId);
            await orderPersistence.SaveOrderAndOutboxAsync(orderSnapshot, @event, context.RequestAborted);
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
    }

    private static IResult CommerceProblem(int statusCode, string errorCode) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

    private static OrderDto ToOrderDto(CommerceOrderView order) =>
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
}
