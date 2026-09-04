using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class IdentityLocalizationEndpoints
{
    public static void MapIdentityLocalizationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/localization", async (
            string[]? key,
            string? locale,
            string? facilityId,
            HttpContext httpContext,
            IdentityDbContext db,
            FacilityContext facilityContext,
            CancellationToken ct) =>
        {
            var requestedLocale = NormalizeLocale(locale ?? httpContext.Request.Headers["Accept-Language"].ToString());
            var keys = (key ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToArray();
            var scopeId = !facilityContext.IsCrossFacility && !string.IsNullOrWhiteSpace(facilityContext.FacilityId)
                ? facilityContext.FacilityId
                : string.IsNullOrWhiteSpace(facilityId) ? IdentityScope.Global : facilityId.Trim();
            var query = db.LocalizationTranslations.AsNoTracking()
                .Where(translation => (translation.ScopeId == IdentityScope.Global || translation.ScopeId == scopeId) &&
                    (translation.Locale == requestedLocale || translation.Locale == "vi-VN"));
            if (keys.Length > 0) query = query.Where(translation => keys.Contains(translation.ResourceKey));

            var translations = await query.ToListAsync(ct);
            var values = translations
                .GroupBy(translation => translation.ResourceKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group
                    .OrderByDescending(translation => translation.ScopeId == scopeId)
                    .ThenByDescending(translation => translation.Locale.Equals(requestedLocale, StringComparison.OrdinalIgnoreCase))
                    .Select(translation => translation.Value)
                    .First(), StringComparer.OrdinalIgnoreCase);

            return Results.Ok(new { locale = requestedLocale, fallbackLocale = "vi-VN", values });
        }).AllowAnonymous();
    }

    private static string NormalizeLocale(string value)
    {
        var candidate = value.Split(',', ';')[0].Trim();
        if (candidate.Equals("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return candidate.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "vi-VN";
    }
}
