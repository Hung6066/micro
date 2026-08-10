using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace His.Hope.IdentityService.Api.Metrics;

public static class IdentitySloMetrics
{
    private static readonly Meter Meter = new("His.Hope.Identity", "1.0.0");

    public static readonly Counter<long> TokensIssued = Meter.CreateCounter<long>(
        "identity.tokens.issued",
        description: "Total tokens issued by grant type");

    public static readonly Counter<long> TokenFailures = Meter.CreateCounter<long>(
        "identity.tokens.failures",
        description: "Failed token requests by grant type and failure reason");

    public static readonly Counter<long> LoginsSucceeded = Meter.CreateCounter<long>(
        "identity.logins.succeeded",
        description: "Successful logins by authentication method");

    public static readonly Counter<long> LoginsFailed = Meter.CreateCounter<long>(
        "identity.logins.failed",
        description: "Failed logins by failure reason");

    public static readonly Counter<long> Introspections = Meter.CreateCounter<long>(
        "identity.introspections.total",
        description: "Total token introspection calls");

    public static readonly Counter<long> TokensRevoked = Meter.CreateCounter<long>(
        "identity.tokens.revoked",
        description: "Total tokens revoked by reason");

    public static readonly Histogram<double> TokenIssueDuration = Meter.CreateHistogram<double>(
        "identity.tokens.issue_duration_ms",
        unit: "ms",
        description: "Token issue duration in milliseconds");

    public static readonly Histogram<double> IntrospectionDuration = Meter.CreateHistogram<double>(
        "identity.introspection.duration_ms",
        unit: "ms",
        description: "Token introspection duration in milliseconds");

    public static readonly Histogram<double> LoginDuration = Meter.CreateHistogram<double>(
        "identity.login.duration_ms",
        unit: "ms",
        description: "Login duration by provider in milliseconds");

    public static void RecordTokenIssued(string grantType)
    {
        TokensIssued.Add(1, new KeyValuePair<string, object?>("grant_type", grantType));
    }

    public static void RecordTokenFailure(string grantType, string reason)
    {
        TokenFailures.Add(1,
            new KeyValuePair<string, object?>("grant_type", grantType),
            new KeyValuePair<string, object?>("reason", reason));
    }

    public static void RecordLoginSucceeded(string provider)
    {
        LoginsSucceeded.Add(1, new KeyValuePair<string, object?>("provider", provider));
    }

    public static void RecordLoginFailed(string reason)
    {
        LoginsFailed.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public static void RecordIntrospection()
    {
        Introspections.Add(1);
    }

    public static void RecordTokenRevoked(string reason)
    {
        TokensRevoked.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public static IDisposable MeasureTokenIssue() => new TimerScope(TokenIssueDuration);
    public static IDisposable MeasureIntrospection() => new TimerScope(IntrospectionDuration);
    public static IDisposable MeasureLogin() => new TimerScope(LoginDuration);

    private sealed class TimerScope : IDisposable
    {
        private readonly Histogram<double> _histogram;
        private readonly long _start;
        public TimerScope(Histogram<double> histogram)
        {
            _histogram = histogram;
            _start = Stopwatch.GetTimestamp();
        }
        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
            _histogram.Record(elapsed);
        }
    }
}
