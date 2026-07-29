using His.Hope.AspNetCore;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Observability;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
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
using His.Hope.IdentityService.Api.Configuration;
using His.Hope.IdentityService.Api.Handlers;
using His.Hope.IdentityService.Application;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.Persistence;
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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore.Configuration;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.EntityFrameworkCore;
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
        builder.Services.AddScoped<SamlRuntimeConfigurationService>();
        builder.Services.AddScoped<IPushDeliveryService, PushDeliveryService>();
        builder.Services.AddScoped<OidcLoginCompletionService>();
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
        
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb"))
                .UseSnakeCaseNamingConvention());
        builder.Services.AddHisHopeMigrationRunner<IdentityDbContext>();
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        
        builder.Services.AddHisHopeEnterpriseInfrastructure(
            builder.Configuration,
            "identity-service",
            builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));
        
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
        builder.Services.AddSingleton<IUserSessionTracker, UserSessionTracker>();
        
        builder.Services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
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
                // API calls with Bearer token → validate via OpenIddict (knows RSA keys)
                if (context.Request.Headers.ContainsKey("Authorization"))
                    return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                // Browser requests → cookie
                 return IdentityConstants.ApplicationScheme;
             };
         })
         .AddCookie(IdentityConstants.ApplicationScheme, options =>
         {
             options.Cookie.Name = "hishop_auth";
             options.Cookie.HttpOnly = true;
             options.Cookie.SameSite = SameSiteMode.Lax;
             options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
        
        // SECURITY: Token blacklist service for JWT revocation
        builder.Services.AddHisHopeTokenBlacklist();
        
        // SECURITY: Register permission-based authorization policies
        builder.Services.AddHisHopeAuthorization();
        
        // SECURITY: Facility/tenant boundary — scoped FacilityContext + authorization handler
        builder.Services.AddFacilityBoundary();
        builder.Services.AddScoped<JwtTokenGenerator>();
        builder.Services.AddScoped<IIdentityService, His.Hope.IdentityService.Infrastructure.Services.IdentityService>();
        builder.Services.AddScoped<TotpService>();
        builder.Services.AddScoped<RecoveryCodeService>();
        builder.Services.AddScoped<IdentityBrokerService>();
        builder.Services.AddScoped<BulkUserImportService>();
        
        // CORS for dashboard app (separate origin)
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(
                        "http://localhost:8081", "http://localhost:8082", "http://localhost:8083",
                        "http://localhost:4200", "http://localhost:4201", "http://localhost:4202", "http://localhost:4300",
                        "https://localhost", "http://localhost", "capacitor://localhost")
                    .WithHeaders("Authorization", "DPoP", "Content-Type", "X-CSRF-Token", "X-Correlation-ID")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .AllowCredentials();
            });
        });
        
        // Configure rate limiting specifically for auth endpoints
        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "rate_limit_exceeded" }, cancellationToken);
            };
            options.AddPolicy("auth", context =>
            {
                var key = context.Request.Headers["X-RateLimit-Key"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
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
                var key = context.Request.Headers["X-RateLimit-Key"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
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
                var key = context.Request.Headers["X-RateLimit-Key"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
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
        
        // Durable admin bulk/export jobs. Redis Streams provides at-least-once delivery;
        // job state and export results have bounded TTLs to avoid unbounded Redis growth.
        builder.Services.AddSingleton<RedisAdminJobStore>();
        builder.Services.AddHostedService<AdminJobWorker>();
        
        // SECURITY: Binds tokens to (user_id, ip_hash, client_id) to prevent cross-IP replay attacks
        builder.Services.AddSingleton<TokenBindingService>();
        
        builder.Services.AddIdentityApplication();
        
        // LDAP Sync service (disabled by default)
        builder.Services.AddScoped<ExternalIdentityProviderRuntime>();
        builder.Services.AddScoped<LdapSyncService>();
        builder.Services.AddHostedService<LdapBackgroundService>();
        
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
        builder.Services.AddHealthChecks()
            .AddCheck<VaultHealthCheck>("vault-transit", tags: new[] { "ready" })
            .AddCheck("identity-db", new DbHealthCheck(
                builder.Configuration.GetConnectionString("IdentityDb")!), tags: new[] { "ready" })
            .AddCheck("redis", new RedisHealthCheck(
                builder.Configuration.GetConnectionString("Redis")
                    ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
                    ?? "localhost:6379"), tags: new[] { "ready" });
        
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
                ConnectionMultiplexer.Connect(dataProtectionRedis),
                dataProtectionKeyName);
        builder.Services.AddSingleton<SessionTokenProtector>();
        
        // MFA Secret Encryption: Vault transit (prod) or DataProtection (dev)
        var vaultTransitEnabled = builder.Configuration.GetValue("Vault:EnableTransit", builder.Environment.IsProduction());
        if (vaultTransitEnabled)
        {
            builder.Services.AddSingleton<IMfaSecretEncryptor, VaultMfaSecretEncryptor>();
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
        
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                options.SetLogoutEndpointUris("/connect/logout");
                options.SetRevocationEndpointUris("/connect/revoke");
                options.SetIntrospectionEndpointUris("/connect/introspect");
        
                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow()
                       .AllowClientCredentialsFlow()
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
                    "hishop:admin");
        
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
        var auditChannel = System.Threading.Channels.Channel.CreateBounded<His.Hope.Infrastructure.Audit.PhiAuditEntry>(
            new System.Threading.Channels.BoundedChannelOptions(10_000)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
            });
        builder.Services.AddSingleton(auditChannel);
        
        var defaultAuditDescriptor = builder.Services.FirstOrDefault(
            sd => sd.ServiceType == typeof(His.Hope.Infrastructure.Audit.IAuditService));
        if (defaultAuditDescriptor != null)
            builder.Services.Remove(defaultAuditDescriptor);
        
        builder.Services.AddSingleton<DatabaseAuditService>();
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
        builder.Services.AddSingleton<IAuditSink, IdentityObservabilityAuditSink>();
        
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5001, listenOptions =>
                {
                    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                });
                options.ListenAnyIP(5012, listenOptions =>
                {
                    listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                });
            });
        }
    }
}
