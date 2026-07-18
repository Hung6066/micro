using System.Collections.Concurrent;
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
///   <item><b>P0–P1</b>: always admitted (high-priority interactive requests).</item>
///   <item><b>P2</b>: admitted if P0 + P1 + P2 active requests &lt; 70% of max.</item>
///   <item><b>P3–P4</b>: admitted if total active requests &lt; 50% of max.</item>
/// </list>
/// </para>
///
/// <para>
/// Uses per-bucket atomic counters for lightweight, lock-free tracking.
/// This is a best-effort admission mechanism — it does not guarantee
/// exact accounting under extreme concurrency but provides a robust
/// signal during traffic surges.
/// </para>
/// </summary>
public sealed class PriorityAdmissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PriorityAdmissionMiddleware> _logger;
    private readonly PriorityAdmissionOptions _options;

    // Per-priority active request counters (indexed by rank 0–4)
    private static readonly long[] _activeCounts = new long[5];
    private static long _totalActive;

    public PriorityAdmissionMiddleware(
        RequestDelegate next,
        ILogger<PriorityAdmissionMiddleware> logger,
        PriorityAdmissionOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
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

        // Determine if this request should be admitted
        if (!ShouldAdmit(rank))
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

        // Track this request
        Interlocked.Increment(ref _activeCounts[rank]);
        Interlocked.Increment(ref _totalActive);

        try
        {
            await _next(context);
        }
        finally
        {
            Interlocked.Decrement(ref _activeCounts[rank]);
            Interlocked.Decrement(ref _totalActive);
        }
    }

    /// <summary>
    /// Determines whether a request of the given rank should be admitted.
    /// </summary>
    private bool ShouldAdmit(int rank)
    {
        var total = Interlocked.Read(ref _totalActive);

        // P0 and P1: always admitted
        if (rank is 0 or 1)
            return true;

        // P2: admitted if high-priority + medium active < 70% of max
        if (rank is 2)
        {
            var highAndMedium = Interlocked.Read(ref _activeCounts[0])
                              + Interlocked.Read(ref _activeCounts[1])
                              + Interlocked.Read(ref _activeCounts[2]);
            return highAndMedium < (long)(_options.MaxConcurrentRequests * 0.70);
        }

        // P3 and P4: admitted if total active < 50% of max
        return total < (long)(_options.MaxConcurrentRequests * 0.50);
    }

    private string FormatCounts()
    {
        var parts = new string[5];
        for (var i = 0; i < 5; i++)
        {
            parts[i] = $"{PriorityConstants.AllPriorities[i]}={Interlocked.Read(ref _activeCounts[i])}";
        }
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Configuration options for <see cref="PriorityAdmissionMiddleware"/>.
/// Bind from configuration section <c>PriorityAdmission</c> or set programmatically.
/// </summary>
public sealed class PriorityAdmissionOptions
{
    /// <summary>
    /// Maximum number of concurrent requests across all priorities before
    /// lower-priority requests are rejected. Default: 500.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 500;

    /// <summary>
    /// Seconds to suggest the client wait before retrying a rejected request.
    /// Returned as the <c>Retry-After</c> header. Default: 5.
    /// </summary>
    public int RetryAfterSeconds { get; set; } = 5;
}
