using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Requirements;

/// <summary>
/// Accepts either an explicit OAuth resource scope or a legacy permission claim.
/// Used while commerce resource scopes roll out to existing OIDC clients.
/// </summary>
public sealed class CommerceScopeOrPermissionRequirement : IAuthorizationRequirement
{
    public CommerceScopeOrPermissionRequirement(string scope, string permissionCode)
    {
        Scope = scope.Trim();
        PermissionCode = permissionCode.Trim();
    }

    public string Scope { get; }
    public string PermissionCode { get; }
}
