using System.Security.Claims;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Middleware;

public sealed class SecurityVersionMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public async Task InvokeAsync(HttpContext context, IdentityDbContext db, IConnectionMultiplexer redis)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var subject = context.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var presented = context.User.FindFirstValue("securityVersion");
        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(presented))
        {
            await next(context);
            return;
        }

        var cache = redis.GetDatabase();
        var cacheKey = $"hishop:identity:security-version:{userId}";
        var current = await cache.StringGetAsync(cacheKey);
        if (current.IsNullOrEmpty)
        {
            current = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.SecurityStamp)
                .SingleOrDefaultAsync(context.RequestAborted);
            if (!current.IsNullOrEmpty)
                await cache.StringSetAsync(cacheKey, current, CacheDuration);
        }

        if (current.IsNullOrEmpty || !string.Equals(current.ToString(), presented, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
