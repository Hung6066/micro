using System.Security.Claims;
using Microsoft.AspNetCore.Http;

internal sealed class MobileOperationReplayFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var operationId = context.HttpContext.Request.Headers["X-HisHope-Operation-Id"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(operationId)) return await next(context);

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
