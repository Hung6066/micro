using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Bff.Core.Authentication;

public static class OidcSetup
{
    private const string SessionCookieName = "hishop_sid";

    public static IServiceCollection AddBffOidc(
        this IServiceCollection services, IConfiguration configuration)
    {
        var sessionOptions = configuration
            .GetSection(SessionCookieOptions.SectionName)
            .Get<SessionCookieOptions>() ?? new SessionCookieOptions();

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = ".Hishop.Bff.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(1);
            options.SlidingExpiration = true;
        })
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            var oidcSection = configuration.GetSection("Oidc");
            options.Authority = oidcSection["Authority"] ?? "https://identity.his-hope.local";
            options.ClientId = oidcSection["ClientId"] ?? "his-hope-spa";
            options.ClientSecret = oidcSection["ClientSecret"];
            options.ResponseType = "code";
            options.UsePkce = true;
            options.ResponseMode = "form_post";

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Scope.Add("hishop:permissions");

            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = false;

            options.CallbackPath = oidcSection["CallbackPath"] ?? "/signin-oidc";
            options.SignedOutCallbackPath = oidcSection["SignedOutCallbackPath"] ?? "/signout-callback-oidc";

            options.Events.OnTokenValidated = async ctx =>
            {
                var redis = ctx.HttpContext.RequestServices
                    .GetRequiredService<IConnectionMultiplexer>();
                var loggerFactory = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("His.Hope.Bff.Core.Authentication.OidcSetup");

                if (ctx.TokenEndpointResponse is null)
                {
                    logger.LogWarning("TokenEndpointResponse is null after OIDC validation");
                    return;
                }

                try
                {
                    var sessionId = Guid.NewGuid().ToString("N");
                    var accessToken = ctx.TokenEndpointResponse.AccessToken;
                    var refreshToken = ctx.TokenEndpointResponse.RefreshToken;
                    var subjectId = ctx.Principal?.FindFirst("sub")?.Value
                        ?? ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? "unknown";

                    var sessionData = new
                    {
                        UserId = subjectId,
                        Jwt = accessToken ?? "",
                        RefreshToken = refreshToken ?? "",
                        Permissions = Array.Empty<string>(),
                        CsrfToken = Guid.NewGuid().ToString("N"),
                        UserAgentHash = ComputeSha256(
                            ctx.Request.Headers.UserAgent.ToString()),
                        IssuedAt = DateTimeOffset.UtcNow,
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                    };

                    var sessionJson = JsonSerializer.Serialize(sessionData);
                    var db = redis.GetDatabase();
                    await db.StringSetAsync(
                        $"session:{sessionId}",
                        sessionJson,
                        TimeSpan.FromHours(1));

                    // Track this session in the user's session set for cross-port logout
                    var userSessionsKey = $"HisHope:user_sessions:{subjectId}";
                    await db.SetAddAsync(userSessionsKey, sessionId);
                    await db.KeyExpireAsync(userSessionsKey, TimeSpan.FromDays(7));

                    ctx.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
                    {
                        HttpOnly = sessionOptions.HttpOnly,
                        Secure = sessionOptions.Secure,
                        SameSite = sessionOptions.SameSite,
                        Path = sessionOptions.CookiePath,
                        MaxAge = TimeSpan.FromSeconds(sessionOptions.CookieMaxAgeSeconds)
                    });

                    logger.LogInformation(
                        "OIDC session created for user '{UserId}' (session={SessionId})",
                        subjectId, sessionId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create session from OIDC token");
                    ctx.Fail("Session creation failed");
                }
            };
        });

        return services;
    }

    public static IApplicationBuilder UseBffOidc(this IApplicationBuilder builder)
    {
        builder.UseAuthentication();
        builder.UseAuthorization();
        return builder;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
