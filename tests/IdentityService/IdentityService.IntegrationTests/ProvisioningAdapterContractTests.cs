using His.Hope.IdentityService.Application.Provisioning;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class ProvisioningAdapterContractTests
{
    [Fact]
    public async Task DisabledTargetsFailClosedWithoutCallingExternalDependencies()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provisioning:Entra:Enabled"] = "false",
            ["Provisioning:GoogleWorkspace:Enabled"] = "false"
        }).Build();
        var clients = Mock.Of<IHttpClientFactory>();
        var change = new ProvisioningChange("entra", "create", "User", "u-1", System.Text.Json.JsonDocument.Parse("{}"));

        var entra = await new EntraOutboundProvisioningTarget(clients, null!, config).ApplyAsync(change);
        var google = await new GoogleWorkspaceProvisioningTarget(clients, null!, config).ApplyAsync(change with { Target = "google-workspace" });

        Assert.False(entra.Success);
        Assert.Contains("disabled", entra.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(google.Success);
        Assert.Contains("disabled", google.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScimTargetRejectsMissingConfiguration()
    {
        var config = new ConfigurationBuilder().Build();
        var result = await new ScimOutboundProvisioningTarget(Mock.Of<IHttpClientFactory>(), null!, config)
            .ApplyAsync(new ProvisioningChange("scim", "create", "User", "u-1", System.Text.Json.JsonDocument.Parse("{}")));

        Assert.False(result.Success);
        Assert.Contains("not configured", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EntraTargetCachesClientCredentialsTokenAndBindsCreatedId()
    {
        var tokenCalls = 0;
        var graphCalls = 0;
        var factory = new StubHttpClientFactory((request, _) =>
        {
            if (request.RequestUri!.Host == "login.test")
            {
                tokenCalls++;
                return Task.FromResult(Json(HttpStatusCode.OK, new { access_token = "entra-token", expires_in = 3600 }));
            }

            graphCalls++;
            return Task.FromResult(Json(HttpStatusCode.Created, new { id = "entra-user-1" }));
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provisioning:Entra:Enabled"] = "true",
            ["Provisioning:Entra:BaseUrl"] = "https://graph.test/v1.0",
            ["Provisioning:Entra:TokenUrl"] = "https://login.test/token",
            ["Provisioning:Entra:ClientId"] = "entra-client",
            ["Provisioning:Entra:Scope"] = "https://graph.microsoft.com/.default"
        }).Build();
        var secrets = await CreateSecretStoreAsync(config, "entra-client", "entra-secret");
        var target = new EntraOutboundProvisioningTarget(factory, secrets, config);
        var change = new ProvisioningChange("entra", "create", "User", "u-1", JsonDocument.Parse("{\"displayName\":\"Test\"}"));

        var first = await target.ApplyAsync(change);
        var second = await target.ApplyAsync(change with { Operation = "update", ExternalId = first.ExternalId });

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        Assert.Equal("entra-user-1", first.ExternalId);
        Assert.Equal(1, tokenCalls);
        Assert.Equal(2, graphCalls);
    }

    [Fact]
    public async Task GoogleWorkspaceTargetUsesDelegatedJwtAndCachesToken()
    {
        using var rsa = RSA.Create(2048);
        var tokenCalls = 0;
        var apiCalls = 0;
        var factory = new StubHttpClientFactory((request, _) =>
        {
            if (request.RequestUri!.Host == "oauth.test")
            {
                tokenCalls++;
                return Task.FromResult(Json(HttpStatusCode.OK, new { access_token = "google-token", expires_in = 3600 }));
            }

            apiCalls++;
            return Task.FromResult(Json(HttpStatusCode.OK, new { id = "group-1" }));
        });
        var account = JsonSerializer.Serialize(new
        {
            client_email = "service-account@project.iam.gserviceaccount.com",
            private_key = rsa.ExportRSAPrivateKeyPem()
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provisioning:GoogleWorkspace:Enabled"] = "true",
            ["Provisioning:GoogleWorkspace:BaseUrl"] = "https://admin.test/admin/directory/v1",
            ["Provisioning:GoogleWorkspace:TokenUrl"] = "https://oauth.test/token",
            ["Provisioning:GoogleWorkspace:DelegatedAdmin"] = "admin@example.test",
            ["Provisioning:GoogleWorkspace:ServiceAccountSecretId"] = "google-account"
        }).Build();
        var secrets = await CreateSecretStoreAsync(config, "google-account", account);
        var target = new GoogleWorkspaceProvisioningTarget(factory, secrets, config);
        var change = new ProvisioningChange("google-workspace", "create", "Group", "g-1", JsonDocument.Parse("{\"email\":\"group@example.test\"}"));

        var first = await target.ApplyAsync(change);
        var second = await target.ApplyAsync(change with { Operation = "update", ExternalId = first.ExternalId });

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        Assert.Equal("group-1", first.ExternalId);
        Assert.Equal(1, tokenCalls);
        Assert.Equal(2, apiCalls);
    }

    [Fact]
    public async Task ScimTargetCachesOAuthTokenAndUsesExternalBindingOnUpdate()
    {
        var tokenCalls = 0;
        var apiCalls = 0;
        var factory = new StubHttpClientFactory((request, _) =>
        {
            if (request.RequestUri!.Host == "oauth.test")
            {
                tokenCalls++;
                return Task.FromResult(Json(HttpStatusCode.OK, new { access_token = "scim-token", expires_in = 3600 }));
            }

            apiCalls++;
            return Task.FromResult(Json(HttpStatusCode.Created, new { id = "scim-user-1" }));
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provisioning:Scim:BaseUrl"] = "https://scim.test/v2",
            ["Provisioning:Scim:TokenUrl"] = "https://oauth.test/token",
            ["Provisioning:Scim:ClientId"] = "scim-client",
            ["Provisioning:Scim:Scope"] = "scim.write"
        }).Build();
        var secrets = await CreateSecretStoreAsync(config, "scim-client", "scim-secret");
        var target = new ScimOutboundProvisioningTarget(factory, secrets, config);
        var change = new ProvisioningChange("scim", "create", "User", "u-1", JsonDocument.Parse("{\"userName\":\"user@example.test\"}"));

        var first = await target.ApplyAsync(change);
        var second = await target.ApplyAsync(change with { Operation = "update", ExternalId = first.ExternalId });

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        Assert.Equal("scim-user-1", first.ExternalId);
        Assert.Equal(1, tokenCalls);
        Assert.Equal(2, apiCalls);
    }

    private static async Task<VaultClientSecretStore> CreateSecretStoreAsync(IConfiguration config, string id, string secret)
    {
        var store = new VaultClientSecretStore(
            config,
            NullLogger<VaultClientSecretStore>.Instance,
            new TestHostEnvironment(),
            Mock.Of<IVaultTokenProvider>(),
            new StubHttpClientFactory((_, _) => Task.FromResult(Json(HttpStatusCode.NotFound, new { }))));
        await store.StoreSecretAsync(id, secret);
        return store;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object value) =>
        new(status) { Content = JsonContent.Create(value) };

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(handler), disposeHandler: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "IdentityService.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
