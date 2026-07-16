using Microsoft.AspNetCore.Authorization;
using His.Hope.Infrastructure.Security.Authorization.Requirements;

namespace His.Hope.Infrastructure.Security.Authorization;

/// <summary>
/// Specifies that a controller action or endpoint requires a specific permission.
/// This is a convenience attribute that wraps <see cref="PermissionRequirement"/>
/// for use with ASP.NET Core's authorization policy system.
///
/// Usage:
/// <code>
/// [HasPermission("patients.view")]
/// public IActionResult GetPatient() { ... }
/// </code>
///
/// For Minimal API endpoints, use RequireAuthorization with the policy name:
/// <code>
/// app.MapGet("/patients", handler)
///    .RequireAuthorization("Permission:patients.view");
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// The permission code required (e.g., "patients.view").
    /// </summary>
    public string PermissionCode { get; }

    /// <summary>
    /// Creates an authorization attribute that requires the specified permission.
    /// The policy name is automatically derived as "Permission:{permissionCode}".
    /// </summary>
    /// <param name="permissionCode">The permission code required.</param>
    public HasPermissionAttribute(string permissionCode)
    {
        PermissionCode = permissionCode;
        Policy = $"Permission:{permissionCode}";
    }
}
