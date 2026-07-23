using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public interface IUserSessionTracker
{
    Task AddSessionAsync(string userId, string sessionId);
    Task<string[]> GetUserSessionsAsync(string userId);
    Task ClearUserSessionsAsync(string userId);
}

public sealed class UserSessionTracker : IUserSessionTracker
{
    private readonly IDatabase _db;
    private const string UserSessionsPrefix = "HisHope:user_sessions:";

    public UserSessionTracker(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task AddSessionAsync(string userId, string sessionId)
    {
        var key = UserSessionsPrefix + userId;
        await _db.SetAddAsync(key, sessionId);
        await _db.KeyExpireAsync(key, TimeSpan.FromDays(7));
    }

    public async Task<string[]> GetUserSessionsAsync(string userId)
    {
        var key = UserSessionsPrefix + userId;
        var members = await _db.SetMembersAsync(key);
        return members.Select(m => m.ToString()).ToArray();
    }

    public async Task ClearUserSessionsAsync(string userId)
    {
        var key = UserSessionsPrefix + userId;
        await _db.KeyDeleteAsync(key);
    }
}
