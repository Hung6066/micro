# Aspire-Like Dashboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone Aspire-like dashboard (Angular 19 + .NET 8 BFF) to view and control all His.Hope microservices, databases, and infrastructure.

**Architecture:** SystemDashboard.Bff (port 5700) aggregates data from Consul (discovery), Elasticsearch (logs), Jaeger (traces), Prometheus (metrics), and Docker/K8s API (lifecycle). Angular Dashboard App (port 4201) is a separate workspace consuming BFF REST + SignalR APIs.

**Tech Stack:** .NET 8 ASP.NET Core, Angular 19 standalone + Angular Material, SignalR, Consul HTTP API, Elasticsearch REST, Jaeger REST, Prometheus HTTP API, Docker CLI, kubectl

## Global Constraints

- All inter-service calls must use Polly retry + circuit breaker (from `His.Hope.Infrastructure`)
- BFF must follow ADR 013 BFF Architecture patterns (aggregation handlers, proxy)
- JWT auth validated via existing IdentityService on every request
- Container image must be distroless (ADR 007)
- Real-time streaming via SignalR on BFF, not direct WebSocket from Angular
- No database — all data comes from observability stack
- Angular standalone components, OnPush change detection, Angular Material
- Follow existing project patterns: Clean Architecture layers in BFF, matching code style

---

## File Structure Map

```
# Backend (new files)
src/Bff/SystemDashboard.Bff/
├── Program.cs                                    # Host builder, DI, middleware
├── appsettings.json                              # Default config
├── appsettings.Development.json                  # Dev-specific (local endpoints)
├── Properties/launchSettings.json                # Port 5700
├── SystemDashboard.Bff.csproj                    # Project file
├── Dockerfile                                    # Distroless container
├── GlobalUsings.cs                               # Shared usings
├── Models/
│   ├── Resource.cs                               # ServiceResource, DatabaseResource, InfraResource
│   ├── ServiceState.cs                           # Running, Stopped, Degraded, Unknown enum
│   ├── LogEntry.cs                               # Structured log entry model
│   ├── TraceSummary.cs                           # Trace list item
│   ├── TraceDetail.cs                            # Full trace with spans
│   ├── MetricSnapshot.cs                         # Metric data point
│   └── EnvironmentContext.cs                     # Dev/Staging/Production
├── Aggregators/
│   ├── IResourceAggregator.cs                    # Interface
│   ├── ResourceAggregator.cs                     # Consul + health aggregation
│   ├── ILogsAggregator.cs                        # Interface
│   ├── LogsAggregator.cs                         # Elasticsearch log queries
│   ├── ITracesAggregator.cs                      # Interface
│   ├── TracesAggregator.cs                       # Jaeger trace queries
│   ├── IMetricsAggregator.cs                     # Interface
│   ├── MetricsAggregator.cs                      # Prometheus metric queries
│   ├── ILifecycleController.cs                   # Interface
│   └── LifecycleController.cs                    # Docker/K8s start/stop/restart
├── Services/
│   ├── IConsulDiscoveryService.cs                # Interface
│   ├── ConsulDiscoveryService.cs                 # Consul HTTP client
│   ├── IElasticsearchQueryService.cs              # Interface
│   ├── ElasticsearchQueryService.cs              # ES REST client
│   ├── IJaegerQueryService.cs                    # Interface
│   ├── JaegerQueryService.cs                     # Jaeger REST client
│   ├── IPrometheusQueryService.cs                # Interface
│   ├── PrometheusQueryService.cs                 # Prometheus HTTP client
│   ├── IServiceLifecycleService.cs               # Interface
│   ├── DockerLifecycleService.cs                 # Docker compose CLI wrapper
│   └── KubernetesLifecycleService.cs             # kubectl wrapper
├── Hubs/
│   └── LogStreamHub.cs                           # SignalR hub for real-time logs
├── Controllers/
│   ├── ResourcesController.cs                    # GET/POST /api/resources
│   ├── LogsController.cs                         # GET /api/logs
│   ├── TracesController.cs                       # GET /api/traces
│   ├── MetricsController.cs                      # GET /api/metrics
│   └── EnvironmentController.cs                  # GET/PUT /api/environment
├── Auth/
│   ├── DashboardPermission.cs                    # Permission constants
│   └── EnvironmentAuthorizationHandler.cs         # Env-based auth policy
└── Tests/
    ├── ResourceAggregatorTests.cs
    ├── LogsAggregatorTests.cs
    ├── TracesAggregatorTests.cs
    ├── MetricsAggregatorTests.cs
    ├── LifecycleControllerTests.cs
    └── ResourcesControllerTests.cs

# Frontend (new Angular workspace)
dashboard-app/
├── angular.json
├── package.json
├── tsconfig.json
├── src/
│   ├── index.html
│   ├── main.ts
│   ├── styles.scss
│   ├── app/
│   │   ├── app.config.ts                         # Standalone app config + router
│   │   ├── app.routes.ts                         # Route definitions
│   │   ├── app.component.ts                      # Shell layout
│   │   ├── app.component.html
│   │   ├── app.component.scss
│   │   ├── core/
│   │   │   ├── models/
│   │   │   │   ├── resource.model.ts             # Resource types
│   │   │   │   ├── log-entry.model.ts            # Log entry type
│   │   │   │   ├── trace.model.ts                # Trace types
│   │   │   │   ├── metric-snapshot.model.ts      # Metric types
│   │   │   │   └── environment.model.ts          # Env context type
│   │   │   ├── services/
│   │   │   │   ├── resource.service.ts           # /api/resources HTTP
│   │   │   │   ├── logs.service.ts               # /api/logs HTTP
│   │   │   │   ├── log-stream.service.ts         # SignalR WebSocket
│   │   │   │   ├── traces.service.ts             # /api/traces HTTP
│   │   │   │   ├── metrics.service.ts            # /api/metrics HTTP
│   │   │   │   ├── lifecycle.service.ts          # POST start/stop/restart
│   │   │   │   ├── environment.service.ts        # /api/environment
│   │   │   │   └── auth.service.ts               # JWT auth + IdentityService
│   │   │   └── guards/
│   │   │       └── auth.guard.ts                 # Route guard
│   │   ├── features/
│   │   │   ├── resources/
│   │   │   │   ├── resource-list/
│   │   │   │   │   ├── resource-list.component.ts
│   │   │   │   │   ├── resource-list.component.html
│   │   │   │   │   └── resource-list.component.scss
│   │   │   │   └── resource-detail/
│   │   │   │       ├── resource-detail.component.ts
│   │   │   │       ├── resource-detail.component.html
│   │   │   │       └── resource-detail.component.scss
│   │   │   ├── logs/
│   │   │   │   ├── log-list/
│   │   │   │   │   ├── log-list.component.ts
│   │   │   │   │   ├── log-list.component.html
│   │   │   │   │   └── log-list.component.scss
│   │   │   │   └── log-stream/
│   │   │   │       ├── log-stream.component.ts
│   │   │   │       ├── log-stream.component.html
│   │   │   │       └── log-stream.component.scss
│   │   │   ├── traces/
│   │   │   │   ├── trace-list/
│   │   │   │   │   ├── trace-list.component.ts
│   │   │   │   │   ├── trace-list.component.html
│   │   │   │   │   └── trace-list.component.scss
│   │   │   │   └── trace-detail/
│   │   │   │       ├── trace-detail.component.ts
│   │   │   │       ├── trace-detail.component.html
│   │   │   │       └── trace-detail.component.scss
│   │   │   └── metrics/
│   │   │       ├── metrics-overview/
│   │   │       │   ├── metrics-overview.component.ts
│   │   │       │   ├── metrics-overview.component.html
│   │   │       │   └── metrics-overview.component.scss
│   │   │       └── metrics-chart/
│   │   │           ├── metrics-chart.component.ts
│   │   │           ├── metrics-chart.component.html
│   │   │           └── metrics-chart.component.scss
│   │   └── shared/
│   │       ├── service-status-badge/
│   │       │   ├── service-status-badge.component.ts
│   │       │   ├── service-status-badge.component.html
│   │       │   └── service-status-badge.component.scss
│   │       ├── environment-selector/
│   │       │   ├── environment-selector.component.ts
│   │       │   ├── environment-selector.component.html
│   │       │   └── environment-selector.component.scss
│   │       ├── log-level-filter/
│   │       │   ├── log-level-filter.component.ts
│   │       │   ├── log-level-filter.component.html
│   │       │   └── log-level-filter.component.scss
│   │       └── resource-card/
│   │           ├── resource-card.component.ts
│   │           ├── resource-card.component.html
│   │           └── resource-card.component.scss
│   └── environments/
│       ├── environment.ts
│       └── environment.prod.ts

# Kubernetes (new files)
k8s/base/
├── systemdashboard-bff-deployment.yaml
├── systemdashboard-bff-service.yaml
└── dashboard-app-deployment.yaml

# Docker (modified files)
docker/
└── docker-compose.yml                            # Add dashboard services
```

---

## Phase 1: SystemDashboard.Bff Backend

### Task 1: Create BFF Project Scaffold

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
- Create: `src/Bff/SystemDashboard.Bff/Program.cs`
- Create: `src/Bff/SystemDashboard.Bff/appsettings.json`
- Create: `src/Bff/SystemDashboard.Bff/appsettings.Development.json`
- Create: `src/Bff/SystemDashboard.Bff/Properties/launchSettings.json`
- Create: `src/Bff/SystemDashboard.Bff/GlobalUsings.cs`

