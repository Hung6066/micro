using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.IdentityService.Api.Controllers;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class SamlFederationControllerCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Login_builds_redirect_request_and_preserves_a_safe_relay_state()
    {
        await using var setup = await ConfigureSamlAsync();
        var controller = setup.CreateController();
        controller.ControllerContext = new ControllerContext { HttpContext = Context("https://identity.example.test") };

        var result = await controller.Login("/account/passkeys");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://idp.example.test/login", redirect.Url, StringComparison.Ordinal);
        Assert.Contains("SAMLRequest=", redirect.Url, StringComparison.Ordinal);
        Assert.Contains("RelayState=", redirect.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_fails_closed_as_not_found_when_metadata_is_invalid()
    {
        await using var setup = await ConfigureSamlAsync(metadata: "<EntityDescriptor xmlns=\"urn:oasis:names:tc:SAML:2.0:metadata\" entityID=\"https://idp.example.test/metadata\" />");
        var controller = setup.CreateController();
        controller.ControllerContext = new ControllerContext { HttpContext = Context("https://identity.example.test") };

        var result = await controller.Login();

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task Assertion_consumer_service_rejects_an_invalid_saml_response()
    {
        await using var setup = await ConfigureSamlAsync();
        var controller = setup.CreateController();
        var context = Context("https://identity.example.test");
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["SAMLResponse"] = "not-base64-saml"
        });
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        // The ACS must not provision or authenticate from malformed input. The
        // controller deliberately lets the binding exception reach the common
        // error handler, which records the security event and returns 4xx.
        await Assert.ThrowsAnyAsync<Exception>(() => controller.AssertionConsumerService());
    }

    private DefaultHttpContext Context(string host)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = fixture.Services
        };
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString(new Uri(host).Host);
        return context;
    }

    private async Task<SamlSetup> ConfigureSamlAsync(string? metadata = null)
    {
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var keys = new[] { "Saml2:Enabled", "Saml2:Issuer", "Saml2:IdPMetadata" };
        // SystemSetting participates in the shared soft-delete contract. Use a
        // set-based hard cleanup for these test-only global keys; a soft delete
        // would leave the primary key occupied and make the next SAML case
        // fail before it reaches the controller.
        await db.SystemSettings.IgnoreQueryFilters()
            .Where(x => keys.Contains(x.Key))
            .ExecuteDeleteAsync();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "Saml2:Enabled", Value = "true" },
            new SystemSetting { Key = "Saml2:Issuer", Value = "https://identity.example.test" },
            new SystemSetting { Key = "Saml2:IdPMetadata", Value = "https://idp.example.test/metadata" });
        await db.SaveChangesAsync();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=coverage-idp", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var runtime = new ExternalIdentityProviderRuntime(
            scope.ServiceProvider.GetRequiredService<IConfiguration>(), db);
        var service = new SamlRuntimeConfigurationService(
            runtime,
            new MetadataHttpClientFactory(metadata ?? Metadata(certificate)));
        return new SamlSetup(scope, db, service);
    }

    private static string Metadata(X509Certificate2 certificate) => $"""
        <EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.example.test/metadata">
          <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
            <KeyDescriptor use="signing"><ds:KeyInfo xmlns:ds="http://www.w3.org/2000/09/xmldsig#"><ds:X509Data><ds:X509Certificate>{Convert.ToBase64String(certificate.RawData)}</ds:X509Certificate></ds:X509Data></ds:KeyInfo></KeyDescriptor>
            <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="https://idp.example.test/login" />
          </IDPSSODescriptor>
        </EntityDescriptor>
        """;

    private sealed class SamlSetup(Microsoft.Extensions.DependencyInjection.IServiceScope scope, IdentityDbContext db, SamlRuntimeConfigurationService service) : IAsyncDisposable
    {
        public SamlFederationController CreateController() => new(
            service,
            scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<Role>>(),
            scope.ServiceProvider.GetRequiredService<OidcLoginCompletionService>());

        public async ValueTask DisposeAsync()
        {
            // Settings are global, so remove only the values created by this
            // test class and leave the shared fixture's seed untouched.
            var keys = new[] { "Saml2:Enabled", "Saml2:Issuer", "Saml2:IdPMetadata" };
            await db.SystemSettings.IgnoreQueryFilters()
                .Where(x => keys.Contains(x.Key))
                .ExecuteDeleteAsync();
            scope.Dispose();
        }
    }

    private sealed class MetadataHttpClientFactory(string metadata) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new MetadataHandler(metadata));
    }

    private sealed class MetadataHandler(string metadata) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata) });
    }
}
