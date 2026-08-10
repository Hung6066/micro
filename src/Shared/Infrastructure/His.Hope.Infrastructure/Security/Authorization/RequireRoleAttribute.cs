using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Infrastructure.Security.Authorization;

/// <summary>
/// Specifies that a controller action or endpoint requires a specific role.
/// This is a convenience wrapper around the standard [Authorize(Roles = "...")].
///
/// Usage:
/// <code>
/// [RequireRole("Admin")]
/// public IActionResult AdminOnly() { ... }
/// </code>
///
/// For multiple roles, apply the attribute multiple times or use comma separation:
/// <code>
/// [RequireRole("Admin,Provider")]
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequireRoleAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Creates an authorization attribute that requires the specified role(s).
    /// Multiple roles can be comma-separated (e.g., "Admin,Provider").
    /// </summary>
    /// <param name="role">The role(s) required.</param>
    public RequireRoleAttribute(string role)
    {
        Roles = role;
    }
}
