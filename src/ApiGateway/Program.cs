using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.RateLimiting;
using His.Hope.Infrastructure.Idempotency;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Qos;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using His.Hope.AspNetCore;
using His.Hope.Observability;
using Serilog;
using StackExchange.Redis;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.DataProtection;
using His.Hope.Configuration;

var builder = WebApplication.CreateBuilder(args);
var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(builder.Configuration, "ApiGateway");

builder.Services.AddHisHopeRuntimeConfiguration(builder.Configuration, "ApiGateway");

var reverseProxyClusters = new Dictionary<string, string?>
{
    ["ReverseProxy:Clusters:identity:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("identity-api").ToString(),
    ["ReverseProxy:Clusters:patients:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("patient-api").ToString(),
    ["ReverseProxy:Clusters:appointments:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("appointment-api").ToString(),
    ["ReverseProxy:Clusters:clinical:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("clinical-api").ToString(),
    ["ReverseProxy:Clusters:lab:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("lab-api").ToString(),
    ["ReverseProxy:Clusters:billing:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("billing-api").ToString(),
    ["ReverseProxy:Clusters:pharmacy:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("pharmacy-api").ToString(),
    ["ReverseProxy:Clusters:lab-bff:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("lab-bff").ToString(),
    ["ReverseProxy:Clusters:billing-bff:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("billing-bff").ToString(),
    ["ReverseProxy:Clusters:dashboard-bff:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("dashboard-bff").ToString(),
    ["ReverseProxy:Clusters:systemdashboard-bff:Destinations:dest:Address"] = runtimeEndpoints.GetRequired("systemdashboard-bff").ToString(),
    // The runtime contract exposes SERVICE_DATABASE_CONTINUITY_URL. Keep the
    // logical key identical across Compose, VM and Kubernetes so the gateway
    // never falls back to localhost inside its own container.
    ["ReverseProxy:Clusters:database-continuity:Destinations:database-continuity/dest:Address"] = runtimeEndpoints.GetRequired("database-continuity").ToString()
};

// Manufacturing is an optional vertical slice during the rollout. Keep the
// gateway bootable in environments that have not deployed it yet, while
// enabling the buyer app route whenever SERVICE_MANUFACTURING_URL is present.
var manufacturingEndpoint = runtimeEndpoints.GetOptional("manufacturing");
if (manufacturingEndpoint is not null)
{
    reverseProxyClusters["ReverseProxy:Clusters:manufacturing:Destinations:dest:Address"] = manufacturingEndpoint.ToString();
    reverseProxyClusters["ReverseProxy:Routes:manufacturing:ClusterId"] = "manufacturing";
    reverseProxyClusters["ReverseProxy:Routes:manufacturing:Match:Path"] = "/api/v1/manufacturing/{**catch-all}";
}
builder.Configuration.AddInMemoryCollection(reverseProxyClusters);
builder.Services.AddHisHopeAspNetCore();
builder.Services.AddObservability(options => options.ServiceName = "ApiGateway");

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// SECURITY: CORS configured with explicit allowed origins from configuration.
// CORS uses explicit configured origins and never falls back to a wildcard.
// with credentials. Methods and headers are explicitly allowlisted.
var allowedOrigins = builder.Configuration.GetValue<string>("CORS:AllowedOrigins", "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length == 0)
            throw new InvalidOperationException("CORS:AllowedOrigins must contain at least one explicit origin.");

        // SECURITY: only configured origins are allowed. Never fall back to a wildcard.
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .WithHeaders("Accept", "Authorization", "DPoP", "Content-Type", "If-Match", "If-None-Match",
                  "X-Correlation-ID", "X-CSRF-Token", "X-Requested-With", "X-Timezone", "X-Currency")
              .WithExposedHeaders("Authorization", "ETag", "X-Correlation-ID")
              .AllowCredentials();
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<DpopHeaderTransform>()
    .ConfigureHttpClient((context, handler) =>
    {
        // Recycle connections every 2 minutes to prevent stale connections
        // across container restarts, while still benefiting from pooling.
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
        handler.MaxConnectionsPerServer = 20;
    });

// BFF session bridge: keep access tokens server-side in Redis and attach the
// current session JWT only to internal downstream proxy requests.
var redisConnection = RuntimeConfigurationExtensions.ToRedisConnectionString(
    runtimeEndpoints.GetRequired("redis"));
var redis = RedisConnectionFactory.Connect(redisConnection, builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDataProtection()
    .SetApplicationName("His.Hope.IdentityService")
    .PersistKeysToStackExchangeRedis(
        redis,
        builder.Configuration["DataProtection:KeyName"]
            ?? "HisHope:IdentityService:DataProtection:Keys");
builder.Services.AddSingleton<SessionTokenProtector>();

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

// === QoS: 5-tier request priority admission control ===
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new PriorityAdmissionOptions();
    config.GetSection("PriorityAdmission").Bind(options);
    return options;
});

// === Idempotency: safe retries for POST/PUT/PATCH requests ===
builder.Services.AddIdempotency(builder.Configuration);

// === Rate limiting per IP + per user for the gateway ===
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                // Keep the gateway limiter configurable. A single admin/IAM
                // route loads several read endpoints in parallel; the old
                // hard-coded 100 requests/minute caused legitimate navigation
                // to receive 429s and made the UI appear intermittently down.
                // Authentication endpoints retain their stricter named policy.
                PermitLimit = builder.Configuration.GetValue("RateLimiting:MaxRequestsPerIp", 1000),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:WindowMinutes", 1)),
                QueueLimit = builder.Configuration.GetValue("RateLimiting:QueueLimit", 25),
            }));
});

builder.WebHost.ConfigureKestrel(options =>
{
    var env = builder.Environment;
    if (env.IsDevelopment())
    {
        // Development: HTTP only on port 5000
        options.ListenAnyIP(5000, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
    }
    else
    {
        // Production: HTTPS on 5000, HTTP on 5011
        options.ListenAnyIP(5000, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
        options.ListenAnyIP(5011, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
    }
});

var app = builder.Build();

app.UseHisHopeAspNetCore();

app.UseSecurityHeaders();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Account"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data:; " +
                "connect-src 'self' http://localhost:5000 http://localhost:8081 http://localhost:8082 http://localhost:8083";
            return Task.CompletedTask;
        });
    }

    await next();
});
app.UseCors();  // Must be after UseSecurityHeaders, before UseRateLimiter
app.UseRateLimiter();
app.UseSerilogRequestLogging();

// YARP forwards OIDC responses from the container network. Rewrite absolute
// IdentityService redirects to the public gateway origin before they reach a
// browser; Docker hostnames must never be exposed to SPA clients.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/connect") ||
        context.Request.Path.StartsWithSegments("/Account"))
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.Headers.TryGetValue("Location", out var location))
            {
                var publicOrigin = $"{context.Request.Scheme}://{context.Request.Host}".TrimEnd('/');
                context.Response.Headers.Location = location.ToString()
                    .Replace("http://identityservice:5003", publicOrigin, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://identityservice:5001", publicOrigin, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://identityservice:5000", publicOrigin, StringComparison.OrdinalIgnoreCase)
                    // OpenIddict uses its configured issuer when creating the
                    // login redirect. Keep the host selected by the caller so
                    // native clients do not receive a localhost or Docker URL.
                    .Replace("http://localhost:5001", publicOrigin, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://localhost:5000", publicOrigin, StringComparison.OrdinalIgnoreCase);
            }

            return Task.CompletedTask;
        });
    }

    await next();
});

