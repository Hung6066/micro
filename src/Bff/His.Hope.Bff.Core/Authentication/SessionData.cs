namespace His.Hope.Bff.Core.Authentication;

public sealed record SessionData
{
    public required string UserId { get; init; }
    public required string Jwt { get; init; }
    public string? RefreshToken { get; init; }
    public required string[] Permissions { get; init; }
    public required string CsrfToken { get; init; }
    public required string UserAgentHash { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
