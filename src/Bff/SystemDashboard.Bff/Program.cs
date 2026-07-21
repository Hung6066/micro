using System.Text.Json;
using System.Text.Json.Serialization;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Hubs;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<ConsulOptions>(builder.Configuration.GetSection(ConsulOptions.SectionName));
builder.Services.Configure<DockerOptions>(builder.Configuration.GetSection(DockerOptions.SectionName));
builder.Services.Configure<KubernetesOptions>(builder.Configuration.GetSection(KubernetesOptions.SectionName));
builder.Services.Configure<ElasticsearchOptions>(builder.Configuration.GetSection(ElasticsearchOptions.SectionName));
builder.Services.Configure<PrometheusOptions>(builder.Configuration.GetSection(PrometheusOptions.SectionName));
builder.Services.Configure<JaegerOptions>(builder.Configuration.GetSection(JaegerOptions.SectionName));

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4201")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// SignalR
builder.Services.AddSignalR();

// Health checks
builder.Services.AddHealthChecks();

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

// Logs aggregator
builder.Services.AddSingleton<ILogsAggregator, LogsAggregator>();

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

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHub<LogStreamHub>("/ws/logs/stream").RequireAuthorization();

app.Run();

public partial class Program { }
