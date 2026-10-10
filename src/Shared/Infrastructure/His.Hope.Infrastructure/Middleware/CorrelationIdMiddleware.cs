using His.Hope.Infrastructure.Observability;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Http;

namespace His.Hope.Infrastructure.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HisHopeProtocolConstants.Headers.CorrelationId].FirstOrDefault();

        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12];
        }

        CorrelationContext.CurrentId = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HisHopeProtocolConstants.Headers.CorrelationId))
            {
                context.Response.Headers[HisHopeProtocolConstants.Headers.CorrelationId] = correlationId;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
