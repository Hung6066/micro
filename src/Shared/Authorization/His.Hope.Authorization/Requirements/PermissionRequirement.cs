using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Requirements;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = string.IsNullOrWhiteSpace(permissionCode)
            ? throw new ArgumentException("Permission code is required.", nameof(permissionCode))
            : permissionCode;
    }

    public string PermissionCode { get; }
}
