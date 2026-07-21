using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// JWT token generator using HMAC-SHA256 symmetric signing.
/// Uses the shared Jwt:Key configuration so all backend services can validate tokens
/// without needing to distribute RSA public keys.
/// </summary>
public class JwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
        var key = configuration["Jwt:Key"] ?? "super-secret-key-his-hope-2024-at-least-32-chars!";
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// Generates a JWT access token signed with HMAC-SHA256.
    /// Token includes: sub, email, role claims, permission claims, jti, iat, exp
    /// </summary>
    public (string token, DateTime expiresAt) GenerateAccessToken(
        User user,
        IList<string> roles,
        IList<string>? permissions = null,
        IList<string>? amrValues = null)
    {
        var claims = new List<Claim>
        {
            // SECURITY: Use standard JWT registered claims for interoperability
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),  // Unique token ID for replay prevention
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("fullName", user.FullName),
            new("licenseNumber", user.LicenseNumber ?? string.Empty),
            new("securityVersion", user.SecurityStamp ?? "1"),
        };

        // SECURITY: Add amr (Authentication Methods References) claim for MFA
        if (amrValues is { Count: > 0 })
        {
            claims.AddRange(amrValues.Select(amr => new Claim("amr", amr)));
        }

        // Add role claims for RBAC enforcement
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // SECURITY: Add explicit permission claims for fine-grained authorization
        if (permissions is { Count: > 0 })
        {
            claims.Add(new Claim("permissions", string.Join(",", permissions)));
        }

        var expiresAt = DateTime.UtcNow.Add(
            TimeSpan.Parse(_configuration["Jwt:Expiry"] ?? "08:00:00"));

        var secToken = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(secToken), expiresAt);
    }

    /// <summary>
    /// Generates a cryptographically secure random refresh token.
    /// Uses RNGCryptoServiceProvider for FIPS-compliant randomness.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Validates an expired token to extract claims for refresh token flow.
    /// Uses the shared symmetric key.
    /// </summary>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,  // SECURITY: Intentionally skipped for refresh token flow
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, validation, out _);
    }
}
