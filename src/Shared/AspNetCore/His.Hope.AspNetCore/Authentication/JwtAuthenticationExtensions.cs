using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.AspNetCore.Authentication;

public sealed class JwtAuthenticationOptions
{
    public string? Key { get; set; }
    public string? Authority { get; set; }
    public string? MetadataAddress { get; set; }
    public string? PublicKeyPath { get; set; }
    public string? EncryptionPrivateKey { get; set; }
    public string? EncryptionPrivateKeyPath { get; set; }
    public string? Issuer { get; set; }
    public string[] ValidIssuers { get; set; } = [];
    public string? Audience { get; set; }
    public bool AllowHttp { get; set; }
    public int BackchannelTimeoutSeconds { get; set; } = 10;
    public int ClockSkewMinutes { get; set; } = 1;
}

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddHisHopeJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtAuthenticationOptions>? configure = null)
    {
        var options = new JwtAuthenticationOptions
        {
            Key = configuration["Jwt:Key"],
            Authority = configuration["Jwt:Authority"],
            MetadataAddress = configuration["Jwt:MetadataAddress"],
            PublicKeyPath = configuration["Jwt:RsaPublicKeyPath"],
            EncryptionPrivateKey = configuration["Jwt:RsaEncryptionPrivateKey"],
            EncryptionPrivateKeyPath = configuration["Jwt:RsaEncryptionPrivateKeyPath"],
            Issuer = configuration["Jwt:Issuer"],
            ValidIssuers = configuration.GetSection("Jwt:ValidIssuers").Get<string[]>() ?? [],
            Audience = configuration["Jwt:Audience"],
            AllowHttp = configuration.GetValue("Jwt:AllowHttp", false),
            BackchannelTimeoutSeconds = configuration.GetValue("Jwt:BackchannelTimeoutSeconds", 10),
            ClockSkewMinutes = configuration.GetValue("Jwt:ClockSkewMinutes", 1)
        };
        configure?.Invoke(options);

        // Configuration templates intentionally contain ${...} markers. They
        // must never activate the legacy HMAC branch in production.
        var useOidc = string.IsNullOrWhiteSpace(options.Key)
            || options.Key.Contains("${", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(options.PublicKeyPath)
            || !string.IsNullOrWhiteSpace(options.EncryptionPrivateKeyPath);
        services.AddHttpClient("HisHopeJwtBackchannel", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.BackchannelTimeoutSeconds));
        });

        services.AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(jwt => ConfigureBearer(jwt, options, useOidc));

        services.AddAuthorization();
        return services;
    }

    private static void ConfigureBearer(
        JwtBearerOptions jwt,
        JwtAuthenticationOptions settings,
        bool useOidc)
    {
        jwt.RequireHttpsMetadata = !settings.AllowHttp;
        jwt.TokenValidationParameters = useOidc
            ? CreateOidcValidationParameters(settings)
            : CreateSymmetricValidationParameters(settings);

        if (useOidc)
        {
            var authority = settings.Authority ?? "http://identityservice:5001";
            jwt.Authority = authority;
            jwt.MetadataAddress = settings.MetadataAddress
                ?? $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        }

        jwt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()
                    ?.CreateLogger("His.Hope.AspNetCore.Authentication");
                logger?.LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            }
        };
    }

    private static TokenValidationParameters CreateOidcValidationParameters(JwtAuthenticationOptions settings)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // TODO: Re-enable audience validation after aligning IdentityService
            // audience with client resource requests. OpenIddict sets the audience
            // based on the resource parameter, which may differ from the configured
            // Jwt__Audience value.
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.ValidIssuers is { Length: > 0 }
                ? null
                : settings.Issuer ?? "His.Hope.IdentityService",
            ValidIssuers = settings.ValidIssuers is { Length: > 0 }
                ? settings.ValidIssuers
                : null,
            ValidAudience = settings.Audience ?? "His.Hope",
            // The session token is a nested JWT: RSA-SHA256 signs the inner JWS,
            // while RSA-OAEP/RSA-OAEP-256 wraps the JWE content-encryption key.
            // Include both key-wrapping variants and the content algorithm so
            // rotated OpenIddict and BFF session tokens reach the decryption keys.
            ValidAlgorithms =
            [
                SecurityAlgorithms.RsaSha256,
                SecurityAlgorithms.RsaOAEP,
                SecurityAlgorithms.RsaOaepKeyWrap,
                SecurityAlgorithms.Aes256CbcHmacSha512
            ],
            ClockSkew = TimeSpan.FromMinutes(Math.Max(0, settings.ClockSkewMinutes))
        };

        if (!string.IsNullOrWhiteSpace(settings.PublicKeyPath) && File.Exists(settings.PublicKeyPath))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(settings.PublicKeyPath));
            parameters.IssuerSigningKey = new RsaSecurityKey(rsa);
        }

        var encryptionRsa = LoadRsaPrivateKey(settings.EncryptionPrivateKey, settings.EncryptionPrivateKeyPath);
        if (encryptionRsa is not null)
        {
            // Export/import via parameters to guarantee full private key material is preserved
            // across RSA implementation boundaries (RSAOpenSsl / RSACng).
            var rsaParams = encryptionRsa.ExportParameters(true);
            var decryptionRsa = RSA.Create(rsaParams);
            var decryptionKey = new RsaSecurityKey(decryptionRsa);
            var oidcDecryptionKey = new RsaSecurityKey(decryptionRsa)
            {
                KeyId = "oidc-encryption-v1"
            };
            var jwtDecryptionKey = new RsaSecurityKey(decryptionRsa)
            {
                KeyId = "jwt-encryption-v1"
            };
            var decryptionKeys = new[] { decryptionKey, oidcDecryptionKey, jwtDecryptionKey };

            // Register via multiple mechanisms for resilience:
            // - TokenDecryptionKey: direct single-key fallback
            // - TokenDecryptionKeys: multi-key list
            // - TokenDecryptionKeyResolver: dynamic lookup (called per-request)
            parameters.TokenDecryptionKey = decryptionKey;
            parameters.TokenDecryptionKeys = decryptionKeys;
            parameters.TokenDecryptionKeyResolver = (token, securityToken, kid, validationParameters) =>
                decryptionKeys;
        }

        return parameters;
    }

    private static RSA? LoadRsaPrivateKey(string? pem, string? path)
    {
        var material = !string.IsNullOrWhiteSpace(pem) && !pem.Contains("${", StringComparison.Ordinal)
            ? pem
            : !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? File.ReadAllText(path)
                : new[]
                {
                    "/etc/hishop/certs/jwt-encryption-private-key.pem",
                    "/etc/hishop/certs/oidc-encryption-private-key.pem",
                    Path.Combine(AppContext.BaseDirectory, "Certificates", "jwt-encryption-private-key.pem"),
                    Path.Combine(AppContext.BaseDirectory, "Certificates", "oidc-encryption-private-key.pem")
                }
                .Where(File.Exists)
                .Select(File.ReadAllText)
                .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(material)) return null;

        var rsa = RSA.Create();
        rsa.ImportFromPem(material);
        return rsa;
    }

    private static TokenValidationParameters CreateSymmetricValidationParameters(JwtAuthenticationOptions settings) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = !string.IsNullOrEmpty(settings.Audience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key!)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(Math.Max(0, settings.ClockSkewMinutes))
        };
}
