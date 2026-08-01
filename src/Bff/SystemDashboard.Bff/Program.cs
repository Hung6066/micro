using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Security;
using His.Hope.AspNetCore;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Observability;
using His.Hope.Observability.OpenTelemetry;
using His.Hope.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Hubs;
using SystemDashboard.Bff.Middleware;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisHopeAspNetCore();
builder.Services.AddObservability(options => options.ServiceName = "SystemDashboard.Bff");
builder.Services.AddHisHopeResilience(builder.Configuration);
var redis = ConnectionMultiplexer.Connect(
    builder.Configuration["Redis:ConnectionString"] ?? "redis:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDataProtection()
    .SetApplicationName("His.Hope.IdentityService")
    .PersistKeysToStackExchangeRedis(
        redis,
        builder.Configuration["DataProtection:KeyName"]
            ?? "HisHope:IdentityService:DataProtection:Keys");
builder.Services.AddSingleton<SessionTokenProtector>();

// Configuration
builder.Services.Configure<ConsulOptions>(builder.Configuration.GetSection(ConsulOptions.SectionName));
builder.Services.Configure<DockerOptions>(builder.Configuration.GetSection(DockerOptions.SectionName));
builder.Services.Configure<KubernetesOptions>(builder.Configuration.GetSection(KubernetesOptions.SectionName));
builder.Services.Configure<ElasticsearchOptions>(builder.Configuration.GetSection(ElasticsearchOptions.SectionName));
builder.Services.Configure<PrometheusOptions>(builder.Configuration.GetSection(PrometheusOptions.SectionName));
builder.Services.Configure<AlertManagerOptions>(builder.Configuration.GetSection(AlertManagerOptions.SectionName));
builder.Services.Configure<JaegerOptions>(builder.Configuration.GetSection(JaegerOptions.SectionName));

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Shared JWT/OIDC validation and authorization defaults.
His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);

// CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    if (allowedOrigins.Length == 0)
        throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one explicit origin.");

    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Accept", "Authorization", "Content-Type", "If-Match", "If-None-Match",
                "X-Correlation-ID", "X-CSRF-Token", "X-Requested-With")
            .WithExposedHeaders("ETag", "X-Correlation-ID")
            .AllowCredentials();
    });
});

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// SignalR
builder.Services.AddSignalR();

// Health checks
builder.Services.AddHealthChecks();

// Dashboard audit channel + background writer
builder.Services.AddSingleton(Channel.CreateUnbounded<AuditEvent>(
    new UnboundedChannelOptions { SingleReader = true }));
builder.Services.AddHostedService<AuditEventWriter>();

// Memory cache for aggregator responses
builder.Services.AddMemoryCache();

