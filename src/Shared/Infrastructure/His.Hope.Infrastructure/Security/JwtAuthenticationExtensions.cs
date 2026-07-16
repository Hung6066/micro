using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.Infrastructure.Security;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddHisHopeJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !configuration.GetValue<bool>("Jwt:AllowHttp", false);

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],

                IssuerSigningKey = LoadRsaPublicKey(configuration),

                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },

                ClockSkew = TimeSpan.FromMinutes(configuration.GetValue("Jwt:ClockSkewMinutes", 1)),
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerHandler>>();
                    logger.LogWarning(context.Exception,
                        "JWT authentication failed for {RemoteIp}",
                        context.HttpContext.Connection.RemoteIpAddress);
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerHandler>>();

                    var jti = context.Principal?.FindFirst("jti")?.Value;
                    var userId = context.Principal?.FindFirst("sub")?.Value;

                    // SECURITY: Check if this specific token has been revoked
                    if (!string.IsNullOrEmpty(jti))
                    {
                        var blacklistService = context.HttpContext.RequestServices
                            .GetService<ITokenBlacklistService>();

                        if (blacklistService != null && await blacklistService.IsBlacklistedAsync(jti))
                        {
                            logger.LogWarning(
                                "Token revoked: jti={Jti}, userId={UserId}", jti, userId);
                            context.Fail("Token has been revoked.");
                            return;
                        }
                    }

                    // SECURITY: Check if all tokens for this user have been revoked
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var blacklistService = context.HttpContext.RequestServices
                            .GetService<ITokenBlacklistService>();

                        if (blacklistService != null)
                        {
                            var revokedAt = await blacklistService
                                .GetUserRevocationTimestampAsync(userId);

                            if (revokedAt.HasValue)
                            {
                                var iatClaim = context.Principal?.FindFirst("iat")?.Value;
                                if (long.TryParse(iatClaim, out var iatUnix))
                                {
                                    var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix);
                                    if (issuedAt.UtcDateTime < revokedAt.Value)
                                    {
                                        logger.LogWarning(
                                            "User token revoked (issued before revocation): " +
                                            "UserId={UserId}", userId);
                                        context.Fail("User tokens have been revoked.");
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    logger.LogInformation(
                        "JWT token validated for user {UserId}, jti={Jti}", userId, jti);
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerHandler>>();
                    logger.LogWarning(
                        "JWT challenge issued for {RemoteIp}, error: {Error}",
                        context.HttpContext.Connection.RemoteIpAddress,
                        context.Error);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddHisHopeTokenBlacklist(
        this IServiceCollection services)
    {
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        return services;
    }

    private static RsaSecurityKey LoadRsaPublicKey(IConfiguration configuration)
    {
        var vaultKey = configuration["Jwt:RsaPublicKey"];
        if (!string.IsNullOrEmpty(vaultKey))
        {
            var rsa = RSA.Create();
            try
            {
                rsa.ImportRSAPublicKey(Convert.FromBase64String(vaultKey), out _);
            }
            catch
            {
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(vaultKey), out _);
            }
            return new RsaSecurityKey(rsa);
        }

        var publicKeyPath = configuration["Jwt:RsaPublicKeyPath"];
        if (!string.IsNullOrEmpty(publicKeyPath) && File.Exists(publicKeyPath))
        {
            var rsa = RSA.Create();
            var pemBytes = File.ReadAllBytes(publicKeyPath);
            try
            {
                rsa.ImportRSAPublicKey(pemBytes, out _);
            }
            catch
            {
                rsa.ImportSubjectPublicKeyInfo(pemBytes, out _);
            }
            return new RsaSecurityKey(rsa);
        }

        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Certificates", "jwt-public-key.pem"),
            "/certs/jwt-public-key.pem",
            "jwt-public-key.pem"
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                var rsa = RSA.Create();
                var pemBytes = File.ReadAllBytes(path);
                try
                {
                    rsa.ImportRSAPublicKey(pemBytes, out _);
                }
                catch
                {
                    rsa.ImportSubjectPublicKeyInfo(pemBytes, out _);
                }
                return new RsaSecurityKey(rsa);
            }
        }

        var devRsa = RSA.Create(2048);
        var devKey = new RsaSecurityKey(devRsa);

        var dir = Path.Combine(AppContext.BaseDirectory, "Certificates");
        Directory.CreateDirectory(dir);
        var pubPath = Path.Combine(dir, "jwt-public-key.pem");
        if (!File.Exists(pubPath))
        {
            File.WriteAllBytes(pubPath, devRsa.ExportRSAPublicKey());
        }

        return devKey;
    }
}
