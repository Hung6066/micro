namespace His.Hope.IdentityService.Application.Interfaces;

public sealed record WorkloadSessionRecord(
    string SessionId,
    string ClientId,
    string WorkloadRoleId,
    DateTime IssuedAt,
    DateTime ExpiresAt);

public interface IWorkloadSessionStore
{
    Task RegisterAsync(WorkloadSessionRecord session, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkloadSessionRecord>> ListAsync(string clientId, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string clientId, string sessionId, CancellationToken cancellationToken = default);
    Task<int> RevokeAllAsync(string clientId, CancellationToken cancellationToken = default);
}
