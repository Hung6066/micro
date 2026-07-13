using Microsoft.AspNetCore.Builder;

namespace His.Hope.Infrastructure.Security;

public static class SecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<RateLimitingMiddleware>();
}
