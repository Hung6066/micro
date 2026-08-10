using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace His.Hope.Infrastructure.Backpressure;

/// <summary>
/// Adaptive concurrency limiter inspired by Netflix Concurrency Limits.
/// Uses a rolling 1-minute latency window to self-tune the max concurrency
/// level based on current p99 latency relative to the established baseline.
/// Thread-safe and designed for singleton lifetime.
/// </summary>
/// <remarks>
/// Strategy:
/// <list type="bullet">
///   <item>Establish a baseline p99 from the first <see cref="BaselineSampleCount"/> samples.</item>
///   <item>Every <see cref="AdjustmentIntervalMs"/>ms, compare current p99 to the baseline.</item>
///   <item>If current p99 &gt; baseline × 1.2 → reduce concurrency by 10 % (floor: <see cref="MinConcurrency"/>).</item>
///   <item>If current p99 &lt; baseline × 0.9 → increase concurrency by 5 % (ceiling: <see cref="MaxConcurrency"/>).</item>
///   <item>Otherwise keep the current limit unchanged.</item>
/// </list>
/// </remarks>
public sealed class AdaptiveConcurrencyLimiter : IDisposable
{
    // ── Constants ──────────────────────────────────────────────────────────
    private const int BaselineSampleCount = 100;
    private const int AdjustmentIntervalMs = 5_000;
    private const int RollingWindowSeconds = 60;
    private const double LatencyIncreaseMultiplier = 1.2;   // current / baseline ratio at which we reduce
    private const double LatencyDecreaseMultiplier = 0.9;   // current / baseline ratio at which we increase
    private const double DecreaseFactor = 0.90;              // multiply limit by 0.9 when reducing ( −10 %)
    private const double IncreaseFactor = 1.05;              // multiply limit by 1.05 when increasing ( +5 %)
    private const int MinConcurrency = 5;
    private const int MaxConcurrency = 100;
    private const int DefaultConcurrency = 10;

    // ── State ──────────────────────────────────────────────────────────────
    private readonly ConcurrentQueue<LatencySample> _samples = new();
    private readonly Meter _meter;
    private readonly ObservableGauge<int> _limitGauge;
    private readonly ObservableGauge<double> _p99Gauge;
    private readonly ObservableGauge<double> _baselineGauge;
    private readonly Timer _adjustmentTimer;
    private readonly object _calculationLock = new();

    private int _currentLimit = DefaultConcurrency;
    private long _totalSamplesEnqueued;
    private double _baselineP99;
    private volatile bool _baselineEstablished;

    private bool _disposed;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Gets the current adaptive concurrency limit.</summary>
    public int CurrentLimit => Volatile.Read(ref _currentLimit);

    /// <summary>Gets the established baseline p99 latency (ms). 0 until baseline is set.</summary>
    public double BaselineP99 => Volatile.Read(ref _baselineP99);

    /// <summary>true once the initial baseline has been calculated.</summary>
    public bool BaselineEstablished => _baselineEstablished;

    public AdaptiveConcurrencyLimiter()
    {
        _meter = new Meter("His.Hope.Infrastructure.Backpressure", "1.0.0");

        _limitGauge = _meter.CreateObservableGauge<int>(
            name: "adaptive_concurrency_limit",
            observeValue: () => CurrentLimit,
            description: "Current adaptive concurrency limit");

        _p99Gauge = _meter.CreateObservableGauge<double>(
            name: "adaptive_concurrency_p99_ms",
            observeValue: () => BaselineEstablished ? CalculateP99() : 0,
            description: "Current p99 latency in milliseconds");

        _baselineGauge = _meter.CreateObservableGauge<double>(
            name: "adaptive_concurrency_baseline_p99_ms",
            observeValue: () => BaselineP99,
            description: "Baseline p99 latency in milliseconds");

        _adjustmentTimer = new Timer(
            callback: _ => AdjustConcurrency(),
            state: null,
            dueTime: AdjustmentIntervalMs,
            period: AdjustmentIntervalMs);
    }

    /// <summary>
    /// Records a latency sample into the rolling window.
    /// Thread-safe; can be called concurrently from any execution context.
    /// </summary>
    /// <param name="latencyMs">The observed latency in milliseconds.</param>
    public void RecordLatency(long latencyMs)
    {
        var sample = new LatencySample(DateTimeOffset.UtcNow, latencyMs);
        _samples.Enqueue(sample);
        Interlocked.Increment(ref _totalSamplesEnqueued);

        TrimExpiredSamples();

        // Attempt to establish baseline once we have enough samples.
        if (!_baselineEstablished && Volatile.Read(ref _totalSamplesEnqueued) >= BaselineSampleCount)
        {
            TryEstablishBaseline();
        }
    }

    /// <summary>Releases all resources used by the limiter.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _adjustmentTimer?.Dispose();
        _meter?.Dispose();
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void TrimExpiredSamples()
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-RollingWindowSeconds);
        while (_samples.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _samples.TryDequeue(out _);
        }
    }

    private void TryEstablishBaseline()
    {
        lock (_calculationLock)
        {
            if (_baselineEstablished) return;

            var snapshot = _samples.ToArray();
            if (snapshot.Length < BaselineSampleCount) return;

            var sorted = snapshot
                .Select(static s => s.LatencyMs)
                .OrderBy(static v => v)
                .ToArray();

            Volatile.Write(ref _baselineP99, CalculatePercentile(sorted, 0.99));
            _baselineEstablished = true;
        }
    }

    private double CalculateP99()
    {
        lock (_calculationLock)
        {
            var snapshot = _samples
                .Select(static s => s.LatencyMs)
                .OrderBy(static v => v)
                .ToArray();

            return snapshot.Length == 0
                ? 0
                : CalculatePercentile(snapshot, 0.99);
        }
    }

    private void AdjustConcurrency()
    {
        if (_disposed || !_baselineEstablished) return;

        var currentP99 = CalculateP99();
        if (currentP99 <= 0) return;

        int newLimit;

        var baseline = Volatile.Read(ref _baselineP99);
        var current = Volatile.Read(ref _currentLimit);

        if (currentP99 > baseline * LatencyIncreaseMultiplier)
        {
            // Latency is too high — reduce concurrency by 10 %
            newLimit = (int)Math.Max(MinConcurrency, current * DecreaseFactor);
        }
        else if (currentP99 < baseline * LatencyDecreaseMultiplier)
        {
            // Latency is comfortably low — increase concurrency by 5 %
            newLimit = (int)Math.Min(MaxConcurrency, current * IncreaseFactor);
        }
        else
        {
            // Within the acceptable band — no change.
            return;
        }

        if (newLimit != current)
        {
            Interlocked.Exchange(ref _currentLimit, newLimit);
        }
    }

    /// <summary>
    /// Calculates the given percentile from a sorted array using linear interpolation.
    /// Equivalent to Excel's PERCENTILE.INC / numpy's percentile(..., interpolation='linear').
    /// </summary>
    private static double CalculatePercentile(long[] sorted, double percentile)
    {
        Debug.Assert(sorted.Length > 0);
        Debug.Assert(percentile is >= 0.0 and <= 1.0);

        if (sorted.Length == 1) return sorted[0];

        double rank = percentile * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);

        if (Math.Abs(lower - upper) < 0.001) return sorted[lower];

        double frac = rank - lower;
        return sorted[lower] * (1.0 - frac) + sorted[upper] * frac;
    }

    /// <summary>
    /// A single latency observation with a timestamp for rolling-window expiry.
    /// </summary>
    private readonly record struct LatencySample(DateTimeOffset Timestamp, long LatencyMs);
}
