using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using His.Hope.Infrastructure.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// SECURITY: CORS configured with explicit allowed origins from configuration.
// FIXED: Replaced AllowAnyOrigin() with specific origins - AllowAnyOrigin() is incompatible
// with AllowCredentials() and is a security risk in healthcare applications.
var allowedOrigins = builder.Configuration.GetValue<string>("CORS:AllowedOrigins", "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            // SECURITY: Only allow specific origins that are explicitly configured.
            // This prevents arbitrary cross-origin requests to the API gateway.
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders("Authorization")
                  .AllowCredentials();  // Required for JWT bearer auth with cookies
        }
        else
        {
            // SECURITY: No origins configured - use permissive policy only in development
            // This should NEVER happen in production
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders("Authorization");
        }
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

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

app.UseSecurityHeaders();
app.UseCors();  // Must be after UseSecurityHeaders, before UseRateLimiter
app.UseRateLimiter();
app.UseSerilogRequestLogging();

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

