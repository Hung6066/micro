using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// PHI audit service that writes audit events to the AuditLogs table in the database.
/// This complements the Serilog-based audit logging in the shared AuditService.
///
/// HIPAA Context (164.312(b)):
///   Provides a queryable, persistent audit trail for compliance reporting.
///   Records who accessed what PHI, when, and from where.
///
/// Thread Safety:
///   Uses IServiceScopeFactory to create scoped DbContext instances,
///   making this service safe for singleton registration used by the middleware.
/// </summary>
public class DatabaseAuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseAuditService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void LogPhiAccess(PhiAuditEntry entry)
    {
        // Synchronous call - write to DB in a background task
        // to avoid blocking the request pipeline
        Task.Run(async () =>
        {
            try
            {
                await WriteAuditLogAsync(entry);
            }
            catch
            {
                // SECURITY: Audit logging failures must never crash the application.
                // The Serilog audit service in the base infrastructure still captures the event.
                // Swallowing the exception ensures PHI access is never blocked by DB issues.
            }
        });
    }

    public async Task LogPhiAccessAsync(PhiAuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            await WriteAuditLogAsync(entry);
        }
        catch
        {
            // Swallow exceptions - Serilog audit is the primary audit channel
        }
    }

    private async Task WriteAuditLogAsync(PhiAuditEntry entry)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = entry.UserId,
            UserName = null, // Could enrich from the user service if needed
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            Details = $"{entry.HttpMethod} {entry.Path}",
            IpAddress = entry.ClientIp,
            UserAgent = entry.UserAgent,
            Timestamp = entry.Timestamp
        });

        await db.SaveChangesAsync();
    }
}
