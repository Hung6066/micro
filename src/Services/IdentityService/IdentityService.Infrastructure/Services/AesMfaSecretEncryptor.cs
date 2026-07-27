using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class AesMfaSecretEncryptor : IMfaSecretEncryptor
{
    private readonly IDataProtector _protector;

    public AesMfaSecretEncryptor(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("HisHope.MfaSecret");
    }

    public string Encrypt(string plaintext)
    {
        var protectedBytes = _protector.Protect(System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(protectedBytes);
    }

    public string Decrypt(string ciphertext)
    {
        var protectedBytes = Convert.FromBase64String(ciphertext);
        var plaintextBytes = _protector.Unprotect(protectedBytes);
        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
