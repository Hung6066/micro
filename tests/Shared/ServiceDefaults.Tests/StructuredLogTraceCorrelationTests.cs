using System.Diagnostics;
using His.Hope.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace His.Hope.ServiceDefaults.Tests;

public sealed class StructuredLogTraceCorrelationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StructuredLogs_IncludeOnlyAvailableAuthoritativeActivityIds(bool withActivity)
    {
        var logger = new CaptureLogger();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logger));
        services.AddObservability();
        using var provider = services.BuildServiceProvider();
        using var activity = withActivity ? new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start() : null;

        provider.GetRequiredService<IStructuredLogger>().Log(LogLevel.Information, "Span log");

        Assert.NotNull(logger.State);
        Assert.Equal("Span log", logger.State["Message"]);
        if (activity is not null)
        {
            Assert.Equal(activity.TraceId.ToString(), logger.State["TraceId"]);
            Assert.Equal(activity.SpanId.ToString(), logger.State["SpanId"]);
        }
        else
        {
            Assert.DoesNotContain("TraceId", logger.State.Keys);
            Assert.DoesNotContain("SpanId", logger.State.Keys);
        }
    }

    private sealed class CaptureLogger : ILoggerProvider, ILogger
    {
        public IReadOnlyDictionary<string, object?>? State { get; private set; }
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Dispose() { }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            State = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
