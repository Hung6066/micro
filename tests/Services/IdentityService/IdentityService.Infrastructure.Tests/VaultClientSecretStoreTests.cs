using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class VaultClientSecretStoreTests
{
    [Fact]
    public async Task Generate_store_validate_and_revoke_use_isolated_cache()
    {
        var store = CreateStore();

        var generated = store.GenerateSecret("client-a");
        generated.Should().NotBeNullOrWhiteSpace();
        generated.Should().NotContain("+").And.NotContain("/").And.NotEndWith("=");
        (await store.ValidateSecretAsync("client-a", generated)).Should().BeTrue();
        (await store.ValidateSecretAsync("client-a", generated + "-wrong")).Should().BeFalse();

        await store.StoreSecretAsync("client-b", "secret-b");
        (await store.GetSecretAsync("client-b")).Should().Be("secret-b");

        await store.RevokeSecretAsync("client-b");
        (await store.GetSecretAsync("client-b")).Should().BeNull();
    }

    [Fact]
    public void Production_requires_vault_address_when_configured_to_require_vault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:RequireVault"] = "true"
            })
            .Build();

        var act = () => new VaultClientSecretStore(
            config,
            NullLogger<VaultClientSecretStore>.Instance,
            new TestHostEnvironment(Environments.Production),
            Mock.Of<IVaultTokenProvider>(),
            Mock.Of<IHttpClientFactory>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Vault:Address is required*");
    }

    [Fact]
    public async Task Vault_backed_store_reads_and_caches_secret_with_token_and_escaped_path()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { data = new { data = new { secret = "vault-secret" } } })
        });
        var store = CreateVaultStore(handler);

        (await store.GetSecretAsync("client/a")).Should().Be("vault-secret");
        (await store.GetSecretAsync("client/a")).Should().Be("vault-secret");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/v1/kv/data/identity/client-secrets/client%2Fa");
        handler.Requests[0].Headers.GetValues("X-Vault-Token").Should().ContainSingle("vault-token");
    }

    [Fact]
    public async Task Vault_backed_store_writes_and_revokes_secret()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var store = CreateVaultStore(handler);

        await store.StoreSecretAsync("client-a", "secret-a");
        (await store.ValidateSecretAsync("client-a", "secret-a")).Should().BeTrue();
        await store.RevokeSecretAsync("client-a");

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[1].Method.Should().Be(HttpMethod.Delete);
        (await store.GetSecretAsync("client-a")).Should().BeNull();
    }

    [Fact]
    public async Task Vault_backed_store_treats_missing_secret_payload_as_cache_miss()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { data = new { data = new { } } })
        });
        var store = CreateVaultStore(handler);

        (await store.GetSecretAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task Vault_backed_store_treats_invalid_json_payload_as_cache_miss()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });
        var store = CreateVaultStore(handler);

        (await store.GetSecretAsync("invalid")).Should().BeNull();
    }

    private static VaultClientSecretStore CreateStore()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new VaultClientSecretStore(
            config,
            NullLogger<VaultClientSecretStore>.Instance,
            new TestHostEnvironment(Environments.Development),
            Mock.Of<IVaultTokenProvider>(),
            Mock.Of<IHttpClientFactory>());
    }

    private static VaultClientSecretStore CreateVaultStore(RecordingHandler handler)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:Address"] = "https://vault.test/",
                ["Vault:SecretsMount"] = "kv",
                ["Vault:SecretsPathPrefix"] = "identity/client-secrets"
            })
            .Build();
        var factory = Mock.Of<IHttpClientFactory>(f =>
            f.CreateClient("vault") == new HttpClient(handler));
        var tokenProvider = Mock.Of<IVaultTokenProvider>(p =>
            p.GetTokenAsync(It.IsAny<CancellationToken>()) == Task.FromResult<string?>("vault-token"));
        return new VaultClientSecretStore(
            config,
            NullLogger<VaultClientSecretStore>.Instance,
            new TestHostEnvironment(Environments.Development),
            tokenProvider,
            factory);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "IdentityService.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
