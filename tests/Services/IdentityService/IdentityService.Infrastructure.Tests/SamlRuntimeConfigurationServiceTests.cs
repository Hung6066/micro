using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class SamlRuntimeConfigurationServiceTests
{
    [Fact]
    public async Task CreateAsync_fails_closed_when_saml_is_disabled_or_metadata_missing()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        var runtime = new ExternalIdentityProviderRuntime(new ConfigurationBuilder().Build(), db);
        var factory = new Mock<IHttpClientFactory>();
        var service = new SamlRuntimeConfigurationService(runtime, factory.Object);

        var act = () => service.CreateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SAML federation is not configured.");
        factory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_builds_custom_validation_configuration_from_metadata()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=identity-idp", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "Saml2:Enabled", Value = "true" },
            new SystemSetting { Key = "Saml2:Issuer", Value = "https://sp.example.test" },
            new SystemSetting { Key = "Saml2:IdPMetadata", Value = "https://idp.example.test/metadata" });
        await db.SaveChangesAsync();

        var metadata = $"""
            <EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.example.test/metadata">
              <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
                <KeyDescriptor use="signing">
                  <ds:KeyInfo xmlns:ds="http://www.w3.org/2000/09/xmldsig#"><ds:X509Data><ds:X509Certificate>{Convert.ToBase64String(certificate.RawData)}</ds:X509Certificate></ds:X509Data></ds:KeyInfo>
                </KeyDescriptor>
                <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="https://idp.example.test/login" />
              </IDPSSODescriptor>
            </EntityDescriptor>
            """;
        var factory = new TestHttpClientFactory(metadata);
        var runtime = new ExternalIdentityProviderRuntime(new ConfigurationBuilder().Build(), db);
        var service = new SamlRuntimeConfigurationService(runtime, factory);

        var result = await service.CreateAsync();

        result.Configuration.Issuer.Should().Be("https://sp.example.test");
        result.Configuration.AllowedIssuer.Should().Be("https://idp.example.test/metadata");
        result.Configuration.SingleSignOnDestination!.AbsoluteUri.Should().Be("https://idp.example.test/login");
        result.Configuration.SignatureValidationCertificates.Should().ContainSingle();
        result.Configuration.DetectReplayedTokens.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_rejects_metadata_without_an_idp_descriptor()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "Saml2:Enabled", Value = "true" },
            new SystemSetting { Key = "Saml2:IdPMetadata", Value = "https://idp.example.test/metadata" });
        await db.SaveChangesAsync();

        const string metadata = "<EntityDescriptor xmlns=\"urn:oasis:names:tc:SAML:2.0:metadata\" entityID=\"https://idp.example.test/metadata\" />";
        var service = new SamlRuntimeConfigurationService(
            new ExternalIdentityProviderRuntime(new ConfigurationBuilder().Build(), db),
            new TestHttpClientFactory(metadata));

        var act = () => service.CreateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SAML IdP metadata has no IdPSSODescriptor");
    }

    [Fact]
    public async Task CreateAsync_rejects_metadata_without_valid_signing_certificates()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "Saml2:Enabled", Value = "true" },
            new SystemSetting { Key = "Saml2:IdPMetadata", Value = "https://idp.example.test/metadata" });
        await db.SaveChangesAsync();

        const string metadata = """
            <EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.example.test/metadata">
              <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
                <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="https://idp.example.test/login" />
              </IDPSSODescriptor>
            </EntityDescriptor>
            """;
        var service = new SamlRuntimeConfigurationService(
            new ExternalIdentityProviderRuntime(new ConfigurationBuilder().Build(), db),
            new TestHttpClientFactory(metadata));

        var act = () => service.CreateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SAML IdP metadata has no valid signing certificate");
    }

    private sealed class TestHttpClientFactory(string metadata) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new MetadataHandler(metadata));
    }

    private sealed class MetadataHandler(string metadata) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(metadata)
            });
    }
}