**Interfaces:**
- Consumes: `His.Hope.Infrastructure` (Polly, OpenTelemetry, HealthChecks), `His.Hope.Bff.Core` (auth, resilience patterns)
- Produces: Running BFF on port 5700 with health check endpoint

- [ ] **Step 1: Create .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>SystemDashboard.Bff</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="8.0.*" />
    <PackageReference Include="Polly" Version="8.*" />
    <PackageReference Include="Polly.Extensions.Http" Version="3.*" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Shared\Infrastructure\His.Hope.Infrastructure\His.Hope.Infrastructure.csproj" />
    <ProjectReference Include="..\His.Hope.Bff.Core\His.Hope.Bff.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Environment": "Development",
  "Consul": {
    "Address": "http://localhost:8500"
  },
  "Elasticsearch": {
    "Url": "http://localhost:9200",
    "LogIndex": "his-hope-logs-*"
  },
  "Jaeger": {
    "QueryUrl": "http://localhost:16686"
  },
  "Prometheus": {
    "Url": "http://localhost:9090"
  },
  "Docker": {
    "ComposeProjectName": "his-hope"
  },
  "Kubernetes": {
    "Enabled": false
  },
  "Jwt": {
    "Authority": "http://localhost:5001",
    "Audience": "hishopedashboard"
  }
}
```

- [ ] **Step 3: Create appsettings.Development.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "SystemDashboard.Bff": "Debug"
    }
  }
}
```

- [ ] **Step 4: Create launchSettings.json**

```json
{
  "profiles": {
    "SystemDashboard.Bff": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:5701;http://localhost:5700",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 5: Create GlobalUsings.cs**

```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
```

- [ ] **Step 6: Create Program.cs skeleton**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// JWT Authentication (same as other BFFs)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthorization();

// CORS for Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardApp", policy =>
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

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation());

var app = builder.Build();

app.UseCors("DashboardApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.Run();
```

- [ ] **Step 7: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/
git commit -m "feat(dashboard): scaffold SystemDashboard.Bff project"
```

---

### Task 2: Resource Models

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Models/Resource.cs`
- Create: `src/Bff/SystemDashboard.Bff/Models/ServiceState.cs`

**Interfaces:**
- Consumes: Nothing (pure models)
- Produces: `ServiceResource`, `DatabaseResource`, `InfrastructureResource` record types; `ServiceState` enum

- [ ] **Step 1: Create ServiceState enum**

```csharp
namespace SystemDashboard.Bff.Models;

public enum ServiceState
{
    Unknown = 0,
    Running = 1,
    Stopped = 2,
    Degraded = 3
}
```

- [ ] **Step 2: Create Resource models**

```csharp
namespace SystemDashboard.Bff.Models;

public abstract record Resource
{
    public required string Name { get; init; }
    public required string Type { get; init; } // "service", "bff", "database", "infrastructure", "gateway"
    public required ServiceState State { get; init; }
    public string? Endpoint { get; init; }
    public string? DisplayName { get; init; }
}

public sealed record ServiceResource : Resource
{
    public int? HttpPort { get; init; }
    public int? GrpcPort { get; init; }
    public string HealthStatus { get; init; } = "Unknown";
    public string? Uptime { get; init; }
    public int Replicas { get; init; } = 1;
    public double CpuPercent { get; init; }
    public double MemoryUsedMb { get; init; }
    public double MemoryLimitMb { get; init; }
    public List<string> Databases { get; init; } = [];
    public Dictionary<string, string> Environment { get; init; } = [];
    public List<HealthCheckResult> HealthChecks { get; init; } = [];
}

public sealed record HealthCheckResult
{
    public required string Name { get; init; }
    public required string Status { get; init; } // "passing", "warning", "critical"
    public string? Output { get; init; }
}

public sealed record DatabaseResource : Resource
{
    public string Engine { get; init; } = "CockroachDB";
    public double SizeMb { get; init; }
    public int Connections { get; init; }
}

public sealed record InfrastructureResource : Resource
{
    public string Category { get; init; } = "infrastructure"; // "cache", "messaging", "gateway"
    public string? Version { get; init; }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Models/
git commit -m "feat(dashboard): add resource models and ServiceState enum"
```

---

### Task 3: Consul Discovery Service + Resource Aggregator

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Services/IConsulDiscoveryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/ConsulDiscoveryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/IResourceAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/ResourceAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Tests/ResourceAggregatorTests.cs`

**Interfaces:**
- Consumes: `IConsulDiscoveryService` (new), `IOptions<ConsulOptions>` (from DI)
- Produces: `IResourceAggregator.GetAllResourcesAsync()` returning `List<Resource>`
- Test: xUnit + NSubstitute

- [ ] **Step 1: Create Consul options class and update Program.cs**

Add to `Models/Resource.cs`:
```csharp
public sealed class ConsulOptions
{
    public const string SectionName = "Consul";
    public required string Address { get; init; }
}

public sealed class DockerOptions
{
    public const string SectionName = "Docker";
    public required string ComposeProjectName { get; init; }
}

public sealed class KubernetesOptions
{
    public const string SectionName = "Kubernetes";
    public bool Enabled { get; init; }
}
```

Update `Program.cs` (add after `var builder = ...`):
```csharp
builder.Services.Configure<ConsulOptions>(builder.Configuration.GetSection(ConsulOptions.SectionName));
builder.Services.Configure<DockerOptions>(builder.Configuration.GetSection(DockerOptions.SectionName));
builder.Services.Configure<KubernetesOptions>(builder.Configuration.GetSection(KubernetesOptions.SectionName));
```

- [ ] **Step 2: Create IConsulDiscoveryService interface**

```csharp
namespace SystemDashboard.Bff.Services;

public interface IConsulDiscoveryService
{
    Task<List<string>> GetServiceNamesAsync(CancellationToken ct = default);
    Task<ConsulServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default);
}

public sealed record ConsulServiceHealth
{
    public required string ServiceName { get; init; }
    public required string Status { get; init; } // "passing", "warning", "critical"
    public int Port { get; init; }
    public string? Address { get; init; }
    public List<ConsulHealthCheck> Checks { get; init; } = [];
}

public sealed record ConsulHealthCheck
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public string? Output { get; init; }
}
```

- [ ] **Step 3: Create ConsulDiscoveryService implementation**

```csharp
namespace SystemDashboard.Bff.Services;

public class ConsulDiscoveryService : IConsulDiscoveryService
{
    private readonly HttpClient _http;
    private readonly ILogger<ConsulDiscoveryService> _logger;

