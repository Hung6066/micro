using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AuditLogIntegrityPostgresTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task ConcurrentWrites_AllocateDistinctSequencesAndLinkInCommitOrder()
    {
        var first = NewEntry("concurrent-a");
        var second = NewEntry("concurrent-b");
        using var ready = new Barrier(2);

        await Task.WhenAll(
            Task.Run(() => SaveAsync(first, ready)),
            Task.Run(() => SaveAsync(second, ready)));

        await using var verification = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(fixture.PostgresConnectionString)
            .Options);
        var entries = await verification.AuditLogs.AsNoTracking()
            .Where(entry => entry.Id == first.Id || entry.Id == second.Id)
            .OrderBy(entry => entry.IntegritySequence)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.NotNull(entries[0].IntegritySequence);
        Assert.Equal(entries[0].IntegritySequence + 1, entries[1].IntegritySequence);
        Assert.Equal(entries[0].IntegrityHash, entries[1].PreviousIntegrityHash);
        Assert.Equal(
            AuditLogIntegrity.ComputeHash(entries[0], entries[0].PreviousIntegrityHash),
            entries[0].IntegrityHash);
        Assert.Equal(
            AuditLogIntegrity.ComputeHash(entries[1], entries[1].PreviousIntegrityHash),
            entries[1].IntegrityHash);
    }

    private async Task SaveAsync(AuditLog entry, Barrier ready)
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(fixture.PostgresConnectionString)
            .Options);
        db.AuditLogs.Add(entry);
        ready.SignalAndWait();
        await db.SaveChangesAsync();
    }

    private static AuditLog NewEntry(string userId) => new()
    {
        UserId = userId,
        Action = "READ",
        ResourceType = "AuditChainTest",
        Timestamp = DateTime.UtcNow
    };
}
