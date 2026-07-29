using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Legacy access-token generator retained for the BFF login and refresh paths.
/// Tokens are RSA signed and RSA encrypted; production keys are loaded from the
/// configured secret/key paths and plaintext JWT fallback is disabled.
/// </summary>
public class JwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly SecurityKey _signingKey;
    private readonly SecurityKey _encryptionKey;
    private readonly IReadOnlyList<SecurityKey> _decryptionKeys;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
        var signingPem = configuration["Jwt:RsaPrivateKey"];
        var signingPath = configuration["OpenIddict:Signing:PrivateKeyPath"]
            ?? configuration["Jwt:RsaPrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(signingPem) && !string.IsNullOrWhiteSpace(signingPath))
        {
            if (signingPath.Contains("${", StringComparison.Ordinal) || !File.Exists(signingPath))
                throw new InvalidOperationException("RSA signing key path must point to an existing private key.");
            signingPem = File.ReadAllText(signingPath);
        }

        if (string.IsNullOrWhiteSpace(signingPem) || signingPem.Contains("${", StringComparison.Ordinal))
            throw new InvalidOperationException("RSA signing private key is required; HMAC access-token signing is disabled.");

        var signingRsa = RSA.Create();
        try
        {
            signingRsa.ImportFromPem(signingPem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            throw new InvalidOperationException("RSA signing private key is not a valid PEM key.", ex);
        }
        if (signingRsa.KeySize < 2048)
            throw new InvalidOperationException("RSA signing key must be at least 2048 bits.");
        _signingKey = new RsaSecurityKey(signingRsa)
        {
            KeyId = configuration["OpenIddict:Signing:KeyId"] ?? "jwt-signing-v1"
        };
        var encryptionPaths = new List<string?> { configuration["Jwt:RsaEncryptionPrivateKeyPath"] };
        encryptionPaths.Add(configuration["Jwt:RsaEncryptionPreviousPrivateKeyPath"]);
        encryptionPaths.AddRange(configuration.GetSection("Jwt:RsaEncryptionPrivateKeyPaths").GetChildren().Select(section => section.Value));
        for (var index = 0; index < 8; index++)
            encryptionPaths.Add(configuration[$"Jwt:RsaEncryptionPrivateKeyPaths:{index}"]);
        var encryptionPath = encryptionPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && !path.Contains("${", StringComparison.Ordinal));
        var encryptionPem = configuration["Jwt:RsaEncryptionPrivateKey"];
        if (string.IsNullOrWhiteSpace(encryptionPem) && !string.IsNullOrWhiteSpace(encryptionPath))
        {
            if (encryptionPath.Contains("${", StringComparison.Ordinal) || !File.Exists(encryptionPath))
                throw new InvalidOperationException("Jwt:RsaEncryptionPrivateKeyPath must point to an existing private key.");
            encryptionPem = File.ReadAllText(encryptionPath);
        }

        if (string.IsNullOrWhiteSpace(encryptionPem) || encryptionPem.Contains("${", StringComparison.Ordinal))
            throw new InvalidOperationException("Jwt RSA encryption private key is required; plaintext JWTs are disabled.");

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(encryptionPem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            throw new InvalidOperationException("RSA encryption private key is not a valid PEM key.", ex);
        }
        if (rsa.KeySize < 2048)
            throw new InvalidOperationException("RSA encryption key must be at least 2048 bits.");
        var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        _encryptionKey = new RsaSecurityKey(publicRsa)
        {
            KeyId = configuration["Jwt:RsaEncryptionKeyId"] ?? "jwt-encryption-v1"
        };
        var decryptionKeys = new List<SecurityKey>
        {
            new RsaSecurityKey(rsa)
            {
                KeyId = configuration["Jwt:RsaEncryptionKeyId"] ?? "jwt-encryption-v1"
            }
        };
        var previousEncryptionPems = new List<string?> { configuration["Jwt:RsaEncryptionPreviousPrivateKey"] };
        previousEncryptionPems.AddRange(configuration.GetSection("Jwt:RsaEncryptionPreviousPrivateKeys").GetChildren().Select(section => section.Value));
        foreach (var oldPath in encryptionPaths.Skip(1).Where(path => !string.IsNullOrWhiteSpace(path) && !path.Contains("${", StringComparison.Ordinal) && File.Exists(path)))
        {
            var oldRsa = RSA.Create();
            oldRsa.ImportFromPem(File.ReadAllText(oldPath!));
            decryptionKeys.Add(new RsaSecurityKey(oldRsa)
            {
                KeyId = configuration["Jwt:RsaEncryptionKeyId"] ?? "jwt-encryption-v1"
            });
        }
        foreach (var previousPem in previousEncryptionPems.Where(pem => !string.IsNullOrWhiteSpace(pem) && !pem.Contains("${", StringComparison.Ordinal)))
        {
            var oldRsa = RSA.Create();
            oldRsa.ImportFromPem(previousPem);
            decryptionKeys.Add(new RsaSecurityKey(oldRsa)
            {
                KeyId = configuration["Jwt:RsaEncryptionKeyId"] ?? "jwt-encryption-v1"
            });
        }
        _decryptionKeys = decryptionKeys;
    }

    /// <summary>
    /// Generates a nested JWT access token signed with RSA-SHA256 and encrypted as JWE.
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
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

        var now = DateTime.UtcNow;
        var payload = new Dictionary<string, object?>();

        foreach (var claim in claims)
        {
            var claimType = claim.Type == ClaimTypes.Role ? "role" : claim.Type;
            if (claimType is JwtRegisteredClaimNames.Iss or JwtRegisteredClaimNames.Aud or
                JwtRegisteredClaimNames.Iat or JwtRegisteredClaimNames.Nbf or JwtRegisteredClaimNames.Exp)
                continue;

            if (payload.TryGetValue(claimType, out var existing))
            {
                payload[claimType] = existing is List<string> values
                    ? values.Append(claim.Value).ToList()
                    : new List<string> { existing?.ToString() ?? string.Empty, claim.Value };
            }
            else
            {
                payload[claimType] = claim.Value;
            }
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = payload,
            NotBefore = now,
            IssuedAt = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
            EncryptingCredentials = new EncryptingCredentials(
                _encryptionKey,
                SecurityAlgorithms.RsaOAEP,
                SecurityAlgorithms.Aes256CbcHmacSha512)
        };
        return (new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor), expiresAt);
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
    /// Uses the shared signing key and RSA encryption private key.
    /// </summary>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var handler = new JwtSecurityTokenHandler();
        try
        {
            handler.ReadJwtToken(token);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or SecurityTokenException)
        {
            return null;
        }

        var expectedIssuer = _configuration["Jwt:Issuer"];
        var expectedAudience = _configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(expectedIssuer) || string.IsNullOrWhiteSpace(expectedAudience))
            throw new UnauthorizedAccessException("Access token issuer or audience is not configured.");

        var validation = new TokenValidationParameters
        {
            ValidateLifetime = false,  // SECURITY: Intentionally skipped for refresh token flow
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = expectedIssuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
             IssuerSigningKey = _signingKey,
            TokenDecryptionKey = null,
            TokenDecryptionKeys = _decryptionKeys,
            TokenDecryptionKeyResolver = (_, _, _, _) => _decryptionKeys
        };

        ClaimsPrincipal principal;
        SecurityToken validatedToken;
        try
        {
            principal = handler.ValidateToken(token, validation, out validatedToken);
        }
        catch (SecurityTokenException ex)
        {
            throw new UnauthorizedAccessException("Invalid access token.", ex);
        }
        var rawSubject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!string.IsNullOrWhiteSpace(rawSubject) &&
            principal.FindFirst(ClaimTypes.NameIdentifier) is null &&
            principal.FindFirst("sub") is null)
        {
            var identity = principal.Identity as ClaimsIdentity;
            identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, rawSubject));
            identity?.AddClaim(new Claim("sub", rawSubject));
        }

        return principal;
    }
}
