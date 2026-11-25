using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.Infrastructure.Security;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Services;

/// <summary>
/// Ends every session of a user: access tokens (revocation timestamp honoured
/// by all services), OpenIddict authorizations and refresh/reference tokens,
/// and the cached security version so the change applies on the next request.
/// Every deactivation path uses this so that a leaver cannot keep working
/// until token expiry just because they were disabled through SCIM, a bulk
/// action or the HR feed instead of the single-user admin endpoint.
/// </summary>
internal static class UserSessionRevocation
{
    public static async Task RevokeAllAsync(IServiceProvider services, Guid userId, CancellationToken ct)
    {
        await services.GetRequiredService<ITokenBlacklistService>().RevokeAllUserTokensAsync(userId.ToString(), ct);
        await services.GetRequiredService<IOpenIddictSubjectRevoker>().RevokeAllAsync(userId, ct);
        if (services.GetService<IConnectionMultiplexer>() is { } redis)
            await redis.GetDatabase().KeyDeleteAsync($"hishop:identity:security-version:{userId}");
    }
}
