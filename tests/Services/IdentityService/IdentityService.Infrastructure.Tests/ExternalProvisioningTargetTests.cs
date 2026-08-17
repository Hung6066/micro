using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using His.Hope.IdentityService.Application.Provisioning;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class ExternalProvisioningTargetTests
{
    [Fact]
    public async Task Entra_disabled_and_http_endpoints_fail_closed()
    {
        var disabled = Entra(new Dictionary<string, string?>());
        var payload = JsonDocument.Parse("{}");
        (await disabled.ApplyAsync(Change("User", "create", payload))).Error.Should().Contain("disabled");

        var invalid = Entra(new Dictionary<string, string?>
        {
            ["Provisioning:Entra:Enabled"] = "true",
            ["Provisioning:Entra:TokenUrl"] = "http://entra.example.test/token",
            ["Provisioning:Entra:ClientId"] = "client"
        });
        (await invalid.ApplyAsync(Change("User", "create", payload))).Error.Should().Contain("HTTPS");
    }

    [Fact]
    public async Task Entra_create_maps_graph_id_and_caches_token()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)
            ? new(HttpStatusCode.OK) { Content = Json("{\"access_token\":\"entra-token\",\"expires_in\":300}") }
            : new(HttpStatusCode.Created) { Content = Json("{\"id\":\"graph-user-1\"}") });
        var target = Entra(new Dictionary<string, string?>
        {
            ["Provisioning:Entra:Enabled"] = "true",
            ["Provisioning:Entra:BaseUrl"] = "https://graph.example.test/v1.0",
            ["Provisioning:Entra:TokenUrl"] = "https://login.example.test/token",
            ["Provisioning:Entra:ClientId"] = "client"
        }, handler);
        var payload = JsonDocument.Parse("{\"displayName\":\"Alice\"}");

        var result = await target.ApplyAsync(Change("User", "create", payload));

        result.Success.Should().BeTrue();
        result.ExternalId.Should().Be("graph-user-1");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Headers.Authorization!.Parameter.Should().Be("entra-token");
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/v1.0/users");
    }

    [Fact]
    public async Task Google_workspace_disabled_and_http_endpoints_fail_closed()
    {
        var payload = JsonDocument.Parse("{}");
        var disabled = Google(new Dictionary<string, string?>());
        (await disabled.ApplyAsync(Change("Group", "create", payload))).Error.Should().Contain("disabled");
        var invalid = Google(new Dictionary<string, string?>
        {
            ["Provisioning:GoogleWorkspace:Enabled"] = "true",
            ["Provisioning:GoogleWorkspace:BaseUrl"] = "http://admin.example.test"
        });
        (await invalid.ApplyAsync(Change("Group", "create", payload))).Error.Should().Contain("HTTPS");
    }

    [Fact]
    public async Task Google_workspace_create_mints_delegated_jwt_and_maps_email_id()
    {
        using var rsa = RSA.Create(2048);
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)
            ? new(HttpStatusCode.OK) { Content = Json("{\"access_token\":\"google-token\",\"expires_in\":300}") }
            : new(HttpStatusCode.OK) { Content = Json("{\"email\":\"alice@example.test\"}") });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provisioning:GoogleWorkspace:Enabled"] = "true",
            ["Provisioning:GoogleWorkspace:BaseUrl"] = "https://admin.example.test/admin/directory/v1",
            ["Provisioning:GoogleWorkspace:TokenUrl"] = "https://oauth.example.test/token",
            ["Provisioning:GoogleWorkspace:ServiceAccountSecretId"] = "google-sa",
            ["Provisioning:GoogleWorkspace:DelegatedAdmin"] = "admin@example.test"
        }).Build();
        var factory = Factory(handler);
        var secrets = Secrets(configuration, factory);
        await secrets.StoreSecretAsync("google-sa", JsonSerializer.Serialize(new
        {
            client_email = "service@example.test",
            private_key = rsa.ExportPkcs8PrivateKeyPem()
        }));
        var target = new GoogleWorkspaceProvisioningTarget(factory.Object, secrets, configuration);

        var result = await target.ApplyAsync(Change("User", "create", JsonDocument.Parse("{\"name\":{\"givenName\":\"Alice\"}}")));

        result.Success.Should().BeTrue();
        result.ExternalId.Should().Be("alice@example.test");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Content.Should().NotBeNull();
        handler.Requests[1].Headers.Authorization!.Parameter.Should().Be("google-token");
    }

    private static ProvisioningChange Change(string resource, string operation, JsonDocument payload) =>
        new("external", operation, resource, "resource-1", payload);

    private static EntraOutboundProvisioningTarget Entra(IDictionary<string, string?> values, HttpMessageHandler? handler = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var factory = Factory(handler);
        var secrets = Secrets(configuration, factory);
        secrets.GenerateSecret(configuration["Provisioning:Entra:ClientId"] ?? "client");
        return new EntraOutboundProvisioningTarget(factory.Object, secrets, configuration);
    }

    private static GoogleWorkspaceProvisioningTarget Google(IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var factory = Factory(null);
        return new GoogleWorkspaceProvisioningTarget(factory.Object, Secrets(configuration, factory), configuration);
    }

    private static Mock<IHttpClientFactory> Factory(HttpMessageHandler? handler)
    {
        var client = new HttpClient(handler ?? new RecordingHandler(_ => new(HttpStatusCode.OK)));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
        return factory;
    }

    private static VaultClientSecretStore Secrets(IConfiguration configuration, Mock<IHttpClientFactory> factory)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);
        return new VaultClientSecretStore(configuration, NullLogger<VaultClientSecretStore>.Instance,
            environment.Object, Mock.Of<IVaultTokenProvider>(), factory.Object);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response(request));
        }
    }
}
