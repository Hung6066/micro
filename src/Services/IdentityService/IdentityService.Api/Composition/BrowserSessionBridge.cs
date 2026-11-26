using System.Text.Json;
using System.Text.Json.Nodes;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Composition;

/// <summary>
/// Helpers for the hishop_sid session bridge in <see cref="IdentityServicePipelineExtensions"/>.
/// </summary>
internal static class BrowserSessionBridge
{
    internal const string ExpiredItemKey = "HisHope.BrowserSessionExpired";

    internal static readonly TimeSpan PrivilegedIdleWindow = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan StandardIdleWindow = TimeSpan.FromMinutes(30);

    /// <summary>Skip the Redis write while less than this much of the idle window has been used.</summary>
    internal static readonly TimeSpan SlideThreshold = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The request carried a session cookie whose session has ended. Authentication
    /// must then answer 401 instead of falling back to the Identity cookie: that
    /// cookie has no human principal, super_admin or portal_class claims, so the
    /// request would be authenticated but forbidden (403) and the browser would
    /// never re-exchange its session.
    /// </summary>
    internal static void MarkExpired(HttpContext context) => context.Items[ExpiredItemKey] = true;

    internal static bool IsExpired(HttpContext context) =>
        context.Items.TryGetValue(ExpiredItemKey, out var value) && value is true;

    /// <summary>
    /// Moves IdleExpiresAt to now + idle window (15 min privileged, 30 min otherwise),
    /// never past AbsoluteExpiresAt, and keeps every other field of the stored session.
    /// Returns the JSON written, or null when nothing changed.
    /// </summary>
    internal static async Task<string?> SlideIdleExpiryAsync(
        IDatabase db, string sessionId, string sessionJson, DateTimeOffset now)
    {
        var updated = SlideIdleExpiry(sessionJson, now, out var ttl);
        if (updated is null)
            return null;
        await db.StringSetAsync($"session:{sessionId}", updated, ttl);
        return updated;
    }

    internal static string? SlideIdleExpiry(string sessionJson, DateTimeOffset now, out TimeSpan ttl)
    {
        ttl = TimeSpan.Zero;
        JsonObject? session;
        try
        {
            session = JsonNode.Parse(sessionJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
        if (session is null || ReadTime(session, "IdleExpiresAt") is not { } idle)
            return null;

        var privileged = session["IsPrivileged"] is JsonValue flag && flag.TryGetValue<bool>(out var isPrivileged) && isPrivileged;
        var absolute = ReadTime(session, "AbsoluteExpiresAt");
        var refreshed = now.Add(privileged ? PrivilegedIdleWindow : StandardIdleWindow);
        if (absolute is { } absoluteLimit && refreshed > absoluteLimit)
            refreshed = absoluteLimit;
        if (refreshed - idle < SlideThreshold)
            return null;

        // Keep the record until the absolute limit (or at least the token expiry) so a
        // session that ends by idling is still recognised as ended and answers 401.
        var tokenExpiry = ReadTime(session, "ExpiresAt");
        var expiry = absolute ?? tokenExpiry ?? refreshed;
        if (tokenExpiry is { } token && token > expiry)
            expiry = token;
        ttl = expiry - now;
        if (ttl <= TimeSpan.Zero)
            return null;

        session["IdleExpiresAt"] = refreshed;
        return session.ToJsonString();
    }

    private static DateTimeOffset? ReadTime(JsonObject session, string name) =>
        session[name] is JsonValue value && value.TryGetValue<DateTimeOffset>(out var time) ? time : null;
}