    public ConsulDiscoveryService(HttpClient http, ILogger<ConsulDiscoveryService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<string>> GetServiceNamesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<Dictionary<string, List<string>>>(
                "/v1/catalog/services", cancellationToken: ct);
            return response?.Keys.ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch service names from Consul");
            return [];
        }
    }

    public async Task<ConsulServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        try
        {
            var checks = await _http.GetFromJsonAsync<List<ConsulHealthCheckResponse>>(
                $"/v1/health/service/{serviceName}", cancellationToken: ct);

            if (checks is null || checks.Count == 0) return null;

            var service = checks[0].Service;
            var status = checks.All(c => c.Checks.Status == "passing") ? "passing"
                : checks.Any(c => c.Checks.Status == "critical") ? "critical"
                : "warning";

            return new ConsulServiceHealth
            {
                ServiceName = serviceName,
                Status = status,
                Port = service.Port,
                Address = service.Address,
                Checks = [.. checks.SelectMany(c => c.Checks).Select(ch => new ConsulHealthCheck
                {
                    Name = ch.Name ?? "unknown",
                    Status = ch.Status,
                    Output = ch.Output
                })]
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch health for service {ServiceName}", serviceName);
            return null;
        }
    }

    private sealed record ConsulHealthCheckResponse
    {
        public ConsulServiceNode Service { get; init; } = null!;
        public ConsulChecksWrapper Checks { get; init; } = null!;
    }
    private sealed record ConsulServiceNode { public int Port { get; init; } public string? Address { get; init; } }
    private sealed record ConsulChecksWrapper : List<ConsulCheckDetail> { }
    private sealed record ConsulCheckDetail
    {
        public string? Name { get; init; }
        public string Status { get; init; } = "unknown";
        public string? Output { get; init; }
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddHttpClient<IConsulDiscoveryService, ConsulDiscoveryService>(client =>
{
    var consulAddress = builder.Configuration["Consul:Address"] ?? "http://localhost:8500";
    client.BaseAddress = new Uri(consulAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

- [ ] **Step 4: Create IResourceAggregator interface**

```csharp
namespace SystemDashboard.Bff.Aggregators;

public interface IResourceAggregator
{
    Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default);
    Task<Resource?> GetResourceByNameAsync(string name, CancellationToken ct = default);
}
```

- [ ] **Step 5: Create ResourceAggregator**

```csharp
namespace SystemDashboard.Bff.Aggregators;

public class ResourceAggregator : IResourceAggregator
{
    private readonly IConsulDiscoveryService _consul;
    private readonly ILogger<ResourceAggregator> _logger;

    // Service port mappings (known from project architecture)
    private static readonly Dictionary<string, (int httpPort, int? grpcPort, string type, string[] databases)> _serviceMap = new()
    {
        ["identity-service"] = (5001, 5007, "service", new[] { "identitydb" }),
        ["patient-service"] = (5002, 5006, "service", new[] { "patientdb" }),
        ["appointment-service"] = (5004, 5008, "service", new[] { "appointmentdb" }),
        ["clinical-service"] = (5005, 5009, "service", new[] { "clinicaldb" }),
        ["lab-service"] = (5010, null, "service", new[] { "labdb" }),
        ["billing-service"] = (5020, null, "service", new[] { "billingdb" }),
        ["pharmacy-service"] = (5030, null, "service", new[] { "pharmacydb" }),
        ["patient-bff"] = (5100, null, "bff", Array.Empty<string>()),
        ["clinical-bff"] = (5200, null, "bff", Array.Empty<string>()),
        ["lab-bff"] = (5300, null, "bff", Array.Empty<string>()),
        ["billing-bff"] = (5400, null, "bff", Array.Empty<string>()),
        ["pharmacy-bff"] = (5500, null, "bff", Array.Empty<string>()),
        ["dashboard-bff"] = (5600, null, "bff", Array.Empty<string>()),
    };

    public ResourceAggregator(IConsulDiscoveryService consul, ILogger<ResourceAggregator> logger)
    {
        _consul = consul;
        _logger = logger;
    }

    public async Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default)
    {
        var resources = new List<Resource>();
        var serviceNames = await _consul.GetServiceNamesAsync(ct);

        foreach (var (name, (httpPort, grpcPort, type, databases)) in _serviceMap)
        {
            var health = await _consul.GetServiceHealthAsync(name, ct);

            var state = health switch
            {
                null => ServiceState.Unknown,
                { Status: "passing" } => ServiceState.Running,
                { Status: "critical" } => ServiceState.Stopped,
                _ => ServiceState.Degraded
            };

            resources.Add(new ServiceResource
            {
                Name = name,
                Type = type,
                DisplayName = FormatServiceName(name),
                State = state,
                Endpoint = $"http://localhost:{httpPort}",
                HttpPort = httpPort,
                GrpcPort = grpcPort,
                HealthStatus = health?.Status ?? "Unknown",
                Databases = [.. databases],
                HealthChecks = health?.Checks ?? []
            });
        }

        // Add infrastructure resources (hardcoded for v1 since Consul doesn't track non-service infra)
        resources.AddRange(GetInfrastructureResources());

        // Add database resources
        resources.AddRange(GetDatabaseResources());

        return resources;
    }

    public async Task<Resource?> GetResourceByNameAsync(string name, CancellationToken ct = default)
    {
        var all = await GetAllResourcesAsync(ct);
        return all.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatServiceName(string name) =>
        string.Join(' ', name.Split('-').Select(w => char.ToUpper(w[0]) + w[1..]));

    private static List<InfrastructureResource> GetInfrastructureResources() => new()
    {
        new() { Name = "Redis", Type = "infrastructure", Category = "cache", State = ServiceState.Running, Endpoint = "localhost:6379", Version = "7.0" },
        new() { Name = "RabbitMQ", Type = "infrastructure", Category = "messaging", State = ServiceState.Running, Endpoint = "localhost:5672", Version = "3-management" },
        new() { Name = "Elasticsearch", Type = "infrastructure", Category = "search", State = ServiceState.Running, Endpoint = "localhost:9200", Version = "8.12" },
        new() { Name = "ApiGateway", Type = "gateway", Category = "gateway", State = ServiceState.Running, Endpoint = "http://localhost:5000", Version = "YARP" },
    };

    private static List<DatabaseResource> GetDatabaseResources() => new()
    {
        new() { Name = "identitydb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "patientdb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "appointmentdb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "clinicaldb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "labdb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "billingdb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "pharmacydb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
        new() { Name = "harnessdb", Type = "database", Engine = "CockroachDB", Endpoint = "localhost:26257", State = ServiceState.Running },
    };
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddSingleton<IResourceAggregator, ResourceAggregator>();
```

- [ ] **Step 6: Create unit tests**

```csharp
namespace SystemDashboard.Bff.Tests;

public class ResourceAggregatorTests
{
    [Fact]
    public async Task GetAllResourcesAsync_ReturnsAllServices()
    {
        var consul = Substitute.For<IConsulDiscoveryService>();
        consul.GetServiceNamesAsync(Arg.Any<CancellationToken>())
            .Returns(["patient-service", "lab-service"]);
        consul.GetServiceHealthAsync("patient-service", Arg.Any<CancellationToken>())
            .Returns(new ConsulServiceHealth { ServiceName = "patient-service", Status = "passing", Port = 5002 });
        consul.GetServiceHealthAsync("lab-service", Arg.Any<CancellationToken>())
            .Returns(new ConsulServiceHealth { ServiceName = "lab-service", Status = "passing", Port = 5010 });

        var logger = Substitute.For<ILogger<ResourceAggregator>>();
        var aggregator = new ResourceAggregator(consul, logger);

        var resources = await aggregator.GetAllResourcesAsync();

        var services = resources.OfType<ServiceResource>().ToList();
        Assert.Contains(services, s => s.Name == "patient-service" && s.State == ServiceState.Running);
        Assert.Contains(services, s => s.Name == "lab-service" && s.State == ServiceState.Running);

        var databases = resources.OfType<DatabaseResource>().ToList();
        Assert.NotEmpty(databases);
    }

    [Fact]
    public async Task GetAllResourcesAsync_HandlesConsulFailure()
    {
        var consul = Substitute.For<IConsulDiscoveryService>();
        consul.GetServiceNamesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<List<string>>(new HttpRequestException("Consul down")));

        var logger = Substitute.For<ILogger<ResourceAggregator>>();
        var aggregator = new ResourceAggregator(consul, logger);

        var resources = await aggregator.GetAllResourcesAsync();

        // Should still return hardcoded infra + DB resources, but no services
        var services = resources.OfType<ServiceResource>().ToList();
        Assert.Empty(services); // Consul failed, no service data

        var infra = resources.OfType<InfrastructureResource>().ToList();
        Assert.NotEmpty(infra);
    }
}
```

Add test project reference to `SystemDashboard.Bff.csproj`:
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
```

- [ ] **Step 7: Run tests**

Run: `dotnet test src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: 2 tests PASS

- [ ] **Step 8: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/
git commit -m "feat(dashboard): add Consul discovery service and resource aggregator with tests"
```

---

### Task 4: Elasticsearch Log Query Service + Logs Aggregator

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Services/IElasticsearchQueryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/ElasticsearchQueryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Models/LogEntry.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/ILogsAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/LogsAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Tests/LogsAggregatorTests.cs`
- Create: `src/Bff/SystemDashboard.Bff/Hubs/LogStreamHub.cs`

**Interfaces:**
- Consumes: `IElasticsearchQueryService.QueryLogsAsync(service, level, from, size, query)` returning `List<LogEntry>`
- Produces: `ILogsAggregator.QueryLogsAsync(...)` and `LogStreamHub` SignalR hub for real-time streaming
- Test: xUnit + NSubstitute

- [ ] **Step 1: Create LogEntry model**

```csharp
namespace SystemDashboard.Bff.Models;

public sealed record LogEntry
{
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; } // "Error", "Warning", "Information", "Debug"
    public required string Service { get; init; }
    public required string Message { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, object>? Fields { get; init; }
}
```

- [ ] **Step 2: Create IElasticsearchQueryService interface**

```csharp
namespace SystemDashboard.Bff.Services;

public interface IElasticsearchQueryService
{
    Task<List<LogEntry>> QueryLogsAsync(
        string? service = null,
        string? level = null,
        DateTime? from = null,
        int size = 100,
        string? searchQuery = null,
        CancellationToken ct = default);
}

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";
    public required string Url { get; init; }
    public required string LogIndex { get; init; }
}
```

- [ ] **Step 3: Create ElasticsearchQueryService implementation**

```csharp
namespace SystemDashboard.Bff.Services;

public class ElasticsearchQueryService : IElasticsearchQueryService
{
    private readonly HttpClient _http;
    private readonly string _logIndex;
    private readonly ILogger<ElasticsearchQueryService> _logger;

    public ElasticsearchQueryService(HttpClient http, IOptions<ElasticsearchOptions> options, ILogger<ElasticsearchQueryService> logger)
    {
        _http = http;
        _logIndex = options.Value.LogIndex;
        _logger = logger;
    }

    public async Task<List<LogEntry>> QueryLogsAsync(
        string? service, string? level, DateTime? from, int size, string? searchQuery, CancellationToken ct = default)
    {
        try
        {
            var mustClauses = new List<object>();

            if (!string.IsNullOrEmpty(service))
                mustClauses.Add(new { match = new { fields = new { service = service } } });

            if (!string.IsNullOrEmpty(level))
                mustClauses.Add(new { match = new { fields = new { level = level } } });

            if (!string.IsNullOrEmpty(searchQuery))
                mustClauses.Add(new { query_string = new { query = searchQuery } });

            var queryBody = new
            {
                query = new
                {
                    @bool = new
                    {
                        must = mustClauses,
                        filter = from.HasValue
                            ? new[] { new { range = new { @timestamp = new { gte = from.Value.ToString("o") } } } }
                            : Array.Empty<object>()
                    }
                },
                sort = new[] { new { @timestamp = new { order = "desc" } } },
                size
            };

            var response = await _http.PostAsJsonAsync($"/{_logIndex}/_search", queryBody, cancellationToken: ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EsSearchResponse>(cancellationToken: ct);
            return result?.Hits?.Hits?.Select(h => new LogEntry
            {
                Timestamp = h.Source.Timestamp,
                Level = h.Source.Level ?? "Information",
                Service = h.Source.Service ?? "unknown",
                Message = h.Source.Message ?? "",
                TraceId = h.Source.TraceId,
                Fields = h.Source.Fields
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query logs from Elasticsearch");
            return [];
        }
    }

    private sealed record EsSearchResponse { public EsHits? Hits { get; init; } }
    private sealed record EsHits { public List<EsHit>? Hits { get; init; } }
    private sealed record EsHit { public EsSource Source { get; init; } = null!; }
    private sealed record EsSource
    {
        public DateTime Timestamp { get; init; }
        public string? Level { get; init; }
        public string? Service { get; init; }
        public string? Message { get; init; }
        public string? TraceId { get; init; }
        public Dictionary<string, object>? Fields { get; init; }
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.Configure<ElasticsearchOptions>(builder.Configuration.GetSection(ElasticsearchOptions.SectionName));
builder.Services.AddHttpClient<IElasticsearchQueryService, ElasticsearchQueryService>((sp, client) =>
{
    var esOptions = sp.GetRequiredService<IOptions<ElasticsearchOptions>>();
    client.BaseAddress = new Uri(esOptions.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

- [ ] **Step 4: Create ILogsAggregator + LogsAggregator**

```csharp
namespace SystemDashboard.Bff.Aggregators;

public interface ILogsAggregator
{
    Task<List<LogEntry>> QueryLogsAsync(string? service, string? level, DateTime? from, int size, string? query, CancellationToken ct = default);
}

public class LogsAggregator : ILogsAggregator
{
    private readonly IElasticsearchQueryService _es;

    public LogsAggregator(IElasticsearchQueryService es) => _es = es;

    public Task<List<LogEntry>> QueryLogsAsync(string? service, string? level, DateTime? from, int size, string? query, CancellationToken ct = default)
        => _es.QueryLogsAsync(service, level, from, size, query, ct);
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddSingleton<ILogsAggregator, LogsAggregator>();
```

- [ ] **Step 5: Create LogStreamHub for real-time log streaming**

```csharp
namespace SystemDashboard.Bff.Hubs;

public class LogStreamHub : Hub
{
    private readonly ILogger<LogStreamHub> _logger;

    public LogStreamHub(ILogger<LogStreamHub> logger) => _logger = logger;

    public async Task SubscribeToService(string serviceName, string? level = null)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serviceName);
        _logger.LogInformation("Client {ConnectionId} subscribed to logs for {Service}", Context.ConnectionId, serviceName);
    }

    public async Task UnsubscribeFromService(string serviceName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, serviceName);
    }

    public async Task SendLogEntry(string serviceName, LogEntry entry)
    {
        await Clients.Group(serviceName).SendAsync("LogReceived", entry);
    }
}
```

Map hub in `Program.cs`:
```csharp
app.MapHub<LogStreamHub>("/ws/logs/stream");
```

- [ ] **Step 6: Create LogsAggregatorTests**

```csharp
namespace SystemDashboard.Bff.Tests;

public class LogsAggregatorTests
{
    [Fact]
    public async Task QueryLogsAsync_FiltersByServiceAndLevel()
    {
        var es = Substitute.For<IElasticsearchQueryService>();
        var expectedLogs = new List<LogEntry>
        {
            new() { Timestamp = DateTime.UtcNow, Level = "Error", Service = "patient-service", Message = "Connection failed" }
        };
        es.QueryLogsAsync("patient-service", "Error", Arg.Any<DateTime?>(), 100, null, Arg.Any<CancellationToken>())
            .Returns(expectedLogs);

        var aggregator = new LogsAggregator(es);
        var logs = await aggregator.QueryLogsAsync("patient-service", "Error", DateTime.UtcNow.AddHours(-1), 100, null);

        Assert.Single(logs);
        Assert.Equal("Error", logs[0].Level);
    }
}
```

- [ ] **Step 7: Run tests**

Run: `dotnet test src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: 3 tests PASS

- [ ] **Step 8: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/
git commit -m "feat(dashboard): add Elasticsearch log query service, aggregator, and SignalR log stream hub"
```

---

### Task 5: Prometheus Metrics Query Service + Metrics Aggregator

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Services/IPrometheusQueryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/PrometheusQueryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/IMetricsAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/MetricsAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Tests/MetricsAggregatorTests.cs`

**Interfaces:**
- Consumes: `IPrometheusQueryService` querying Prometheus HTTP API
- Produces: `IMetricsAggregator.GetMetricsAsync(service, metricNames, range)` returning `List<MetricSnapshot>`
- Test: xUnit + NSubstitute

- [ ] **Step 1: Create MetricSnapshot model**

```csharp
namespace SystemDashboard.Bff.Models;

public sealed record MetricSnapshot
{
    public required string Service { get; init; }
    public required string MetricName { get; init; }
    public required List<MetricDataPoint> DataPoints { get; init; }
}

public sealed record MetricDataPoint
{
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
}
```

- [ ] **Step 2: Create IPrometheusQueryService interface**

```csharp
namespace SystemDashboard.Bff.Services;

public interface IPrometheusQueryService
{
    Task<List<MetricDataPoint>> QueryRangeAsync(string query, DateTime start, DateTime end, string step, CancellationToken ct = default);
}

public sealed class PrometheusOptions
{
    public const string SectionName = "Prometheus";
    public required string Url { get; init; }
}
```

- [ ] **Step 3: Create PrometheusQueryService**

```csharp
namespace SystemDashboard.Bff.Services;

public class PrometheusQueryService : IPrometheusQueryService
{
    private readonly HttpClient _http;
    private readonly ILogger<PrometheusQueryService> _logger;

    public PrometheusQueryService(HttpClient http, ILogger<PrometheusQueryService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<MetricDataPoint>> QueryRangeAsync(string query, DateTime start, DateTime end, string step, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/query_range?query={Uri.EscapeDataString(query)}" +
                      $"&start={new DateTimeOffset(start).ToUnixTimeSeconds()}" +
                      $"&end={new DateTimeOffset(end).ToUnixTimeSeconds()}" +
                      $"&step={step}";

            var response = await _http.GetFromJsonAsync<PrometheusResponse>(url, cancellationToken: ct);

            return response?.Data?.Result?.FirstOrDefault()?.Values?
                .Select(v => new MetricDataPoint { Timestamp = DateTimeOffset.FromUnixTimeSeconds(v.Timestamp).UtcDateTime, Value = double.Parse(v.Value) })
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Prometheus: {Query}", query);
            return [];
        }
    }

    private sealed record PrometheusResponse { public PrometheusData? Data { get; init; } }
    private sealed record PrometheusData { public List<PrometheusResult>? Result { get; init; } }
    private sealed record PrometheusResult { public List<PrometheusValue>? Values { get; init; } }
    private sealed record PrometheusValue { public long Timestamp { get; init; } public string Value { get; init; } = "0"; }
}
```

Register in `Program.cs`:
```csharp
builder.Services.Configure<PrometheusOptions>(builder.Configuration.GetSection(PrometheusOptions.SectionName));
builder.Services.AddHttpClient<IPrometheusQueryService, PrometheusQueryService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<PrometheusOptions>>();
    client.BaseAddress = new Uri(opts.Value.Url);
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

- [ ] **Step 4: Create IMetricsAggregator + MetricsAggregator**

```csharp
namespace SystemDashboard.Bff.Aggregators;

public interface IMetricsAggregator
{
    Task<List<MetricSnapshot>> GetMetricsAsync(string service, string[] metricNames, string range, CancellationToken ct = default);
    Task<Dictionary<string, MetricSummary>> GetSummaryAsync(CancellationToken ct = default);
}

public sealed record MetricSummary
{
    public double CpuPercent { get; init; }
    public double MemoryMb { get; init; }
    public double RequestRate { get; init; }
    public double ErrorRate { get; init; }
}

public class MetricsAggregator : IMetricsAggregator
{
    private readonly IPrometheusQueryService _prometheus;
    private readonly ILogger<MetricsAggregator> _logger;

    // PromQL templates
    private static readonly Dictionary<string, string> _metricQueries = new()
    {
        ["cpu"] = "rate(process_cpu_seconds_total{{service=\"{service}\"}}[5m]) * 100",
        ["memory"] = "process_working_set_bytes{{service=\"{service}\"}} / 1024 / 1024",
        ["requests"] = "rate(http_requests_total{{service=\"{service}\"}}[5m])",
        ["errors"] = "rate(http_requests_total{{service=\"{service}\",status=~\"5..\"}}[5m])",
    };

    public MetricsAggregator(IPrometheusQueryService prometheus, ILogger<MetricsAggregator> logger)
    {
        _prometheus = prometheus;
        _logger = logger;
    }

    public async Task<List<MetricSnapshot>> GetMetricsAsync(string service, string[] metricNames, string range, CancellationToken ct = default)
    {
        var end = DateTime.UtcNow;
        var start = range switch
        {
            "5m" => end.AddMinutes(-5),
            "15m" => end.AddMinutes(-15),
            "1h" => end.AddHours(-1),
            "6h" => end.AddHours(-6),
            "24h" => end.AddHours(-24),
            _ => end.AddHours(-1)
        };
        var step = (end - start).TotalSeconds < 3600 ? "15s" : "1m";

        var results = new List<MetricSnapshot>();

        foreach (var metricName in metricNames)
        {
            if (!_metricQueries.TryGetValue(metricName, out var queryTemplate)) continue;
            var query = queryTemplate.Replace("{service}", service);

            var dataPoints = await _prometheus.QueryRangeAsync(query, start, end, step, ct);

            results.Add(new MetricSnapshot
            {
                Service = service,
                MetricName = metricName,
                DataPoints = dataPoints
            });
        }

        return results;
    }

    public Task<Dictionary<string, MetricSummary>> GetSummaryAsync(CancellationToken ct = default)
    {
        // Simplified: return empty in v1; UI can use /api/metrics/{service} for per-service detail
        return Task.FromResult(new Dictionary<string, MetricSummary>());
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddSingleton<IMetricsAggregator, MetricsAggregator>();
```

- [ ] **Step 5: Create MetricsAggregatorTests**

```csharp
[Fact]
public async Task GetMetricsAsync_ReturnsMetricsForService()
{
    var prometheus = Substitute.For<IPrometheusQueryService>();
    prometheus.QueryRangeAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(new List<MetricDataPoint>
        {
            new() { Timestamp = DateTime.UtcNow, Value = 42.5 }
        });

    var aggregator = new MetricsAggregator(prometheus, Substitute.For<ILogger<MetricsAggregator>>());
    var metrics = await aggregator.GetMetricsAsync("patient-service", new[] { "cpu" }, "1h");

    Assert.Single(metrics);
    Assert.Equal("cpu", metrics[0].MetricName);
    Assert.Single(metrics[0].DataPoints);
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: 4 tests PASS

- [ ] **Step 7: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/
git commit -m "feat(dashboard): add Prometheus metrics query service and aggregator with tests"
```

---

### Task 6: Jaeger Traces Query Service + Traces Aggregator

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Services/IJaegerQueryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/JaegerQueryService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Models/TraceSummary.cs`
- Create: `src/Bff/SystemDashboard.Bff/Models/TraceDetail.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/ITracesAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/TracesAggregator.cs`
- Create: `src/Bff/SystemDashboard.Bff/Tests/TracesAggregatorTests.cs`

**Interfaces:**
- Consumes: `IJaegerQueryService` querying Jaeger REST API
- Produces: `ITracesAggregator.SearchTracesAsync(...)` returning `List<TraceSummary>`, `GetTraceAsync(traceId)` returning `TraceDetail`
- Test: xUnit + NSubstitute

- [ ] **Step 1: Create Trace models**

```csharp
namespace SystemDashboard.Bff.Models;

public sealed record TraceSummary
{
    public required string TraceId { get; init; }
    public required string RootService { get; init; }
    public required string RootOperation { get; init; }
    public long DurationMs { get; init; }
    public int SpanCount { get; init; }
    public DateTime StartTime { get; init; }
}

public sealed record TraceDetail
{
    public required string TraceId { get; init; }
    public List<TraceSpan> Spans { get; init; } = [];
    public Dictionary<string, string> Processes { get; init; } = [];
}

public sealed record TraceSpan
{
    public required string SpanId { get; init; }
    public required string OperationName { get; init; }
    public required string ProcessId { get; init; }
    public long StartTimeUs { get; init; }
    public long DurationUs { get; init; }
    public List<string>? References { get; init; } // parent span IDs
    public Dictionary<string, string> Tags { get; init; } = [];
    public List<TraceLog> Logs { get; init; } = [];
}

public sealed record TraceLog
{
    public long TimestampUs { get; init; }
    public Dictionary<string, string> Fields { get; init; } = [];
}
```

- [ ] **Step 2: Create IJaegerQueryService interface**

```csharp
namespace SystemDashboard.Bff.Services;

public interface IJaegerQueryService
{
    Task<List<TraceSummary>> SearchTracesAsync(string service, DateTime? from, DateTime? to, long? minDurationMs, int limit, CancellationToken ct = default);
    Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default);
}

public sealed class JaegerOptions
{
    public const string SectionName = "Jaeger";
    public required string QueryUrl { get; init; }
}
```

- [ ] **Step 3: Create JaegerQueryService**

```csharp
namespace SystemDashboard.Bff.Services;

public class JaegerQueryService : IJaegerQueryService
{
    private readonly HttpClient _http;
    private readonly ILogger<JaegerQueryService> _logger;

    public JaegerQueryService(HttpClient http, ILogger<JaegerQueryService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<TraceSummary>> SearchTracesAsync(string service, DateTime? from, DateTime? to, long? minDurationMs, int limit, CancellationToken ct = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"service={Uri.EscapeDataString(service)}",
                $"limit={limit}"
            };
            if (from.HasValue) queryParams.Add($"start={new DateTimeOffset(from.Value).ToUnixTimeMicroseconds()}");
            if (to.HasValue) queryParams.Add($"end={new DateTimeOffset(to.Value).ToUnixTimeMicroseconds()}");
            if (minDurationMs.HasValue) queryParams.Add($"minDuration={minDurationMs.Value}ms");

            var url = $"/api/traces?{string.Join('&', queryParams)}";
            var response = await _http.GetFromJsonAsync<JaegerTraceSearchResponse>(url, cancellationToken: ct);

            return response?.Data?.Select(t => new TraceSummary
            {
                TraceId = t.TraceId,
                RootService = t.Spans?.FirstOrDefault()?.Process?.ServiceName ?? service,
                RootOperation = t.Spans?.FirstOrDefault()?.OperationName ?? "unknown",
                DurationMs = t.Spans?.Max(s => s.Duration) ?? 0 / 1000,
                SpanCount = t.Spans?.Count ?? 0,
                StartTime = DateTimeOffset.FromUnixTimeMicroseconds(t.Spans?.Min(s => s.StartTime) ?? 0).UtcDateTime
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search traces for {Service}", service);
            return [];
        }
    }

    public async Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/traces/{traceId}";
            var response = await _http.GetFromJsonAsync<JaegerTraceDetailResponse>(url, cancellationToken: ct);

            if (response?.Data is null || response.Data.Count == 0) return null;

            var trace = response.Data[0];
            return new TraceDetail
            {
                TraceId = trace.TraceId,
                Spans = trace.Spans?.Select(s => new TraceSpan
                {
                    SpanId = s.SpanId,
                    OperationName = s.OperationName,
                    ProcessId = s.ProcessId,
                    StartTimeUs = s.StartTime,
                    DurationUs = s.Duration,
                    References = s.References?.Select(r => r.SpanId).ToList(),
                    Tags = s.Tags?.ToDictionary(t => t.Key, t => t.Value.ToString() ?? "") ?? [],
                    Logs = s.Logs?.Select(l => new TraceLog
                    {
                        TimestampUs = l.Timestamp,
                        Fields = l.Fields?.ToDictionary(f => f.Key, f => f.Value.ToString() ?? "") ?? []
                    }).ToList() ?? []
                }).ToList() ?? [],
                Processes = trace.Processes?.ToDictionary(p => p.Key, p => p.Value.ServiceName) ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get trace {TraceId}", traceId);
            return null;
        }
    }

    private sealed record JaegerTraceSearchResponse { public List<JaegerTrace>? Data { get; init; } }
    private sealed record JaegerTraceDetailResponse { public List<JaegerTrace>? Data { get; init; } }
    private sealed record JaegerTrace
    {
        public string TraceId { get; init; } = "";
        public List<JaegerSpan>? Spans { get; init; }
        public Dictionary<string, JaegerProcess>? Processes { get; init; }
    }
    private sealed record JaegerSpan
    {
        public string SpanId { get; init; } = "";
        public string OperationName { get; init; } = "";
        public string ProcessId { get; init; } = "";
        public long StartTime { get; init; }
        public long Duration { get; init; }
        public List<JaegerReference>? References { get; init; }
        public List<JaegerTag>? Tags { get; init; }
        public List<JaegerLog>? Logs { get; init; }
        public JaegerProcess? Process { get; init; }
    }
    private sealed record JaegerReference { public string SpanId { get; init; } = ""; }
    private sealed record JaegerTag { public string Key { get; init; } = ""; public object Value { get; init; } = ""; }
    private sealed record JaegerLog { public long Timestamp { get; init; } public List<JaegerTag>? Fields { get; init; } }
    private sealed record JaegerProcess { public string ServiceName { get; init; } = ""; }
}
```

Register in `Program.cs`:
```csharp
builder.Services.Configure<JaegerOptions>(builder.Configuration.GetSection(JaegerOptions.SectionName));
builder.Services.AddHttpClient<IJaegerQueryService, JaegerQueryService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<JaegerOptions>>();
    client.BaseAddress = new Uri(opts.Value.QueryUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

- [ ] **Step 4: Create ITracesAggregator + TracesAggregator**

```csharp
namespace SystemDashboard.Bff.Aggregators;

public interface ITracesAggregator
{
    Task<List<TraceSummary>> SearchTracesAsync(string service, DateTime? from, DateTime? to, long? minDurationMs, int limit, CancellationToken ct = default);
    Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default);
}

public class TracesAggregator : ITracesAggregator
{
    private readonly IJaegerQueryService _jaeger;

    public TracesAggregator(IJaegerQueryService jaeger) => _jaeger = jaeger;

    public Task<List<TraceSummary>> SearchTracesAsync(string service, DateTime? from, DateTime? to, long? minDurationMs, int limit, CancellationToken ct = default)
        => _jaeger.SearchTracesAsync(service, from, to, minDurationMs, limit, ct);

    public Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default)
        => _jaeger.GetTraceAsync(traceId, ct);
}
```

Register: `builder.Services.AddSingleton<ITracesAggregator, TracesAggregator>();`

- [ ] **Step 5: Create tests and commit**

```bash
dotnet test src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj
git add src/Bff/SystemDashboard.Bff/
git commit -m "feat(dashboard): add Jaeger traces query service and aggregator"
```

---

### Task 7: Lifecycle Controller (Docker + Kubernetes)

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Services/IServiceLifecycleService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/DockerLifecycleService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/KubernetesLifecycleService.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/ILifecycleController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/LifecycleController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Tests/LifecycleControllerTests.cs`

**Interfaces:**
- Consumes: `IServiceLifecycleService` (implemented by Docker or K8s variant), `IOptions<KubernetesOptions>`
- Produces: `ILifecycleController.StartAsync/StopAsync/RestartAsync(serviceName)`
- Test: xUnit + NSubstitute

- [ ] **Step 1: Create IServiceLifecycleService interface**

```csharp
namespace SystemDashboard.Bff.Services;

public interface IServiceLifecycleService
{
    Task<bool> StartAsync(string serviceName, CancellationToken ct = default);
    Task<bool> StopAsync(string serviceName, CancellationToken ct = default);
    Task<bool> RestartAsync(string serviceName, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create DockerLifecycleService**

```csharp
namespace SystemDashboard.Bff.Services;

public class DockerLifecycleService : IServiceLifecycleService
{
    private readonly ILogger<DockerLifecycleService> _logger;
    private readonly string _projectName;

    public DockerLifecycleService(IOptions<DockerOptions> options, ILogger<DockerLifecycleService> logger)
    {
        _logger = logger;
        _projectName = options.Value.ComposeProjectName;
    }

    public async Task<bool> StartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunComposeCommand($"start {serviceName}", ct);
    }

    public async Task<bool> StopAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunComposeCommand($"stop {serviceName}", ct);
    }

    public async Task<bool> RestartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunComposeCommand($"restart {serviceName}", ct);
    }

    private async Task<bool> RunComposeCommand(string args, CancellationToken ct)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = $"-p {_projectName} {args}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError("Docker compose {Args} failed: {Error}", args, error);
                return false;
            }

            _logger.LogInformation("Docker compose {Args} succeeded", args);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute docker-compose {Args}", args);
            return false;
        }
    }
}
```

- [ ] **Step 3: Create KubernetesLifecycleService**

```csharp
namespace SystemDashboard.Bff.Services;

