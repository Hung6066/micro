using System.Threading.Channels;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class DatabaseAuditService : IAuditService
{
    private readonly ChannelWriter<PhiAuditEntry> _writer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseAuditService> _logger;

    public DatabaseAuditService(
        Channel<PhiAuditEntry> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseAuditService> logger)
    {
        _writer = channel.Writer;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void LogPhiAccess(PhiAuditEntry entry)
    {
        if (!_writer.TryWrite(entry))
        {
            _logger.LogWarning(
                "Audit channel is closed; audit event could not be queued for {ResourceType}/{ResourceId}",
                entry.ResourceType, entry.ResourceId);
        }
    }

    public async Task LogPhiAccessAsync(PhiAuditEntry entry, CancellationToken ct = default)
    {
        await WriteAuditLogAsync(entry, ct);
    }

    private async Task WriteAuditLogAsync(PhiAuditEntry entry, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = entry.UserId,
            UserName = null,
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            Details = $"{entry.HttpMethod} {entry.Path}",
            IpAddress = entry.ClientIp,
            UserAgent = entry.UserAgent,
            Timestamp = entry.Timestamp
        });

        await db.SaveChangesAsync(ct);
    }
}
