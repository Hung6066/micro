using Microsoft.Extensions.Logging;
using NSubstitute;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class LogsAggregatorTests
{
    [Fact]
    public async Task QueryLogsAsync_FiltersByServiceAndLevel()
    {
        // Arrange
        var esService = Substitute.For<IElasticsearchQueryService>();
        var expectedLogs = new List<LogEntry>
        {
            new()
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Service = "identity-service",
                Message = "Test error message",
                TraceId = "trace-123"
            }
        };

        esService.QueryLogsAsync(
                service: Arg.Is<string>(s => s == "identity-service"),
                level: Arg.Is<string>(l => l == "Error"),
                from: Arg.Any<int?>(),
                size: Arg.Any<int>(),
                searchQuery: Arg.Any<string?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(expectedLogs);

        var logger = Substitute.For<ILogger<LogsAggregator>>();
        var aggregator = new LogsAggregator(esService, logger);

        // Act
        var results = await aggregator.QueryLogsAsync(
            service: "identity-service",
            level: "Error");

        // Assert
        Assert.Single(results);
        var log = results[0];
        Assert.Equal("identity-service", log.Service);
        Assert.Equal("Error", log.Level);
        Assert.Equal("Test error message", log.Message);
        Assert.Equal("trace-123", log.TraceId);
    }

    [Fact]
    public async Task QueryLogsAsync_PassesParametersThrough()
    {
        // Arrange
        var esService = Substitute.For<IElasticsearchQueryService>();
        var fromOffset = 50;

        esService.QueryLogsAsync(
                service: Arg.Is<string?>(s => s == "patient-service"),
                level: Arg.Is<string?>(l => l == "Warning"),
                from: Arg.Is<int?>(d => d == fromOffset),
                size: Arg.Is<int>(s => s == 50),
                searchQuery: Arg.Is<string?>(q => q == "timeout"),
                ct: Arg.Any<CancellationToken>())
            .Returns([]);

        var logger = Substitute.For<ILogger<LogsAggregator>>();
        var aggregator = new LogsAggregator(esService, logger);

        // Act
        var results = await aggregator.QueryLogsAsync(
            service: "patient-service",
            level: "Warning",
            from: fromOffset,
            size: 50,
            searchQuery: "timeout");

        // Assert
        Assert.Empty(results);
        // Verifies the ES service was called with exact parameters
        await esService.Received(1).QueryLogsAsync(
            "patient-service", "Warning", fromOffset, 50, "timeout", default);
    }

    [Fact]
    public async Task QueryLogsAsync_ReturnsEmptyOnEsFailure()
    {
        // Arrange
        var esService = Substitute.For<IElasticsearchQueryService>();
        esService.QueryLogsAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns<List<LogEntry>>(_ => throw new HttpRequestException("ES unavailable"));

        var logger = Substitute.For<ILogger<LogsAggregator>>();
        var aggregator = new LogsAggregator(esService, logger);

        // Act
        var results = await aggregator.QueryLogsAsync(service: "identity-service");

        // Assert
        Assert.Empty(results);
    }
}