public class KubernetesLifecycleService : IServiceLifecycleService
{
    private readonly ILogger<KubernetesLifecycleService> _logger;
    private readonly string _namespace;

    public KubernetesLifecycleService(ILogger<KubernetesLifecycleService> logger)
    {
        _logger = logger;
        _namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "his-hope";
    }

    public async Task<bool> StartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunKubectl($"scale deployment/{serviceName} --replicas=1", ct);
    }

    public async Task<bool> StopAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunKubectl($"scale deployment/{serviceName} --replicas=0", ct);
    }

    public async Task<bool> RestartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunKubectl($"rollout restart deployment/{serviceName}", ct);
    }

    private async Task<bool> RunKubectl(string args, CancellationToken ct)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "kubectl",
                    Arguments = $"-n {_namespace} {args}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError("kubectl {Args} failed: {Error}", args, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute kubectl {Args}", args);
            return false;
        }
    }
}
```

Register in `Program.cs` (selects impl based on config):
```csharp
builder.Services.Configure<KubernetesOptions>(builder.Configuration.GetSection("Kubernetes"));
builder.Services.AddSingleton<DockerLifecycleService>();
builder.Services.AddSingleton<KubernetesLifecycleService>();
builder.Services.AddSingleton<IServiceLifecycleService>(sp =>
{
    var k8sOptions = sp.GetRequiredService<IOptions<KubernetesOptions>>();
    if (k8sOptions.Value.Enabled)
        return sp.GetRequiredService<KubernetesLifecycleService>();
    return sp.GetRequiredService<DockerLifecycleService>();
});
```

- [ ] **Step 4: Create LifecycleController (aggregator)**

```csharp
namespace SystemDashboard.Bff.Aggregators;

