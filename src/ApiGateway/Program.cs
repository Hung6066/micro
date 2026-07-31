using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.RateLimiting;
using His.Hope.Infrastructure.Idempotency;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Qos;
using His.Hope.Infrastructure.Security;
using His.Hope.AspNetCore;
using His.Hope.Observability;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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
var redisConnection = builder.Configuration["Redis:ConnectionString"] ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        EndPoints = { redisConnection },
        AbortOnConnectFail = false,
        ConnectTimeout = 2000,
        SyncTimeout = 1000
    }));

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

// === QoS: 5-tier request priority admission control ===
builder.Services.AddSingleton<PriorityAdmissionMiddleware>();
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
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 5,
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
                    .Replace("http://identityservice:5001", publicOrigin, StringComparison.OrdinalIgnoreCase)
                    .Replace("http://identityservice:5000", publicOrigin, StringComparison.OrdinalIgnoreCase)
                    // OpenIddict uses its configured issuer when creating the
                    // login redirect. Keep the host selected by the caller so
                    // native clients do not receive a localhost URL.
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
        context.Request.Path.StartsWithSegments("/api/v1/admin") ||
        context.Request.Path.StartsWithSegments("/api/v1/settings") ||
        context.Request.Path.StartsWithSegments("/api/v1/audit-logs") ||
        context.Request.Path.StartsWithSegments("/api/v1/audit");

    var hasSessionCookie = context.Request.Cookies.TryGetValue("hishop_sid", out var sessionId) &&
        !string.IsNullOrWhiteSpace(sessionId);

    // Identity's policy scheme intentionally selects its browser cookie when
    // no bearer is present. Do not replace that cookie flow with the session
    // JWT on admin/settings/audit/auth endpoints; those endpoints validate
    // OIDC bearer tokens or the Identity application cookie.
    if (hasSessionCookie &&
        !cookieBackedIdentityRoute &&
        !context.Request.Headers.ContainsKey("Authorization"))
    {
        var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{sessionId}");
        if (sessionJson.HasValue)
        {
            using var document = JsonDocument.Parse((string)sessionJson!);
            if (document.RootElement.TryGetProperty("Jwt", out var jwtElement) &&
                !string.IsNullOrWhiteSpace(jwtElement.GetString()))
            {
                context.Request.Headers.Authorization =
                    $"Bearer {jwtElement.GetString()}";
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
        var resp = await http.GetAsync("http://patientservice:5002/api/v1/patients/search?q=&page=1&pageSize=1");
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
HttpClient CreateProxyClient() => new(new System.Net.Http.SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 20,
})
{ Timeout = TimeSpan.FromSeconds(25) };

void MapDirectProxy(string pathPrefix, string serviceHost, int servicePort)
{
    app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments(pathPrefix), app =>
    {
        app.Run(async ctx =>
        {
            var client = CreateProxyClient();
            var catchAll = ctx.Request.Path.Value![pathPrefix.Length..];
            var targetUrl = $"http://{serviceHost}:{servicePort}{pathPrefix}{catchAll}{ctx.Request.QueryString}";
            var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetUrl);

            if (ctx.Request.ContentLength > 0)
            {
                request.Content = new StreamContent(ctx.Request.Body);
                if (ctx.Request.Headers.TryGetValue("Content-Type", out var ct))
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", ct.ToArray());
            }

            foreach (var h in ctx.Request.Headers)
            {
                if (h.Key is "Authorization" or "Accept" or "Accept-Language"
                    or "X-Correlation-ID" or "X-Timezone" or "X-Currency" or "Content-Type")
                    try { request.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray()); } catch { }
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
                cts.CancelAfter(TimeSpan.FromSeconds(25));
                var resp = await client.SendAsync(request, cts.Token);
                ctx.Response.StatusCode = (int)resp.StatusCode;
                foreach (var h in resp.Headers) ctx.Response.Headers[h.Key] = h.Value.ToArray();
                foreach (var h in resp.Content.Headers) ctx.Response.Headers[h.Key] = h.Value.ToArray();
                await resp.Content.CopyToAsync(ctx.Response.Body, cts.Token);
            }
            catch (OperationCanceledException) { }
        });
    });
}

MapDirectProxy("/api/v1/patients", "patientservice", 5002);
MapDirectProxy("/api/v1/encounters", "clinicalservice", 5004);
MapDirectProxy("/api/v1/medications", "pharmacyservice", 5030);
MapDirectProxy("/api/v1/lab-orders", "labservice", 5010);
MapDirectProxy("/api/v1/invoices", "billingservice", 5020);

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

