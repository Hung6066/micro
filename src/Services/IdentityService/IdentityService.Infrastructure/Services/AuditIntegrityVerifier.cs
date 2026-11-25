using System.Diagnostics.Metrics;
using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed record AuditIntegrityStatus(
    bool IsValid,
    long? CheckedThroughSequence,
    int EntriesChecked,
    string? FailureReason,
    long? FailedSequence,
    string? HeadHash,
    bool FullScan,
    DateTime CheckedAt);

/// <summary>
/// Verifies the audit hash chain on a schedule. Nothing used to check the chain,
/// so tampering would never have been noticed.
/// <para>
/// Each run checks the entries written since the last checkpoint and that the
/// checkpoint entry itself is unchanged (which catches rewritten history and
/// removed newest entries). A full scan from the first entry runs periodically
/// to catch an old entry edited in place. The checkpoint lives in Redis, outside
/// the audited database, and the verified head hash is logged on every run so
/// the log pipeline holds an independent anchor.
/// </para>
/// </summary>
public sealed class AuditIntegrityVerifier(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<AuditIntegrityVerifier> logger) : BackgroundService
{
    public const string CheckpointKey = "hishop:identity:audit-integrity:checkpoint";
    public const string StatusKey = "hishop:identity:audit-integrity:status";
    private const int BatchSize = 5000;

    private static readonly Meter Meter = new("His.Hope.Identity.Audit");
    private static readonly Counter<long> Failures =
        Meter.CreateCounter<long>("audit_integrity_failures_total", description: "Audit hash-chain verification failures");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Audit:IntegrityVerification:Enabled", true))
            return;

        var interval = TimeSpan.FromMinutes(Math.Clamp(
            configuration.GetValue("Audit:IntegrityVerification:IntervalMinutes", 30), 1, 1440));
        var fullScanEvery = TimeSpan.FromHours(Math.Clamp(
            configuration.GetValue("Audit:IntegrityVerification:FullScanIntervalHours", 24), 1, 24 * 30));
        var lastFullScan = DateTime.MinValue;

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var fullScan = DateTime.UtcNow - lastFullScan >= fullScanEvery;
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                    var status = await VerifyAsync(db, redis.GetDatabase(), fullScan, stoppingToken);
                    if (fullScan)
                        lastFullScan = DateTime.UtcNow;

                    if (status.IsValid)
                    {
                        logger.LogInformation(
                            "Audit chain verified through sequence {Sequence} ({Entries} entries, full scan: {FullScan}). Head hash {HeadHash}.",
                            status.CheckedThroughSequence, status.EntriesChecked, status.FullScan, status.HeadHash);
                    }
                    else
                    {
                        Failures.Add(1);
                        logger.LogCritical(
                            "AUDIT INTEGRITY FAILURE: {Reason} at sequence {Sequence}. The audit trail may have been altered.",
                            status.FailureReason, status.FailedSequence);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Audit integrity verification could not run.");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static async Task<AuditIntegrityStatus> VerifyAsync(
        IdentityDbContext db, IDatabase redis, bool fullScan, CancellationToken ct)
    {
        long? checkpointSequence = null;
        string? checkpointHash = null;
        var stored = await redis.StringGetAsync(CheckpointKey);
        if (stored.HasValue)
        {
            var parts = stored.ToString().Split('|', 2);
            if (parts.Length == 2 && long.TryParse(parts[0], out var parsed))
            {
                checkpointSequence = parsed;
                checkpointHash = parts[1];
            }
        }

        var chain = db.AuditLogs.AsNoTracking().IgnoreQueryFilters().Where(entry => entry.IntegritySequence != null);
        var status = await VerifyCoreAsync(chain, checkpointSequence, checkpointHash, fullScan, ct);

        await redis.StringSetAsync(StatusKey, JsonSerializer.Serialize(status));
        if (status.IsValid && status.CheckedThroughSequence is { } sequence && status.HeadHash is { } head)
            await redis.StringSetAsync(CheckpointKey, $"{sequence}|{head}");
        return status;
    }

    private static async Task<AuditIntegrityStatus> VerifyCoreAsync(
        IQueryable<AuditLog> chain, long? checkpointSequence, string? checkpointHash, bool fullScan, CancellationToken ct)
    {
        AuditIntegrityStatus Invalid(string reason, long? sequence, int entriesChecked) =>
            new(false, null, entriesChecked, reason, sequence, null, fullScan, DateTime.UtcNow);

        var fromGenesis = fullScan || checkpointSequence is null;
        long position = 0;
        string? previous = null;
        var trustFirstLink = true;

        if (checkpointSequence is { } checkpoint)
        {
            // The checkpoint entry must still exist with the hash that was verified.
            var current = await chain.Where(entry => entry.IntegritySequence == checkpoint)
                .Select(entry => entry.IntegrityHash).SingleOrDefaultAsync(ct);
            if (current is null)
                return Invalid("entries-removed-after-checkpoint", checkpoint, 0);
            if (!string.Equals(current, checkpointHash, StringComparison.Ordinal))
                return Invalid("checkpoint-entry-changed", checkpoint, 0);
            if (!fromGenesis)
            {
                position = checkpoint;
                previous = checkpointHash;
                trustFirstLink = false;
            }
        }

        var entriesChecked = 0;
        string? head = fromGenesis ? null : checkpointHash;
        long? headSequence = fromGenesis ? null : checkpointSequence;
        while (true)
        {
            var from = position;
            var batch = await chain.Where(entry => entry.IntegritySequence > from)
                .OrderBy(entry => entry.IntegritySequence)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0)
                break;

            foreach (var entry in batch)
            {
                var sequence = entry.IntegritySequence!.Value;
                // The oldest retained entry is the trust anchor: its link to
                // whatever preceded it cannot be checked.
                if (trustFirstLink)
                {
                    previous = entry.PreviousIntegrityHash;
                    trustFirstLink = false;
                }
                if (!string.Equals(entry.PreviousIntegrityHash, previous, StringComparison.Ordinal))
                    return Invalid("previous-hash-mismatch", sequence, entriesChecked);
                if (string.IsNullOrWhiteSpace(entry.IntegrityHash))
                    return Invalid("missing-hash", sequence, entriesChecked);
                if (!string.Equals(AuditLogIntegrity.ComputeHash(entry, previous), entry.IntegrityHash, StringComparison.Ordinal))
                    return Invalid("hash-mismatch", sequence, entriesChecked);

                previous = entry.IntegrityHash;
                head = entry.IntegrityHash;
                headSequence = sequence;
                position = sequence;
                entriesChecked++;
            }

            if (batch.Count < BatchSize)
                break;
        }

        return new AuditIntegrityStatus(true, headSequence, entriesChecked, null, null, head, fullScan, DateTime.UtcNow);
    }
}