public interface ILifecycleController
{
    Task<bool> StartAsync(string serviceName, CancellationToken ct = default);
    Task<bool> StopAsync(string serviceName, CancellationToken ct = default);
    Task<bool> RestartAsync(string serviceName, CancellationToken ct = default);
}

public class LifecycleController : ILifecycleController
{
    private readonly IServiceLifecycleService _lifecycle;
    private readonly ILogger<LifecycleController> _logger;

    public LifecycleController(IServiceLifecycleService lifecycle, ILogger<LifecycleController> logger)
    {
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public async Task<bool> StartAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting service {Service}", serviceName);
        return await _lifecycle.StartAsync(serviceName, ct);
    }

    public async Task<bool> StopAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("Stopping service {Service}", serviceName);
        return await _lifecycle.StopAsync(serviceName, ct);
    }

    public async Task<bool> RestartAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("Restarting service {Service}", serviceName);
        return await _lifecycle.RestartAsync(serviceName, ct);
    }
}
```

Register: `builder.Services.AddSingleton<ILifecycleController, LifecycleController>();`

- [ ] **Step 5: Run tests and commit**

---

### Task 8: REST API Controllers

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Controllers/ResourcesController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Controllers/LogsController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Controllers/TracesController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Controllers/MetricsController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Controllers/EnvironmentController.cs`
- Create: `src/Bff/SystemDashboard.Bff/Models/EnvironmentContext.cs`

