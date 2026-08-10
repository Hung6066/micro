using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;

namespace His.Hope.IdentityService.Api.Services;

/// <summary>
/// Pins SAML signing certificates obtained from the configured IdP metadata.
/// This supports Keycloak's self-signed realm certificate without disabling
/// certificate or XML signature validation globally.
/// </summary>
public sealed class MetadataCertificateValidator : X509CertificateValidator
{
    private readonly HashSet<string> _thumbprints;

    public MetadataCertificateValidator(IEnumerable<X509Certificate2> certificates)
    {
        _thumbprints = certificates
            .Select(certificate => Normalize(certificate.Thumbprint))
            .Where(thumbprint => thumbprint.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public override void Validate(X509Certificate2 certificate)
    {
        if (!_thumbprints.Contains(Normalize(certificate.Thumbprint)))
            throw new SecurityTokenValidationException("The SAML signing certificate is not trusted by the configured IdP metadata.");

        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
            throw new SecurityTokenValidationException("The SAML signing certificate is outside its validity period.");
    }

    private static string Normalize(string? thumbprint) =>
        string.IsNullOrWhiteSpace(thumbprint)
            ? string.Empty
            : thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
