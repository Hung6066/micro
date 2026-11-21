using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.OpenShip;

/// <summary>Console access derived from IAM state for one user.</summary>
/// <param name="Groups">Active IAM group keys that start with the configured console prefix.</param>
/// <param name="WriteUntil">End of the latest active, approved console-write break-glass window, if any.</param>
public sealed record OpenShipConsoleAccess(IReadOnlyList<string> Groups, DateTime? WriteUntil);

public interface IOpenShipConsoleAccessResolver
{
    Task<OpenShipConsoleAccess> ResolveAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves OpenShip console roles (IAM groups) and time-boxed write
/// (approved break-glass) for a user. Both are evaluated server-side on every
/// call so revoking a membership or a break-glass request takes effect on the
/// next token issuance or console-access lookup.
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
        var prefix = _options.GroupPrefix;
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
            .Where(key => !string.IsNullOrWhiteSpace(key) && key.StartsWith(prefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var permission = _options.WritePermission;
        var writeUntil = await _db.BreakGlassRequests.AsNoTracking()
            .TagWith("Identity.OpenShip.ResolveConsoleWrite")
            .Where(item => item.SubjectUserId == userId &&
                item.PermissionCode == permission &&
                item.Status == "approved" &&
                item.RevokedAt == null &&
                item.ExpiresAt > now)
            .Select(item => (DateTime?)item.ExpiresAt)
            .MaxAsync(cancellationToken);

        return new OpenShipConsoleAccess(groups, writeUntil);
    }
}
