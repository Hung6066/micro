using His.Hope.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Messaging.Sql;

public static class SqlMessagingExtensions
{
    public static IServiceCollection AddHisHopeSqlMessaging(this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        services.AddDbContext<SqlMessagingDbContext>(configure);
        services.AddScoped<IOutboxStore, SqlOutboxStore>();
        services.AddScoped<IInboxStore, SqlInboxStore>();
        services.AddScoped<IIdempotencyStore, SqlIdempotencyStore>();
        return services;
    }
}

public sealed class SqlMessagingDbContext(DbContextOptions<SqlMessagingDbContext> options) : DbContext(options)
{
    public DbSet<SqlOutboxMessage> OutboxMessages => Set<SqlOutboxMessage>();
    public DbSet<SqlInboxMessage> InboxMessages => Set<SqlInboxMessage>();
    public DbSet<SqlIdempotencyRecord> IdempotencyRecords => Set<SqlIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlOutboxMessage>().ToTable("his_hope_outbox").HasKey(x => x.Id);
        modelBuilder.Entity<SqlInboxMessage>().ToTable("his_hope_inbox").HasKey(x => new { x.EventId, x.Consumer });
        modelBuilder.Entity<SqlIdempotencyRecord>().ToTable("his_hope_idempotency").HasKey(x => x.Key);
        modelBuilder.Entity<SqlOutboxMessage>().Property(x => x.EventJson).IsRequired();
        modelBuilder.Entity<SqlInboxMessage>().Property(x => x.Consumer).HasMaxLength(200);
        modelBuilder.Entity<SqlIdempotencyRecord>().Property(x => x.Key).HasMaxLength(255);
    }
}

public sealed class SqlOutboxMessage
{
    public Guid Id { get; set; }
    public string EventJson { get; set; } = "{}";
    public DateTimeOffset AvailableAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class SqlInboxMessage
{
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class SqlIdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? Response { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class SqlOutboxStore(SqlMessagingDbContext db) : IOutboxStore
{
    public async ValueTask EnqueueAsync(EventEnvelope @event, CancellationToken cancellationToken = default)
    {
        EventDeliveryPolicy.Default.Validate(@event);
        db.OutboxMessages.Add(new SqlOutboxMessage { Id = @event.Id, EventJson = System.Text.Json.JsonSerializer.Serialize(@event), AvailableAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<OutboxMessage>> ReadPendingAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var rows = await db.OutboxMessages
            .Where(x => x.PublishedAt == null && x.AvailableAt <= DateTimeOffset.UtcNow)
            .OrderBy(x => x.AvailableAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new OutboxMessage(
            x.Id,
            System.Text.Json.JsonSerializer.Deserialize<EventEnvelope>(x.EventJson)!,
            x.AvailableAt,
            x.AttemptCount,
            x.PublishedAt,
            x.LastError)).ToList();
    }

    public async ValueTask MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await db.OutboxMessages.FindAsync([messageId], cancellationToken) ?? throw new KeyNotFoundException($"Outbox message {messageId} not found.");
        message.PublishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask MarkFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default)
    {
        var message = await db.OutboxMessages.FindAsync([messageId], cancellationToken) ?? throw new KeyNotFoundException($"Outbox message {messageId} not found.");
        message.AttemptCount++;
        message.LastError = error;
        message.AvailableAt = nextAttemptAt;
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class SqlInboxStore(SqlMessagingDbContext db) : IInboxStore
{
    public async ValueTask<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        if (await db.InboxMessages.AnyAsync(x => x.EventId == eventId && x.Consumer == consumer, cancellationToken)) return false;
        db.InboxMessages.Add(new SqlInboxMessage { EventId = eventId, Consumer = consumer });
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException) { return false; }
    }

    public async ValueTask MarkCompletedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        var message = await db.InboxMessages.FindAsync([eventId, consumer], cancellationToken) ?? throw new KeyNotFoundException($"Inbox event {eventId} not found.");
        message.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask ReleaseAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        var message = await db.InboxMessages.FindAsync([eventId, consumer], cancellationToken);
        if (message is null) return;
        db.InboxMessages.Remove(message);
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class SqlIdempotencyStore(SqlMessagingDbContext db) : IIdempotencyStore
{
    public async ValueTask<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await db.IdempotencyRecords.FindAsync([key], cancellationToken);
        return record is null ? null : new IdempotencyRecord(record.Key, record.RequestFingerprint, record.StatusCode, record.Response, record.CompletedAt);
    }

    public async ValueTask<bool> TryBeginAsync(string key, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        db.IdempotencyRecords.Add(new SqlIdempotencyRecord { Key = key, RequestFingerprint = requestFingerprint });
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException) { return false; }
    }

    public async ValueTask CompleteAsync(string key, int statusCode, string response, CancellationToken cancellationToken = default)
    {
        var record = await db.IdempotencyRecords.FindAsync([key], cancellationToken) ?? throw new KeyNotFoundException($"Idempotency key {key} not found.");
        record.StatusCode = statusCode;
        record.Response = response;
        record.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
