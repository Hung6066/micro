using System.Text.Json;
using His.Hope.IdentityService.Application.Interfaces;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class RedisWorkloadSessionStore(IConnectionMultiplexer redis) : IWorkloadSessionStore
{
    private const string Prefix = "HisHope:workload_sessions:";

    public async Task RegisterAsync(WorkloadSessionRecord session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redis.GetDatabase();
        var key = SessionKey(session.ClientId, session.SessionId);
        var ttl = session.ExpiresAt - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        await database.StringSetAsync(key, JsonSerializer.Serialize(session), ttl);
        await database.SetAddAsync(ClientKey(session.ClientId), session.SessionId);
        await database.KeyExpireAsync(ClientKey(session.ClientId), ttl + TimeSpan.FromMinutes(5));
    }

    public async Task<IReadOnlyList<WorkloadSessionRecord>> ListAsync(string clientId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redis.GetDatabase();
        var members = await database.SetMembersAsync(ClientKey(clientId));
        var sessions = new List<WorkloadSessionRecord>(members.Length);
        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var raw = await database.StringGetAsync(SessionKey(clientId, member.ToString()));
            if (!raw.HasValue)
            {
                await database.SetRemoveAsync(ClientKey(clientId), member);
                continue;
            }
            try
            {
                var session = JsonSerializer.Deserialize<WorkloadSessionRecord>(raw!);
                if (session is not null && session.ExpiresAt > DateTime.UtcNow)
                    sessions.Add(session);
                else
                    await RevokeAsync(clientId, member.ToString(), cancellationToken);
            }
            catch (JsonException)
            {
                await RevokeAsync(clientId, member.ToString(), cancellationToken);
            }
        }
        return sessions.OrderByDescending(item => item.IssuedAt).ToArray();
    }

    public async Task<bool> RevokeAsync(string clientId, string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redis.GetDatabase();
        var removed = await database.KeyDeleteAsync(SessionKey(clientId, sessionId));
        await database.SetRemoveAsync(ClientKey(clientId), sessionId);
        return removed;
    }

    public async Task<int> RevokeAllAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var members = await database.SetMembersAsync(ClientKey(clientId));
        var count = 0;
        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await database.KeyDeleteAsync(SessionKey(clientId, member.ToString()))) count++;
        }
        await database.KeyDeleteAsync(ClientKey(clientId));
        return count;
    }

    private static string ClientKey(string clientId) => Prefix + clientId;
    private static string SessionKey(string clientId, string sessionId) => ClientKey(clientId) + ":" + sessionId;
}