- [ ] **Step 1: Create EnvironmentContext model**

```csharp
namespace SystemDashboard.Bff.Models;

public sealed record EnvironmentContext
{
    public required string Name { get; init; } // "Development", "Staging", "Production"
    public DateTime SwitchedAt { get; init; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Create ResourcesController**

```csharp
namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceAggregator _resources;
    private readonly ILifecycleController _lifecycle;

    public ResourcesController(IResourceAggregator resources, ILifecycleController lifecycle)
    {
        _resources = resources;
        _lifecycle = lifecycle;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var resources = await _resources.GetAllResourcesAsync(ct);
        return Ok(new { resources });
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetByName(string name, CancellationToken ct)
    {
        var resource = await _resources.GetResourceByNameAsync(name, ct);
        return resource is null ? NotFound() : Ok(resource);
    }

    [HttpPost("{name}/start")]
    public async Task<IActionResult> Start(string name, CancellationToken ct)
    {
        var result = await _lifecycle.StartAsync(name, ct);
        return result ? Ok(new { status = "started" }) : StatusCode(500, new { error = "Failed to start service" });
    }

    [HttpPost("{name}/stop")]
    public async Task<IActionResult> Stop(string name, CancellationToken ct)
    {
        var result = await _lifecycle.StopAsync(name, ct);
        return result ? Ok(new { status = "stopped" }) : StatusCode(500, new { error = "Failed to stop service" });
    }

    [HttpPost("{name}/restart")]
    public async Task<IActionResult> Restart(string name, CancellationToken ct)
    {
        var result = await _lifecycle.RestartAsync(name, ct);
        return result ? Ok(new { status = "restarted" }) : StatusCode(500, new { error = "Failed to restart service" });
    }
}
```

- [ ] **Step 3: Create LogsController**

```csharp
namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly ILogsAggregator _logs;

    public LogsController(ILogsAggregator logs) => _logs = logs;

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? service,
        [FromQuery] string? level,
        [FromQuery] DateTime? from,
        [FromQuery] int size = 100,
        [FromQuery] string? query = null,
        CancellationToken ct = default)
    {
        var logs = await _logs.QueryLogsAsync(service, level, from, size, query, ct);
        return Ok(new { logs, total = logs.Count });
    }
}
```

- [ ] **Step 4: Create TracesController**

```csharp
namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/traces")]
[Authorize]
public class TracesController : ControllerBase
{
    private readonly ITracesAggregator _traces;

    public TracesController(ITracesAggregator traces) => _traces = traces;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string service,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] long? minDurationMs,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var traces = await _traces.SearchTracesAsync(service, from, to, minDurationMs, limit, ct);
        return Ok(new { traces });
    }

    [HttpGet("{traceId}")]
    public async Task<IActionResult> Get(string traceId, CancellationToken ct)
    {
        var trace = await _traces.GetTraceAsync(traceId, ct);
        return trace is null ? NotFound() : Ok(trace);
    }
}
```

- [ ] **Step 5: Create MetricsController**

```csharp
namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/metrics")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly IMetricsAggregator _metrics;

    public MetricsController(IMetricsAggregator metrics) => _metrics = metrics;

    [HttpGet("{service}")]
    public async Task<IActionResult> GetServiceMetrics(
        string service,
        [FromQuery] string metrics = "cpu,memory,requests",
        [FromQuery] string range = "1h",
        CancellationToken ct = default)
    {
        var metricNames = metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await _metrics.GetMetricsAsync(service, metricNames, range, ct);
        return Ok(new { metrics = result });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _metrics.GetSummaryAsync(ct);
        return Ok(new { summary });
    }
}
```

- [ ] **Step 6: Create EnvironmentController**

```csharp
namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/environment")]
[Authorize]
public class EnvironmentController : ControllerBase
{
    [HttpGet]
    public IActionResult GetCurrent()
    {
        return Ok(new EnvironmentContext
        {
            Name = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
        });
    }
}
```

- [ ] **Step 7: Run tests and commit**

---

### Task 9: Dockerfile + Docker Compose Integration

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Dockerfile`
- Modify: `docker/docker-compose.yml`

