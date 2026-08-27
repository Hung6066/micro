using System.Security.Claims;
using Microsoft.AspNetCore.Http;

internal sealed class MobileOperationReplayFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Idempotency applies to state-changing operations only. Read requests must
        // remain cacheable/repeatable even when a client happens to send a key.
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || HttpMethods.IsHead(context.HttpContext.Request.Method)
            || HttpMethods.IsOptions(context.HttpContext.Request.Method))
            return await next(context);

        var operationId = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim()
            ?? context.HttpContext.Request.Headers["X-HisHope-Operation-Id"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(operationId)) return await next(context);
        if (operationId.Length > 200)
            return Results.BadRequest(new { errorCode = "invalid_idempotency_key" });

        var tenant = context.HttpContext.User.FindFirst("tenant_id")?.Value
            ?? context.HttpContext.User.FindFirst("tenant")?.Value
            ?? "unknown";
        var subject = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.HttpContext.User.FindFirst("sub")?.Value
            ?? "unknown";
        var method = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        var store = context.HttpContext.RequestServices.GetRequiredService<ManufacturingMobileOperationReplayStore>();
        if (!await store.TryReserveAsync(tenant, subject, method, path, operationId, context.HttpContext.RequestAborted))
        {
            context.HttpContext.Response.Headers["X-HisHope-Operation-Replay"] = "true";
            context.HttpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Conflict(new { errorCode = "operation_replayed", operationId });
        }

        try { return await next(context); }
        catch
        {
            await store.ReleaseAsync(tenant, subject, method, path, operationId, CancellationToken.None);
            throw;
        }
    }
}
