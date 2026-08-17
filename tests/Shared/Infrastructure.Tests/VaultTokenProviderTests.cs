using His.Hope.Secrets;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace His.Hope.Infrastructure.Tests;

public sealed class VaultTokenProviderTests
{
    [Fact]
    public async Task Production_rejects_approle_even_when_configured()
    {
        var options = new VaultOptions
        {
            Address = "https://vault.test",
            AuthMethod = "approle",
            Role = "patient-service",
            RoleId = "role",
            SecretId = "secret",
            AllowStaticToken = false
        };
        var provider = CreateProvider(options, "Production", new FakeHttpClientFactory(_ => throw new InvalidOperationException("HTTP must not be called")));

        var act = () => provider.GetTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AppRole*forbidden*");
    }

    [Fact]
    public async Task Spiffe_jwt_uses_configured_jwt_mount_and_returns_token()
    {
        var tokenFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tokenFile, "signed-svid");
        try
        {
            var options = new VaultOptions
            {
                Address = "https://vault.test",
                AuthMethod = "spiffe-jwt",
                AuthMount = "jwt-spiffe",
                Role = "patient-service",
                SpiffeJwtTokenFile = tokenFile,
                AllowStaticToken = false
            };
            var factory = new FakeHttpClientFactory(request =>
            {
                request.RequestUri!.PathAndQuery.Should().Be("/v1/auth/jwt-spiffe/login");
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                body.Should().Contain("signed-svid");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { auth = new { client_token = "vault-token", lease_duration = 900 } })
                };
            });
            var provider = CreateProvider(options, "Production", factory);

            (await provider.GetTokenAsync()).Should().Be("vault-token");
        }
        finally
        {
            File.Delete(tokenFile);
        }
    }

    private static VaultTokenProvider CreateProvider(VaultOptions options, string environmentName, IHttpClientFactory factory)
        => new(factory, new StaticOptionsMonitor<VaultOptions>(options), new TestHostEnvironment { EnvironmentName = environmentName }, NullLogger<VaultTokenProvider>.Instance);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable OnChange(Action<T, string> listener) => new NoopDisposable();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(responder)) { BaseAddress = new Uri("https://vault.test") };

        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(responder(request));
        }
    }
}
