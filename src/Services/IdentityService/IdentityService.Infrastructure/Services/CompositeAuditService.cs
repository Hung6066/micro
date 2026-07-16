using His.Hope.Infrastructure.Audit;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Composite audit service that writes PHI audit events to BOTH
/// the Serilog sink (via the shared AuditService) AND the database
/// (via DatabaseAuditService). This ensures audit events are never lost
/// even if one channel fails.
///
/// This is registered as the single IAuditService in the IdentityService,
/// replacing the default AuditService registered by AddPhiAudit().
/// It delegates to both underlying services.
/// </summary>
public class CompositeAuditService : IAuditService
{
    private readonly IAuditService _serilogAudit;
    private readonly DatabaseAuditService _databaseAudit;

    public CompositeAuditService(IAuditService serilogAudit, DatabaseAuditService databaseAudit)
    {
        _serilogAudit = serilogAudit;
        _databaseAudit = databaseAudit;
    }

    public void LogPhiAccess(PhiAuditEntry entry)
    {
        // Always log to Serilog (fast, non-blocking)
        _serilogAudit.LogPhiAccess(entry);

        // Fire-and-forget database write (never blocks the request pipeline)
        _ = Task.Run(async () =>
        {
            try
            {
                await _databaseAudit.LogPhiAccessAsync(entry);
            }
            catch
            {
                // Silently handle - Serilog already captured the event
            }
        });
    }

    public async Task LogPhiAccessAsync(PhiAuditEntry entry, CancellationToken ct = default)
    {
        // Log to Serilog synchronously
        _serilogAudit.LogPhiAccess(entry);

        // Log to database asynchronously
        await _databaseAudit.LogPhiAccessAsync(entry, ct);
    }
}
