using System.Security.Claims;

namespace His.Hope.IdentityService.Application.Scim;

public static class ScimAuthorization
{
    public const string ReadScope = "scim.read";
    public const string WriteScope = "scim.write";

    public static bool HasProvisioningScope(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true &&
        principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(scope => scope is ReadScope or WriteScope);

    public static bool HasScope(ClaimsPrincipal principal, string requiredScope) =>
        principal.Identity?.IsAuthenticated == true &&
        principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
}
