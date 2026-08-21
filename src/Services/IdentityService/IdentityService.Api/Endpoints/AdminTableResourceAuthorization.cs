using System.Security.Claims;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class AdminTableResourceAuthorization
{
    internal static async Task<IResult?> AuthorizeTableResourceAsync(
        HttpContext http,
        string resource,
        bool write,
        CancellationToken ct)
    {
        if (!TableViewEndpoints.TryNormalizeResource(resource, out _))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_view_resource" });

        var policy = ResolvePolicy(resource, write);
        if (policy is null)
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_view_resource" });

        var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(http.User, null, policy);
        return result.Succeeded ? null : Results.Forbid();
    }

    private static string? ResolvePolicy(string resource, bool write) =>
        resource switch
        {
            "users" => write
                ? AuthorizationPolicyNames.Permissions.AdminUsersWrite
                : AuthorizationPolicyNames.Permissions.AdminUsersRead,
            "roles" => write
                ? AuthorizationPolicyNames.Permissions.AdminRolesWrite
                : AuthorizationPolicyNames.Permissions.AdminRolesRead,
            "clients" => write
                ? AuthorizationPolicyNames.Permissions.AdminClientsWrite
                : AuthorizationPolicyNames.Permissions.AdminClientsRead,
            _ => null
        };
}
