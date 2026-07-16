using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Infrastructure.Security.Authorization.Requirements;

/// <summary>
/// Authorization requirement that checks whether the current user
/// possesses a specific permission code in their JWT claims.
/// The permission claim format is: "permissions" : ["patients.view", "patients.create", ...]
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The permission code required (e.g., "patients.view").
    /// </summary>
    public string PermissionCode { get; }

    /// <summary>
    /// Creates a requirement for the specified permission code.
    /// </summary>
    /// <param name="permissionCode">The permission code to check (e.g., "patients.view").</param>
    public PermissionRequirement(string permissionCode)
    {
        ArgumentNullException.ThrowIfNull(permissionCode);
        PermissionCode = permissionCode;
    }
}