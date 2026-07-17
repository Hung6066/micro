using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace His.Hope.Infrastructure.DataLifecycle;

/// <summary>
/// EF Core SaveChangesInterceptor that implements soft-delete.
/// When an entity implementing <see cref="ISoftDeletable"/> is deleted,
/// this interceptor converts the deletion into a modification: it sets
/// <see cref="ISoftDeletable.DeletedAt"/> and <see cref="ISoftDeletable.DeletedBy"/>
/// instead of removing the row from the database.
///
/// Register via <c>optionsBuilder.AddInterceptors(new SoftDeleteInterceptor())</c>
/// or through DI with <c>services.AddSingleton&lt;SoftDeleteInterceptor&gt;()</c>.
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Optional: provide a delegate that resolves the current user ID
    /// at the time of deletion. If omitted, DeletedBy will be null.
    /// </summary>
    private readonly Func<Guid?>? _currentUserIdProvider;

    public SoftDeleteInterceptor(Func<Guid?>? currentUserIdProvider = null)
    {
        _currentUserIdProvider = currentUserIdProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplySoftDelete(DbContext? context)
    {
        if (context is null) return;

        var deletedEntries = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Deleted && e.Entity is ISoftDeletable)
            .ToList();

        if (deletedEntries.Count == 0) return;

        var currentUserId = _currentUserIdProvider?.Invoke();
        var utcNow = DateTime.UtcNow;

        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;

            if (entry.Entity is ISoftDeletable softDeletable)
            {
                softDeletable.DeletedAt = utcNow;
                softDeletable.DeletedBy = currentUserId;
            }

            // Mark only the soft-delete properties as modified; skip other
            // properties to avoid unnecessarily updating large columns.
            MarkSoftDeletePropertiesAsModified(entry);
        }
    }

    private static void MarkSoftDeletePropertiesAsModified(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.Name is nameof(ISoftDeletable.DeletedAt)
                or nameof(ISoftDeletable.DeletedBy))
            {
                property.IsModified = true;
            }
            else
            {
                property.IsModified = false;
            }
        }
    }
}
