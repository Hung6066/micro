using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.Infrastructure.Caching;

/// <summary>
/// Prevents a response cached for one authenticated subject/facility scope from
/// being returned to another subject. The marker makes partitioning idempotent
/// because hybrid cache tiers call one another with the already-normalized key.
/// </summary>
public sealed class AuthorizationCacheKeyPartitioner(IHttpContextAccessor httpContextAccessor)
{
    private const string Marker = "authz-cache:";

    public string Partition(string key)
    {
        if (key.StartsWith(Marker, StringComparison.Ordinal))
            return key;

        var httpContext = httpContextAccessor.HttpContext;
        var principal = httpContext?.User;
        var subject = principal?.FindFirst(HisHopeProtocolConstants.Claims.Subject)?.Value
            ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";
        var token = principal?.FindFirst("jti")?.Value ?? "no-token";
        var securityVersion = principal?.FindFirst("securityVersion")?.Value
            ?? principal?.FindFirst("security_version")?.Value
            ?? "no-security-version";
        var facilities = principal?.FindAll("facility_ids")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(principal.FindAll("facility_id").Select(claim => claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{subject}|{token}|{securityVersion}|{string.Join(',', facilities)}")))[..24];
        return $"{Marker}{fingerprint}:{key}";
    }
}
