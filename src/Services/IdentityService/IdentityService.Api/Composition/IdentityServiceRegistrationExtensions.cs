using His.Hope.AspNetCore;
using His.Hope.Contracts.Identity;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Persistence;
using His.Hope.Observability;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Api.Jobs;
using His.Hope.IdentityService.Api.Services;
using His.Hope.SharedKernel.Authorization;
using His.Hope.IdentityService.Api.Configuration;
using His.Hope.IdentityService.Api.Handlers;
using His.Hope.IdentityService.Application;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Assurance;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Application.Scim;
using His.Hope.IdentityService.Application.Provisioning;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Contracts;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using MediatR;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore.Configuration;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.EntityFrameworkCore;
using His.Hope.Infrastructure.DataLifecycle;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace His.Hope.IdentityService.Api.Composition;

public static class IdentityServiceRegistrationExtensions
{
    public static void AddIdentityService(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<PushProviderOptions>()
            .Bind(builder.Configuration.GetSection("PushProviders"))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.FirebaseCredentialsJson) &&
                    !string.IsNullOrWhiteSpace(options.FirebaseCredentialsFile) &&
                    File.Exists(options.FirebaseCredentialsFile))
                {
                    options.FirebaseCredentialsJson = File.ReadAllText(options.FirebaseCredentialsFile);
                }
            })
            // Production must never be able to disable provider validation via
            // configuration. Development can still run without push material.
            .Validate(options => !builder.Environment.IsProduction() || !options.Validate().Any(),
                "Production push provider credentials are incomplete")
            .ValidateOnStart();

        builder.Services.AddSingleton(sp => new Fido2NetLib.Fido2(new Fido2NetLib.Fido2Configuration
        {
            ServerDomain = builder.Configuration["Passkeys:RpId"] ?? new Uri(builder.Configuration["OpenIddict:Issuer"] ?? "https://localhost").Host,
            ServerName = builder.Configuration["Passkeys:RpName"] ?? "His.Hope",
            Origins = new HashSet<string>(builder.Configuration.GetSection("Passkeys:Origins").Get<string[]>() ?? new[] { builder.Configuration["OpenIddict:Issuer"] ?? "https://localhost" })
        }));
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<SoftDeleteInterceptor>();
        builder.Services.AddHttpClient("vault")
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                var caPath = builder.Configuration["Vault:TlsCaFile"];
                if (!string.IsNullOrWhiteSpace(caPath))
                {
                    if (!File.Exists(caPath))
                        throw new InvalidOperationException($"Vault TLS CA file '{caPath}' is missing.");
                    var ca = new X509Certificate2(caPath);
                    handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                    {
                        if (certificate is null) return false;
                        using var chain = new X509Chain();
                        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(ca);
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        return chain.Build(new X509Certificate2(certificate));
                    };
                }
                return handler;
            });
        builder.Services.AddScoped<SamlRuntimeConfigurationService>();
        builder.Services.AddScoped<His.Hope.IdentityService.Application.Interfaces.IEmailSender, NoOpEmailSender>();
        builder.Services.AddScoped<IPushDeliveryService, PushDeliveryService>();
        builder.Services.AddScoped<OidcLoginCompletionService>();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<PushNotificationOutboxWorker>();
        builder.Services.BindConfig<Saml2Configuration>(builder.Configuration, "Saml2", (services, configuration) =>
        {
            configuration.DetectReplayedTokens = true;
            configuration.AudienceRestricted = true;
            if (!string.IsNullOrWhiteSpace(configuration.Issuer))
                configuration.AllowedAudienceUris.Add(configuration.Issuer);
            if (builder.Configuration.GetValue("Saml2:Enabled", false))
            {
                var metadata = builder.Configuration["Saml2:IdPMetadata"];
                if (string.IsNullOrWhiteSpace(metadata) || metadata.Contains("${", StringComparison.Ordinal))
                    throw new InvalidOperationException("Saml2:IdPMetadata is required when SAML federation is enabled");
                var descriptor = new EntityDescriptor();
                descriptor.ReadIdPSsoDescriptorFromUrlAsync(services.GetRequiredService<IHttpClientFactory>(), new Uri(metadata))
                    .GetAwaiter().GetResult();
                var idp = descriptor.IdPSsoDescriptor ?? throw new InvalidOperationException("SAML IdP metadata has no IdPSSODescriptor");
                configuration.AllowedIssuer = descriptor.EntityId;
                configuration.SingleSignOnDestination = idp.SingleSignOnServices.First().Location;
                foreach (var certificate in idp.SigningCertificates.Where(c => c.NotAfter > DateTime.UtcNow))
                    configuration.SignatureValidationCertificates.Add(certificate);
                if (configuration.SignatureValidationCertificates.Count == 0)
                    throw new InvalidOperationException("SAML IdP metadata has no valid signing certificate");
                configuration.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.Custom;
                configuration.CustomCertificateValidator = new MetadataCertificateValidator(
                    configuration.SignatureValidationCertificates);
            }
            return configuration;
        });
        builder.Services.AddSaml2();
        builder.Services.AddControllersWithViews();

        builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "IdentityService");

        builder.Host.UseSerilog((context, config) =>
            config.ReadFrom.Configuration(context.Configuration)
                        .Destructure.With<His.Hope.Infrastructure.Logging.PhiDestructuringPolicy>()
                        .Enrich.WithProperty("service", "identity-service"));

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHisHopeContractProblemDetails();

        builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
            options.UseHisHopeNpgsql(serviceProvider, builder.Configuration, "IdentityDb")
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<SoftDeleteInterceptor>()));
        builder.Services.AddHisHopeMigrationRunner<IdentityDbContext>();
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        builder.Services.AddHisHopeServicePlatform(builder.Configuration, "identity-service");

        // Use Redis distributed cache for token blacklist + refresh token storage (shared across services).
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis")
                ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
                ?? "localhost:6379";
            options.InstanceName = "HisHope:";
        });

        // IdentityService user-management requests do not use distributed locks, so keep
        // MediatR off Redis here to avoid an unnecessary IConnectionMultiplexer dependency.
        builder.Services.AddSingleton<ILockManager, NoOpLockManager>();
        builder.Services.AddSingleton<IdentityRedisLock>();
        builder.Services.AddSingleton<IUserSessionTracker, UserSessionTracker>();

        builder.Services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = builder.Environment.IsProduction() ? 14 : 8;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = builder.Environment.IsProduction() ? 5 : 10;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            if (builder.Environment.IsProduction())
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.SignIn.RequireConfirmedAccount = true;
            }
        })
        .AddRoles<His.Hope.IdentityService.Domain.Entities.Role>()
        .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddScoped<SignInManager<User>>();

        // SECURITY: JWT authentication with RSA public key validation
        His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);

        // Policy scheme: use JWT for API calls (Authorization header), cookie for browser.
        // This allows both cookie-based browser sessions and JWT-based API auth to coexist.
        const string policyScheme = "HisHope.BrowserOrApi";
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = policyScheme;
            options.DefaultAuthenticateScheme = policyScheme;
            options.DefaultChallengeScheme = policyScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        })
         .AddPolicyScheme(policyScheme, policyScheme, options =>
         {
             options.ForwardDefaultSelector = context =>
             {
                 // Interactive OIDC/browser endpoints must redirect to the
                 // server-rendered login page. API endpoints keep bearer 401s.
                 if (context.Request.Path.StartsWithSegments("/connect") ||
                     context.Request.Path.StartsWithSegments("/Account"))
                     return IdentityConstants.ApplicationScheme;
                 // Browser SSO probes must stay on the Identity cookie even when
                 // a stale hishop_sid injects a failing Authorization header.
                 if (context.Request.Path.StartsWithSegments("/api/v1/auth/session-status") ||
                     context.Request.Path.StartsWithSegments("/api/v1/auth/session/exchange"))
                     return IdentityConstants.ApplicationScheme;
                 // API bearer tokens use the shared RSA/JWS/JWE validator. This
                 // handles both OpenIddict access tokens and the BFF session
                 // token without relying on a proxy-specific header surviving
                 // every reverse-proxy path.
                 if (context.Request.Headers.ContainsKey("Authorization"))
                     return JwtBearerDefaults.AuthenticationScheme;
                 // Browser requests → cookie
                 return IdentityConstants.ApplicationScheme;
             };
         })
         .AddCookie(IdentityConstants.ApplicationScheme, options =>
         {
             options.Cookie.Name = "hishop_auth";
             options.Cookie.HttpOnly = true;
             options.Cookie.SameSite = SameSiteMode.Lax;
             options.Cookie.SecurePolicy = builder.Environment.IsProduction()
                 ? CookieSecurePolicy.Always
                 : CookieSecurePolicy.SameAsRequest;
             options.LoginPath = "/Account/Login";
             options.Events.OnRedirectToLogin = context =>
             {
                 if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
                     context.Request.Path.StartsWithSegments("/scim", StringComparison.OrdinalIgnoreCase))
                 {
                     context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                     return Task.CompletedTask;
                 }

                 context.Response.Redirect(context.RedirectUri);
                 return Task.CompletedTask;
             };
             options.Events.OnRedirectToAccessDenied = context =>
             {
                 if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                 {
                     context.Response.StatusCode = StatusCodes.Status403Forbidden;
                     return Task.CompletedTask;
                 }

                 context.Response.Redirect(context.RedirectUri);
                 return Task.CompletedTask;
             };
         })
         .AddCookie(IdentityConstants.ExternalScheme);

        // SSO cookie sharing is opt-in. Production deployments must configure the exact parent domain.
        var authCookieDomain = builder.Configuration["Authentication:CookieDomain"];
        if (!string.IsNullOrWhiteSpace(authCookieDomain))
        {
            builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                options => options.Cookie.Domain = authCookieDomain);
        }

        // ─── External Identity Providers (Federation) ───
        var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
        var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
        {
            builder.Services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.SaveTokens = true;
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                });
        }

        var msClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
        var msClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
        if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
        {
            builder.Services.AddAuthentication()
                .AddMicrosoftAccount(options =>
                {
                    options.ClientId = msClientId;
                    options.ClientSecret = msClientSecret;
                    options.SaveTokens = true;
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                });
        }

        // Enterprise Microsoft Entra ID federation. This is intentionally a
        // separate scheme from the consumer MicrosoftAccount handler so tenant,
        // issuer and scopes cannot be confused at login time.
        var entraClientId = builder.Configuration["Authentication:Entra:ClientId"];
        var entraClientSecret = builder.Configuration["Authentication:Entra:ClientSecret"];
        var entraAuthority = builder.Configuration["Authentication:Entra:Authority"];
        if (!string.IsNullOrWhiteSpace(entraClientId) &&
            !string.IsNullOrWhiteSpace(entraClientSecret) &&
            Uri.TryCreate(entraAuthority, UriKind.Absolute, out _))
        {
            builder.Services.AddAuthentication()
                .AddOpenIdConnect("Entra", options =>
                {
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                    options.Authority = entraAuthority;
                    options.ClientId = entraClientId;
                    options.ClientSecret = entraClientSecret;
                    options.ResponseType = "code";
                    options.UsePkce = true;
                    options.SaveTokens = false;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.TokenValidationParameters.RoleClaimType = "roles";
                });
        }

        // Configured external OIDC sources are isolated schemes. Names are
        // validated before registration so an upstream cannot select an
        // arbitrary authentication handler through the callback route.
        foreach (var source in builder.Configuration.GetSection("Authentication:ExternalSources").GetChildren())
        {
            var name = source["Name"];
            var authority = source["Authority"];
            var clientId = source["ClientId"];
            var clientSecret = source["ClientSecret"];
            if (string.IsNullOrWhiteSpace(name) || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_-]{1,31}$") ||
                !Uri.TryCreate(authority, UriKind.Absolute, out var sourceAuthority) || sourceAuthority.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                continue;
            builder.Services.AddAuthentication().AddOpenIdConnect(name, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Authority = sourceAuthority.ToString().TrimEnd('/');
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
            });
        }

        // SECURITY: Token blacklist service for JWT revocation
        builder.Services.AddHisHopeTokenBlacklist();

        // SECURITY: Register permission-based authorization policies
        builder.Services.AddHisHopeAuthorization();
        builder.Services.PostConfigure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
        {
            // Identity registers explicit authorization on secured routes. The
            // shared fallback policy rejects anonymous browser probes such as
            // /session-status whenever a stale BFF cookie injects bearer auth.
            options.FallbackPolicy = null;
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("ScimM2M", policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                {
                    var hasScimScope = ScimAuthorization.HasProvisioningScope(context.User);
                    var hasClientIdentity = context.User.HasClaim(claim =>
                        claim.Type is "client_id" or "azp" && !string.IsNullOrWhiteSpace(claim.Value));
                    var isWorkload = string.Equals(
                        context.User.FindFirst(AuthorizationConstants.Claims.PrincipalType)?.Value,
                        AuthorizationConstants.PrincipalTypes.Workload,
                        StringComparison.Ordinal);
                    if (hasScimScope && hasClientIdentity && isWorkload)
                        return true;

                    // Preserve existing integration-test/admin workflow only in
                    // non-production environments; production is M2M-only.
                    var http = context.Resource as HttpContext;
                    var environment = http?.RequestServices.GetService<IHostEnvironment>();
                    return environment?.IsDevelopment() == true ||
                           environment?.EnvironmentName == "Testing" && context.User.IsInRole("Admin");
                }));
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("ScimM2MRead", policy => policy.RequireAssertion(context =>
                ScimAuthorization.HasScope(context.User, ScimAuthorization.ReadScope) ||
                ScimAuthorization.HasScope(context.User, ScimAuthorization.WriteScope)))
            .AddPolicy("ScimM2MWrite", policy => policy.RequireAssertion(context =>
                ScimAuthorization.HasScope(context.User, ScimAuthorization.WriteScope)));

        // SECURITY: Facility/tenant boundary — scoped FacilityContext + authorization handler
        builder.Services.AddFacilityBoundary();
        builder.Services.AddScoped<JwtTokenGenerator>();
        builder.Services.AddScoped<IIdentityService, His.Hope.IdentityService.Infrastructure.Services.IdentityService>();
        builder.Services.AddHttpClient("security-signals", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient("directory-provisioning", client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddScoped<IProvisioningTarget, ScimOutboundProvisioningTarget>();
        builder.Services.AddScoped<IProvisioningTarget, EntraOutboundProvisioningTarget>();
        builder.Services.AddScoped<IProvisioningTarget, GoogleWorkspaceProvisioningTarget>();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<DirectoryProvisioningDispatcher>();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<SecuritySignalDispatcher>();
        builder.Services.AddScoped<TotpService>();
        builder.Services.AddScoped<RecoveryCodeService>();
        builder.Services.AddScoped<IdentityBrokerService>();
        builder.Services.AddScoped<BulkUserImportService>();

        // CORS for browser SPAs on separate localhost ports (BFF cookie + session exchange).
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                var productionOrigins = configuredOrigins.Length > 0
                    ? configuredOrigins
                    : new[] { "https://app.his-hope.vn", "https://dashboard.his-hope.vn", "https://admin.his-hope.vn" };

                policy
                    .AllowCredentials()
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");

                if (builder.Environment.IsProduction())
                {
                    policy.WithOrigins(productionOrigins)
                        .WithHeaders(
                            "Authorization", "DPoP", "Content-Type", "X-CSRF-Token",
                            "X-Correlation-ID", "Accept-Language", "X-Timezone", "X-Currency");
                }
                else
                {
                    // Dev/staging: allow any localhost port so new SPA apps (e.g. 4203)
                    // do not require a CORS whitelist edit for every preflight POST.
                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            if (string.IsNullOrWhiteSpace(origin))
                                return false;
                            if (origin.Equals("capacitor://localhost", StringComparison.OrdinalIgnoreCase))
                                return true;
                            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                                return false;
                            if (uri.Host is "localhost" or "127.0.0.1")
                                return true;
                            if (uri.Host.EndsWith(".his-hope.local", StringComparison.OrdinalIgnoreCase))
                                return true;
                            return configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
                        })
                        .AllowAnyHeader();
                }
            });
        });

        // Configure rate limiting specifically for auth endpoints
        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { errorCode = ApiErrorCodes.RateLimitExceeded }, cancellationToken);
            };
            string RateLimitKey(HttpContext context)
            {
                var configuredForwardedKey = context.Request.Headers["X-RateLimit-Key"].FirstOrDefault();
                if (builder.Configuration.GetValue("RateLimiting:TrustForwardedKey", false) &&
                    !string.IsNullOrWhiteSpace(configuredForwardedKey))
                    return configuredForwardedKey;
                return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }
            options.AddPolicy("auth", context =>
            {
                var key = RateLimitKey(context);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 30),
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
            options.AddPolicy("mfa", context =>
            {
                var key = RateLimitKey(context);
                return RateLimitPartition.GetFixedWindowLimiter($"mfa:{key}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue(
                        "RateLimiting:Mfa:PermitLimit",
                        builder.Configuration.GetValue("RateLimiting:MfaPermitLimit", 5)),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
            options.AddPolicy("scim", context =>
            {
                var key = RateLimitKey(context);
                return RateLimitPartition.GetFixedWindowLimiter($"scim:{key}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue(
                        "RateLimiting:Scim:PermitLimit",
                        builder.Configuration.GetValue("RateLimiting:ScimPermitLimit", 60)),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

        // SECURITY: Redis-backed refresh token store (replaces in-memory ConcurrentDictionary)
        builder.Services.AddSingleton<RedisRefreshTokenStore>();
        builder.Services.AddSingleton<IWorkloadSessionStore, RedisWorkloadSessionStore>();

        // Durable admin bulk/export jobs. Redis Streams provides at-least-once delivery;
        // job state and export results have bounded TTLs to avoid unbounded Redis growth.
        builder.Services.AddSingleton<RedisAdminJobStore>();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<AdminJobWorker>();

        // SECURITY: Binds tokens to (user_id, ip_hash, client_id) to prevent cross-IP replay attacks
        builder.Services.AddSingleton<TokenBindingService>();

        builder.Services.Configure<ConglomerateOptions>(
            builder.Configuration.GetSection(ConglomerateOptions.SectionName));
        builder.Services.AddIdentityApplication();
        builder.Services.AddSingleton<His.Hope.IdentityService.Application.DevicePosture.DevicePosturePolicyEvaluator>();

        // LDAP Sync service (disabled by default)
        builder.Services.AddScoped<ExternalIdentityProviderRuntime>();
        builder.Services.AddScoped<LdapSyncService>();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<LdapBackgroundService>();
        builder.Services.AddOptions<IdentityRetentionOptions>()
            .Bind(builder.Configuration.GetSection("IdentityRetention"))
            .Validate(settings => settings.MaxRowsPerRun is > 0 and <= 100_000,
                "IdentityRetention:MaxRowsPerRun must be between 1 and 100000.")
            .Validate(settings => settings.BatchSize is > 0 and <= 10_000,
                "IdentityRetention:BatchSize must be between 1 and 10000.")
            .ValidateOnStart();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<IdentityRetentionWorker>();

        // gRPC services
        builder.Services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        });
        builder.Services.AddGrpcReflection();

        // ─── Vault transit signing (production only; development uses ephemeral RSA) ───
        builder.Services.AddSingleton<IVaultKeyProvider, VaultKeyService>();
        builder.Services.AddSingleton<VaultClientSecretStore>();

        // Health checks: Vault, DB, Redis — tagged for readiness probe
        builder.Services.AddSingleton<DbHealthCheck>(sp => new DbHealthCheck(
            builder.Configuration.GetConnectionString("IdentityDb")!,
            sp.GetRequiredService<His.Hope.Secrets.IVaultDatabaseConnectionStringResolver>()));
        builder.Services.AddHealthChecks()
            .AddCheck<VaultHealthCheck>("vault-transit", tags: new[] { "ready" })
            .AddCheck<DbHealthCheck>("identity-db", tags: new[] { "ready" })
            .AddCheck("redis", new RedisHealthCheck(
                builder.Configuration.GetConnectionString("Redis")
                    ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
                    ?? "localhost:6379",
                builder.Configuration), tags: new[] { "ready" });

        // Persist the DataProtection key ring in Redis so cookies, BFF session protection,
        // and MFA encryption survive container replacement and work across replicas.
        var dataProtectionRedis = builder.Configuration.GetConnectionString("Redis")
            ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
            ?? "localhost:6379";
        var dataProtectionKeyName = builder.Configuration["DataProtection:KeyName"]
            ?? "HisHope:IdentityService:DataProtection:Keys";
        builder.Services.AddDataProtection()
            .SetApplicationName("His.Hope.IdentityService")
            .PersistKeysToStackExchangeRedis(
                RedisConnectionFactory.Connect(dataProtectionRedis, builder.Configuration),
                dataProtectionKeyName);
        builder.Services.AddSingleton<AssurancePolicyService>(sp =>
            new AssurancePolicyService(AssurancePolicyService.LoadConfiguredPolicy(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHostEnvironment>())));

        builder.Services.AddSingleton<SessionTokenProtector>();

        // MFA Secret Encryption: Vault transit (prod) or DataProtection (dev)
        var vaultTransitEnabled = builder.Configuration.GetValue("Vault:EnableTransit", builder.Environment.IsProduction());
        if (vaultTransitEnabled)
        {
            builder.Services.AddSingleton<IMfaSecretEncryptor, VaultMfaSecretEncryptor>();
        }
        else if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException("Vault:EnableTransit must be true in production for MFA secret encryption.");
        }
        else
        {
            builder.Services.AddSingleton<IMfaSecretEncryptor, AesMfaSecretEncryptor>();
        }

        builder.Services.AddSingleton<IDpopReplayCache>(sp =>
            new RedisDpopReplayCache(sp.GetRequiredService<IConnectionMultiplexer>()));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<DpopProofValidator>();

        // ─── OpenIddict OAuth2/OIDC Authorization Server ───
        var oidcConfig = builder.Configuration.GetSection("OpenIddict");
        var oidcSecurity = OidcSecurityConfiguration.Resolve(builder.Configuration, builder.Environment);

        builder.Services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<IdentityDbContext>();
            })
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(oidcConfig["Issuer"]!));

                options.SetAuthorizationEndpointUris(IdentityApiRoutes.OidcAuthorize);
                options.SetTokenEndpointUris(IdentityApiRoutes.OidcToken);
                options.SetLogoutEndpointUris(IdentityApiRoutes.OidcLogout);
                options.SetRevocationEndpointUris(IdentityApiRoutes.OidcRevoke);
                options.SetIntrospectionEndpointUris(IdentityApiRoutes.OidcIntrospect);

                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow()
                       .AllowClientCredentialsFlow()
                       .AllowCustomFlow(AuthorizationConstants.GrantTypes.TokenExchange)
                       .RequireProofKeyForCodeExchange();

                // Keep refresh-token payloads server-side and make redeemed tokens
                // unusable immediately. The custom API refresh path additionally uses
                // Redis family reuse detection; OpenIddict owns /connect/token.
                options.UseReferenceRefreshTokens();
                options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                // Register OIDC scopes used by SPA, dashboard, and admin apps
                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Roles,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    "hishop:permissions",
                    "hishop:admin",
                    "hishop:patients",
                    "hishop:appointments",
                    "hishop:clinical",
                    "hishop:lab",
                    "hishop:billing",
                    "hishop:pharmacy",
                    // Resource-specific scopes keep interoperability clients
                    // from turning a broad clinical permission into an
                    // unrestricted FHIR read surface.
                    "fhir.patient.read",
                    "fhir.encounter.read",
                    "platform.continuity.write");

                options.SetAccessTokenLifetime(TimeSpan.Parse(oidcConfig["AccessTokenLifetime"]!));
                options.SetRefreshTokenLifetime(TimeSpan.Parse(oidcConfig["RefreshTokenLifetime"]!));
                options.SetAuthorizationCodeLifetime(TimeSpan.Parse(oidcConfig["AuthorizationCodeLifetime"]!));

                if (oidcSecurity.SigningKey is not null)
                {
                    options.AddSigningKey(oidcSecurity.SigningKey);
                }
                else
                {
                    // Development only. Production fails fast in OidcSecurityConfiguration.
                    options.AddEphemeralSigningKey();
                }

                if (oidcSecurity.EncryptionKey is not null)
                {
                    foreach (var encryptionKey in oidcSecurity.EncryptionKeys)
                        options.AddEncryptionKey(encryptionKey);
                }
                else
                {
                    // Development only. Production fails fast in OidcSecurityConfiguration.
                    options.AddEphemeralEncryptionKey();
                }

                var aspNetCore = options.UseAspNetCore();
                options.AddEventHandler(FixDiscoveryBaseUriHandler.Descriptor);
                options.AddEventHandler(DpopTokenBindingHandler.Descriptor);
                options.AddEventHandler(DpopTokenResponseHandler.Descriptor);
                options.AddEventHandler(CustomValidateAuthorizationRequest.Descriptor);
                options.AddEventHandler(CustomPopulateTokenClaims.Descriptor);
                options.AddEventHandler(CustomHandleClientCredentialsRequest.Descriptor);
                options.AddEventHandler(CustomHandleTokenExchangeRequest.Descriptor);
                if (builder.Environment.IsDevelopment() || oidcConfig.GetValue<bool>("AllowInsecureHttp"))
                    aspNetCore.DisableTransportSecurityRequirement();

                aspNetCore.EnableAuthorizationEndpointPassthrough()
                           .EnableLogoutEndpointPassthrough()
                           .EnableStatusCodePagesIntegration();
            })
            .AddValidation(options =>
            {
                // Keep API validation aligned with the public OIDC issuer used by the gateway.
                // UseLocalServer shares signing keys, while SetIssuer prevents the internal
                // container hostname from becoming the accepted token issuer.
                options.SetIssuer(new Uri(oidcConfig["Issuer"]!));
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        // PHI Audit with durable background processing (HIPAA 164.312(b))
        // Audit events must not be discarded under load. The worker drains an
        // unbounded in-process queue to the durable database; shutdown drains
        // remaining entries before completing.
        var auditChannel = System.Threading.Channels.Channel.CreateUnbounded<His.Hope.Infrastructure.Audit.PhiAuditEntry>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        builder.Services.AddSingleton(auditChannel);

        var defaultAuditDescriptor = builder.Services.FirstOrDefault(
            sd => sd.ServiceType == typeof(His.Hope.Infrastructure.Audit.IAuditService));
        if (defaultAuditDescriptor != null)
            builder.Services.Remove(defaultAuditDescriptor);

        builder.Services.AddSingleton<DatabaseAuditService>();
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Services.AddHostedService<DatabaseAuditBackgroundService>();

        builder.Services.AddSingleton<His.Hope.Infrastructure.Audit.IAuditService>(sp =>
        {
            var serilogAudit = new His.Hope.Infrastructure.Audit.AuditService();
            var dbAudit = sp.GetRequiredService<DatabaseAuditService>();
            return new CompositeAuditService(serilogAudit, dbAudit);
        });
        var defaultObservabilityAuditSink = builder.Services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IAuditSink));
        if (defaultObservabilityAuditSink != null)
            builder.Services.Remove(defaultObservabilityAuditSink);
        builder.Services.AddSingleton<IdentityObservabilityAuditSink>();
        builder.Services.AddHttpClient<SiemWormAuditForwarder>();
        builder.Services.AddSingleton<SiemWormAuditForwarder>();
        builder.Services.AddSingleton<IAuditSink, IdentityDurableAuditSink>();

        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(System.Net.IPAddress.Any, 5003, listenOptions =>
                {
                    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                });
                options.Listen(System.Net.IPAddress.Any, 5007, listenOptions =>
                {
                    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                });
            });
        }
    }
}
