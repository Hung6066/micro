using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace His.Hope.Bff.Core.Authentication;

/// <summary>
/// Protects bearer and refresh tokens before they are written to the shared
/// Redis BFF session. The Data Protection key ring must be shared by replicas.
/// </summary>
public sealed class SessionTokenProtector
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector _protector;

    public SessionTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HisHope.Bff.SessionTokens.v1");
    }

    public string Protect(string token) =>
        string.IsNullOrEmpty(token) ? token : Prefix + _protector.Protect(token);

    public string? ProtectOptional(string? token) =>
        string.IsNullOrEmpty(token) ? token : Protect(token);

    public string Unprotect(string protectedToken)
    {
        if (string.IsNullOrEmpty(protectedToken)) return protectedToken;
        if (!protectedToken.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("The session token is not protected.");

        return _protector.Unprotect(protectedToken[Prefix.Length..]);
    }

    public string? UnprotectOptional(string? protectedToken) =>
        string.IsNullOrEmpty(protectedToken) ? protectedToken : Unprotect(protectedToken);
}
