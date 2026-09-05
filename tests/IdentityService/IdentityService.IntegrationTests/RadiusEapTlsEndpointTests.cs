using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class RadiusEapTlsEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public RadiusEapTlsEndpointTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task EapTls_assertion_is_not_discoverable_when_the_feature_is_disabled()
    {
        var configuration = _fixture.Services.GetRequiredService<IConfiguration>();
        var previous = configuration["Radius:EapTls:Enabled"];
        configuration["Radius:EapTls:Enabled"] = "false";

        try
        {
            using var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.RadiusEapTls);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            configuration["Radius:EapTls:Enabled"] = previous;
        }
    }

    [Fact]
    public async Task EapTls_assertion_requires_a_client_certificate_when_enabled()
    {
        var configuration = _fixture.Services.GetRequiredService<IConfiguration>();
        var previous = configuration["Radius:EapTls:Enabled"];
        configuration["Radius:EapTls:Enabled"] = "true";

        try
        {
            using var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.RadiusEapTls);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            configuration["Radius:EapTls:Enabled"] = previous;
        }
    }

    [Fact]
    public async Task EapTls_status_requires_human_admin_authorization()
    {
        using var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.AdminRadiusEapTlsStatus);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EapTls_status_exposes_feature_and_trust_configuration_to_an_admin()
    {
        var configuration = _fixture.Services.GetRequiredService<IConfiguration>();
        var previousEnabled = configuration["Radius:EapTls:Enabled"];
        var previousCa = configuration["Mtls:TrustedCaFile"];
        var caPath = Path.Combine(Path.GetTempPath(), $"identity-radius-ca-{Guid.NewGuid():N}.pem");
        await File.WriteAllTextAsync(caPath, "test-ca");
        configuration["Radius:EapTls:Enabled"] = "true";
        configuration["Mtls:TrustedCaFile"] = caPath;

        try
        {
            using var session = _fixture.CreateSessionClient();
            Assert.Equal(HttpStatusCode.OK,
                (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);

            using var response = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminRadiusEapTlsStatus);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("enabled").GetBoolean());
            Assert.True(body.GetProperty("trustedCaConfigured").GetBoolean());
            Assert.True(body.GetProperty("trustedCaReachable").GetBoolean());
            Assert.Equal("radius-outpost", body.GetProperty("sharedSecretManagedBy").GetString());
        }
        finally
        {
            configuration["Radius:EapTls:Enabled"] = previousEnabled;
            configuration["Mtls:TrustedCaFile"] = previousCa;
            File.Delete(caPath);
        }
    }

    [Fact]
    public async Task EapTls_status_reports_unconfigured_ca_without_leaking_shared_secret()
    {
        var configuration = _fixture.Services.GetRequiredService<IConfiguration>();
        var previousEnabled = configuration["Radius:EapTls:Enabled"];
        var previousCa = configuration["Mtls:TrustedCaFile"];
        configuration["Radius:EapTls:Enabled"] = "true";
        configuration["Mtls:TrustedCaFile"] = "";

        try
        {
            using var session = _fixture.CreateSessionClient();
            Assert.Equal(HttpStatusCode.OK,
                (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);

            using var response = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminRadiusEapTlsStatus);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("enabled").GetBoolean());
            Assert.False(body.GetProperty("trustedCaConfigured").GetBoolean());
            Assert.False(body.GetProperty("trustedCaReachable").GetBoolean());
            Assert.Equal("radius-outpost", body.GetProperty("sharedSecretManagedBy").GetString());
            // The contract intentionally exposes only the owner label, never the
            // configured shared-secret value itself.
            var configuredSecret = configuration["Radius:EapTls:SharedSecret"];
            if (!string.IsNullOrEmpty(configuredSecret))
                Assert.DoesNotContain(configuredSecret, body.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            configuration["Radius:EapTls:Enabled"] = previousEnabled;
            configuration["Mtls:TrustedCaFile"] = previousCa;
        }
    }
}
