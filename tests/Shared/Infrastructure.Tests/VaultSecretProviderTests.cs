using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using His.Hope.Secrets;
using Microsoft.Extensions.Options;

namespace His.Hope.Infrastructure.Tests;

public sealed class VaultSecretProviderTests
{
    [Fact]
    public async Task Get_reads_kv_v2_secret_from_shared_adapter()
    {
        var factory = new FakeHttpClientFactory(request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.PathAndQuery.Should().Be("/v1/secret/data/his-hope/identity/client-secrets/client-a");
            request.Headers.GetValues("X-Vault-Token").Single().Should().Be("vault-token");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { data = new { data = new { secret = "value-a" } } })
            };
        });
        var provider = Create(factory);

        var result = await provider.GetAsync("identity/client-secrets/client-a", "secret");

        result.Should().Be("value-a");
    }

    [Fact]
    public async Task Put_and_delete_use_the_same_shared_kv_adapter()
    {
        var methods = new List<HttpMethod>();
        var factory = new FakeHttpClientFactory(request =>
        {
            methods.Add(request.Method);
            request.RequestUri!.PathAndQuery.Should().Be("/v1/secret/data/his-hope/service-a");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = Create(factory);

        await provider.PutAsync("service-a", "password", "value");
        await provider.DeleteAsync("service-a");

        methods.Should().Equal(HttpMethod.Post, HttpMethod.Delete);
    }

    [Fact]
    public async Task Traversal_paths_are_rejected_before_http()
    {
        var factory = new FakeHttpClientFactory(_ => throw new InvalidOperationException("HTTP must not be called"));
        var provider = Create(factory);

        var act = () => provider.GetAsync("service/../other", "secret");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static VaultSecretProvider Create(IHttpClientFactory factory) => new(
        factory,
        new StaticOptionsMonitor<VaultOptions>(new VaultOptions
        {
            Address = "https://vault.test",
            SecretsMount = "secret",
            SecretsPathPrefix = "his-hope"
        }),
        new FakeVaultTokenProvider());

    private sealed class FakeVaultTokenProvider : IVaultTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken ct = default) => Task.FromResult("vault-token");
        public Task InvalidateAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new CallbackHandler(handler))
        {
            BaseAddress = new Uri("https://vault.test")
        };
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(callback(request));
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
