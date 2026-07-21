using System.Diagnostics.Metrics;

namespace His.Hope.Bff.Core.Telemetry;

public static class BffMetrics
{
    private static readonly Meter Meter = new("His.Hope.Bff", "1.0.0");

    // Request metrics
    public static readonly Counter<long> BffRequestsTotal = Meter.CreateCounter<long>(
        "hishop_bff_requests_total", description: "Total BFF requests by module and status");

    public static readonly Histogram<double> BffRequestDuration = Meter.CreateHistogram<double>(
        "hishop_bff_request_duration_seconds", "ms", "Request duration");

    // Session metrics
    public static readonly Counter<long> SessionHits = Meter.CreateCounter<long>(
        "hishop_bff_session_hits_total", description: "Redis session cache hits");
    public static readonly Counter<long> SessionMisses = Meter.CreateCounter<long>(
        "hishop_bff_session_misses_total", description: "Redis session cache misses");
    public static readonly Counter<long> SessionExpired = Meter.CreateCounter<long>(
        "hishop_bff_session_expired_total", description: "Expired session attempts");

    // Auth metrics
    public static readonly Counter<long> AuthFailures = Meter.CreateCounter<long>(
        "hishop_bff_auth_failures_total", description: "401 responses");
    public static readonly Counter<long> CsrfFailures = Meter.CreateCounter<long>(
        "hishop_bff_csrf_failures_total", description: "403 CSRF rejections");

    // Aggregation metrics
    public static readonly Histogram<double> AggregationDuration = Meter.CreateHistogram<double>(
        "hishop_bff_aggregation_duration_seconds", "ms", "Aggregation execution time");
    public static readonly Counter<long> AggregationDegraded = Meter.CreateCounter<long>(
        "hishop_bff_aggregation_degraded_total", description: "Partial failure responses");

    // Downstream metrics
    public static readonly Counter<long> DownstreamErrors = Meter.CreateCounter<long>(
        "hishop_bff_downstream_errors_total", description: "Downstream service failures");
    public static readonly Histogram<double> DownstreamDuration = Meter.CreateHistogram<double>(
        "hishop_bff_downstream_duration_seconds", "ms", "Downstream call duration");

    // Circuit breaker
    public static readonly UpDownCounter<int> CircuitBreakerState = Meter.CreateUpDownCounter<int>(
        "hishop_bff_circuit_breaker_state", description: "Circuit breaker state (0=closed, 1=open)");
}
