using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Application.Interfaces;

public interface IVaultKeyProvider
{
    /// <summary>Returns the RSA signing key for JWT creation. Private key never leaves Vault.</summary>
    Task<SecurityKey> GetSigningKeyAsync(CancellationToken ct = default);

    /// <summary>Returns JWKS representation for .well-known/jwks endpoint.</summary>
    Task<IEnumerable<JsonWebKey>> GetJwksAsync(CancellationToken ct = default);

    /// <summary>Signs data using Vault transit engine. Returns base64 signature.</summary>
    Task<string> SignAsync(byte[] data, CancellationToken ct = default);

    /// <summary>Checks Vault connectivity and key existence. Returns false if unhealthy.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
