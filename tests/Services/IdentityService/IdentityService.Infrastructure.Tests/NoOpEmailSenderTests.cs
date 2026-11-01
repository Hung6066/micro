using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class NoOpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_logs_recipient_subject_and_body_and_completes()
    {
        var logger = new RecordingLogger<NoOpEmailSender>();
        var sender = new NoOpEmailSender(logger);

        var task = sender.SendAsync("user@example.test", "Verify account", "one-time code: 123456");

        task.IsCompletedSuccessfully.Should().BeTrue();
        await task;
        logger.Messages.Should().ContainSingle(message =>
            message.Contains("user@example.test", StringComparison.Ordinal)
            && message.Contains("Verify account", StringComparison.Ordinal)
            && message.Contains("one-time code: 123456", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_accepts_cancellation_token_without_external_side_effects()
    {
        var logger = new RecordingLogger<NoOpEmailSender>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await new NoOpEmailSender(logger).SendAsync("user@example.test", "Subject", "Body", cancellation.Token);

        logger.Messages.Should().ContainSingle();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
