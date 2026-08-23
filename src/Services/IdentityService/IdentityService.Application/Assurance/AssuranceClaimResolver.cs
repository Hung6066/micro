using System.Security.Claims;

namespace His.Hope.IdentityService.Application.Assurance;

public static class AssuranceClaimResolver
{
    public static string ResolveAssuranceLevel(ClaimsPrincipal principal)
    {
        var methods = principal.FindAll("amr")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (methods.Any(method => method.Equals("mtls", StringComparison.OrdinalIgnoreCase)))
            return "aal3";
        if (methods.Any(method => method is "passkey" or "mfa" or "totp" or "webauthn"))
            return "aal2";
        if (methods.Any(method => method is "pwd" or "password"))
            return "aal1";
        return "standard";
    }

    public static bool HasFreshDevicePosture(ClaimsPrincipal principal) =>
        principal.FindFirst("device_posture_fresh")?.Value is "true" or "1";
}
