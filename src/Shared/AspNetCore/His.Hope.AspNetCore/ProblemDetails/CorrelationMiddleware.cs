using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace His.Hope.AspNetCore.ProblemDetails;

public static class HisHopeCorrelation
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string GetId(HttpContext context) =>
        context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Current.Value
        ?? context.TraceIdentifier;

    internal static string? CurrentId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

internal sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        correlationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")[..12]
            : correlationId;

        HisHopeCorrelation.CurrentId = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            HisHopeCorrelation.CurrentId = null;
        }
    }
}

public static class CorrelationMiddlewareExtensions
{
    public static IApplicationBuilder UseHisHopeCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationMiddleware>();
}
