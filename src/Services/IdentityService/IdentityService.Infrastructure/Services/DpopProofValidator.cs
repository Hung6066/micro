using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public interface IDpopReplayCache
{
    bool TryRegister(string jti, DateTimeOffset expiresAt);
}

public sealed class InMemoryDpopReplayCache : IDpopReplayCache
{
    private readonly ConcurrentDictionary<string, long> _entries = new(StringComparer.Ordinal);

    public bool TryRegister(string jti, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var entry in _entries)
        {
            if (entry.Value <= now)
                _entries.TryRemove(entry.Key, out _);
        }

        return _entries.TryAdd(jti, expiresAt.ToUnixTimeSeconds());
    }
}

public sealed class RedisDpopReplayCache(IConnectionMultiplexer redis) : IDpopReplayCache
{
    private readonly IDatabase _database = redis.GetDatabase();

    public bool TryRegister(string jti, DateTimeOffset expiresAt)
    {
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        return ttl > TimeSpan.Zero &&
            _database.StringSet($"hishop:dpop:jti:{jti}", "1", ttl, When.NotExists);
    }
}

public sealed record DpopProofValidationResult(string JwkThumbprint, string Jti);

public sealed class DpopValidationException(string message) : InvalidOperationException(message);

public sealed class DpopProofValidator
{
    private static readonly TimeSpan MaximumProofAge = TimeSpan.FromMinutes(5);
    private readonly IDpopReplayCache _replayCache;
    private readonly TimeProvider _timeProvider;

    public DpopProofValidator(IDpopReplayCache replayCache, TimeProvider timeProvider)
    {
        _replayCache = replayCache;
        _timeProvider = timeProvider;
    }

    public DpopProofValidationResult Validate(
        HttpRequest request,
        string? expectedJwkThumbprint = null,
        string? expectedUri = null)
    {
        var proof = request.Headers["DPoP"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(proof))
            throw new DpopValidationException("DPoP proof is required.");

        var parts = proof.Split('.');
        if (parts.Length != 3)
            throw new DpopValidationException("DPoP proof must be a compact JWT.");

        using var header = ParseJson(parts[0], "DPoP header");
        using var payload = ParseJson(parts[1], "DPoP payload");

        var typ = header.RootElement.GetProperty("typ").GetString();
        if (!string.Equals(typ, "dpop+jwt", StringComparison.OrdinalIgnoreCase))
            throw new DpopValidationException("DPoP typ is invalid.");

        var alg = header.RootElement.GetProperty("alg").GetString();
        if (!string.Equals(alg, SecurityAlgorithms.EcdsaSha256, StringComparison.Ordinal))
            throw new DpopValidationException("Only ES256 DPoP proofs are accepted.");

        if (!header.RootElement.TryGetProperty("jwk", out var jwkElement))
            throw new DpopValidationException("DPoP proof must contain a public jwk.");

        var jwk = new JsonWebKey(jwkElement.GetRawText());
        if (!string.Equals(jwk.Kty, JsonWebAlgorithmsKeyTypes.EllipticCurve, StringComparison.Ordinal) ||
            !string.Equals(jwk.Crv, "P-256", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(jwk.X) || string.IsNullOrWhiteSpace(jwk.Y))
            throw new DpopValidationException("DPoP proof must use a P-256 public key.");

        ValidateSignature(proof, jwk);

        var method = payload.RootElement.GetProperty("htm").GetString();
        if (!string.Equals(method, request.Method, StringComparison.OrdinalIgnoreCase))
            throw new DpopValidationException("DPoP htm does not match the request method.");

        var uri = payload.RootElement.GetProperty("htu").GetString();
        if (!string.Equals(uri, expectedUri ?? GetRequestUri(request), StringComparison.OrdinalIgnoreCase))
            throw new DpopValidationException("DPoP htu does not match the request URI.");

        var jti = payload.RootElement.GetProperty("jti").GetString();
        if (string.IsNullOrWhiteSpace(jti) || jti.Length > 200)
            throw new DpopValidationException("DPoP jti is invalid.");

        var issuedAt = payload.RootElement.GetProperty("iat").GetInt64();
        var issuedAtTime = DateTimeOffset.FromUnixTimeSeconds(issuedAt);
        var now = _timeProvider.GetUtcNow();
        if (issuedAtTime < now - MaximumProofAge || issuedAtTime > now + TimeSpan.FromMinutes(1))
            throw new DpopValidationException("DPoP iat is outside the accepted clock window.");

        var thumbprint = CreateJwkThumbprint(jwk);
        if (expectedJwkThumbprint is not null &&
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(thumbprint), Encoding.ASCII.GetBytes(expectedJwkThumbprint)))
            throw new DpopValidationException("DPoP key does not match the token binding.");

        if (!_replayCache.TryRegister(jti, issuedAtTime + MaximumProofAge))
            throw new DpopValidationException("DPoP proof replay detected.");

        return new DpopProofValidationResult(thumbprint, jti);
    }

    public static string CreateJwkThumbprint(JsonWebKey jwk)
    {
        var canonical = $"{{\"crv\":\"{jwk.Crv}\",\"kty\":\"{jwk.Kty}\",\"x\":\"{jwk.X}\",\"y\":\"{jwk.Y}\"}}";
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ValidateSignature(string proof, JsonWebKey jwk)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(proof, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwk,
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            throw new DpopValidationException("DPoP signature is invalid.");
        }
    }

    private static JsonDocument ParseJson(string segment, string name)
    {
        try
        {
            return JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(segment));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            throw new DpopValidationException($"{name} is invalid.");
        }
    }

    /// <summary>
    /// Returns the URI as seen by the public client. Identity Service is normally
    /// behind the API gateway, so the proxy-provided origin must be used instead
    /// of the internal container host when validating DPoP htu.
    /// </summary>
    public static string GetRequestUri(HttpRequest request)
    {
        var forwardedProto = request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? request.Scheme : forwardedProto;
        var host = string.IsNullOrWhiteSpace(forwardedHost) ? request.Host.Value : forwardedHost;

        return $"{scheme}://{host}{request.PathBase}{request.Path}";
    }
}