// QoS: Resolve X-Priority header (P0–P4, default P1) and store in HttpContext.Items
app.UsePriorityHeader();

// QoS: Admission control — shed low-priority requests when service is at capacity
app.UsePriorityAdmission();

app.UseIdempotency();

app.Use(async (context, next) =>
{
    var cookieBackedIdentityRoute =
        // OIDC and server-rendered account endpoints must authenticate with
        // the Identity application cookie. Injecting the BFF HMAC session JWT
        // here makes Identity select JWT validation and breaks /connect/authorize.
        context.Request.Path.StartsWithSegments("/connect") ||
        context.Request.Path.StartsWithSegments("/Account") ||
        context.Request.Path.StartsWithSegments("/api/v1/auth") ||
        // Database continuity has its own shared-session bridge and policy
        // boundary. Let it unprotect hishop_sid locally instead of receiving
        // the gateway's generic bearer projection.
        context.Request.Path.StartsWithSegments("/api/v1/admin/database-continuity");
    var continuityRoute = context.Request.Path.StartsWithSegments("/api/v1/admin/database-continuity");

    var hasSessionCookie = context.Request.Cookies.TryGetValue("hishop_sid", out var sessionId) &&
        !string.IsNullOrWhiteSpace(sessionId);

    // Identity's policy scheme intentionally selects its browser cookie when
    // no bearer is present. Do not replace that cookie flow with the session
    // JWT on admin/settings/audit/auth endpoints; those endpoints validate
    // OIDC bearer tokens or the Identity application cookie.
    if (hasSessionCookie &&
        (!cookieBackedIdentityRoute || continuityRoute) &&
        (!context.Request.Headers.ContainsKey("Authorization") || continuityRoute))
    {
        var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var tokenProtector = context.RequestServices.GetRequiredService<SessionTokenProtector>();
        var db = redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{sessionId}");
        if (sessionJson.HasValue)
        {
            try
            {
                using var document = JsonDocument.Parse((string)sessionJson!);
                if (document.RootElement.TryGetProperty("Jwt", out var jwtElement))
                {
                    var protectedJwt = jwtElement.GetString();
                    if (!string.IsNullOrWhiteSpace(protectedJwt))
                    {
                        if (continuityRoute)
                        {
                            // Keep the protected session token opaque at the
                            // gateway boundary. The continuity service owns
                            // the same DataProtection key ring and unwraps
                            // X-HisHope-Session-Token immediately before JWT
                            // authentication. Unprotecting here first made a
                            // transient key-ring mismatch degrade to an empty
                            // Authorization header and an unexplained 401.
                            context.Request.Headers.Remove("Authorization");
                            context.Request.Headers["X-HisHope-Session-Token"] = protectedJwt;
                            await next();
                            return;
                        }
                        var jwt = tokenProtector.Unprotect(protectedJwt);
                        context.Request.Headers.Authorization = $"Bearer {jwt}";
                        // Mark the internal BFF session token so IdentityService
                        // selects its RSA/JWE validator instead of treating it
                        // as an OpenIddict access token.
                        context.Request.Headers["X-HisHope-Session"] = "1";
                    }
                }
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Never forward plaintext or invalid session tokens.
            }
        }
    }

    await next();
});

