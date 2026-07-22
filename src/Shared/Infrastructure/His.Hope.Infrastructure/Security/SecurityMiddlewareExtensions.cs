using His.Hope.Infrastructure.Abuse;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.Security;

public static class SecurityMiddlewareExtensions
{
    public static IServiceCollection AddHisHopeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<PerUserRateLimitingMiddleware>();
}
