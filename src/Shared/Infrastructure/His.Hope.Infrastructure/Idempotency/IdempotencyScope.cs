using System.Security.Cryptography;
using System.Text;

namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// Derives a bounded storage key for an idempotent request. The client key is
/// deliberately scoped so it cannot collide across tenants, subjects,
/// operations, or gateway instances.
/// </summary>
public static class IdempotencyScope
{
    public static string CreateStorageKey(
        string service,
        string tenant,
        string subject,
        string method,
        string endpoint,
        string clientKey)
    {
        var canonical = new StringBuilder();
        Append(canonical, service);
        Append(canonical, tenant);
        Append(canonical, subject);
        Append(canonical, method.ToUpperInvariant());
        Append(canonical, endpoint);
        Append(canonical, clientKey);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string value)
    {
        var normalized = value.Trim();
        target.Append(normalized.Length).Append(':').Append(normalized);
    }
}