// Cookie-authenticated browser mutations must present the synchronizer token.
// The session exchange is exempt because it creates the CSRF cookie.
app.Use(async (context, next) =>
{
      if (context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE"
          && context.Request.Path.StartsWithSegments("/api")
          && !context.Request.Path.StartsWithSegments("/api/v1/auth/session/exchange")
          // Preserve logout for sessions issued before the CSRF cookie rollout.
          && !context.Request.Path.StartsWithSegments("/api/v1/auth/logout"))
    {
        var sessionId = context.Request.Cookies["hishop_sid"];
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var csrfCookie = context.Request.Cookies["hishop_csrf"];
            var csrfHeader = context.Request.Headers["X-CSRF-Token"].FirstOrDefault();
            var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
            var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
            var valid = false;
            if (sessionJson.HasValue && !string.IsNullOrWhiteSpace(csrfCookie) && !string.IsNullOrWhiteSpace(csrfHeader))
            {
                try
                {
                    using var document = JsonDocument.Parse((string)sessionJson!);
                    var expected = document.RootElement.TryGetProperty("CsrfToken", out var csrf)
                        ? csrf.GetString()
                        : null;
                    valid = !string.IsNullOrWhiteSpace(expected)
                        && CryptographicOperations.FixedTimeEquals(
                            System.Text.Encoding.UTF8.GetBytes(expected),
                            System.Text.Encoding.UTF8.GetBytes(csrfCookie))
                        && CryptographicOperations.FixedTimeEquals(
                            System.Text.Encoding.UTF8.GetBytes(expected),
                            System.Text.Encoding.UTF8.GetBytes(csrfHeader));
                }
                catch (JsonException) { }
            }

            if (!valid)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
    }

    await next();
});

