using His.Hope.Authorization;
using His.Hope.CommerceService.Application.Customer;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.CommerceService.Api;

internal static class CommerceRfqEndpoints
{
    public static void MapCommerceRfqEndpoints(this RouteGroupBuilder commerce)
    {
        var rfqs = commerce.MapGroup("/rfqs");

        rfqs.MapPost("/", async (
            HttpContext context,
            ICommerceRfqWorkflow workflow,
            CreateRfqRequest request) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var rfq = await workflow.CreateAsync(
                tenantKey,
                userId,
                request.Message,
                request.Lines.Select(line => new CommerceRfqLineSnapshot(line.ProductId, line.Quantity, line.Notes)).ToArray(),
                context.RequestAborted);
            if (rfq is null)
                return Problem(StatusCodes.Status400BadRequest, "invalid_rfq");
            return Results.Created($"/api/v1/commerce/rfqs/{rfq.Id}", ToRfqDto(rfq));
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.BuyerWrite,
            AuthorizationPolicyNames.Permissions.CommerceRfqCreate);

        rfqs.MapGet("/", async (HttpContext context, ICommerceRfqWorkflow workflow) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var buyerOnly = !string.Equals(context.User.GetPortalClass(), PortalClassConstants.Operator, StringComparison.OrdinalIgnoreCase);
            var items = await workflow.GetManyAsync(tenantKey, buyerOnly ? userId : null, context.RequestAborted);
            return Results.Ok(new { items = items.Select(ToRfqDto).ToArray() });
        })
        .RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceRfqView);

        rfqs.MapGet("/{rfqId:guid}", async (Guid rfqId, HttpContext context, ICommerceRfqWorkflow workflow) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context);
            var userId = context.User.GetUserId();
            if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(userId))
                return Results.Forbid();

            var rfq = await workflow.GetAsync(rfqId, tenantKey, context.RequestAborted);
            if (rfq is null)
                return Problem(StatusCodes.Status404NotFound, "not_found");

            var isOperator = string.Equals(context.User.GetPortalClass(), PortalClassConstants.Operator, StringComparison.OrdinalIgnoreCase);
            if (!isOperator && !string.Equals(rfq.BuyerUserId, userId, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();
            return Results.Ok(ToRfqDto(rfq));
        })
        .RequireAuthorization(AuthorizationPolicyNames.Permissions.CommerceRfqView);

        rfqs.MapPatch("/{rfqId:guid}/respond", async (
            Guid rfqId,
            HttpContext context,
            ICommerceRfqWorkflow workflow,
            RespondRfqRequest request) =>
        {
            var tenantKey = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: true);
            if (string.IsNullOrWhiteSpace(tenantKey))
                return Results.Forbid();

            var rfq = await workflow.RespondAsync(rfqId, tenantKey, request.Status, request.QuotedTotal, request.OperatorNotes, context.RequestAborted);
            return rfq is null
                ? Problem(StatusCodes.Status404NotFound, "not_found")
                : Results.Ok(ToRfqDto(rfq));
        })
        .RequireAuthorization(
            CommerceAuthorizationPolicies.OperatorFulfill,
            AuthorizationPolicyNames.Permissions.CommerceRfqRespond);
    }

    private static IResult Problem(int statusCode, string errorCode) => Results.Problem(
        statusCode: statusCode,
        extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

    private static RfqDto ToRfqDto(CommerceRfqSnapshot rfq) =>
        new(rfq.Id, rfq.TenantKey, rfq.BuyerUserId, rfq.Status, rfq.Message, rfq.QuotedTotal, rfq.OperatorNotes,
            rfq.CreatedAt, rfq.RespondedAt, rfq.Lines.Select(line => new RfqLineDto(line.ProductId, line.Quantity, line.Notes)).ToArray());
}