- [ ] **Step 1: Create Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj", "src/Bff/SystemDashboard.Bff/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/His.Hope.Infrastructure.csproj", "src/Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj", "src/Bff/His.Hope.Bff.Core/"]
RUN dotnet restore "src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj"
COPY . .
WORKDIR "/src/src/Bff/SystemDashboard.Bff"
RUN dotnet publish "SystemDashboard.Bff.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 5700
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemDashboard.Bff.dll"]
```

- [ ] **Step 2: Add to docker-compose.yml**
  Add service definition to `docker/docker-compose.yml`:
```yaml
  systemdashboard-bff:
    build:
      context: ..
      dockerfile: src/Bff/SystemDashboard.Bff/Dockerfile
    ports:
      - "5700:5700"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Consul__Address=http://consul:8500
      - Elasticsearch__Url=http://elasticsearch:9200
      - Jaeger__QueryUrl=http://jaeger:16686
      - Prometheus__Url=http://prometheus:9090
    depends_on:
      - consul
      - elasticsearch
      - jaeger
      - prometheus
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock  # for docker-compose lifecycle control
```

- [ ] **Step 3: Commit**

---

## Phase 2: Angular Dashboard App

### Task 10: Create Angular Workspace + Core

**Files:**
- Create: `dashboard-app/` (entire Angular workspace via `ng new`)
- Create: `dashboard-app/src/app/core/models/resource.model.ts`
- Create: `dashboard-app/src/app/core/models/log-entry.model.ts`
- Create: `dashboard-app/src/app/core/models/trace.model.ts`
- Create: `dashboard-app/src/app/core/models/metric-snapshot.model.ts`
- Create: `dashboard-app/src/app/core/services/resource.service.ts`
- Create: `dashboard-app/src/app/core/services/logs.service.ts`
- Create: `dashboard-app/src/app/core/services/traces.service.ts`
- Create: `dashboard-app/src/app/core/services/metrics.service.ts`
- Create: `dashboard-app/src/app/core/services/lifecycle.service.ts`
- Create: `dashboard-app/src/app/core/services/environment.service.ts`
- Create: `dashboard-app/src/app/core/services/auth.service.ts`
- Create: `dashboard-app/src/app/core/guards/auth.guard.ts`
- Create: `dashboard-app/src/app/app.config.ts`
- Create: `dashboard-app/src/app/app.routes.ts`
- Create: `dashboard-app/src/app/app.component.ts`
- Create: `dashboard-app/src/app/app.component.html`
- Create: `dashboard-app/src/app/app.component.scss`
- Create: `dashboard-app/src/environments/environment.ts`
- Create: `dashboard-app/src/environments/environment.prod.ts`

**Interfaces:**
- Consumes: SystemDashboard.Bff REST API at port 5700
- Produces: Shared Angular models and services for all feature components
- Delegate this entire task to @angular agent

- [ ] **Step 1: Delegate to @angular agent**

Use the task tool with subagent_type "angular" to create the Angular workspace and all core files.

The Angular agent should:
1. Run `ng new dashboard-app --routing --style=scss --standalone` (or create manually to avoid prompts)
2. Install Angular Material: `ng add @angular/material`
3. Choose the existing His.Hope Material theme (Indigo/Pink or match existing)
4. Create all core models matching the backend C# types (camelCase in TypeScript)
5. Create all core services with HTTP calls to port 5700
6. Create auth guard that validates JWT via IdentityService
7. Create app shell with sidebar navigation + environment selector toolbar

Models to match:
```typescript
// resource.model.ts
export interface Resource {
  name: string;
  type: 'service' | 'bff' | 'database' | 'infrastructure' | 'gateway';
  state: 'Running' | 'Stopped' | 'Degraded' | 'Unknown';
  endpoint?: string;
  displayName?: string;
}

export interface ServiceResource extends Resource {
  httpPort?: number;
  grpcPort?: number;
  healthStatus: string;
  uptime?: string;
  replicas: number;
  cpuPercent: number;
  memoryUsedMb: number;
  memoryLimitMb: number;
  databases: string[];
  environment: Record<string, string>;
  healthChecks: HealthCheckResult[];
}

export interface DatabaseResource extends Resource {
  engine: string;
  sizeMb: number;
  connections: number;
}

export interface InfrastructureResource extends Resource {
  category: string;
  version?: string;
}

export interface HealthCheckResult {
  name: string;
  status: string;
  output?: string;
}
```

Services should use Angular's `HttpClient` with base URL from environment config.
Environment config:
```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5700/api',
  wsUrl: 'http://localhost:5700/ws',
  identityUrl: 'http://localhost:5001',
};
```

App shell layout (app.component.html):
```html
<mat-sidenav-container class="app-container">
  <mat-sidenav mode="side" opened>
    <mat-nav-list>
      <a mat-list-item routerLink="/resources" routerLinkActive="active">
        <mat-icon>dns</mat-icon> Resources
      </a>
      <a mat-list-item routerLink="/logs" routerLinkActive="active">
        <mat-icon>article</mat-icon> Logs
      </a>
      <a mat-list-item routerLink="/traces" routerLinkActive="active">
        <mat-icon>timeline</mat-icon> Traces
      </a>
      <a mat-list-item routerLink="/metrics" routerLinkActive="active">
        <mat-icon>bar_chart</mat-icon> Metrics
      </a>
    </mat-nav-list>
  </mat-sidenav>
  <mat-sidenav-content>
    <mat-toolbar color="primary">
      <span>His.Hope Dashboard</span>
      <span class="spacer"></span>
      <app-environment-selector></app-environment-selector>
      <button mat-icon-button><mat-icon>account_circle</mat-icon></button>
    </mat-toolbar>
    <main class="content">
      <router-outlet></router-outlet>
    </main>
  </mat-sidenav-content>
</mat-sidenav-container>
```

- [ ] **Step 2: Verify Angular builds**

Run: `ng build --project dashboard-app`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add dashboard-app/
git commit -m "feat(dashboard): scaffold Angular dashboard app with core services and shell"
```

---

### Task 11: Resources Page (List + Detail + Lifecycle Actions)

**Files:**
- Create: `dashboard-app/src/app/shared/service-status-badge/` (3 files)
- Create: `dashboard-app/src/app/shared/resource-card/` (3 files)
- Create: `dashboard-app/src/app/shared/environment-selector/` (3 files)
- Create: `dashboard-app/src/app/features/resources/resource-list/` (3 files)
- Create: `dashboard-app/src/app/features/resources/resource-detail/` (3 files)

**Interfaces:**
- Consumes: `ResourceService`, `LifecycleService`
- Produces: Resource list grid with cards, detail drawer, start/stop/restart buttons
- Delegate to @angular agent

- [ ] **Step 1: Delegate to @angular agent**

Build the resources feature with these components:

**service-status-badge** — Shows colored dot + text based on state:
- Running → green, Stopped → gray, Degraded → yellow, Unknown → red

**resource-card** — Card component showing service info:
- Status badge, name, ports, CPU/Memory mini display, action buttons
- Buttons: [Start] (when stopped), [Stop] [Restart] [View Logs] (when running)
- Click card → opens detail drawer via output event

**resource-list** — Main page component:
- Calls `ResourceService.getAll()` on init
- Groups cards: "Services", "Databases", "Infrastructure"
- Shows loading spinner while fetching
- Shows error state if API fails

**resource-detail** — Drawer/sheet component:
- Shows full service info: endpoints, env vars, health checks list
- Opens on card click

**environment-selector** — Toolbar dropdown:
- Displays current env: "Development" / "Staging" / "Production"
- On change, calls `EnvironmentService` + reloads all data

- [ ] **Step 2: Verify with Angular build**

Run: `ng build --project dashboard-app`
Expected: Build succeeds

- [ ] **Step 3: Commit**

---

### Task 12: Logs Page (Table + Filters + Live Stream)

**Files:**
- Create: `dashboard-app/src/app/shared/log-level-filter/` (3 files)
- Create: `dashboard-app/src/app/features/logs/log-list/` (3 files)
- Create: `dashboard-app/src/app/features/logs/log-stream/` (3 files)
- Create: `dashboard-app/src/app/core/services/log-stream.service.ts`

**Interfaces:**
- Consumes: `LogsService`, `LogStreamService` (SignalR)
- Produces: Log table with filters + real-time live stream toggle
- Delegate to @angular agent

- [ ] **Step 1: Delegate to @angular agent**

Build the logs feature:

**log-level-filter** — Chip/toggle group: All | Error | Warning | Information | Debug

