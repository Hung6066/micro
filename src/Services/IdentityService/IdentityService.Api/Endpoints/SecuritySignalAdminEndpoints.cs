using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Contracts.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>Administrative SSF/CAEP health and replay controls. Signing keys and SET payloads never leave the service.</summary>
public static class SecuritySignalAdminEndpoints
{
    public static void MapSecuritySignalAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(IdentityApiRoutes.AdminSecuritySignals)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSecuritySignalsManage);

        group.MapGet("/status", async (IdentityDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var subscriptions = configuration.GetSection("SecuritySignals:Subscriptions").GetChildren()
                .Select(section => new { Url = section["Url"], Audience = section["Audience"] })
                .Where(item => Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                .Select(item => new { host = new Uri(item.Url!).Host, item.Audience })
                .ToArray();
            var pending = await db.SecuritySignalOutbox.CountAsync(item => item.DispatchedAt == null, ct);
            var failed = await db.SecuritySignalOutbox.CountAsync(item => item.DispatchedAt == null && item.Attempts > 0 && item.LastError != null, ct);
            var lastDelivery = await db.SecuritySignalOutbox.Where(item => item.DispatchedAt != null).MaxAsync(item => (DateTime?)item.DispatchedAt, ct);
            return Results.Ok(new
            {
                enabled = configuration.GetValue("SSF_ENABLED", configuration.GetValue("SecuritySignals:Enabled", false)),
                subscriptionCount = subscriptions.Length,
                subscriptions,
                pending,
                failed,
                lastDelivery
            });
        });

        group.MapGet("/outbox", async (IdentityDbContext db, CancellationToken ct) =>
        {
            var entries = await db.SecuritySignalOutbox.AsNoTracking()
                .Where(item => item.DispatchedAt == null)
                .OrderByDescending(item => item.AvailableAt)
                .Take(100)
                .Select(item => new
                {
                    item.Id,
                    item.EventType,
                    item.CreatedAt,
                    item.AvailableAt,
                    item.Attempts,
                    item.LastError
                })
                .ToListAsync(ct);
            return Results.Ok(entries.Select(item => new
            {
                item.Id, item.EventType, item.CreatedAt, item.AvailableAt, item.Attempts,
                lastError = item.LastError == null ? null : item.LastError.Length > 240 ? item.LastError.Substring(0, 240) : item.LastError
            }));
        });

        group.MapPost("/outbox/{id:guid}/retry", async (Guid id, IdentityDbContext db, CancellationToken ct) =>
        {
            var entry = await db.SecuritySignalOutbox.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (entry is null) return Results.NotFound();
            entry.DispatchedAt = null;
            entry.AvailableAt = DateTime.UtcNow;
            entry.LastError = null;
            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/api/v1/admin/security-signals/outbox/{id}", new { entry.Id, status = "queued" });
        });
    }
}
