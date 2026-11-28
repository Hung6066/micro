using His.Hope.Authorization;
using His.Hope.CommerceService.Application.Customer;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.CommerceService.Api;

internal static class CommerceCustomerEndpoints
{
    public static void MapCommerceCustomerEndpoints(this RouteGroupBuilder commerce)
    {
        commerce.MapGet("/cart", async (
            HttpContext context,
            ICommerceCustomerWorkflow workflow) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var cart = await workflow.GetCartAsync(tenantKey, userId, context.RequestAborted);
            return Results.Ok(new CartDto(
                cart.TenantKey,
                cart.Lines.Select(line => new CartLineDto(line.ProductId, line.Quantity)).ToArray()));
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.BuyerRead,
            AuthorizationPolicyNames.Permissions.CommerceCatalogView);

        commerce.MapPut("/cart", async (
            HttpContext context,
            ICommerceCustomerWorkflow workflow,
            UpdateCartRequest request) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var cart = await workflow.UpdateCartAsync(
                tenantKey,
                userId,
                request.Lines.Select(line => new CommerceCartLineSnapshot(line.ProductId, line.Quantity)).ToArray(),
                context.RequestAborted);
            return Results.Ok(new CartDto(
                cart.TenantKey,
                cart.Lines.Select(line => new CartLineDto(line.ProductId, line.Quantity)).ToArray()));
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.BuyerWrite,
            AuthorizationPolicyNames.Permissions.CommerceCatalogView);

        commerce.MapGet("/profile", async (
            HttpContext context,
            ICommerceCustomerWorkflow workflow) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var profile = await workflow.GetProfileAsync(
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
            ICommerceCustomerWorkflow workflow,
            UpdateProfileRequest request) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var profile = await workflow.UpdateProfileAsync(
                tenantKey,
                userId,
                context.User.GetEmail(),
                request.DisplayName,
                request.Phone,
                request.CompanyName,
                request.PriceTier,
                context.RequestAborted);
            return Results.Ok(profile);
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.BuyerWrite,
            AuthorizationPolicyNames.Permissions.CommerceProfileManage);

        commerce.MapGet("/notifications", async (
            HttpContext context,
            ICommerceCustomerWorkflow workflow) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var items = await workflow.GetNotificationsAsync(tenantKey, userId, context.RequestAborted);
            return Results.Ok(new { items });
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.BuyerRead,
            AuthorizationPolicyNames.Permissions.CommerceNotificationsView);

        commerce.MapPatch("/notifications/{notificationId:guid}/read", async (
            Guid notificationId,
            HttpContext context,
            ICommerceCustomerWorkflow workflow) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            await workflow.MarkNotificationAsReadAsync(
                notificationId,
                tenantKey,
                userId,
                context.RequestAborted);
            return Results.NoContent();
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.BuyerWrite,
            AuthorizationPolicyNames.Permissions.CommerceNotificationsView);
    }
}
