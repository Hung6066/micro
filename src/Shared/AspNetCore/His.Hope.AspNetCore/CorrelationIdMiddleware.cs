using Microsoft.AspNetCore.Http;

namespace His.Hope.AspNetCore;

internal sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HisHopeAspNetCoreOptions _options;

    public CorrelationIdMiddleware(RequestDelegate next, HisHopeAspNetCoreOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[_options.CorrelationHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > _options.MaximumCorrelationIdLength)
            correlationId = Guid.NewGuid().ToString("N");

        context.Items[HisHopeAspNetCoreExtensions.CorrelationIdItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[_options.CorrelationHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
