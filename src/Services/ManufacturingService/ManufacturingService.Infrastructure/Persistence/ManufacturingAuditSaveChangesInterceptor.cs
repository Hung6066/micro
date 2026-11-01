using His.Hope.Infrastructure.DataLifecycle;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class ManufacturingAuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AppendAuditEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEvents(DbContext? context)
    {
        if (context is not ManufacturingDbContext db)
            return;

        var pendingChanges = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is not ManufacturingAuditEventEntity)
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Where(entry => entry.Metadata.FindProperty("TenantKey") is not null)
            .ToArray();

        foreach (var entry in pendingChanges)
        {
            var tenantKey = entry.Property("TenantKey").CurrentValue as string;
            if (string.IsNullOrWhiteSpace(tenantKey))
                continue;

            var id = entry.Metadata.FindPrimaryKey()?.Properties.Count == 1
                ? entry.Property(entry.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue
                : null;
            if (id is not Guid entityId)
                continue;

            var isDeleted = entry.Metadata.FindProperty("IsDeleted") is not null
                && entry.Property("IsDeleted").CurrentValue as bool? == true;
            var action = isDeleted ? "Deleted" : entry.State == EntityState.Added ? ManufacturingStatusCodes.Created : "Updated";
            var actor = ResolveActor(entry);
            var changedProperties = string.Join(",", entry.Properties
                .Where(property => entry.State == EntityState.Added || property.IsModified)
                .Select(property => property.Metadata.Name)
                .Where(name => !string.Equals(name, "UpdatedAt", StringComparison.Ordinal))
                .Take(20));

            db.AuditEvents.Add(new ManufacturingAuditEventEntity
            {
                Id = Guid.NewGuid(),
                TenantKey = tenantKey.Trim(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                Action = action,
                Actor = actor,
                Details = string.IsNullOrWhiteSpace(changedProperties) ? "state_changed" : changedProperties,
                OccurredAt = DateTimeOffset.UtcNow
            });
        }
    }

    private string ResolveActor(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value
            ?? entry.Property("UpdatedBy").CurrentValue as string
            ?? entry.Property("CreatedBy").CurrentValue as string
            ?? "system";
    }
}