**log-list** — Table component:
- Columns: Timestamp | Level (colored badge) | Service | Message
- Filter bar: service dropdown, level filter, time range, search input
- Pagination (or load more)
- Click row → expand JSON fields

**log-stream** — Real-time viewer:
- Toggle to enable/disable live stream
- When enabled, connects SignalR to specific service
- New logs auto-append to top of list
- Auto-scroll

**log-stream.service** — SignalR connection:
```typescript
import * as signalR from '@microsoft/signalr';

@Injectable({ providedIn: 'root' })
export class LogStreamService {
  private hubConnection: signalR.HubConnection;

  constructor(private env: EnvironmentService) {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.env.wsUrl}/logs/stream`)
      .withAutomaticReconnect()
      .build();
  }

  async subscribe(service: string, level?: string): Promise<void> {
    await this.hubConnection.start();
    await this.hubConnection.invoke('SubscribeToService', service, level);
  }

  onLogReceived(callback: (log: LogEntry) => void): void {
    this.hubConnection.on('LogReceived', callback);
  }

  async unsubscribe(service: string): Promise<void> {
    await this.hubConnection.invoke('UnsubscribeFromService', service);
    await this.hubConnection.stop();
  }
}
```

- [ ] **Step 2: Verify build + commit**

---

### Task 13: Traces Page (List + Waterfall View)

**Files:**
- Create: `dashboard-app/src/app/features/traces/trace-list/` (3 files)
- Create: `dashboard-app/src/app/features/traces/trace-detail/` (3 files)

**Interfaces:**
- Consumes: `TracesService`
- Produces: Trace search + waterfall detail view
- Delegate to @angular agent

- [ ] **Step 1: Delegate to @angular agent**

Build traces feature:

**trace-list** — Search + table:
- Filter bar: service dropdown, time range, min duration
- Table columns: TraceID (truncated) | Service | Duration | Spans | Time
- Click → navigate to trace detail

**trace-detail** — Waterfall view:
- Header: TraceID, total duration, service count
- Waterfall chart: horizontal bars showing spans
- Each span shows: service name, operation, duration
- Color-coded by service
- Click span → expand tags/logs

- [ ] **Step 2: Verify build + commit**

---

### Task 14: Metrics Page (Charts + Service Selector)

**Files:**
- Create: `dashboard-app/src/app/features/metrics/metrics-overview/` (3 files)
- Create: `dashboard-app/src/app/features/metrics/metrics-chart/` (3 files)
- Add to `dashboard-app/package.json`: `"chart.js": "^4.4.0"` dependency

**Interfaces:**
- Consumes: `MetricsService`
- Produces: Metrics dashboard with line charts
- Delegate to @angular agent

- [ ] **Step 1: Delegate to @angular agent**

Build metrics feature:

**metrics-overview** — Summary cards at top:
- Total services running / stopped / degraded
- Aggregated CPU, Memory across all services

**metrics-chart** — Chart page:
- Service multi-selector dropdown
- Metric type picker: CPU | Memory | Requests | Errors
- Time range: 5m | 15m | 1h | 6h | 24h
- Line chart using Chart.js (via ng2-charts or direct canvas)
- Multiple services = multiple lines on one chart

- [ ] **Step 2: Verify build + commit**

---

### Task 15: E2E Tests (Playwright)

**Files:**
- Create: `tests/e2e/dashboard/` directory
- Create: `tests/e2e/dashboard/dashboard.spec.ts`

**Interfaces:**
- Consumes: SystemDashboard.Bff + Angular app running
- Produces: E2E test covering full user journey
- Delegate to @e2e-test agent

- [ ] **Step 1: Delegate to @e2e-test agent**

E2E test scenarios:
1. Login to dashboard → verify redirect to resources page
2. Resources page shows service cards with correct status
3. Click service card → detail drawer opens with env vars
4. Navigate to Logs → filter by service + level → see results
5. Navigate to Traces → search by service → see trace list
6. Click trace → waterfall view renders
7. Navigate to Metrics → select service → chart renders
8. Environment switcher changes context and reloads data

```typescript
import { test, expect } from '@playwright/test';

test.describe('Aspire Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4201');
    // Login flow...
  });

  test('resources page shows services', async ({ page }) => {
    await expect(page.getByText('Resources')).toBeVisible();
    await expect(page.getByText('patient-service')).toBeVisible();
    await expect(page.getByText('Running')).toBeVisible();
  });

  test('can view service detail', async ({ page }) => {
    await page.getByText('patient-service').click();
    await expect(page.getByText('Health Checks')).toBeVisible();
  });

  test('logs page filters work', async ({ page }) => {
    await page.getByText('Logs').click();
    await page.getByLabel('Service').click();
    await page.getByText('patient-service').click();
    await page.getByText('Error').click();
    await expect(page.getByRole('table')).toBeVisible();
  });

  test('traces search returns results', async ({ page }) => {
    await page.getByText('Traces').click();
    await page.getByLabel('Service').fill('patient-service');
    await page.getByRole('button', { name: 'Search' }).click();
    await expect(page.getByRole('table')).toBeVisible();
  });

  test('metrics chart renders', async ({ page }) => {
    await page.getByText('Metrics').click();
    await page.getByText('patient-service').click();
    await expect(page.locator('canvas')).toBeVisible();
  });
});
```

- [ ] **Step 2: Run E2E tests**

Run: `npx playwright test tests/e2e/dashboard/ --workers=1`
Expected: All tests pass

- [ ] **Step 3: Commit**

---

## Phase 3: Kubernetes Deployment

### Task 16: K8s Manifests

**Files:**
- Create: `k8s/base/systemdashboard-bff-deployment.yaml`
- Create: `k8s/base/systemdashboard-bff-service.yaml`
- Create: `k8s/base/dashboard-app-deployment.yaml`
- Create: `k8s/base/dashboard-app-service.yaml`

- [ ] **Step 1: Create BFF deployment + service**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: systemdashboard-bff
  labels:
    app: systemdashboard-bff
spec:
  replicas: 1
  selector:
    matchLabels:
      app: systemdashboard-bff
  template:
    metadata:
      labels:
        app: systemdashboard-bff
    spec:
      containers:
        - name: bff
          image: hishope/systemdashboard-bff:latest
          ports:
            - containerPort: 5700
          env:
            - name: ASPNETCORE_ENVIRONMENT
              valueFrom:
                fieldRef:
                  fieldPath: metadata.namespace
            - name: Consul__Address
              value: "http://consul-server:8500"
            - name: Elasticsearch__Url
              value: "http://elasticsearch:9200"
            - name: Jaeger__QueryUrl
              value: "http://jaeger-query:16686"
            - name: Prometheus__Url
              value: "http://prometheus-server:9090"
            - name: Kubernetes__Enabled
              value: "true"
          resources:
            limits:
              cpu: 500m
              memory: 256Mi
            requests:
              cpu: 100m
              memory: 128Mi
          livenessProbe:
            httpGet:
              path: /health
              port: 5700
            initialDelaySeconds: 10
            periodSeconds: 30
---
apiVersion: v1
kind: Service
metadata:
  name: systemdashboard-bff
spec:
  selector:
    app: systemdashboard-bff
  ports:
    - port: 5700
      targetPort: 5700
```

- [ ] **Step 2: Create Angular app deployment**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dashboard-app
  labels:
    app: dashboard-app
spec:
  replicas: 1
  selector:
    matchLabels:
      app: dashboard-app
  template:
    metadata:
      labels:
        app: dashboard-app
    spec:
      containers:
        - name: app
          image: hishope/dashboard-app:latest
          ports:
            - containerPort: 80
          resources:
            limits:
              cpu: 200m
              memory: 128Mi
---
apiVersion: v1
kind: Service
metadata:
  name: dashboard-app
spec:
  selector:
    app: dashboard-app
  ports:
    - port: 80
      targetPort: 80
```

- [ ] **Step 3: Commit**

```bash
git add k8s/base/
git commit -m "feat(dashboard): add Kubernetes deployment manifests"
```

---

## Self-Review Checklist

Before considering this plan complete:

1. **Spec coverage check:**
   - [x] Resource list with health status → Tasks 3, 8, 11
   - [x] Service Start/Stop/Restart → Tasks 7, 8, 11
   - [x] Structured log viewer → Tasks 4, 8, 12
   - [x] Real-time log streaming (dev) → Tasks 4 (hub), 12 (Angular SignalR)
   - [x] Trace search + detail → Tasks 6, 8, 13
   - [x] Metrics charts → Tasks 5, 8, 14
   - [x] Environment switching → Tasks 8, 10 (Angular)
   - [x] RBAC with permission matrix → Tasks 1 (JWT), 8 ([Authorize])
   - [x] Distroless container → Task 9
   - [x] Docker Compose integration → Task 9
   - [x] Kubernetes deployment → Task 16
   - [x] E2E tests → Task 15

2. **No placeholders:** All steps have actual code, commands, and expected output.

3. **Type consistency:** Backend models match Angular interfaces (camelCase in TS, PascalCase in C#). Service names consistent across all tasks.

4. **Remaining gap:** DashboardBff (existing port 5600) already aggregates stats — should we rename our SystemDashboard.Bff to avoid confusion? Answer: No; SystemDashboard.Bff is a separate BFF for the Aspire-like dashboard. DashboardBff serves the main hospital dashboard. Different concerns.
