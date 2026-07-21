using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace His.Hope.Bff.Core.Telemetry;

public sealed class BffMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public BffMetricsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var module = context.Request.Path.Value?.Split('/').Skip(2).FirstOrDefault() ?? "unknown";
        await _next(context);
        sw.Stop();

        BffMetrics.BffRequestsTotal.Add(1, new KeyValuePair<string, object?>[]
            { new("module", module), new("status", context.Response.StatusCode.ToString()) });
        BffMetrics.BffRequestDuration.Record(sw.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("module", module));
    }
}

public static class BffMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseBffMetrics(this IApplicationBuilder builder)
        => builder.UseMiddleware<BffMetricsMiddleware>();
}
