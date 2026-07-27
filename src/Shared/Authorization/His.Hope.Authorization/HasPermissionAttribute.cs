using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permissionCode)
    {
        PermissionCode = permissionCode;
        Policy = $"Permission:{permissionCode}";
    }

    public string PermissionCode { get; }
}
