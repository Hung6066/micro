using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace His.Hope.Observability;

public static class AuditSinkValidation
{
    public static void RequireDurableAuditSink(this IServiceProvider services)
    {
        if (services.GetService<IAuditSink>() is not IDurableAuditSink)
            throw new InvalidOperationException(
                "A durable IAuditSink is required in production. Register an IDurableAuditSink implementation.");
    }
}

public sealed class ObservabilityOptions
{
    public string ServiceName { get; set; } = "His.Hope";
    public string ServiceVersion { get; set; } = "1.0.0";
}

public static class ObservabilityRegistration
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        Action<ObservabilityOptions>? configure = null)
    {
        var options = new ObservabilityOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton(sp => new ActivitySource(
            sp.GetRequiredService<ObservabilityOptions>().ServiceName,
            sp.GetRequiredService<ObservabilityOptions>().ServiceVersion));
        services.TryAddSingleton(sp => new Meter(
            sp.GetRequiredService<ObservabilityOptions>().ServiceName,
            sp.GetRequiredService<ObservabilityOptions>().ServiceVersion));
        services.TryAddSingleton<ITracer, ActivitySourceTracer>();
        services.TryAddSingleton<IMetrics, MeterMetrics>();
        services.TryAddSingleton<IStructuredLogger, LoggerStructuredLogger>();
        services.TryAddSingleton<IAuditSink, NullAuditSink>();

        return services;
    }
}

internal sealed class ActivitySourceTracer(ActivitySource source) : ITracer
{
    public Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null) =>
        source.StartActivity(name, kind, default(ActivityContext), tags);
}

internal sealed class MeterMetrics(Meter meter) : IMetrics
{
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    public void Increment(
        string name,
        long value = 1,
        IReadOnlyDictionary<string, object?>? tags = null)
    {
        var counter = _counters.GetOrAdd(name, metricName => meter.CreateCounter<long>(metricName));
        counter.Add(value, CreateTags(tags));
    }

    public void Record(
        string name,
        double value,
        string? unit = null,
        IReadOnlyDictionary<string, object?>? tags = null)
    {
        var histogram = _histograms.GetOrAdd(
            name,
            metricName => meter.CreateHistogram<double>(metricName, unit));
        histogram.Record(value, CreateTags(tags));
    }

    private static TagList CreateTags(IReadOnlyDictionary<string, object?>? tags)
    {
        var tagList = new TagList();
        if (tags is not null)
        {
            foreach (var tag in tags)
                tagList.Add(tag.Key, tag.Value);
        }

        return tagList;
    }
}

internal sealed class LoggerStructuredLogger(ILoggerFactory loggerFactory) : IStructuredLogger
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("His.Hope.Observability");

    public void Log(
        LogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var state = properties is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(properties);
        state["Message"] = message;

        _logger.Log(
            level,
            new EventId(0, "StructuredLog"),
            state,
            exception,
            static (values, _) => values["Message"]?.ToString() ?? string.Empty);
    }
}

internal sealed class NullAuditSink : IAuditSink
{
    public ValueTask WriteAsync(AuditRecord auditRecord, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
