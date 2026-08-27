using Microsoft.EntityFrameworkCore;

public sealed class ManufacturingMobileOperationReplayEntity
{
    public Guid Id { get; set; }
    public required string TenantKey { get; set; }
    public required string SubjectId { get; set; }
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required string OperationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingMobileOperationReplayStore(IDbContextFactory<ManufacturingDbContext> dbFactory)
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<bool> TryReserveAsync(string tenantKey, string subjectId, string method, string path, string operationId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Keep the replay table bounded without requiring a separate scheduler.
        // Seven days covers mobile retry/offline windows while preventing
        // unbounded growth in long-running tenants.
        var retentionCutoff = DateTimeOffset.UtcNow.Subtract(Retention);
        await db.MobileOperationReplays
            .Where(x => x.CreatedAt < retentionCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        db.MobileOperationReplays.Add(new ManufacturingMobileOperationReplayEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, SubjectId = subjectId, Method = method,
            Path = path, OperationId = operationId, CreatedAt = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException) { return false; }
    }

    public async Task ReleaseAsync(string tenantKey, string subjectId, string method, string path, string operationId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.MobileOperationReplays
            .Where(x => x.TenantKey == tenantKey && x.SubjectId == subjectId && x.Method == method && x.Path == path && x.OperationId == operationId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
