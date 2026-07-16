using System;
using System.Security.Cryptography;
using System.Text;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class RefreshTokenRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string UserId { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public string FamilyId { get; init; } = string.Empty;
    public int Generation { get; init; }
    public string? PreviousTokenHash { get; init; }
    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; }
    public string? DeviceInfo { get; init; }
    public string? IpAddress { get; init; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }

    public static string ComputeHash(string refreshToken)
    {
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
