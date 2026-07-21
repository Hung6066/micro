using System.Text.Json;
using System.Text.Json.Serialization;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<ConsulOptions>(builder.Configuration.GetSection(ConsulOptions.SectionName));
builder.Services.Configure<DockerOptions>(builder.Configuration.GetSection(DockerOptions.SectionName));
builder.Services.Configure<KubernetesOptions>(builder.Configuration.GetSection(KubernetesOptions.SectionName));

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
builder.Services.AddTransient<ResiliencePipelineHandler>();

// Consul service discovery with retry + circuit breaker
builder.Services.AddHttpClient<IConsulDiscoveryService, ConsulDiscoveryService>(client =>
{
    var consulAddress = builder.Configuration["Consul:Address"] ?? "http://localhost:8500";
    client.BaseAddress = new Uri(consulAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler<ResiliencePipelineHandler>();

// Resource aggregator
builder.Services.AddSingleton<IResourceAggregator, ResourceAggregator>();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
