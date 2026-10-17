using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Security;

internal sealed class DpopResourceProofValidator
{
    private readonly IConnectionMultiplexer _redis;

    public DpopResourceProofValidator(IConnectionMultiplexer redis) => _redis = redis;

    public bool Validate(HttpRequest request, string expectedThumbprint, string accessToken)
    {
        var proof = request.Headers["DPoP"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(proof)) return false;


        try
        {
            var parts = proof.Split('.');
            if (parts.Length != 3) return false;
            using var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[0]));
            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[1]));
            var root = header.RootElement;
            if (root.GetProperty("typ").GetString() != "dpop+jwt" ||
                root.GetProperty("alg").GetString() != SecurityAlgorithms.EcdsaSha256)
                return false;

            var jwk = new JsonWebKey(root.GetProperty("jwk").GetRawText());
            if (jwk.Kty != JsonWebAlgorithmsKeyTypes.EllipticCurve || jwk.Crv != "P-256") return false;
            var canonical = $"{{\"crv\":\"{jwk.Crv}\",\"kty\":\"{jwk.Kty}\",\"x\":\"{jwk.X}\",\"y\":\"{jwk.Y}\"}}";
            var thumbprint = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(thumbprint), Encoding.ASCII.GetBytes(expectedThumbprint)))
                return false;

            new JwtSecurityTokenHandler().ValidateToken(proof, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwk,
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256]
            }, out _);

            var claims = payload.RootElement;
            if (!string.Equals(claims.GetProperty("htm").GetString(), request.Method, StringComparison.OrdinalIgnoreCase)) return false;
            // DPoP htu is created by the public client against the gateway URL.
            // Resource services sit behind YARP, so validate the forwarded
            // public origin when the proxy supplied it instead of the internal
            // container host (for example identityservice:5001).
            var forwardedProto = request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? request.Scheme : forwardedProto;
            var host = string.IsNullOrWhiteSpace(forwardedHost) ? request.Host.Value : forwardedHost;
            var uri = $"{scheme}://{host}{request.PathBase}{request.Path}";
            if (!string.Equals(claims.GetProperty("htu").GetString(), uri, StringComparison.OrdinalIgnoreCase)) return false;
            var expectedAth = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)));
            if (!string.Equals(claims.GetProperty("ath").GetString(), expectedAth, StringComparison.Ordinal)) return false;
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(claims.GetProperty("iat").GetInt64());
            if (issuedAt < DateTimeOffset.UtcNow.AddMinutes(-5) || issuedAt > DateTimeOffset.UtcNow.AddMinutes(1)) return false;

            var jti = claims.GetProperty("jti").GetString();
            return !string.IsNullOrWhiteSpace(jti) &&
                _redis.GetDatabase().StringSet($"hishop:dpop:resource:{jti}", "1", TimeSpan.FromMinutes(5), When.NotExists);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal static class DpopAuthorizationScheme
{
    public const string IsDpopRequest = "HisHope.DpopRequest";
}

internal sealed class DpopAuthorizationSchemeMiddleware
{
    private readonly RequestDelegate _next;

    public DpopAuthorizationSchemeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (authorization is not null &&
            authorization.StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase) &&
            authorization.Length > "DPoP ".Length)
        {
            context.Items[DpopAuthorizationScheme.IsDpopRequest] = true;
            // OpenIddict validation consumes the standard Bearer scheme. Keep
            // the wire-level DPoP semantics while normalizing only the local
            // authentication input; the post-auth middleware enforces proof.
            context.Request.Headers.Authorization = "Bearer " + authorization["DPoP ".Length..];
        }

        await _next(context);
    }
}

internal sealed class DpopAccessTokenMiddleware
{
    private readonly RequestDelegate _next;

    public DpopAccessTokenMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, DpopResourceProofValidator validator)
    {
        var isDpopRequest = context.Items.ContainsKey(DpopAuthorizationScheme.IsDpopRequest);
        var binding = context.User.FindFirst("cnf")?.Value;
        if (isDpopRequest != !string.IsNullOrWhiteSpace(binding))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!string.IsNullOrWhiteSpace(binding))
        {
            try
            {
                var authorization = context.Request.Headers.Authorization.ToString();
                if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                var accessToken = authorization["Bearer ".Length..].Trim();
                using var document = JsonDocument.Parse(binding);
                var thumbprint = document.RootElement.GetProperty("jkt").GetString();
                if (string.IsNullOrWhiteSpace(thumbprint) || string.IsNullOrWhiteSpace(accessToken) ||
                    !validator.Validate(context.Request, thumbprint, accessToken))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}

public static class DpopAccessTokenMiddlewareExtensions
{
    public static IServiceCollection AddHisHopeDpopValidation(this IServiceCollection services)
    {
        services.AddSingleton<DpopResourceProofValidator>();
        return services;
    }

    public static IApplicationBuilder UseDpopAuthorizationSchemeNormalization(this IApplicationBuilder app) =>
        app.UseMiddleware<DpopAuthorizationSchemeMiddleware>();

    public static IApplicationBuilder UseDpopAccessTokenValidation(this IApplicationBuilder app) =>
        app.UseMiddleware<DpopAccessTokenMiddleware>();
}
