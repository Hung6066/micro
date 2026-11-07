using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.ServiceDefaults;

public interface IExternalEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

public interface IFirebasePushSender
{
    Task SendAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default);
}

public interface IPaymentProvider
{
    Task<PaymentProviderResult> AuthorizeAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProviderResult> CaptureAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProviderResult> RefundAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default);
}

public interface IShipmentProvider
{
    Task<ShipmentProviderResult> CreateAsync(ShipmentProviderRequest request, CancellationToken cancellationToken = default);
    Task<ShipmentProviderResult> DispatchAsync(ShipmentProviderRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(ShipmentProviderRequest request, CancellationToken cancellationToken = default);
}

public sealed record PaymentProviderRequest(
    Guid OrderId, string TenantKey, decimal Amount, string Currency, string IdempotencyKey,
    string? ProviderPaymentId = null);

public sealed record PaymentProviderResult(
    bool Succeeded, string ProviderPaymentId, string? FailureCode = null);

public sealed record ShipmentProviderRequest(
    Guid OrderId, string TenantKey, string IdempotencyKey, string? ProviderShipmentId = null);

public sealed record ShipmentProviderResult(
    bool Succeeded, string ProviderShipmentId, string? FailureCode = null);

public static class ExternalProviderBindingExtensions
{
    public static IServiceCollection AddHisHopeExternalProviderBindings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmailProviderOptions>()
            .Bind(configuration.GetSection(EmailProviderOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<SmsProviderOptions>()
            .Bind(configuration.GetSection(SmsProviderOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<FirebaseProviderOptions>()
            .Bind(configuration.GetSection(FirebaseProviderOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.CredentialsJson))
                {
                    options.CredentialsJson = configuration["PushProviders:FirebaseCredentialsJson"] ?? string.Empty;
                    options.CredentialsFile = configuration["PushProviders:FirebaseCredentialsFile"] ?? options.CredentialsFile;
                    options.CredentialsSecretPath = configuration["PushProviders:FirebaseCredentialsSecretPath"] ?? options.CredentialsSecretPath;
                    options.CredentialsSecretKey = configuration["PushProviders:FirebaseCredentialsSecretKey"] ?? options.CredentialsSecretKey;
                    options.Enabled = options.Enabled || !string.IsNullOrWhiteSpace(options.CredentialsJson) || !string.IsNullOrWhiteSpace(options.CredentialsFile) || !string.IsNullOrWhiteSpace(options.CredentialsSecretPath);
                }
                if (string.IsNullOrWhiteSpace(options.CredentialsJson) &&
                    !string.IsNullOrWhiteSpace(options.CredentialsFile) &&
                    File.Exists(options.CredentialsFile))
                    options.CredentialsJson = File.ReadAllText(options.CredentialsFile);
            })
            .ValidateOnStart();
        services.AddOptions<PaymentProviderOptions>()
            .Bind(configuration.GetSection(PaymentProviderOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<ShipmentProviderOptions>()
            .Bind(configuration.GetSection(ShipmentProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddHisHopeExternalHttpClient("external-email", "external.email");
        services.AddHisHopeExternalHttpClient("external-sms", "external.sms");
        services.AddHisHopeExternalHttpClient("firebase-oauth", "external.firebase.oauth", client =>
            client.BaseAddress = new Uri("https://oauth2.googleapis.com/"));
        services.AddHisHopeExternalHttpClient("firebase-messaging", "external.firebase.messaging");
        services.AddHisHopeExternalHttpClient("external-apns", "external.apns");
        services.AddHisHopeExternalHttpClient("external-payment", "external.payment");
        services.AddHisHopeExternalHttpClient("external-shipment", "external.shipment");
        services.AddSingleton<IExternalEmailSender, HttpExternalEmailSender>();
        services.AddSingleton<ISmsSender, HttpSmsSender>();
        services.AddSingleton<IFirebasePushSender, FirebasePushSender>();
        services.AddSingleton<IPaymentProvider, HttpPaymentProvider>();
        services.AddSingleton<IShipmentProvider, HttpShipmentProvider>();
        return services;
    }
}

internal sealed class HttpPaymentProvider(
    IHttpClientFactory clients,
    IVaultSecretProvider secrets,
    IOptions<PaymentProviderOptions> settings,
    ILogger<HttpPaymentProvider> logger) : IPaymentProvider
{
    public Task<PaymentProviderResult> AuthorizeAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync("authorize", request, cancellationToken);

    public Task<PaymentProviderResult> CaptureAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync("capture", request, cancellationToken);

    public Task<PaymentProviderResult> RefundAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync("refund", request, cancellationToken);

    private async Task<PaymentProviderResult> SendAsync(string operation, PaymentProviderRequest request, CancellationToken ct)
    {
        var options = settings.Value;
        if (!options.Enabled || options.Provider.Equals("not-configured", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment provider is not configured.");
        if (!options.Provider.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException("Payment provider requires a valid HTTP endpoint.");

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, operation))
        {
            Content = JsonContent.Create(request)
        };
        await HttpExternalEmailSender.AddApiKeyAsync(message, options.ApiKeySecretPath, options.ApiKeySecretKey, secrets, ct);
        using var response = await clients.CreateClient("external-payment").SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Payment provider operation {Operation} failed with status {StatusCode}", operation, (int)response.StatusCode);
            throw new HttpRequestException($"Payment provider operation failed with {(int)response.StatusCode}.");
        }
        var result = await response.Content.ReadFromJsonAsync<PaymentProviderResult>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Payment provider returned an empty response.");
    }
}

internal sealed class HttpShipmentProvider(
    IHttpClientFactory clients,
    IVaultSecretProvider secrets,
    IOptions<ShipmentProviderOptions> settings,
    ILogger<HttpShipmentProvider> logger) : IShipmentProvider
{
    public Task<ShipmentProviderResult> CreateAsync(ShipmentProviderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync("shipments", HttpMethod.Post, request, cancellationToken);

    public Task<ShipmentProviderResult> DispatchAsync(ShipmentProviderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync($"shipments/{Uri.EscapeDataString(request.ProviderShipmentId ?? throw new InvalidOperationException("Provider shipment id is required."))}/dispatch", HttpMethod.Post, request, cancellationToken);

    public async Task CancelAsync(ShipmentProviderRequest request, CancellationToken cancellationToken = default)
    {
        await SendAsync($"shipments/{Uri.EscapeDataString(request.ProviderShipmentId ?? throw new InvalidOperationException("Provider shipment id is required."))}", HttpMethod.Delete, request, cancellationToken);
    }

    private async Task<ShipmentProviderResult> SendAsync(string path, HttpMethod method, ShipmentProviderRequest request, CancellationToken ct)
    {
        var options = settings.Value;
        if (!options.Enabled || options.Provider.Equals("not-configured", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Shipment provider is not configured.");
        if (!options.Provider.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException("Shipment provider requires a valid HTTP endpoint.");

        using var message = new HttpRequestMessage(method, new Uri(baseUri, path))
        {
            Content = JsonContent.Create(request)
        };
        await HttpExternalEmailSender.AddApiKeyAsync(message, options.ApiKeySecretPath, options.ApiKeySecretKey, secrets, ct);
        using var response = await clients.CreateClient("external-shipment").SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Shipment provider operation {Path} failed with status {StatusCode}", path, (int)response.StatusCode);
            throw new HttpRequestException($"Shipment provider operation failed with {(int)response.StatusCode}.");
        }
        if (method == HttpMethod.Delete)
            return new ShipmentProviderResult(true, request.ProviderShipmentId!);
        var result = await response.Content.ReadFromJsonAsync<ShipmentProviderResult>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Shipment provider returned an empty response.");
    }
}

internal sealed class HttpExternalEmailSender(
    IHttpClientFactory clients,
    IVaultSecretProvider secrets,
    IOptions<EmailProviderOptions> settings,
    ILogger<HttpExternalEmailSender> logger) : IExternalEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var options = settings.Value;
        if (!options.Enabled || options.Provider.Equals("noop", StringComparison.OrdinalIgnoreCase)) return;
        if (!options.Provider.Equals("http", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("External email provider is not configured with a valid HTTP endpoint.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new { to, subject, body, from = options.From })
        };
        await AddApiKeyAsync(request, options.ApiKeySecretPath, options.ApiKeySecretKey, secrets, cancellationToken);
        using var response = await clients.CreateClient("external-email").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("External email provider failed with status {StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException($"External email provider failed with {(int)response.StatusCode}.");
        }
    }

    internal static async Task AddApiKeyAsync(HttpRequestMessage request, string path, string key, IVaultSecretProvider secrets, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("External provider API key Vault path is required.");
        var secret = await secrets.GetAsync(path, key, ct);
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("External provider API key is missing from Vault.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
    }
}

internal sealed class HttpSmsSender(
    IHttpClientFactory clients,
    IVaultSecretProvider secrets,
    IOptions<SmsProviderOptions> settings,
    ILogger<HttpSmsSender> logger) : ISmsSender
{
    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var options = settings.Value;
        if (!options.Enabled || options.Provider.Equals("noop", StringComparison.OrdinalIgnoreCase)) return;
        if (!options.Provider.Equals("http", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("External SMS provider is not configured with a valid HTTP endpoint.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new { to = phoneNumber, message, senderId = options.SenderId })
        };
        await HttpExternalEmailSender.AddApiKeyAsync(request, options.ApiKeySecretPath, options.ApiKeySecretKey, secrets, cancellationToken);
        using var response = await clients.CreateClient("external-sms").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("External SMS provider failed with status {StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException($"External SMS provider failed with {(int)response.StatusCode}.");
        }
    }
}

internal sealed class FirebasePushSender(
    IHttpClientFactory clients,
    IVaultSecretProvider secrets,
    IOptions<FirebaseProviderOptions> settings) : IFirebasePushSender
{
    public async Task SendAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default)
    {
        var options = settings.Value;
        if (!options.Enabled) return;
        var credentialsJson = options.CredentialsJson;
        if (string.IsNullOrWhiteSpace(credentialsJson) && !string.IsNullOrWhiteSpace(options.CredentialsSecretPath))
            credentialsJson = await secrets.GetAsync(options.CredentialsSecretPath, options.CredentialsSecretKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(credentialsJson)) throw new InvalidOperationException("Firebase credentials are not configured.");
        using var credentials = JsonDocument.Parse(credentialsJson);
        var root = credentials.RootElement;
        var projectId = root.GetProperty("project_id").GetString()!;
        var clientEmail = root.GetProperty("client_email").GetString()!;
        var privateKey = root.GetProperty("private_key").GetString()!;
        using var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(privateKey);
        var now = DateTime.UtcNow;
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(clientEmail, "https://oauth2.googleapis.com/token", new[]
        {
            new System.Security.Claims.Claim("scope", "https://www.googleapis.com/auth/firebase.messaging"),
            new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), System.Security.Claims.ClaimValueTypes.Integer64)
        }, now, now.AddMinutes(5), new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256));
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "token") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt) }) };
        using var tokenResponse = await clients.CreateClient("firebase-oauth").SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(projectId)}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { message = new { token = deviceToken, notification = new { title, body }, android = new { notification = new { channel_id = "his_hope_default", sound = "default" } } } });
        using var response = await clients.CreateClient("firebase-messaging").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
