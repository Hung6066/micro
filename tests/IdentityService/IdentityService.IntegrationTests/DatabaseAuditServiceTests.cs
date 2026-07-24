using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class DatabaseAuditServiceTests
{
    [Fact]
    public async Task LogPhiAccessAsync_WhenPersistenceIsMisconfigured_PropagatesFailure()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var service = new DatabaseAuditService(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        var entry = new PhiAuditEntry
        {
            UserId = "user-1",
            Action = "READ",
            ResourceType = "Patient",
            ResourceId = "patient-1",
            Timestamp = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogPhiAccessAsync(entry));
    }
}
