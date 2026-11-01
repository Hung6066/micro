using Microsoft.AspNetCore.Http;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.Bff.Core.Authentication;

public sealed record SessionCookieOptions
{
    public const string SectionName = "Bff:SessionCookie";
    public string CookieName { get; init; } = HisHopeProtocolConstants.Cookies.BrowserSession;
    public string CookieDomain { get; init; } = "";
    public string CookiePath { get; init; } = "/api";
    public int CookieMaxAgeSeconds { get; init; } = 3600;
    public bool Secure { get; init; } = true;
    public bool HttpOnly { get; init; } = true;
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;
}
