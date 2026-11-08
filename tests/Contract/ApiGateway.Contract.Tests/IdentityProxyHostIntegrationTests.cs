using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Xunit;

namespace His.Hope.ApiGateway.Contract.Tests;

[Trait("Category", "Contract")]
public sealed class IdentityProxyHostIntegrationTests
{
    [Fact]
    public async Task IdentityRoute_UsesCertificateHostnameForInternalHttpsDestination()
    {
        using var identityCertificate = CreateIdentityCertificate();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var internalIdentityAddress = new Uri($"https://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        var identityAddress = IdentityClusterHttpClientConfiguration.GetDestinationAddress(
            internalIdentityAddress,
            "identity-api.nacoms.com.vn");
        using var serverCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var identityServer = ServeIdentityResponseAsync(listener, identityCertificate, serverCancellation.Token);

        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "identity-discovery",
                ClusterId = "identity",
                Match = new RouteMatch { Path = "/.well-known/{**catch-all}" }
            }
        };
        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "identity",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["identity"] = new() { Address = identityAddress.ToString() }
                }
            }
        };

        SslPolicyErrors observedCertificateErrors = SslPolicyErrors.None;
        var clientConfigurationCalls = 0;
        using var gateway = new TestServer(new WebHostBuilder()
            .ConfigureServices(services => services
                .AddReverseProxy()
                .LoadFromMemory(routes, clusters)
                .ConfigureHttpClient((context, handler) =>
                {
                    Interlocked.Increment(ref clientConfigurationCalls);
                    handler.UseProxy = false;
                    IdentityClusterHttpClientConfiguration.Configure(
                        context.ClusterId,
                        handler,
                        internalIdentityAddress,
                        "identity-api.nacoms.com.vn");
                    handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, errors) =>
                    {
                        observedCertificateErrors = errors;
                        return (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None;
                    };
                }))
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapReverseProxy());
            }));

        using var client = gateway.CreateClient();
        var response = await client.GetAsync("/.well-known/openid-configuration");
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"TLS certificate errors: {observedCertificateErrors}; ConfigureHttpClient calls: {clientConfigurationCalls}; identity server state: {identityServer.Status}; server error: {identityServer.Exception}; destination: {identityAddress}; body: {responseBody}");
        (observedCertificateErrors & SslPolicyErrors.RemoteCertificateNameMismatch)
            .Should().Be(SslPolicyErrors.None);
        (await identityServer).Should().Contain("GET /.well-known/openid-configuration HTTP/1.1");
    }

    private static async Task<string> ServeIdentityResponseAsync(
        TcpListener listener,
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = certificate,
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                | System.Security.Authentication.SslProtocols.Tls13,
            ApplicationProtocols = [SslApplicationProtocol.Http11]
        }, cancellationToken);

        using var reader = new StreamReader(tls, Encoding.ASCII, leaveOpen: true);
        var requestHeaders = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line && line.Length > 0)
        {
            requestHeaders.AppendLine(line);
        }

        var response = "HTTP/1.1 200 OK\r\nContent-Length: 24\r\nConnection: close\r\n\r\nsecure identity response";
        await tls.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        await tls.FlushAsync(cancellationToken);
        return requestHeaders.ToString();
    }

    private static X509Certificate2 CreateIdentityCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=identity-api.nacoms.com.vn",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("identity-api.nacoms.com.vn");
        request.CertificateExtensions.Add(san.Build());
        using var ephemeralCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(10));
        return new X509Certificate2(
            ephemeralCertificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
    }
}
