using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.OpenShip;

/// <summary>Console and infrastructure access derived from IAM state for one user.</summary>
/// <param name="Groups">
/// Active IAM group keys with a configured prefix, plus the dynamic break-glass
/// groups of every approved, unexpired elevation grant.
/// </param>
/// <param name="WriteUntil">End of the latest active, approved console-write break-glass window, if any.</param>
/// <param name="ElevatedUntil">
/// Earliest expiry among the grants behind the dynamic break-glass groups, if
/// any. A token carrying those groups must not live past this instant.
/// </param>
public sealed record OpenShipConsoleAccess(
    IReadOnlyList<string> Groups,
    DateTime? WriteUntil,
    DateTime? ElevatedUntil = null);

public interface IOpenShipConsoleAccessResolver
{
    Task<OpenShipConsoleAccess> ResolveAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves OpenShip console roles (IAM groups), time-boxed console write and
/// infrastructure elevation (approved break-glass) for a user. Everything is
/// evaluated server-side on every call so revoking a membership or a
/// break-glass request takes effect on the next token issuance or lookup.
/// </summary>
public sealed class OpenShipConsoleAccessResolver : IOpenShipConsoleAccessResolver
{
    private readonly IApplicationDbContext _db;
    private readonly OpenShipConsoleOptions _options;
    private readonly TimeProvider _timeProvider;

    public OpenShipConsoleAccessResolver(
        IApplicationDbContext db,
        IOptions<OpenShipConsoleOptions> options,
        TimeProvider timeProvider)
    {
        _db = db;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<OpenShipConsoleAccess> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var groupKeys = await _db.IamGroupMemberships.AsNoTracking()
            .TagWith("Identity.OpenShip.ResolveConsoleGroups")
            .Where(membership => membership.UserId == userId)
            .Join(_db.IamGroups.AsNoTracking().Where(group => group.IsActive),
                membership => membership.GroupId,
                group => group.Id,
                (_, group) => group.Key)
            .ToListAsync(cancellationToken);

        // Prefix filtering is done in memory with an ordinal comparison so the
        // result does not depend on database collation or LIKE escaping.
        var groups = groupKeys
            .Where(_options.IsIssuedGroup)
            .ToHashSet(StringComparer.Ordinal);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var writePermission = _options.WritePermission;
        var elevationPermissions = _options.BreakGlassGroups.Select(item => item.Permission).ToArray();
        var grants = await _db.BreakGlassRequests.AsNoTracking()
            .TagWith("Identity.OpenShip.ResolveBreakGlass")
            .Where(item => item.SubjectUserId == userId &&
                (item.PermissionCode == writePermission || elevationPermissions.Contains(item.PermissionCode)) &&
                item.Status == "approved" &&
                item.RevokedAt == null &&
                item.ExpiresAt > now)
            .Select(item => new { item.PermissionCode, item.ExpiresAt })
            .ToListAsync(cancellationToken);

        DateTime? writeUntil = grants
            .Where(grant => grant.PermissionCode == writePermission)
            .Select(grant => (DateTime?)grant.ExpiresAt)
            .Max();

        DateTime? elevatedUntil = null;
        foreach (var mapping in _options.BreakGlassGroups)
        {
            // The token must end with the first grant that lapses, but a group
            // stays as long as any of its own grants is still active.
            var latest = grants
                .Where(grant => string.Equals(grant.PermissionCode, mapping.Permission, StringComparison.Ordinal))
                .Select(grant => (DateTime?)grant.ExpiresAt)
                .Max();
            if (latest is null)
                continue;
            groups.Add(mapping.Group);
            elevatedUntil = elevatedUntil is null || latest < elevatedUntil ? latest : elevatedUntil;
        }

        return new OpenShipConsoleAccess(
            groups.Order(StringComparer.Ordinal).ToArray(),
            writeUntil,
            elevatedUntil);
    }
}
