using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Qos;

/// <summary>
/// ASP.NET middleware that reads the <c>X-Priority</c> request header,
/// resolves the priority tier (P0–P4, defaulting to P1), and:
/// <list type="bullet">
///   <item>Stores the resolved priority in <see cref="HttpContext.Items"/> for downstream middleware and handlers.</item>
///   <item>Sets the <c>X-Priority</c> response header so downstream HTTP services receive the tier.</item>
///   <item>Logs the resolved priority for observability and debugging.</item>
/// </list>
///
/// <para>
/// Design Rationale:
/// In a hospital information system, not all requests have equal urgency.
/// Life-critical alerts (P0) must never be dropped, while batch exports (P4)
/// can be queued. By propagating the priority tier through every layer of the
/// stack (HTTP → gRPC → RabbitMQ), each component can apply appropriate
/// admission control, scheduling, and resource allocation.
/// </para>
/// </summary>
public sealed class PriorityHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PriorityHeaderMiddleware> _logger;

    public PriorityHeaderMiddleware(RequestDelegate next, ILogger<PriorityHeaderMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Read the X-Priority header; default to P1 if absent or empty
        var priority = context.Request.Headers[PriorityConstants.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(priority) || !PriorityConstants.AllPriorities.Contains(priority))
        {
            priority = PriorityConstants.DefaultPriority;
        }

        // Store in HttpContext.Items for downstream middleware, handlers, and gRPC interceptors
        context.Items[PriorityConstants.ContextItemsKey] = priority;

        // Propagate via response header so downstream HTTP services can read the tier
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(PriorityConstants.HeaderName))
            {
                context.Response.Headers[PriorityConstants.HeaderName] = priority;
            }
            return Task.CompletedTask;
        });

        _logger.LogDebug("Request priority resolved: {Priority} for {Method} {Path}",
            priority, context.Request.Method, context.Request.Path);

        await _next(context);
    }

    /// <summary>
    /// Helper: reads the priority from <see cref="HttpContext.Items"/> and adds it
    /// as gRPC <see cref="Metadata"/> for outgoing calls. Call this from a gRPC
    /// interceptor or delegating handler to propagate the priority across service boundaries.
    /// </summary>
    public static void AddPriorityToGrpcMetadata(HttpContext? httpContext, Metadata metadata)
    {
        if (httpContext?.Items[PriorityConstants.ContextItemsKey] is string priority)
        {
            metadata.Add(PriorityConstants.HeaderName, priority);
        }
    }

    /// <summary>
    /// Helper: reads the priority from <see cref="HttpContext.Items"/> and returns it
    /// so callers can attach it to RabbitMQ message headers or other outgoing channels.
    /// Returns <see cref="PriorityConstants.DefaultPriority"/> if no priority is set.
    /// </summary>
    public static string GetPriority(HttpContext? httpContext)
    {
        return httpContext?.Items[PriorityConstants.ContextItemsKey] as string
            ?? PriorityConstants.DefaultPriority;
    }
}
