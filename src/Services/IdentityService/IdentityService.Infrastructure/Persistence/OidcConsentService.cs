using System.Text.Json;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Infrastructure.Persistence;

public sealed class OidcConsentService(IdentityDbContext db) : IOidcConsentService
{
    public async Task<OidcConsentClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var application = await db.OpenIddictApplications
            .AsNoTracking()
            .FirstOrDefaultAsync(application => application.ClientId == clientId, cancellationToken);
        if (application is null)
            return null;

        return new OidcConsentClient(
            application.ClientId ?? string.Empty,
            application.DisplayName ?? application.ClientId ?? string.Empty,
            ParseStringList(application.RedirectUris));
    }

    public async Task SaveConsentAsync(
        Guid userId,
        string clientId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var consent = await db.ClientConsents
            .FirstOrDefaultAsync(item => item.UserId == userId && item.ClientId == clientId, cancellationToken);
        if (consent is null)
        {
            consent = new ClientConsent { UserId = userId, ClientId = clientId };
            db.ClientConsents.Add(consent);
        }

        consent.Scopes = JsonSerializer.Serialize(scopes);
        consent.GrantedAt = DateTime.UtcNow;
        consent.IsActive = true;
        consent.RevokedAt = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
