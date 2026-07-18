using His.Hope.Infrastructure.Qos;
using Microsoft.AspNetCore.Builder;

namespace His.Hope.Infrastructure.Middleware;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();

    /// <summary>
    /// Adds the <see cref="PriorityHeaderMiddleware"/> to the pipeline.
    /// Should be placed early (after error handling, before admission control)
    /// so all downstream middleware and handlers can read the resolved priority
    /// from <see cref="HttpContext.Items"/>.
    /// </summary>
    public static IApplicationBuilder UsePriorityHeader(this IApplicationBuilder app) =>
        app.UseMiddleware<PriorityHeaderMiddleware>();

    /// <summary>
    /// Adds the <see cref="PriorityAdmissionMiddleware"/> to the pipeline.
    /// Should be placed after <see cref="UsePriorityHeader"/> and before
    /// rate limiting so that lower-priority requests are shed first.
    /// </summary>
    public static IApplicationBuilder UsePriorityAdmission(this IApplicationBuilder app) =>
        app.UseMiddleware<PriorityAdmissionMiddleware>();
}
