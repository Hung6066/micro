using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AuditIntegrityVerifierTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Chain_written_by_the_service_verifies_and_an_edited_entry_is_detected()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

        var entry = new AuditLog
        {
            UserId = "integrity-test",
            Action = "INTEGRITY_TEST",
            ResourceType = "Test",
            ResourceId = Guid.NewGuid().ToString("N")
        };
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync();
        await redis.KeyDeleteAsync(AuditIntegrityVerifier.CheckpointKey);

        var clean = await AuditIntegrityVerifier.VerifyAsync(db, redis, fullScan: true, CancellationToken.None);
        Assert.True(clean.IsValid, $"{clean.FailureReason} at {clean.FailedSequence}");
        Assert.True(clean.EntriesChecked > 0);
        Assert.NotNull(clean.HeadHash);

        // An incremental run from the checkpoint is clean too.
        var incremental = await AuditIntegrityVerifier.VerifyAsync(db, redis, fullScan: false, CancellationToken.None);
        Assert.True(incremental.IsValid, incremental.FailureReason);

        // Edit one stored entry in place, bypassing the append-only change tracker rule.
        var sequence = await db.AuditLogs.AsNoTracking().Where(item => item.Id == entry.Id)
            .Select(item => item.IntegritySequence).SingleAsync();
        await db.AuditLogs.Where(item => item.Id == entry.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Action, "TAMPERED"));
        try
        {
            var tampered = await AuditIntegrityVerifier.VerifyAsync(db, redis, fullScan: true, CancellationToken.None);
            Assert.False(tampered.IsValid);
            Assert.Equal("hash-mismatch", tampered.FailureReason);
            Assert.Equal(sequence, tampered.FailedSequence);
        }
        finally
        {
            await db.AuditLogs.Where(item => item.Id == entry.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Action, "INTEGRITY_TEST"));
            await redis.KeyDeleteAsync(AuditIntegrityVerifier.CheckpointKey);
        }
    }
}
