using System.Threading.Channels;
using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DatabaseAuditBackpressureTests
{
    [Fact]
    public async Task LogPhiAccess_when_queue_is_full_waits_instead_of_dropping_the_event()
    {
        var channel = Channel.CreateBounded<PhiAuditEntry>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        await channel.Writer.WriteAsync(CreateEntry("already-buffered"));
        var service = new DatabaseAuditService(
            channel,
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<DatabaseAuditService>.Instance);

        var pending = Task.Run(() => service.LogPhiAccess(CreateEntry("must-not-drop")));
        await Task.Delay(100);

        pending.IsCompleted.Should().BeFalse();
        (await channel.Reader.ReadAsync()).ResourceId.Should().Be("already-buffered");
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
        (await channel.Reader.ReadAsync()).ResourceId.Should().Be("must-not-drop");
    }

    private static PhiAuditEntry CreateEntry(string resourceId) => new()
    {
        UserId = "user-1",
        ResourceType = "Patient",
        ResourceId = resourceId,
        Action = "READ"
    };
}
