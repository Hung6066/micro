using System.Net.Http.Headers;
using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace His.Hope.Configuration;

public sealed class ServiceToServiceAuthenticationOptions
{
    public const string SectionName = "ServiceToServiceAuthentication";
    public bool Enabled { get; set; } = true;
    public bool PropagateUserToken { get; set; } = true;
    public bool RequireServiceToken { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
    public string? Audience { get; set; }
}

public static class ServiceToServiceAuthenticationExtensions
{
    public static IServiceCollection AddHisHopeServiceToServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ServiceToServiceAuthenticationOptions>()
            .Bind(configuration.GetSection(ServiceToServiceAuthenticationOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddHttpClient("his-hope-service-auth-token");
        services.AddSingleton<IServiceAccessTokenProvider, ServiceAccessTokenProvider>();
        services.AddSingleton<ServiceAuthorizationCallCredentials>();
        services.AddTransient<ServiceAuthorizationHandler>();
        return services;
    }

    public static IHttpClientBuilder AddHisHopeServiceAuthentication(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<ServiceAuthorizationHandler>();
}

public interface IServiceAccessTokenProvider
{
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

internal sealed class ServiceAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptionsMonitor<ServiceToServiceAuthenticationOptions> optionsMonitor)
    : IServiceAccessTokenProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.Enabled)
            return null;

        if (options.PropagateUserToken)
        {
            var rawAuthorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (AuthenticationHeaderValue.TryParse(rawAuthorization, out var header) &&
                string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(header.Parameter))
                return header.Parameter;
        }

        if (string.IsNullOrWhiteSpace(options.TokenEndpoint) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            if (options.RequireServiceToken)
                throw new InvalidOperationException(
                    "ServiceToServiceAuthentication requires TokenEndpoint, ClientId and ClientSecret for a background call.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
            return _accessToken;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
                return _accessToken;

            using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(BuildTokenForm(options))
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await httpClientFactory
                .CreateClient("his-hope-service-auth-token")
                .SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("The service token endpoint returned an empty response.");
            if (string.IsNullOrWhiteSpace(token.AccessToken))
                throw new InvalidOperationException("The service token endpoint did not return access_token.");

            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn, 60));
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Dictionary<string, string> BuildTokenForm(ServiceToServiceAuthenticationOptions options)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId!,
            ["client_secret"] = options.ClientSecret!
        };
        if (!string.IsNullOrWhiteSpace(options.Scope)) form["scope"] = options.Scope;
        if (!string.IsNullOrWhiteSpace(options.Audience)) form["audience"] = options.Audience;
        return form;
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn = 300);
}

internal sealed class ServiceAuthorizationHandler(IServiceAccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var token = await tokenProvider.GetTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

internal sealed class ServiceAuthorizationCallCredentials(IServiceAccessTokenProvider tokenProvider)
{
    public async Task ApplyAsync(AuthInterceptorContext _, Metadata metadata)
    {
        if (metadata.Any(item => string.Equals(item.Key, "authorization", StringComparison.OrdinalIgnoreCase)))
            return;

        var token = await tokenProvider.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            metadata.Add("authorization", $"Bearer {token}");
    }
}
