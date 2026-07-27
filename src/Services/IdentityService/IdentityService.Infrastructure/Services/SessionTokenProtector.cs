using Microsoft.AspNetCore.DataProtection;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Encrypts/decrypts JWT and refresh tokens stored in BFF Redis sessions.
/// Purpose: Prevent plaintext token exposure if Redis is compromised (HIPAA).
/// Uses ASP.NET Data Protection for encryption-at-rest within Redis.
/// </summary>
public class SessionTokenProtector
{
    private readonly IDataProtector _protector;

    public SessionTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HisHope.SessionTokens.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
