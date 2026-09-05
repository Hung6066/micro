using System.Security.Claims;
using His.Hope.SharedKernel.Protocol;
using System.Globalization;

namespace His.Hope.IdentityService.Application.Assurance;

public static class AssuranceClaimResolver
{
    public static IReadOnlySet<string> ResolveFactors(ClaimsPrincipal principal)
    {
        var factors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in principal.FindAll(HisHopeProtocolConstants.Claims.AuthenticationMethod).Select(claim => claim.Value))
        {
            if (method.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
                method.Equals("password", StringComparison.OrdinalIgnoreCase))
                factors.Add("password");
            else if (method.Equals("passkey", StringComparison.OrdinalIgnoreCase) ||
                     method.Equals("webauthn", StringComparison.OrdinalIgnoreCase))
                factors.Add("passkey");
            else if (method.Equals("mfa", StringComparison.OrdinalIgnoreCase) ||
                     method.Equals("otp", StringComparison.OrdinalIgnoreCase) ||
                     method.Equals("totp", StringComparison.OrdinalIgnoreCase))
                factors.Add("mfa");
            else if (method.Equals("mtls", StringComparison.OrdinalIgnoreCase))
                factors.Add("mtls");
        }
        return factors;
    }

    public static string? ResolveStrongestFactor(ClaimsPrincipal principal) =>
        ResolveFactors(principal)
            .OrderByDescending(factor => factor switch
            {
                "mtls" => 4,
                "passkey" => 3,
                "mfa" => 2,
                "password" => 1,
                _ => 0
            })
            .FirstOrDefault();

    public static string ResolveAssuranceLevel(ClaimsPrincipal principal)
    {
        var methods = principal.FindAll(HisHopeProtocolConstants.Claims.AuthenticationMethod)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (methods.Any(method => method.Equals("mtls", StringComparison.OrdinalIgnoreCase)))
            return "aal3";
        if (methods.Any(method => method.Equals("passkey", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("mfa", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("totp", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("webauthn", StringComparison.OrdinalIgnoreCase)))
            return "aal2";
        if (methods.Any(method => method.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("password", StringComparison.OrdinalIgnoreCase)))
            return "aal1";
        return "standard";
    }

    public static bool HasFreshDevicePosture(ClaimsPrincipal principal) =>
        principal.FindFirst("device_posture_fresh")?.Value is "true" or "1";

    public static bool HasFreshAuthentication(
        ClaimsPrincipal principal,
        int maxAgeMinutes,
        DateTimeOffset? now = null)
    {
        if (maxAgeMinutes is < 1 or > 240)
            return false;

        var value = principal.FindFirst("auth_time")?.Value;
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            return false;

        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var age = (now ?? DateTimeOffset.UtcNow) - authenticatedAt;
        return age >= TimeSpan.FromSeconds(-30) && age <= TimeSpan.FromMinutes(maxAgeMinutes);
    }
}
