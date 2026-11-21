using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using His.Hope.Secrets;
using His.Hope.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.ServiceDefaults.Tests;

public sealed class ExternalProviderBindingTests
{
    [Fact]
    public async Task AddBindings_ResolvesAllProviderPorts_AndNoopDoesNotCallNetwork()
    {
        var handler = new RecordingHandler();
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ExternalProviders:Email:Provider"] = "noop",
            ["ExternalProviders:Sms:Provider"] = "noop",
            ["ExternalProviders:Firebase:Enabled"] = "false"
        }, handler);

        await provider.GetRequiredService<IExternalEmailSender>().SendAsync("a@example.test", "subject", "body");
        await provider.GetRequiredService<ISmsSender>().SendAsync("+84123456789", "message");
        await provider.GetRequiredService<IFirebasePushSender>().SendAsync("device-token", "title", "body");

        Assert.NotNull(provider.GetRequiredService<IExternalEmailSender>());
        Assert.NotNull(provider.GetRequiredService<ISmsSender>());
        Assert.NotNull(provider.GetRequiredService<IFirebasePushSender>());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EmailAndSmsBindings_ReadApiKeyFromVault_AndSendThroughNamedClients()
    {
        var handler = new RecordingHandler();
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ExternalProviders:Email:Enabled"] = "true",
            ["ExternalProviders:Email:Provider"] = "http",
            ["ExternalProviders:Email:Endpoint"] = "https://email.example.test/send",
            ["ExternalProviders:Email:ApiKeySecretPath"] = "external/email",
            ["ExternalProviders:Sms:Enabled"] = "true",
            ["ExternalProviders:Sms:Provider"] = "http",
            ["ExternalProviders:Sms:Endpoint"] = "https://sms.example.test/send",
            ["ExternalProviders:Sms:ApiKeySecretPath"] = "external/sms"
        }, handler, new Dictionary<(string Path, string Key), string>
        {
            [("external/email", "api_key")] = "email-secret",
            [("external/sms", "api_key")] = "sms-secret"
        });

        await provider.GetRequiredService<IExternalEmailSender>().SendAsync("a@example.test", "subject", "body");
        await provider.GetRequiredService<ISmsSender>().SendAsync("+84123456789", "message");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer email-secret", handler.Requests[0].Authorization?.ToString());
        Assert.Equal("Bearer sms-secret", handler.Requests[1].Authorization?.ToString());
        Assert.Contains("a@example.test", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("84123456789", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirebaseBinding_UsesConfiguredCredentials_AndCompletesOAuthAndMessagingCalls()
    {
        using var rsa = RSA.Create(2048);
        var credentials = JsonSerializer.Serialize(new
        {
            project_id = "his-hope-test",
            client_email = "firebase-test@his-hope.test",
            private_key = rsa.ExportPkcs8PrivateKeyPem()
        });
        var handler = new RecordingHandler();
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ExternalProviders:Firebase:Enabled"] = "true",
            ["ExternalProviders:Firebase:CredentialsJson"] = credentials
        }, handler);

        await provider.GetRequiredService<IFirebasePushSender>()
            .SendAsync("device-token", "title", "body");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("oauth2.googleapis.com", handler.Requests[0].Uri, StringComparison.Ordinal);
        Assert.Contains("fcm.googleapis.com", handler.Requests[1].Uri, StringComparison.Ordinal);
        Assert.Equal("Bearer firebase-access", handler.Requests[1].Authorization?.ToString());
    }

    private static ServiceProvider BuildProvider(
        Dictionary<string, string?> values,
        RecordingHandler handler,
        Dictionary<(string Path, string Key), string>? secrets = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHisHopeExternalProviderBindings(configuration);
        services.AddSingleton<IVaultSecretProvider>(new FakeVaultSecretProvider(secrets ?? []));
        services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(handler));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class FakeVaultSecretProvider(Dictionary<(string Path, string Key), string> values) : IVaultSecretProvider
    {
        public Task<string?> GetAsync(string path, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.TryGetValue((path, key), out var value) ? value : null);

        public Task PutAsync(string path, string key, string value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(handler, disposeHandler: false);
            if (name.Equals("firebase-oauth", StringComparison.Ordinal))
                client.BaseAddress = new Uri("https://oauth2.googleapis.com/");
            return client;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            if (request.RequestUri?.Host.Equals("oauth2.googleapis.com", StringComparison.OrdinalIgnoreCase) == true)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"firebase-access\"}"),
                    RequestMessage = request
                };
            return new HttpResponseMessage(HttpStatusCode.Accepted) { RequestMessage = request };
        }
    }

    private sealed record RecordedRequest(string Uri, AuthenticationHeaderValue? Authorization, string Body);
}