// Preserve the public origin for downstream DPoP proof validation. YARP does
// not guarantee these headers for every configured route, so set them from
// the already-routed gateway request and never trust client-supplied values.
app.Use(async (context, next) =>
{
    context.Request.Headers["X-Forwarded-Proto"] = context.Request.Scheme;
    context.Request.Headers["X-Forwarded-Host"] = context.Request.Host.Value;
    await next();
});

app.MapGet("/diag/patients", async (HttpContext ctx) =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var resp = await http.GetAsync(new Uri(
            runtimeEndpoints.GetRequired("patient-api"),
            "api/v1/patients/search?q=&page=1&pageSize=1"));
        sw.Stop();
        await ctx.Response.WriteAsync($"OK: {resp.StatusCode} in {sw.ElapsedMilliseconds}ms");
    }
    catch (Exception ex)
    {
        sw.Stop();
        await ctx.Response.WriteAsync($"FAIL: {ex.GetType().Name}: {ex.Message} after {sw.ElapsedMilliseconds}ms");
    }
});

// Shared proxy helper — bypasses YARP HttpForwarder connection pool issues
// that cause 60s timeouts when proxying to backend services.
// Direct HttpClient works instantly (14ms vs 60000ms through YARP).
var proxyHandler = new System.Net.Http.SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 20,
    ConnectTimeout = TimeSpan.FromSeconds(5),
};
var proxyClient = new HttpClient(proxyHandler)
{
    Timeout = TimeSpan.FromSeconds(25),
};

void MapDirectProxy(
    string pathPrefix,
    Uri serviceBaseAddress,
    Func<HttpContext, bool>? additionalMatch = null)
{
    app.Use(async (ctx, next) =>
    {
        if (!ctx.Request.Path.StartsWithSegments(pathPrefix) ||
            (additionalMatch is not null && !additionalMatch(ctx)))
        {
            await next();
            return;
        }

        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("DirectProxy");
        logger.LogInformation("DirectProxy MATCH: path={Path} -> {BaseAddress}", ctx.Request.Path, serviceBaseAddress);

        var catchAll = ctx.Request.Path.Value![pathPrefix.Length..];
        var targetUrl = new Uri(serviceBaseAddress, $"{pathPrefix.TrimStart('/')}{catchAll}{ctx.Request.QueryString}");
        using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetUrl);

        if (ctx.Request.ContentLength > 0)
        {
            request.Content = new StreamContent(ctx.Request.Body);
            if (ctx.Request.Headers.TryGetValue("Content-Type", out var ct))
                request.Content.Headers.TryAddWithoutValidation("Content-Type", ct.ToArray());
        }

        foreach (var h in ctx.Request.Headers)
        {
            if (h.Key is "Authorization" or "X-HisHope-Session" or "Accept" or "Accept-Language"
                or "X-Correlation-ID" or "X-Timezone" or "X-Currency" or "Content-Type")
                try { request.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray()); } catch { }
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(25));
            using var resp = await proxyClient.SendAsync(request, cts.Token);
            logger.LogInformation("DirectProxy RESP: status={Status} contentType={CT} contentLen={Len}",
                (int)resp.StatusCode, resp.Content.Headers.ContentType, resp.Content.Headers.ContentLength);
            ctx.Response.StatusCode = (int)resp.StatusCode;
            foreach (var h in resp.Headers)
            {
                if (h.Key is not ("Connection" or "Keep-Alive" or "Proxy-Authenticate" or
                    "Proxy-Authorization" or "TE" or "Trailer" or "Transfer-Encoding" or "Upgrade"))
                    ctx.Response.Headers[h.Key] = h.Value.ToArray();
            }

            foreach (var h in resp.Content.Headers)
            {
                if (h.Key is "Content-Length" or "Transfer-Encoding")
                    continue;

                ctx.Response.Headers[h.Key] = h.Value.ToArray();
            }

            if (resp.Content.Headers.ContentLength is long contentLength)
                ctx.Response.ContentLength = contentLength;

            await resp.Content.CopyToAsync(ctx.Response.Body, cts.Token);
            logger.LogInformation("DirectProxy DONE: path={Path}", ctx.Request.Path);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("DirectProxy CANCELED by client: path={Path}", ctx.Request.Path);
            if (!ctx.Response.HasStarted)
                ctx.Response.StatusCode = 499;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "DirectProxy TIMEOUT: path={Path}", ctx.Request.Path);
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                await ctx.Response.WriteAsync("Proxy upstream timeout");
            }
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 502;
            await ctx.Response.WriteAsync($"Proxy error: {ex.GetType().Name}: {ex.Message}");
        }
    });
}

