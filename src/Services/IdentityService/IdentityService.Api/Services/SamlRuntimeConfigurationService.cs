using His.Hope.IdentityService.Infrastructure.Services;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;

namespace His.Hope.IdentityService.Api.Services;

public sealed class SamlRuntimeConfigurationService(
    ExternalIdentityProviderRuntime runtime,
    IHttpClientFactory httpClientFactory)
{
    public async Task<(Saml2Configuration Configuration, SamlRuntimeSettings Settings)> CreateAsync(CancellationToken ct = default)
    {
        var settings = await runtime.GetSamlAsync(ct);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.IdpMetadata))
            throw new InvalidOperationException("SAML federation is not configured.");

        var configuration = new Saml2Configuration
        {
            Issuer = settings.Issuer,
            SingleSignOnDestination = string.IsNullOrWhiteSpace(settings.SingleSignOnDestination)
                ? null
                : new Uri(settings.SingleSignOnDestination),
            AllowedIssuer = settings.AllowedIssuer,
            DetectReplayedTokens = true,
            AudienceRestricted = true
        };
        if (!string.IsNullOrWhiteSpace(configuration.Issuer))
            configuration.AllowedAudienceUris.Add(configuration.Issuer);

        var descriptor = new EntityDescriptor();
        await descriptor.ReadIdPSsoDescriptorFromUrlAsync(
            httpClientFactory, new Uri(settings.IdpMetadata));
        var idp = descriptor.IdPSsoDescriptor
            ?? throw new InvalidOperationException("SAML IdP metadata has no IdPSSODescriptor");
        configuration.AllowedIssuer = string.IsNullOrWhiteSpace(settings.AllowedIssuer)
            ? descriptor.EntityId
            : settings.AllowedIssuer;
        configuration.SingleSignOnDestination ??= idp.SingleSignOnServices.First().Location;
        foreach (var certificate in idp.SigningCertificates.Where(c => c.NotAfter > DateTime.UtcNow))
            configuration.SignatureValidationCertificates.Add(certificate);
        if (configuration.SignatureValidationCertificates.Count == 0)
            throw new InvalidOperationException("SAML IdP metadata has no valid signing certificate");
        // Keycloak commonly publishes a self-signed realm signing certificate.
        // Trust only certificates supplied by the configured metadata endpoint.
        configuration.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.Custom;
        configuration.CustomCertificateValidator = new MetadataCertificateValidator(
            configuration.SignatureValidationCertificates);
        return (configuration, settings);
    }
}
