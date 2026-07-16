using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// JWT token generator using RSA-SHA256 asymmetric signing.
/// SECURITY: RSA is used instead of HMAC (symmetric) to prevent:
///   1. Key distribution attacks - only the IdentityService holds the private key
///   2. Key compromise propagation - public key can be safely distributed for validation
///   3. Non-repudiation - proof of origin is cryptographically verifiable
///
/// Key loading strategy:
///   - Development: loads PEM files from paths specified in configuration
///   - Production: keys should come from Vault (transit engine or PKI secrets engine)
/// </summary>
public class JwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly Lazy<RsaSecurityKey> _privateKey;
    private readonly Lazy<RsaSecurityKey> _publicKey;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
        _privateKey = new Lazy<RsaSecurityKey>(LoadPrivateKey);
        _publicKey = new Lazy<RsaSecurityKey>(LoadPublicKey);
    }

    /// <summary>
    /// Generates a JWT access token signed with RSA-SHA256.
    /// Token includes: sub, email, role claims, permission claims, jti, iat, exp
    /// </summary>
    public (string token, DateTime expiresAt) GenerateAccessToken(
        User user,
        IList<string> roles,
        IList<string>? permissions = null)
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
        };

        // Add role claims for RBAC enforcement
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // SECURITY: Add explicit permission claims for fine-grained authorization
        // These are used by the PermissionHandler in downstream services to authorize
        // access to specific resources and operations without needing to query IdentityService.
        if (permissions is { Count: > 0 })
        {
            // Store permissions as a comma-separated claim to reduce JWT size
            // while allowing multiple permission checks with a single claim lookup
            claims.Add(new Claim("permissions", string.Join(",", permissions)));
        }

        var expiresAt = DateTime.UtcNow.Add(
            TimeSpan.Parse(_configuration["Jwt:Expiry"] ?? "08:00:00"));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            Claims = claims.ToDictionary(c => c.Type, c => (object)c.Value),
            Expires = expiresAt,
            NotBefore = DateTime.UtcNow,
            // SECURITY: RSA-SHA256 asymmetric signing - private key never leaves IdentityService
            SigningCredentials = new SigningCredentials(_privateKey.Value, SecurityAlgorithms.RsaSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
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
    /// Uses RSA public key for signature validation (not the private key).
    /// </summary>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,  // SECURITY: Intentionally skipped for refresh token flow
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = _publicKey.Value,
            // SECURITY: Explicitly require RSA signature algorithm
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
        };

        return new JwtSecurityTokenHandler().ValidateToken(token, validation, out _);
    }

    /// <summary>
    /// Loads the RSA private key for token signing.
    /// Supports PEM files, Vault transit key (base64-encoded), or fallback to dev key.
    /// </summary>
    private RsaSecurityKey LoadPrivateKey()
    {
        // SECURITY: Try Vault-sourced key first (production path)
        var vaultKey = _configuration["Jwt:RsaPrivateKey"];
        if (!string.IsNullOrEmpty(vaultKey))
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(vaultKey), out _);
            return new RsaSecurityKey(rsa) { KeyId = "his-hope-jwt-v1" };
        }

        // Development: load from PEM file
        var privateKeyPath = _configuration["Jwt:RsaPrivateKeyPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "Certificates", "jwt-private-key.pem");

        if (File.Exists(privateKeyPath))
        {
            var rsa = RSA.Create();
            var pemBytes = File.ReadAllBytes(privateKeyPath);
            rsa.ImportRSAPrivateKey(pemBytes, out _);
            return new RsaSecurityKey(rsa) { KeyId = "his-hope-jwt-dev" };
        }

        // SECURITY: Generate ephemeral dev key (NEVER use in production)
        // This ensures the system works out-of-box for development while being
        // cryptographically secure (no hardcoded key material)
        var devRsa = RSA.Create(2048);
        var key = new RsaSecurityKey(devRsa) { KeyId = "his-hope-jwt-dev-ephemeral" };

        // Persist the dev keys for service-to-service validation
        var certDir = Path.Combine(AppContext.BaseDirectory, "Certificates");
        Directory.CreateDirectory(certDir);
        var pubPath = Path.Combine(certDir, "jwt-public-key.pem");
        if (!File.Exists(pubPath))
        {
            File.WriteAllBytes(pubPath, devRsa.ExportRSAPublicKey());
        }

        return key;
    }

    /// <summary>
    /// Loads the RSA public key for token validation.
    /// Used by other services to validate tokens without exposing the private key.
    /// </summary>
    private RsaSecurityKey LoadPublicKey()
    {
        // SECURITY: Try Vault-sourced public key first
        var vaultKey = _configuration["Jwt:RsaPublicKey"];
        if (!string.IsNullOrEmpty(vaultKey))
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(vaultKey), out _);
            return new RsaSecurityKey(rsa);
        }

        // Development: load from PEM file
        var publicKeyPath = _configuration["Jwt:RsaPublicKeyPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "Certificates", "jwt-public-key.pem");

        if (File.Exists(publicKeyPath))
        {
            var rsa = RSA.Create();
            var pemBytes = File.ReadAllBytes(publicKeyPath);
            rsa.ImportRSAPublicKey(pemBytes, out _);
            return new RsaSecurityKey(rsa);
        }

        // Fallback: derive from private key (development only)
        return _privateKey.Value;
    }
}
