using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityRetentionOptionsTests
{
    [Fact]
    public void Defaults_match_the_documented_retention_policy()
    {
        var options = new IdentityRetentionOptions();

        options.CompletedOutboxDays.Should().Be(7);
        options.TelemetryDays.Should().Be(30);
        options.SecurityEventDays.Should().Be(90);
        options.DevicePostureDays.Should().Be(7);
        options.ProcessedPushDays.Should().Be(7);
        options.BatchSize.Should().Be(500);
        options.MaxRowsPerRun.Should().Be(10_000);
        options.IntervalMinutes.Should().Be(30);
        options.LockTtlMinutes.Should().Be(10);
    }

    [Fact]
    public void Values_can_be_overridden_by_configuration_binding()
    {
        var options = new IdentityRetentionOptions
        {
            CompletedOutboxDays = 14,
            TelemetryDays = 45,
            SecurityEventDays = 180,
            DevicePostureDays = 14,
            ProcessedPushDays = 14,
            BatchSize = 100,
            MaxRowsPerRun = 2_000,
            IntervalMinutes = 5,
            LockTtlMinutes = 3
        };

        options.CompletedOutboxDays.Should().Be(14);
        options.TelemetryDays.Should().Be(45);
        options.SecurityEventDays.Should().Be(180);
        options.DevicePostureDays.Should().Be(14);
        options.ProcessedPushDays.Should().Be(14);
        options.BatchSize.Should().Be(100);
        options.MaxRowsPerRun.Should().Be(2_000);
        options.IntervalMinutes.Should().Be(5);
        options.LockTtlMinutes.Should().Be(3);
    }
}
