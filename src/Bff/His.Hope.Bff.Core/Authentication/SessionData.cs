namespace His.Hope.Bff.Core.Authentication;

public sealed record SessionData
{
    public required string UserId { get; init; }
    public required string Jwt { get; init; }
    public string? RefreshToken { get; init; }
    public required string[] Permissions { get; init; }
    /// <summary>Principal classification embedded in the BFF JWT.</summary>
    public string? PrincipalType { get; init; }
    public required string CsrfToken { get; init; }
    public required string UserAgentHash { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? IdleExpiresAt { get; init; }
    public DateTimeOffset? AbsoluteExpiresAt { get; init; }
    public bool IsPrivileged { get; init; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt ||
        (IdleExpiresAt is not null && DateTimeOffset.UtcNow >= IdleExpiresAt) ||
        (AbsoluteExpiresAt is not null && DateTimeOffset.UtcNow >= AbsoluteExpiresAt);
}
