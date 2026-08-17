using System.Security.Claims;

namespace His.Hope.AspNetCore.Authentication;

/// <summary>
/// Canonical user identity access for His.Hope API handlers.
/// </summary>
public static class HisHopeUserClaims
{
    /// <summary>
    /// Ensures both supported subject claim names are present on an
    /// authenticated principal. This is called by the shared JWT handler.
    /// </summary>
    public static void NormalizeSubjectClaims(ClaimsPrincipal? principal)
    {
        var identity = principal?.Identity as ClaimsIdentity;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");
        if (identity is null || string.IsNullOrWhiteSpace(subject))
            return;

        if (principal!.FindFirst("sub") is null)
            identity.AddClaim(new Claim("sub", subject));
        if (principal.FindFirst(ClaimTypes.NameIdentifier) is null)
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
    }

    /// <summary>
    /// Gets the authenticated user's stable identity subject from either
    /// canonical JWT representation. Authentication middleware normally adds
    /// both representations for backwards compatibility.
    /// </summary>
    public static string? GetSubject(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub");

    /// <summary>
    /// Gets the stable subject as a Guid when the API requires a user key.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.GetSubject(), out var userId) ? userId : null;
}
