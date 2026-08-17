using System.Net;
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

public sealed class ScimOutboundProvisioningTargetTests
{
    [Fact]
    public async Task Apply_rejects_missing_or_non_https_configuration()
    {
        var target = Create(new Dictionary<string, string?>());
        var payload = JsonDocument.Parse("{}");

        (await target.ApplyAsync(new ProvisioningChange("scim", "create", "User", "u1", payload)))
            .Error.Should().Contain("not configured");
        var httpsTarget = Create(new Dictionary<string, string?>
        {
            ["Provisioning:Scim:BaseUrl"] = "http://scim.example.test",
            ["Provisioning:Scim:TokenUrl"] = "https://idp.example.test/token",
            ["Provisioning:Scim:ClientId"] = "client"
        });
        (await httpsTarget.ApplyAsync(new ProvisioningChange("scim", "create", "User", "u1", payload)))
            .Error.Should().Contain("HTTPS");
    }

    [Fact]
    public async Task Apply_creates_user_uses_bearer_token_and_returns_external_id()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("{\"access_token\":\"access-1\",\"expires_in\":300}") }
            : new HttpResponseMessage(HttpStatusCode.Created) { Content = Json("{\"id\":\"external-1\"}") });
        var target = Create(new Dictionary<string, string?>
        {
            ["Provisioning:Scim:BaseUrl"] = "https://scim.example.test",
            ["Provisioning:Scim:TokenUrl"] = "https://idp.example.test/token",
            ["Provisioning:Scim:ClientId"] = "client"
        }, handler);
        var payload = JsonDocument.Parse("{\"userName\":\"alice\"}");

        var result = await target.ApplyAsync(new ProvisioningChange("scim", "create", "User", "u1", payload));

        result.Success.Should().BeTrue();
        result.ExternalId.Should().Be("external-1");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Headers.Authorization!.Parameter.Should().Be("access-1");
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/Users");
    }

    [Fact]
    public async Task Apply_returns_scim_error_body_and_reuses_cached_token()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("{\"access_token\":\"access-1\"}") }
            : new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad request") });
        var target = Create(new Dictionary<string, string?>
        {
            ["Provisioning:Scim:BaseUrl"] = "https://scim.example.test",
            ["Provisioning:Scim:TokenUrl"] = "https://idp.example.test/token",
            ["Provisioning:Scim:ClientId"] = "client"
        }, handler);
        var payload = JsonDocument.Parse("{}");

        var first = await target.ApplyAsync(new ProvisioningChange("scim", "delete", "Group", "group/1", payload, "g/1"));
        var second = await target.ApplyAsync(new ProvisioningChange("scim", "update", "User", "u1", payload, "u/1"));

        first.Success.Should().BeFalse();
        first.Error.Should().Contain("400").And.Contain("bad request");
        second.Success.Should().BeFalse();
        handler.Requests.Count(r => r.RequestUri!.AbsolutePath.Contains("token", StringComparison.Ordinal)).Should().Be(1);
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/Groups/g%2F1");
    }

    private static ScimOutboundProvisioningTarget Create(
        IDictionary<string, string?> values,
        HttpMessageHandler? handler = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var httpClient = new HttpClient(handler ?? new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);
        var secrets = new VaultClientSecretStore(configuration, NullLogger<VaultClientSecretStore>.Instance,
            environment.Object, Mock.Of<IVaultTokenProvider>(), factory.Object);
        secrets.GenerateSecret(configuration["Provisioning:Scim:ClientId"] ?? "client");
        return new ScimOutboundProvisioningTarget(factory.Object, secrets, configuration);
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
