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
            Issuer = configuration["Jwt:Issuer"],
            ValidIssuers = configuration.GetSection("Jwt:ValidIssuers").Get<string[]>() ?? [],
            Audience = configuration["Jwt:Audience"],
            AllowHttp = configuration.GetValue("Jwt:AllowHttp", false),
            BackchannelTimeoutSeconds = configuration.GetValue("Jwt:BackchannelTimeoutSeconds", 10),
            ClockSkewMinutes = configuration.GetValue("Jwt:ClockSkewMinutes", 1)
        };
        configure?.Invoke(options);

        var useOidc = string.IsNullOrWhiteSpace(options.Key);
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

    private static TokenValidationParameters CreateOidcValidationParameters(JwtAuthenticationOptions settings) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.ValidIssuers is { Length: > 0 }
                ? null
                : settings.Issuer ?? "His.Hope.IdentityService",
            ValidIssuers = settings.ValidIssuers is { Length: > 0 }
                ? settings.ValidIssuers
                : null,
            ValidAudience = settings.Audience ?? "His.Hope",
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(Math.Max(0, settings.ClockSkewMinutes))
        };

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
