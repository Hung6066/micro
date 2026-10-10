using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using His.Hope.IdentityService.Api.Configuration;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.ServiceDefaults;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Api.Services;

public interface IPushDeliveryService
{
    Task<Guid> EnqueueAsync(string userId, string title, string body, string? dataJson = null, CancellationToken cancellationToken = default);
    Task<bool> DeliverAsync(string userId, string title, string body, Guid? outboxId = null, CancellationToken cancellationToken = default);
}

public sealed class PushDeliveryService(
    IdentityDbContext db,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider protectionProvider,
    IOptions<PushProviderOptions> options,
    IFirebasePushSender firebasePushSender,
    ILogger<PushDeliveryService> logger) : IPushDeliveryService
{
    private readonly PushProviderOptions options = options.Value;

    public async Task<Guid> EnqueueAsync(string userId, string title, string body, string? dataJson = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User id is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200) throw new ArgumentException("Push title is invalid", nameof(title));
        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000) throw new ArgumentException("Push body is invalid", nameof(body));
        if (dataJson is { Length: > 8000 }) throw new ArgumentException("Notification data is too large", nameof(dataJson));

        var item = new Domain.Entities.PushNotificationOutbox
        {
            UserId = userId,
            Title = title,
            Body = body,
        };
        db.InAppNotifications.Add(new Domain.Entities.InAppNotification
        {
            UserId = userId,
            Title = title,
            Body = body,
            DataJson = dataJson
        });
        db.PushNotificationOutbox.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task<bool> DeliverAsync(string userId, string title, string body, Guid? outboxId = null, CancellationToken cancellationToken = default)
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
                    await firebasePushSender.SendAsync(token, title, body, cancellationToken);
                else if (device.Platform == "ios")
                {
                    if (!options.ApnsEnabled)
                    {
                        logger.LogWarning("APNs delivery skipped because PushProviders:ApnsEnabled is false");
                        continue;
                    }
                    await SendApnsAsync(token, title, body, cancellationToken);
                }
                else
                    continue;
                delivered = true;
                device.LastSeenAt = DateTime.UtcNow;
                db.PushDeliveryAttempts.Add(new Domain.Entities.PushDeliveryAttempt
                {
                    OutboxId = outboxId ?? Guid.Empty,
                    DeviceId = device.Id,
                    Platform = device.Platform,
                    Status = "sent"
                });
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Push delivery failed for {Platform} device {DeviceId}", device.Platform, device.Id);
                db.PushDeliveryAttempts.Add(new Domain.Entities.PushDeliveryAttempt
                {
                    OutboxId = outboxId ?? Guid.Empty,
                    DeviceId = device.Id,
                    Platform = device.Platform,
                    Status = "failed",
                    ErrorCode = ex.Message[..Math.Min(ex.Message.Length, 200)]
                });
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
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ApnsEndpoint.TrimEnd('/')}/3/device/{Uri.EscapeDataString(deviceToken)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
        request.Headers.TryAddWithoutValidation("apns-topic", options.ApnsBundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");
        request.Content = JsonContent.Create(new { aps = new { alert = new { title, body }, sound = "default" } });
        using var response = await httpClientFactory.CreateClient("external-apns").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var reason = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("APNs request failed: status={StatusCode}, reason={Reason}",
                (int)response.StatusCode, reason.Length > 500 ? reason[..500] : reason);
            throw new HttpRequestException($"APNs request failed with {(int)response.StatusCode}.");
        }
    }
}
