using System.Security.Claims;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Api.Services;

internal static class HumanSessionAuthClaims
{
    public static async Task<(IReadOnlyList<string> Permissions, Claim[] AdditionalClaims)> ResolveAsync(
        UserManager<User> userManager,
        IIdentityService identityService,
        User user,
        CancellationToken cancellationToken = default)
    {
        var permissions = await identityService.GetEffectivePermissionsAsync(user.Id, cancellationToken);
        var userClaims = await userManager.GetClaimsAsync(user);
        var tenantMemberships = userClaims
            .Where(claim => claim.Type == "tenant_membership")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var additionalClaims = new List<Claim>();
        foreach (var membership in tenantMemberships)
            additionalClaims.Add(new Claim("tenant_membership", membership));

        if (tenantMemberships.Length > 0)
        {
            additionalClaims.Add(new Claim("tenant_id", tenantMemberships[0]));
            if (tenantMemberships.Length > 1)
                additionalClaims.Add(new Claim("tenant_memberships", string.Join(",", tenantMemberships)));
        }

        return (permissions, additionalClaims.ToArray());
    }
}
