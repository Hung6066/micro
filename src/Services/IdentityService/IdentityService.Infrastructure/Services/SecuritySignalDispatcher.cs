using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>Best-effort SSF/CAEP transmitter backed by a durable outbox.</summary>
public sealed class SecuritySignalDispatcher(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IVaultKeyProvider keyProvider,
    IConfiguration configuration,
    ILogger<SecuritySignalDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchBatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Security signal dispatch cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        var enabled = configuration.GetValue("SSF_ENABLED", configuration.GetValue("SecuritySignals:Enabled", false));
        if (!enabled) return;

        var subscriptions = configuration.GetSection("SecuritySignals:Subscriptions").GetChildren()
            .Select(section => new { Url = section["Url"], Audience = section["Audience"] })
            .Where(subscription => Uri.TryCreate(subscription.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .ToArray();
        if (subscriptions.Length == 0) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var entries = await db.SecuritySignalOutbox
            .Where(item => item.DispatchedAt == null && item.AvailableAt <= DateTime.UtcNow)
            .OrderBy(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        foreach (var entry in entries)
        {
            try
            {
                foreach (var subscription in subscriptions)
                {
                    var token = await CreateSetAsync(entry, subscription.Audience, ct);
                    using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url);
                    request.Content = new StringContent(token, System.Text.Encoding.UTF8, "application/secevent+jwt");
                    using var response = await httpClientFactory.CreateClient("security-signals").SendAsync(request, ct);
                    response.EnsureSuccessStatusCode();
                }
                entry.DispatchedAt = DateTime.UtcNow;
                entry.LastError = null;
            }
            catch (Exception ex)
            {
                entry.Attempts++;
                entry.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                entry.AvailableAt = DateTime.UtcNow.AddMinutes(Math.Min(entry.Attempts, 30));
                logger.LogWarning(ex, "Security signal {SignalId} delivery failed on attempt {Attempt}", entry.Id, entry.Attempts);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> CreateSetAsync(SecuritySignalOutbox entry, string? audience, CancellationToken ct)
    {
        var key = await keyProvider.GetSigningKeyAsync(ct);
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = configuration["OpenIddict:Issuer"],
            Audience = audience,
            IssuedAt = entry.CreatedAt,
            Claims = new Dictionary<string, object>
            {
                ["jti"] = entry.Id.ToString(),
                ["events"] = new Dictionary<string, object?>
                {
                    [MapEventType(entry.EventType)] = JsonSerializer.Deserialize<JsonElement>(entry.PayloadJson)
                }
            },
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                ["typ"] = "secevent+jwt"
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };
        var jwt = handler.CreateToken(descriptor) as JwtSecurityToken
            ?? throw new InvalidOperationException("Unable to create SSF SET token.");
        jwt.Header["typ"] = "secevent+jwt";
        return handler.WriteToken(jwt);
    }

    private static string MapEventType(string eventType)
    {
        var normalized = eventType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "logout" or "session-revoked" or "session_revoked" =>
                "https://schemas.openid.net/secevent/caep/event-type/session-revoked",
            "credential-change" or "credential_changed" or "password-change" =>
                "https://schemas.openid.net/secevent/caep/event-type/credential-change",
            "mfa-device-change" or "mfa_device_changed" =>
                "https://schemas.openid.net/secevent/caep/event-type/mfa-device-change",
            _ => $"https://his-hope.com/secevent/event-type/{Uri.EscapeDataString(normalized)}"
        };
    }
}
