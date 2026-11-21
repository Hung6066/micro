using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using His.Hope.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace His.Hope.Infrastructure.Tests;

public sealed class VaultDatabaseCredentialProviderTests
{
    [Fact]
    public void Uses_OpenShip_managed_connection_in_production_when_Vault_database_credentials_are_disabled()
    {
        const string configured = "Host=postgres;Database=commercedb;Username=openship-bound;Password=managed;SslMode=VerifyFull";
        var tokens = new Mock<IVaultTokenProvider>();
        tokens.Setup(x => x.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vault should not be called when database credential leasing is disabled."));
        var factory = new Mock<IHttpClientFactory>();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");
        var resolver = new VaultDatabaseConnectionStringResolver(
            factory.Object,
            Monitor(new VaultOptions()),
            Monitor(new VaultDatabaseOptions { Enabled = false }),
            tokens.Object,
            environment.Object,
            NullLogger<VaultDatabaseConnectionStringResolver>.Instance);

        resolver.Resolve(configured, "CommerceDb").Should().Be(configured);
        tokens.Verify(x => x.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Refreshes_database_credential_when_issuing_token_changes()
    {
        var tokens = new Mock<IVaultTokenProvider>();
        var currentToken = "token-one";
        tokens.Setup(x => x.GetTokenAsync(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(currentToken));
        var requests = 0;
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("vault")).Returns(() => new HttpClient(new Handler(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { lease_duration = 3600, data = new { username = $"user-{requests}", password = "password" } }) };
        })));
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");
        var resolver = new VaultDatabaseConnectionStringResolver(factory.Object, Monitor(new VaultOptions { Address = "https://vault.test" }), Monitor(new VaultDatabaseOptions { Enabled = true, Role = "identity-service-db" }), tokens.Object, environment.Object, NullLogger<VaultDatabaseConnectionStringResolver>.Instance);

        var first = resolver.Resolve("Host=localhost;Database=test", "IdentityDb");
        resolver.Resolve("Host=localhost;Database=test", "IdentityDb").Should().Be(first);
        currentToken = "token-two";
        var second = resolver.Resolve("Host=localhost;Database=test", "IdentityDb");

        requests.Should().Be(2);
        second.Should().Contain("user-2");
    }

    [Fact]
    public void Refreshes_database_credential_after_authentication_failure_invalidates_connection_lease()
    {
        var tokens = new Mock<IVaultTokenProvider>();
        tokens.Setup(x => x.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token-one");
        var requests = 0;
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("vault")).Returns(() => new HttpClient(new Handler(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { lease_duration = 3600, data = new { username = $"user-{requests}", password = "password" } })
            };
        })));
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");
        var resolver = new VaultDatabaseConnectionStringResolver(factory.Object, Monitor(new VaultOptions { Address = "https://vault.test" }), Monitor(new VaultDatabaseOptions { Enabled = true, Role = "identity-service-db" }), tokens.Object, environment.Object, NullLogger<VaultDatabaseConnectionStringResolver>.Instance);
        const string configured = "Host=localhost;Database=test";

        resolver.Resolve(configured, "IdentityDb").Should().Contain("user-1");
        resolver.Invalidate("IdentityDb");
        resolver.Resolve(configured, "IdentityDb").Should().Contain("user-2");
        requests.Should().Be(2);
    }

    private static IOptionsMonitor<T> Monitor<T>(T value) where T : class
    {
        var monitor = new Mock<IOptionsMonitor<T>>();
        monitor.SetupGet(x => x.CurrentValue).Returns(value);
        return monitor.Object;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(callback(request));
    }
}