// Consul service discovery with retry + circuit breaker
builder.Services.AddHttpClient<IConsulDiscoveryService, ConsulDiscoveryService>(client =>
{
    var consulAddress = builder.Configuration["Consul:Address"] ?? "http://localhost:8500";
    client.BaseAddress = new Uri(consulAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => new HisHopeResilienceHandler(
    sp.GetRequiredService<HisHopeResiliencePipelines>().CreateHttp("consul-discovery")));

// Elasticsearch log querying with retry + circuit breaker
builder.Services.AddHttpClient<IElasticsearchQueryService, ElasticsearchQueryService>((sp, client) =>
{
    var esOptions = sp.GetRequiredService<IOptions<ElasticsearchOptions>>();
    client.BaseAddress = new Uri(esOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new HisHopeResilienceHandler(
    sp.GetRequiredService<HisHopeResiliencePipelines>().CreateHttp("elasticsearch")));

// Prometheus metrics querying with retry + circuit breaker
builder.Services.AddHttpClient<IPrometheusQueryService, PrometheusQueryService>((sp, client) =>
{
    var promOptions = sp.GetRequiredService<IOptions<PrometheusOptions>>();
    client.BaseAddress = new Uri(promOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new HisHopeResilienceHandler(
    sp.GetRequiredService<HisHopeResiliencePipelines>().CreateHttp("prometheus")));

// Jaeger trace querying with retry + circuit breaker
builder.Services.AddHttpClient<IJaegerQueryService, JaegerQueryService>((sp, client) =>
{
    var jaegerOptions = sp.GetRequiredService<IOptions<JaegerOptions>>();
    client.BaseAddress = new Uri(jaegerOptions.Value.QueryUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new HisHopeResilienceHandler(
    sp.GetRequiredService<HisHopeResiliencePipelines>().CreateHttp("jaeger")));

// AlertManager alert querying with retry + circuit breaker
builder.Services.AddHttpClient<IAlertManagerService, AlertManagerService>((sp, client) =>
{
    var amOptions = sp.GetRequiredService<IOptions<AlertManagerOptions>>();
    client.BaseAddress = new Uri(amOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new HisHopeResilienceHandler(
    sp.GetRequiredService<HisHopeResiliencePipelines>().CreateHttp("alertmanager")));

// Logs aggregator
builder.Services.AddSingleton<ILogsAggregator, LogsAggregator>();

// Named HttpClient for direct health checks (fallback when Consul has no data)
builder.Services.AddHttpClient("health-check", client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
});

// Resource aggregator
builder.Services.AddSingleton<IResourceAggregator, ResourceAggregator>();

// Metrics aggregator
builder.Services.AddSingleton<IMetricsAggregator, MetricsAggregator>();

// Traces aggregator
builder.Services.AddSingleton<ITracesAggregator, TracesAggregator>();

// Lifecycle services (Docker or Kubernetes based on config)
builder.Services.AddSingleton<DockerLifecycleService>();
builder.Services.AddSingleton<KubernetesLifecycleService>();
builder.Services.AddSingleton<DisabledLifecycleService>();
builder.Services.AddSingleton<IServiceLifecycleService>(sp =>
{
    var k8s = sp.GetRequiredService<IOptions<KubernetesOptions>>();
    if (k8s.Value.Enabled)
        return sp.GetRequiredService<KubernetesLifecycleService>();

    var docker = sp.GetRequiredService<IOptions<DockerOptions>>();
    return docker.Value.Enabled
        ? sp.GetRequiredService<DockerLifecycleService>()
        : sp.GetRequiredService<DisabledLifecycleService>();
});
builder.Services.AddSingleton<ILifecycleController, LifecycleController>();

// Background service: polls ES for new logs and pushes via SignalR
builder.Services.AddHostedService<LogStreamBackgroundService>();

// Background service: polls Prometheus every 2s and pushes metrics via SignalR
builder.Services.AddHostedService<MetricsBackgroundService>();

// Rate limiting
builder.Services.AddHisHopeRateLimiting(builder.Configuration);

// OpenTelemetry
builder.Services.AddHisHopeOpenTelemetryExporters(builder.Configuration, "SystemDashboard.Bff");

var app = builder.Build();

app.UseHisHopeAspNetCore();

// The three local apps share the Identity BFF session cookie.  The SPA
// auth interceptor always sends an Authorization header with the OIDC
// access token, but that token may carry an audience that does not match
// the BFF's JwtBearer configuration.  When the session cookie is present,
// inject the session JWT as the authenticated principal so that controller
// authorization works consistently across ports.
app.Use(async (context, next) =>
{
    if (context.Request.Cookies.TryGetValue("hishop_sid", out var sessionId) &&
        !string.IsNullOrWhiteSpace(sessionId))
    {
        var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var tokenProtector = context.RequestServices.GetRequiredService<SessionTokenProtector>();
        var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
        if (sessionJson.HasValue)
        {
            using var document = JsonDocument.Parse((string)sessionJson!);
            if (document.RootElement.TryGetProperty("Jwt", out var jwtElement) &&
                !string.IsNullOrWhiteSpace(jwtElement.GetString()))
            {
                string sessionJwt;
                try
                {
                    sessionJwt = tokenProtector.Unprotect(jwtElement.GetString()!);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                // Session exchange stores the same RSA/JWE access token used by
                // the other APIs. Forward it to the standard JWT middleware so
                // this BFF uses the shared key/issuer/audience validation instead
                // of the obsolete local HMAC session-key contract.
                context.Request.Headers.Authorization = $"Bearer {sessionJwt}";
            }
        }
    }

    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiting();
app.UseMiddleware<DashboardAuditMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<LogStreamHub>("/ws/logshub").RequireAuthorization();
app.MapHub<AlertHub>("/ws/alerthub").RequireAuthorization();
app.MapHub<MetricsHub>("/ws/metricshub").RequireAuthorization();

app.Run();

public partial class Program { }
