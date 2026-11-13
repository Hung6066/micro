using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class IdentityKestrelHttpsEndpointTests
{
    [Fact]
    public async Task Production_identity_host_serves_health_over_configured_pem_https_endpoint()
    {
        var endpointPort = GetAvailablePort();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));

        var certificateDirectory = Path.Combine(Path.GetTempPath(), $"identity-kestrel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(certificateDirectory);
        var certificatePath = Path.Combine(certificateDirectory, "tls.crt");
        var keyPath = Path.Combine(certificateDirectory, "tls.key");
        await File.WriteAllTextAsync(certificatePath, certificate.ExportCertificatePem());
        await File.WriteAllTextAsync(keyPath, rsa.ExportPkcs8PrivateKeyPem());

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["urls"] = "http://+:5003",
            ["AllowedHosts"] = "localhost;identity-api.nacoms.com.vn",
            ["Kestrel:Endpoints:IdentityOrigin:Url"] = $"https://+:{endpointPort}",
            ["Kestrel:Endpoints:IdentityOrigin:Protocols"] = "Http1AndHttp2",
            ["Kestrel:Endpoints:IdentityOrigin:Certificate:Path"] = certificatePath,
            ["Kestrel:Endpoints:IdentityOrigin:Certificate:KeyPath"] = keyPath,
            ["OpenIddict:Signing:PrivateKeyPath"] = keyPath,
            ["OpenIddict:Encryption:PrivateKeyPath"] = keyPath,
            ["OpenIddict:AllowInsecureHttp"] = "false",
        });
        builder.Configuration.ApplyOpenshipBindings("IdentityDb");
        builder.AddIdentityService();

        foreach (var hostedService in builder.Services.Where(service => service.ServiceType == typeof(IHostedService)).ToArray())
        {
            builder.Services.Remove(hostedService);
        }

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok());

        try
        {
            await app.StartAsync();
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, serverCertificate, _, _) =>
                    serverCertificate?.GetCertHashString() == certificate.Thumbprint,
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://localhost:{endpointPort}") };

            using var response = await client.GetAsync("/health/live");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
            Directory.Delete(certificateDirectory, recursive: true);
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
