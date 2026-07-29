using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using His.Hope.IdentityService.Api.Configuration;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Api.Services;

public interface IPushDeliveryService
{
    Task<Guid> EnqueueAsync(string userId, string title, string body, CancellationToken cancellationToken = default);
    Task<bool> DeliverAsync(string userId, string title, string body, CancellationToken cancellationToken = default);
}

public sealed class PushDeliveryService(
    IdentityDbContext db,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider protectionProvider,
    IOptions<PushProviderOptions> options,
    ILogger<PushDeliveryService> logger) : IPushDeliveryService
{
    private readonly PushProviderOptions options = options.Value;

    public async Task<Guid> EnqueueAsync(string userId, string title, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User id is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200) throw new ArgumentException("Push title is invalid", nameof(title));
        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000) throw new ArgumentException("Push body is invalid", nameof(body));

        var item = new Domain.Entities.PushNotificationOutbox
        {
            UserId = userId,
            Title = title,
            Body = body,
        };
        db.PushNotificationOutbox.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task<bool> DeliverAsync(string userId, string title, string body, CancellationToken cancellationToken = default)
    {
        var devices = await db.MobileDeviceRegistrations
            .Where(device => device.UserId == userId && device.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var protector = protectionProvider.CreateProtector("HisHope.Mobile.PushToken.v1");
        // No active device is not a successful delivery. Keep the outbox item
        // retryable so a device registered after enqueue can still receive it.
        var delivered = false;

        foreach (var device in devices)
        {
            try
            {
                var token = protector.Unprotect(device.TokenCiphertext);
                if (device.Platform == "android")
                    await SendFirebaseAsync(token, title, body, cancellationToken);
                else if (device.Platform == "ios")
                    await SendApnsAsync(token, title, body, cancellationToken);
                else
                    continue;
                delivered = true;
                device.LastSeenAt = DateTime.UtcNow;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Push delivery failed for {Platform} device {DeviceId}", device.Platform, device.Id);
            }
            catch (CryptographicException)
            {
                device.RevokedAt = DateTime.UtcNow;
                logger.LogWarning("Revoked device {DeviceId} because its protected token cannot be decrypted", device.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return delivered;
    }

    private async Task SendFirebaseAsync(string deviceToken, string title, string body, CancellationToken ct)
    {
        using var credentials = JsonDocument.Parse(options.FirebaseCredentialsJson);
        var root = credentials.RootElement;
        var projectId = root.GetProperty("project_id").GetString()!;
        var clientEmail = root.GetProperty("client_email").GetString()!;
        var privateKey = root.GetProperty("private_key").GetString()!;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: clientEmail,
            audience: "https://oauth2.googleapis.com/token",
            claims: new[] { new System.Security.Claims.Claim("scope", "https://www.googleapis.com/auth/firebase.messaging") },
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        var assertion = new JwtSecurityTokenHandler().WriteToken(jwt);
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            })
        };
        var tokenResponse = await httpClientFactory.CreateClient().SendAsync(tokenRequest, ct);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(projectId)}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { message = new { token = deviceToken, notification = new { title, body } } });
        (await httpClientFactory.CreateClient().SendAsync(request, ct)).EnsureSuccessStatusCode();
    }

    private async Task SendApnsAsync(string deviceToken, string title, string body, CancellationToken ct)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(options.ApnsPrivateKey);
        var jwt = new JwtSecurityToken(
            new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(ecdsa) { KeyId = options.ApnsKeyId }, SecurityAlgorithms.EcdsaSha256)),
            new JwtPayload(
                issuer: options.ApnsTeamId,
                audience: null,
                claims: null,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(20)));
        var bearer = new JwtSecurityTokenHandler().WriteToken(jwt);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.push.apple.com/3/device/{Uri.EscapeDataString(deviceToken)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
        request.Headers.TryAddWithoutValidation("apns-topic", options.ApnsBundleId);
        request.Content = JsonContent.Create(new { aps = new { alert = new { title, body }, sound = "default" } });
        (await httpClientFactory.CreateClient().SendAsync(request, ct)).EnsureSuccessStatusCode();
    }
}
