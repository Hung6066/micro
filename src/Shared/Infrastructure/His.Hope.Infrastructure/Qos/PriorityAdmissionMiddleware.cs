using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Qos;

/// <summary>
/// Middleware that enforces admission control per priority tier.
/// Rejects requests when the service is under heavy load, using the
/// priority stored in <see cref="HttpContext.Items"/> by
/// <see cref="PriorityHeaderMiddleware"/>.
///
/// <para>
/// Thresholds (configurable via <see cref="PriorityAdmissionOptions"/>):
/// <list type="bullet">
///   <item><b>P0–P1</b>: reserved capacity and bounded wait.</item>
///   <item><b>P2–P4</b>: bounded queue with priority aging to prevent starvation.</item>
/// </list>
/// </para>
///
/// <para>
/// Uses a bounded in-process queue with explicit leases so active work is
/// released even when the downstream request fails or is cancelled.
/// </para>
/// </summary>
public sealed class PriorityAdmissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PriorityAdmissionMiddleware> _logger;
    private readonly PriorityAdmissionOptions _options;

    private readonly PriorityAdmissionController _controller;

    public PriorityAdmissionMiddleware(
        RequestDelegate next,
        ILogger<PriorityAdmissionMiddleware> logger,
        PriorityAdmissionOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
        _controller = new PriorityAdmissionController(options);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Always allow health checks and metrics to prevent cascading failures
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var priority = context.Items[PriorityConstants.ContextItemsKey] as string
            ?? PriorityConstants.DefaultPriority;
        var rank = PriorityConstants.GetRank(priority);

        await using var lease = await _controller.AcquireAsync(rank, context.RequestAborted);
        if (lease is null)
        {
            _logger.LogWarning(
                "Admission rejected: priority={Priority} rank={Rank} activeCounts=[{Counts}] maxConcurrent={Max}",
                priority, rank, FormatCounts(), _options.MaxConcurrentRequests);

            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.Headers["Retry-After"] = _options.RetryAfterSeconds.ToString();
            context.Response.Headers["X-Priority-Rejected"] = priority;
            await context.Response.WriteAsync(
                $$"""{"error":"Service at capacity","priority":"{{priority}}","retryAfterSeconds":{{_options.RetryAfterSeconds}}}""");
            return;
        }

        await _next(context);
    }

    private string FormatCounts() => "controller-managed";
}

/// <summary>
/// Configuration options for <see cref="PriorityAdmissionMiddleware"/>.
/// Bind from configuration section <c>PriorityAdmission</c> or set programmatically.
/// </summary>
public sealed class PriorityAdmissionOptions
{
    /// <summary>
    /// Maximum number of concurrent requests across all priorities. Default: 500.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 500;

    /// <summary>
    /// Seconds to suggest the client wait before retrying a rejected request.
    /// Returned as the <c>Retry-After</c> header. Default: 5.
    /// </summary>
    public int RetryAfterSeconds { get; set; } = 5;

    public int QueueCapacity { get; set; } = 100;

    public int MaxWaitMilliseconds { get; set; } = 250;

    public double ReservedHighPriorityFraction { get; set; } = 0.20;

    public int AgingStepMilliseconds { get; set; } = 1000;
}
