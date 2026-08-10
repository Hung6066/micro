using Microsoft.AspNetCore.Http;

namespace His.Hope.Bff.Core.Authentication;

public sealed record SessionCookieOptions
{
    public const string SectionName = "Bff:SessionCookie";
    public string CookieName { get; init; } = "hishop_sid";
    public string CookieDomain { get; init; } = "";
    public string CookiePath { get; init; } = "/api";
    public int CookieMaxAgeSeconds { get; init; } = 3600;
    public bool Secure { get; init; } = true;
    public bool HttpOnly { get; init; } = true;
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;
}
