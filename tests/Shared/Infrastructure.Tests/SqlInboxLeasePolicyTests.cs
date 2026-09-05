using FluentAssertions;
using His.Hope.Messaging.Sql;

namespace Infrastructure.Tests;

public sealed class SqlInboxLeasePolicyTests
{
    [Fact]
    public void Reclaims_only_incomplete_leases_older_than_the_processing_timeout()
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        SqlInboxLeasePolicy.ShouldReclaim(
                completedAt: null,
                processingAt: now.AddMinutes(-11),
                now)
            .Should().BeTrue();

        SqlInboxLeasePolicy.ShouldReclaim(
                completedAt: null,
                processingAt: now.AddMinutes(-9),
                now)
            .Should().BeFalse();

        SqlInboxLeasePolicy.ShouldReclaim(
                completedAt: now.AddMinutes(-11),
                processingAt: now.AddMinutes(-20),
                now)
            .Should().BeFalse();
    }
}