static bool MatchesPatientAggregate(HttpContext ctx, string resource)
{
    var segments = ctx.Request.Path.Value?
        .Split('/', StringSplitOptions.RemoveEmptyEntries);

    return segments is { Length: 5 } &&
        segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) &&
        segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase) &&
        segments[2].Equals("patients", StringComparison.OrdinalIgnoreCase) &&
        Guid.TryParse(segments[3], out _) &&
        segments[4].Equals(resource, StringComparison.OrdinalIgnoreCase);
}

// Patient aggregate routes must be registered before the broad patient proxy;
// otherwise /api/v1/patients/{id}/... is incorrectly sent to PatientService.
MapDirectProxy("/api/v1/patients", runtimeEndpoints.GetRequired("clinical-api"),
    ctx => MatchesPatientAggregate(ctx, "encounters"));
MapDirectProxy("/api/v1/patients", runtimeEndpoints.GetRequired("appointment-api"),
    ctx => MatchesPatientAggregate(ctx, "appointments"));
MapDirectProxy("/api/v1/patients", runtimeEndpoints.GetRequired("lab-api"),
    ctx => MatchesPatientAggregate(ctx, "lab-orders"));
MapDirectProxy("/api/v1/patients", runtimeEndpoints.GetRequired("pharmacy-api"),
    ctx => MatchesPatientAggregate(ctx, "prescriptions"));
MapDirectProxy("/api/v1/patients", runtimeEndpoints.GetRequired("billing-api"),
    ctx => MatchesPatientAggregate(ctx, "invoices"));

MapDirectProxy("/api/v1/patients", runtimeEndpoints.GetRequired("patient-api"));
MapDirectProxy("/api/v1/encounters", runtimeEndpoints.GetRequired("clinical-api"));
MapDirectProxy("/api/v1/medications", runtimeEndpoints.GetRequired("pharmacy-api"));
MapDirectProxy("/api/v1/lab-orders", runtimeEndpoints.GetRequired("lab-api"));
MapDirectProxy("/api/v1/invoices", runtimeEndpoints.GetRequired("billing-api"));

app.MapReverseProxy();

app.MapGet("/", () => Results.Ok(new
{
    service = "His.Hope API Gateway",
    version = "1.0.0",
    status = "running",
    endpoints = new[] { "/api/v1/auth", "/api/v1/patients", "/api/v1/appointments", "/api/v1/encounters", "/api/v1/invoices", "/api/v1/lab-orders", "/api/v1/medications", "/api/v1/prescriptions" }
}));

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key, status = e.Value.Status.ToString(),
                error = e.Value.Exception?.Message
            })
        });
    }
}).AllowAnonymous();

app.Run();

static X509Certificate2 LoadCertificate(IConfiguration config)
{
    var certPath = config["Certificates:Path"];
    var certPassword = config["Certificates:Password"];
    if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword))
        return new X509Certificate2(certPath, certPassword);
    var pfxPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "server.pfx");
    if (File.Exists(pfxPath))
        return new X509Certificate2(pfxPath, "his-hope-dev");
    return CreateDevCertificate();
}

static X509Certificate2 CreateDevCertificate()
{
    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
        "CN=his-hope-gateway, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName("apigateway"); san.AddDnsName("*.his-hope.internal");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Certificates"));
    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Certificates", "server.pfx"),
        cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "his-hope-dev"));
    return cert;
}

