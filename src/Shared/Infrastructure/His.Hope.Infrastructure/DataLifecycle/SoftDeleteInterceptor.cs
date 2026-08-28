using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace His.Hope.Infrastructure.DataLifecycle;

/// <summary>
/// EF Core SaveChangesInterceptor that implements the shared data lifecycle contract.
/// When a mapped entity marked by <see cref="HisHopeDataConventions.SoftDeleteAnnotation"/> is deleted,
/// this interceptor converts the deletion into a modification and sets
/// the shared lifecycle columns
/// instead of removing the row from the database.
///
/// Register through DI so the request actor can be resolved from IHttpContextAccessor.
/// Background jobs may use the delegate constructor.
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Optional: provide a delegate that resolves the current user ID
    /// at the time of deletion. If omitted, DeletedBy will be null.
    /// </summary>
    private readonly Func<Guid?>? _currentUserIdProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public SoftDeleteInterceptor(Func<Guid?> currentUserIdProvider)
    {
        _currentUserIdProvider = currentUserIdProvider;
    }

    public SoftDeleteInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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

        var currentUserId = ResolveCurrentUserId();
        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            var isSoftDelete = entry.Metadata.FindAnnotation(HisHopeDataConventions.SoftDeleteAnnotation)
                ?.Value as bool? == true;

            if (entry.State is EntityState.Deleted && isSoftDelete)
            {
                entry.State = EntityState.Modified;
                Set(entry, "IsDeleted", true);
                Set(entry, "DeletedAt", utcNow);
                Set(entry, "DeletedBy", currentUserId);
            }

            if (entry.State is EntityState.Added)
            {
                Set(entry, "CreatedAt", utcNow);
                Set(entry, "CreatedBy", currentUserId);
                Set(entry, "IsDeleted", false);
            }

            if (entry.State is EntityState.Modified)
            {
                Set(entry, "UpdatedAt", utcNow);
                Set(entry, "UpdatedBy", currentUserId);
            }
        }
    }

    private string? ResolveCurrentUserId()
    {
        var delegated = _currentUserIdProvider?.Invoke();
        if (delegated.HasValue)
            return delegated.Value.ToString();

        var principal = _httpContextAccessor?.HttpContext?.User;
        return principal?.FindFirst("sub")?.Value
            ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    private static void Set(EntityEntry entry, string propertyName, object? value)
    {
        var property = entry.Metadata.FindProperty(propertyName);
        if (property is not null)
        {
            // Shared lifecycle conventions are used by both DateTime and
            // DateTimeOffset aggregates. Normalize the interceptor value to
            // the mapped CLR type before assigning it; otherwise a
            // DateTimeOffset entity (for example TableView) fails during
            // SaveChanges with an invalid cast.
            var targetType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (value is DateTime dateTime && targetType == typeof(DateTimeOffset))
                value = new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
            else if (value is DateTimeOffset dateTimeOffset && targetType == typeof(DateTime))
                value = dateTimeOffset.UtcDateTime;

            entry.Property(propertyName).CurrentValue = value;
        }
    }
}
