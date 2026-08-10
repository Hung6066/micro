using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class ConsentEndpoints
{
    public static RouteGroupBuilder MapConsentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserConsents);
        group.MapPost("/", GrantConsent);
        group.MapDelete("/{clientId}", RevokeConsent);
        return group;
    }

    private static async Task<Ok<List<ConsentResponse>>> GetUserConsents(
        HttpContext httpContext, IdentityDbContext db, CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return TypedResults.Ok(new List<ConsentResponse>());

        var consents = await db.ClientConsents
            .Where(c => c.UserId == userGuid && c.IsActive)
            .OrderByDescending(c => c.GrantedAt)
            .ToListAsync(ct);

        var response = consents.Select(c => new ConsentResponse(
            c.Id.ToString(),
            c.ClientId,
            ParseScopes(c.Scopes),
            c.GrantedAt,
            c.ExpiresAt
        )).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ConsentResponse>, ProblemHttpResult>> GrantConsent(
        GrantConsentRequest request,
        HttpContext httpContext, IdentityDbContext db, CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return TypedResults.Problem("Not authenticated", statusCode: 401);

        var existing = await db.ClientConsents
            .FirstOrDefaultAsync(c => c.UserId == userGuid && c.ClientId == request.ClientId, ct);

        if (existing is not null)
        {
            existing.Scopes = JsonSerializer.Serialize(request.Scopes);
            existing.GrantedAt = DateTime.UtcNow;
            existing.IsActive = true;
            existing.RevokedAt = null;
        }
        else
        {
            db.ClientConsents.Add(new ClientConsent
            {
                UserId = userGuid,
                ClientId = request.ClientId,
                Scopes = JsonSerializer.Serialize(request.Scopes),
                GrantedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new ConsentResponse(
            (existing?.Id ?? Guid.NewGuid()).ToString(),
            request.ClientId,
            request.Scopes,
            DateTime.UtcNow,
            null));
    }

    private static async Task<Results<NoContent, NotFound>> RevokeConsent(
        string clientId, HttpContext httpContext, IdentityDbContext db, CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return TypedResults.NotFound();

        var consent = await db.ClientConsents
            .FirstOrDefaultAsync(c => c.UserId == userGuid && c.ClientId == clientId && c.IsActive, ct);

        if (consent is null) return TypedResults.NotFound();

        consent.IsActive = false;
        consent.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static List<string> ParseScopes(string scopesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(scopesJson) ?? new();
        }
        catch { return new(); }
    }
}

public record ConsentResponse(string Id, string ClientId, List<string> Scopes, DateTime GrantedAt, DateTime? ExpiresAt);
public record GrantConsentRequest(string ClientId, List<string> Scopes);
