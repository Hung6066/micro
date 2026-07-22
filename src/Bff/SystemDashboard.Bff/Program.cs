using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Hubs;
using SystemDashboard.Bff.Middleware;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

var builder = WebApplication.CreateBuilder(args);

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

// JWT Authentication (shared symmetric key validation used by all services)
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4201", "http://localhost:8082")
            .AllowAnyHeader()
            .AllowAnyMethod()
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

// Resilience policies (retry + circuit breaker for all outbound calls)
builder.Services.AddResiliencePolicies();

// Consul service discovery with retry + circuit breaker
builder.Services.AddHttpClient<IConsulDiscoveryService, ConsulDiscoveryService>(client =>
{
    var consulAddress = builder.Configuration["Consul:Address"] ?? "http://localhost:8500";
    client.BaseAddress = new Uri(consulAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => new ResiliencePipelineHandler(
    sp.GetRequiredService<IResiliencePipelineFactory>(), "consul-discovery"));

// Elasticsearch log querying with retry + circuit breaker
builder.Services.AddHttpClient<IElasticsearchQueryService, ElasticsearchQueryService>((sp, client) =>
{
    var esOptions = sp.GetRequiredService<IOptions<ElasticsearchOptions>>();
    client.BaseAddress = new Uri(esOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new ResiliencePipelineHandler(
    sp.GetRequiredService<IResiliencePipelineFactory>(), "elasticsearch"));

// Prometheus metrics querying with retry + circuit breaker
builder.Services.AddHttpClient<IPrometheusQueryService, PrometheusQueryService>((sp, client) =>
{
    var promOptions = sp.GetRequiredService<IOptions<PrometheusOptions>>();
    client.BaseAddress = new Uri(promOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new ResiliencePipelineHandler(
    sp.GetRequiredService<IResiliencePipelineFactory>(), "prometheus"));

// Jaeger trace querying with retry + circuit breaker
builder.Services.AddHttpClient<IJaegerQueryService, JaegerQueryService>((sp, client) =>
{
    var jaegerOptions = sp.GetRequiredService<IOptions<JaegerOptions>>();
    client.BaseAddress = new Uri(jaegerOptions.Value.QueryUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new ResiliencePipelineHandler(
    sp.GetRequiredService<IResiliencePipelineFactory>(), "jaeger"));

// AlertManager alert querying with retry + circuit breaker
builder.Services.AddHttpClient<IAlertManagerService, AlertManagerService>((sp, client) =>
{
    var amOptions = sp.GetRequiredService<IOptions<AlertManagerOptions>>();
    client.BaseAddress = new Uri(amOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler(sp => new ResiliencePipelineHandler(
    sp.GetRequiredService<IResiliencePipelineFactory>(), "alertmanager"));

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
builder.Services.AddSingleton<IServiceLifecycleService>(sp =>
{
    var k8s = sp.GetRequiredService<IOptions<KubernetesOptions>>();
    return k8s.Value.Enabled
        ? sp.GetRequiredService<KubernetesLifecycleService>()
        : sp.GetRequiredService<DockerLifecycleService>();
});
builder.Services.AddSingleton<ILifecycleController, LifecycleController>();

// Background service: polls ES for new logs and pushes via SignalR
builder.Services.AddHostedService<LogStreamBackgroundService>();

// Background service: polls Prometheus every 2s and pushes metrics via SignalR
builder.Services.AddHostedService<MetricsBackgroundService>();

// Rate limiting
builder.Services.AddHisHopeRateLimiting(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

var app = builder.Build();

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
