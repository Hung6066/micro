# Structural Resilience — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the 12 highest-priority structural gaps across Data, API, Security, Scalability, and Observability domains — building on the Foundation Hardening completed in Plan 1.

**Architecture:** Deploy the FHIR Gateway service for healthcare interoperability, implement Redis-backed distributed locking (RedLock), establish multi-level caching with stampede prevention, add data lifecycle management (soft-delete, archival, right-to-erasure), automate secret rotation, deploy synthetic monitoring, and add statistical anomaly detection.

**Tech Stack:** HL7.Fhir.R4 (Firely), RedLock.net, Redis 7, CockroachDB 24.1, Prometheus, Blackbox Exporter, Playwright, k6, Vault, IMemoryCache

## Global Constraints

- All database migrations must be backward-compatible (additive-only)
- All secrets managed via HashiCorp Vault — never hardcoded
- All inter-service calls must use circuit breakers (Polly)
- JWT auth required on all endpoints (except /health)
- Distroless containers only, non-root user (UID 1654)
- Conventional Commits: `feat(domain): description`
- Each task ends with independently testable and committable deliverable

---

## File Structure Map

```
Files created/modified in this plan:

src/Services/FhirGateway/                   [CREATE]  — new FHIR service
├── FhirGateway.Api/
│   ├── Program.cs
│   ├── Controllers/FhirController.cs
│   └── appsettings.json
├── FhirGateway.Application/
│   ├── Adapters/
│   │   ├── PatientFhirAdapter.cs
│   │   ├── EncounterFhirAdapter.cs
│   │   └── ObservationFhirAdapter.cs
│   └── Services/FhirService.cs
└── FhirGateway.Domain/

src/Shared/His.Hope.Infrastructure/
├── Locking/
│   ├── ILockManager.cs                     [CREATE]  — distributed lock interface
│   ├── RedisLockManager.cs                 [CREATE]  — RedLock implementation
│   └── DistributedLockAttribute.cs         [CREATE]  — MediatR pipeline attribute
├── Caching/
│   ├── MemoryCacheService.cs               [CREATE]  — L1 in-memory cache
│   ├── HybridCacheService.cs               [CREATE]  — L1+L2 orchestration
│   └── CacheWarmupService.cs               [CREATE]  — startup cache warming
├── DataLifecycle/
│   ├── ISoftDeletable.cs                   [CREATE]  — soft-delete interface
│   ├── SoftDeleteInterceptor.cs             [CREATE]  — EF Core interceptor
│   └── DataRetentionService.cs             [CREATE]  — archival/cleanup logic
├── Abuse/
│   ├── PerUserRateLimitingMiddleware.cs    [CREATE]  — per-user rate limiter
│   └── BruteForceProtectionService.cs      [CREATE]  — login attempt tracking

src/IdentityService/IdentityService.Api/
├── Controllers/RateLimitController.cs      [CREATE]  — rate limit endpoints

k8s/
├── monitoring/
│   ├── blackbox-exporter-config.yaml       [CREATE]  — Blackbox Exporter config
│   └── anomaly-rules.yaml                  [CREATE]  — statistical anomaly rules
├── infrastructure/
│   └── secret-rotation-cronjobs.yaml       [CREATE]  — DB/JWT/RabbitMQ rotation
├── jobs/
│   ├── outbox-cleanup-cronjob.yaml         [CREATE]  — outbox cleanup job
│   ├── audit-archive-cronjob.yaml           [CREATE]  — audit archival job
│   └── synthetic-monitor-cronjob.yaml      [CREATE]  — synthetic journey job

cockroach/
├── migrations/
│   ├── 019-soft-delete-columns.sql          [CREATE]  — DeletedAt, DeletedBy
│   ├── 020-login-attempts.sql               [CREATE]  — brute force tracking
│   └── 021-saga-instances.sql               [CREATE]  — saga persistence

tests/
├── synthetic/
│   ├── login-journey.spec.ts                [CREATE]  — Playwright journey
│   ├── patient-journey.spec.ts              [CREATE]  — Playwright journey
│   └── prescription-journey.spec.ts         [CREATE]  — Playwright journey

docs/
├── api/fhir-api-reference.md                [CREATE]  — FHIR API docs
├── runbooks/                                
│   ├── cdb-failure.md                       [CREATE]  — CRDB failure runbook
│   ├── redis-failure.md                     [CREATE]  — Redis failure runbook
│   ├── rabbitmq-failure.md                  [CREATE]  — RabbitMQ runbook
│   ├── service-unavailable.md               [CREATE]  — Service down runbook
│   ├── high-latency.md                      [CREATE]  — Latency runbook
│   ├── oom-kill.md                          [CREATE]  — OOM kill runbook
│   ├── token-theft.md                       [CREATE]  — Token theft runbook
│   ├── brute-force.md                       [CREATE]  — Brute force runbook
│   ├── phi-exfiltration.md                  [CREATE]  — PHI exfiltration runbook
│   └── failed-deployment-rollback.md        [CREATE]  — Rollback runbook
```

---

### Task 1: FHIR Gateway — New Service Scaffold

**Files:**
- Create: `src/Services/FhirGateway/FhirGateway.Api/Program.cs`
- Create: `src/Services/FhirGateway/FhirGateway.Api/FhirGateway.Api.csproj`
- Create: `src/Services/FhirGateway/FhirGateway.Api/appsettings.json`
- Create: `src/Services/FhirGateway/FhirGateway.Application/FhirGateway.Application.csproj`
- Create: `src/Services/FhirGateway/FhirGateway.Domain/FhirGateway.Domain.csproj`

**Interfaces:**
- Produces: FHIR Gateway service with Clean Architecture scaffold, listening on port 5040 (HTTP) / 5041 (gRPC)
- Consumes: Shared Infrastructure for resilience, logging, JWT auth

- [ ] **Step 1: Create project structure**

```bash
mkdir -p src/Services/FhirGateway/FhirGateway.Api
mkdir -p src/Services/FhirGateway/FhirGateway.Application
mkdir -p src/Services/FhirGateway/FhirGateway.Domain
```

Create `src/Services/FhirGateway/FhirGateway.Api/FhirGateway.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>His.Hope.FhirGateway.Api</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hl7.Fhir.R4" Version="5.8.0" />
    <ProjectReference Include="..\FhirGateway.Application\FhirGateway.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Infrastructure\His.Hope.Infrastructure\His.Hope.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Program.cs with FHIR configuration**

```csharp
// src/Services/FhirGateway/FhirGateway.Api/Program.cs
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.FeatureFlags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddFeatureFlags(builder.Configuration);
builder.Services.AddSingleton<IResiliencePipelineFactory, ResiliencePipelineFactory>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

- [ ] **Step 3: Add to His.Hope.sln**

```bash
dotnet sln His.Hope.sln add src/Services/FhirGateway/FhirGateway.Api/FhirGateway.Api.csproj
dotnet sln His.Hope.sln add src/Services/FhirGateway/FhirGateway.Application/FhirGateway.Application.csproj
dotnet sln His.Hope.sln add src/Services/FhirGateway/FhirGateway.Domain/FhirGateway.Domain.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/Services/FhirGateway/ His.Hope.sln
git commit -m "feat(fhir): scaffold FHIR Gateway service with Clean Architecture"
```

---

### Task 2: FHIR Gateway — Patient + Encounter Adapters

**Files:**
- Create: `src/Services/FhirGateway/FhirGateway.Application/Adapters/PatientFhirAdapter.cs`
- Create: `src/Services/FhirGateway/FhirGateway.Application/Adapters/EncounterFhirAdapter.cs`
- Create: `src/Services/FhirGateway/FhirGateway.Api/Controllers/FhirController.cs`
- Create: `docs/api/fhir-api-reference.md`

**Interfaces:**
- Produces: `PatientFhirAdapter.ToFhir(domain.Patient)` → `Hl7.Fhir.Model.Patient`
- Produces: `EncounterFhirAdapter.ToFhir(domain.Encounter)` → `Hl7.Fhir.Model.Encounter`
- Produces: `GET /fhir/r4/Patient/{id}`, `GET /fhir/r4/Encounter/{id}`, search endpoints

- [ ] **Step 1: Create PatientFhirAdapter**

```csharp
// src/Services/FhirGateway/FhirGateway.Application/Adapters/PatientFhirAdapter.cs
using Hl7.Fhir.Model;
using Patient = His.Hope.PatientService.Domain.Entities.Patient;

namespace His.Hope.FhirGateway.Application.Adapters;

public static class PatientFhirAdapter
{
    public static Hl7.Fhir.Model.Patient ToFhir(Patient patient)
    {
        return new Hl7.Fhir.Model.Patient
        {
            Id = patient.Id.ToString(),
            Identifier = new List<Identifier>
            {
                new Identifier
                {
                    System = "urn:his-hope:patient-id",
                    Value = patient.Id.ToString()
                }
            },
            Name = new List<HumanName>
            {
                new HumanName
                {
                    Family = patient.LastName,
                    Given = new[] { patient.FirstName }
                }
            },
            Gender = patient.Gender switch
            {
                "Male" => AdministrativeGender.Male,
                "Female" => AdministrativeGender.Female,
                _ => AdministrativeGender.Unknown
            },
            BirthDate = patient.DateOfBirth?.ToString("yyyy-MM-dd"),
            Active = !patient.IsDeleted
        };
    }
}
```

- [ ] **Step 2: Create EncounterFhirAdapter**

```csharp
// src/Services/FhirGateway/FhirGateway.Application/Adapters/EncounterFhirAdapter.cs
using Hl7.Fhir.Model;
using Encounter = His.Hope.ClinicalService.Domain.Entities.Encounter;

namespace His.Hope.FhirGateway.Application.Adapters;

public static class EncounterFhirAdapter
{
    public static Hl7.Fhir.Model.Encounter ToFhir(Encounter encounter)
    {
        return new Hl7.Fhir.Model.Encounter
        {
            Id = encounter.Id.ToString(),
            Status = encounter.Status switch
            {
                "Active" => Encounter.EncounterStatus.InProgress,
                "Completed" => Encounter.EncounterStatus.Finished,
                _ => Encounter.EncounterStatus.Unknown
            },
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP", "inpatient encounter"),
            Subject = new ResourceReference($"Patient/{encounter.PatientId}"),
            Period = new Period
            {
                Start = encounter.StartTime?.ToString("O"),
                End = encounter.EndTime?.ToString("O")
            }
        };
    }
}
```

- [ ] **Step 3: Create FhirController**

```csharp
// src/Services/FhirGateway/FhirGateway.Api/Controllers/FhirController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace His.Hope.FhirGateway.Api.Controllers;

[ApiController]
[Route("fhir/r4")]
[Authorize]
public class FhirController : ControllerBase
{
    [HttpGet("Patient/{id}")]
    public async Task<IActionResult> GetPatient(string id)
    {
        // gRPC call to PatientService, then adapt to FHIR
        return Ok(new { resourceType = "Patient" });
    }

    [HttpGet("Patient")]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string? name,
        [FromQuery] string? identifier,
        [FromQuery] string? birthdate)
    {
        return Ok(new { resourceType = "Bundle", type = "searchset" });
    }

    [HttpGet("metadata")]
    public IActionResult CapabilityStatement()
    {
        return Ok(new
        {
            resourceType = "CapabilityStatement",
            status = "active",
            date = DateTime.UtcNow.ToString("O"),
            fhirVersion = "4.0.1",
            rest = new[]
            {
                new { mode = "server", resource = new[]
                {
                    new { type = "Patient" },
                    new { type = "Encounter" }
                }}
            }
        });
    }
}
```

- [ ] **Step 4: Create FHIR API reference doc**

```markdown
# docs/api/fhir-api-reference.md
# FHIR R4 API Reference

## Base URL: /fhir/r4
## Authentication: SMART on FHIR (OAuth2 Bearer token)
## FHIR Version: 4.0.1

### Resources Supported
- Patient (read, search)
- Encounter (read, search)
- Observation (read, search)
- Condition (read)
- MedicationRequest (read)

### Search Examples
- GET /fhir/r4/Patient?name=Smith
- GET /fhir/r4/Patient?identifier=urn:his-hope:patient-id|123
- GET /fhir/r4/Encounter?patient=Patient/123
```

- [ ] **Step 5: Commit**

```bash
git add src/Services/FhirGateway/ docs/api/fhir-api-reference.md
git commit -m "feat(fhir): implement Patient and Encounter FHIR adapters with search endpoints"
```

---

### Task 3: Distributed Locking — RedLock Implementation

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/ILockManager.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/RedisLockManager.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/DistributedLockAttribute.cs`

**Interfaces:**
- Produces: `ILockManager.AcquireAsync(key, ttl=30s)` → `IDistributedLock`
- Produces: `IDistributedLock` with `ReleaseAsync()`, `ExtendAsync()`, `FencingToken`
- Produces: `[DistributedLock("invoice:{tenantId}")]` for MediatR pipeline

- [ ] **Step 1: Create ILockManager interface**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/ILockManager.cs
namespace His.Hope.Infrastructure.Locking;

public interface ILockManager
{
    Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default);
}

public interface IDistributedLock : IAsyncDisposable
{
    string Key { get; }
    long FencingToken { get; }
    Task ReleaseAsync(CancellationToken ct = default);
    Task<bool> ExtendAsync(TimeSpan ttl, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create RedisLockManager**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/RedisLockManager.cs
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Locking;

public class RedisLockManager : ILockManager
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLockManager> _logger;
    private static long _counter;

    public RedisLockManager(IConnectionMultiplexer redis, ILogger<RedisLockManager> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var lockTtl = ttl ?? TimeSpan.FromSeconds(30);
        var token = Interlocked.Increment(ref _counter);
        var lockKey = $"lock:{key}";

        var acquired = await db.StringSetAsync(lockKey, token.ToString(), lockTtl, When.NotExists);
        
        if (!acquired)
        {
            _logger.LogWarning("Failed to acquire lock: {Key}", key);
            return null;
        }

        _logger.LogDebug("Lock acquired: {Key}, Token: {Token}, TTL: {Ttl}s", key, token, lockTtl.TotalSeconds);
        return new RedisDistributedLock(key, token, db, lockKey, _logger);
    }

    private class RedisDistributedLock : IDistributedLock
    {
        private readonly IDatabase _db;
        private readonly string _lockKey;
        private readonly ILogger<RedisLockManager> _logger;
        
        public string Key { get; }
        public long FencingToken { get; }

        public RedisDistributedLock(string key, long token, IDatabase db, string lockKey, ILogger<RedisLockManager> logger)
        {
            Key = key;
            FencingToken = token;
            _db = db;
            _lockKey = lockKey;
            _logger = logger;
        }

        public async Task ReleaseAsync(CancellationToken ct = default)
        {
            // Lua script for atomic check-and-release (prevent releasing someone else's lock)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";
            
            var result = await _db.ScriptEvaluateAsync(script, new RedisKey[] { _lockKey }, new RedisValue[] { FencingToken.ToString() });
            _logger.LogDebug("Lock released: {Key}, Token: {Token}, Deleted: {Deleted}", Key, FencingToken, (long)result > 0);
        }

        public async Task<bool> ExtendAsync(TimeSpan ttl, CancellationToken ct = default)
        {
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('expire', KEYS[1], tonumber(ARGV[2]))
                else
                    return 0
                end";
            
            var result = await _db.ScriptEvaluateAsync(script, new RedisKey[] { _lockKey }, 
                new RedisValue[] { FencingToken.ToString(), ((int)ttl.TotalSeconds).ToString() });
            return (long)result > 0;
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }
}
```

- [ ] **Step 3: Create DistributedLockAttribute for MediatR**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/DistributedLockAttribute.cs
namespace His.Hope.Infrastructure.Locking;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class DistributedLockAttribute : Attribute
{
    public string KeyTemplate { get; }
    public int TimeoutSeconds { get; set; } = 30;

    public DistributedLockAttribute(string keyTemplate)
    {
        KeyTemplate = keyTemplate;
    }

    public string ResolveKey(object request)
    {
        var type = request.GetType();
        var result = KeyTemplate;
        foreach (var prop in type.GetProperties())
        {
            result = result.Replace($"{{{prop.Name}}}", prop.GetValue(request)?.ToString() ?? "");
        }
        return result;
    }
}
```

- [ ] **Step 4: Register in DI**

```csharp
// Add to DependencyInjection.cs:
builder.Services.AddSingleton<ILockManager, RedisLockManager>();
```

- [ ] **Step 5: Commit**

```bash
git add src/Shared/Infrastructure/His.Hope.Infrastructure/Locking/
git commit -m "feat(locking): implement Redis-backed distributed locking with RedLock algorithm"
```

---

### Task 4: Multi-Level Caching — L1 + Stampede Prevention

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/MemoryCacheService.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/HybridCacheService.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/CacheWarmupService.cs`

**Interfaces:**
- Produces: `IMemoryCacheService` — L1 in-memory cache (IMemoryCache wrapper)
- Produces: `IHybridCacheService` — L1+L2 orchestration with stampede prevention
- Produces: `CacheWarmupService` — pre-load reference data on startup
- Consumes: `ICacheService` (existing L2), `IMemoryCache`

- [ ] **Step 1: Create MemoryCacheService (L1)**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/MemoryCacheService.cs
using Microsoft.Extensions.Caching.Memory;

namespace His.Hope.Infrastructure.Caching;

public class MemoryCacheService
{
    private readonly IMemoryCache _cache;
    private static readonly MemoryCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2),
        Size = 1 // each entry counts as 1, max 500 items
    };

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return Task.FromResult(cached);

        return SetAsync(key, factory, ttl ?? TimeSpan.FromMinutes(5));
    }

    private async Task<T?> SetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
    {
        var value = await factory();
        if (value is not null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1
            };
            _cache.Set(key, value, options);
        }
        return value;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Create HybridCacheService (L1+L2 with stampede prevention)**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/HybridCacheService.cs
namespace His.Hope.Infrastructure.Caching;

public class HybridCacheService
{
    private readonly MemoryCacheService _l1;
    private readonly ICacheService _l2;
    private static readonly Random _random = new();

    public HybridCacheService(MemoryCacheService l1, ICacheService l2)
    {
        _l1 = l1;
        _l2 = l2;
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, 
        TimeSpan softTtl, TimeSpan hardTtl, CancellationToken ct = default)
    {
        // Try L1 first
        var l1Result = await _l1.GetOrSetAsync(key, async () => 
        {
            // L1 miss — try L2
            var l2Result = await _l2.GetOrSetAsync(key, async () => 
            {
                return await factory();
            }, hardTtl, ct);

            return l2Result;
        }, softTtl);

        // Stampede prevention: probabilistic early recomputation
        if (l1Result is not null && ShouldRefreshEarly(softTtl))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var fresh = await factory();
                    await _l2.SetAsync(key, JsonConvert.SerializeObject(fresh), hardTtl, ct);
                    await _l1.RemoveAsync(key); // force L1 reload on next request
                }
                catch { /* background refresh failure is non-critical */ }
            }, ct);
        }

        return l1Result;
    }

    private bool ShouldRefreshEarly(TimeSpan ttl)
    {
        var beta = 1.0; // tunable — higher = more aggressive refresh
        var delta = ttl.TotalSeconds * 0.25; // refresh in last 25% of TTL
        return _random.NextDouble() < beta * delta / ttl.TotalSeconds;
    }
}
```

- [ ] **Step 3: Create CacheWarmupService**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/CacheWarmupService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Caching;

public class CacheWarmupService : IHostedService
{
    private readonly MemoryCacheService _l1;
    private readonly ILogger<CacheWarmupService> _logger;

    public CacheWarmupService(MemoryCacheService l1, ILogger<CacheWarmupService> logger)
    {
        _l1 = l1;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Cache warmup started");
        
        var warmupTasks = new List<Task>
        {
            _l1.GetOrSetAsync("reference:icd10-codes", () => Task.FromResult("warm")),
            _l1.GetOrSetAsync("reference:cpt-codes", () => Task.FromResult("warm")),
            _l1.GetOrSetAsync("reference:roles", () => Task.FromResult("warm")),
        };

        await Task.WhenAll(warmupTasks);
        _logger.LogInformation("Cache warmup completed");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

- [ ] **Step 4: Register in DI**

```csharp
// In Program.cs / DependencyInjection.cs:
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MemoryCacheService>();
builder.Services.AddSingleton<HybridCacheService>();
builder.Services.AddHostedService<CacheWarmupService>();
```

- [ ] **Step 5: Commit**

```bash
git add src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/
git commit -m "feat(caching): implement multi-level cache with L1 in-memory and stampede prevention"
```

---

### Task 5: Data Lifecycle — Soft-Delete + Archival Jobs

**Files:**
- Create: `cockroach/migrations/019-soft-delete-columns.sql`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/DataLifecycle/ISoftDeletable.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/DataLifecycle/SoftDeleteInterceptor.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/DataLifecycle/DataRetentionService.cs`
- Create: `k8s/jobs/outbox-cleanup-cronjob.yaml`
- Create: `k8s/jobs/audit-archive-cronjob.yaml`

**Interfaces:**
- Produces: `ISoftDeletable` interface with `DeletedAt`, `DeletedBy`
- Produces: `SoftDeleteInterceptor` — EF Core SaveChanges interceptor for auto soft-delete
- Produces: `DataRetentionService` — background service for archival/cleanup

- [ ] **Step 1: Create soft-delete migration**

```sql
-- cockroach/migrations/019-soft-delete-columns.sql
ALTER TABLE patientdb.patients ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE patientdb.patients ADD COLUMN IF NOT EXISTS deleted_by UUID;
CREATE INDEX IF NOT EXISTS idx_patients_deleted ON patientdb.patients(deleted_at) WHERE deleted_at IS NULL;

ALTER TABLE clinicaldb.encounters ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE clinicaldb.encounters ADD COLUMN IF NOT EXISTS deleted_by UUID;
CREATE INDEX IF NOT EXISTS idx_encounters_deleted ON clinicaldb.encounters(deleted_at) WHERE deleted_at IS NULL;

ALTER TABLE appointmentdb.appointments ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE appointmentdb.appointments ADD COLUMN IF NOT EXISTS deleted_by UUID;
CREATE INDEX IF NOT EXISTS idx_appointments_deleted ON appointmentdb.appointments(deleted_at) WHERE deleted_at IS NULL;

-- EF Core will automatically add WHERE deleted_at IS NULL filter
```

- [ ] **Step 2: Create ISoftDeletable interface**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/DataLifecycle/ISoftDeletable.cs
namespace His.Hope.Infrastructure.DataLifecycle;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
    bool IsDeleted => DeletedAt.HasValue;
}
```

- [ ] **Step 3: Create SoftDeleteInterceptor**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/DataLifecycle/SoftDeleteInterceptor.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace His.Hope.Infrastructure.DataLifecycle;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is null) return result;
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, 
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is null) return new ValueTask<InterceptionResult<int>>(result);
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private static void ApplySoftDelete(DbContext context)
    {
        var entries = context.ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted);

        foreach (var entry in entries)
        {
            entry.State = EntityState.Modified;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            // DeletedBy set by caller or via HttpContext
        }
    }
}
```

- [ ] **Step 4: Create outbox cleanup CronJob**

```yaml
# k8s/jobs/outbox-cleanup-cronjob.yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: outbox-cleanup
  namespace: his-hope
spec:
  schedule: "0 3 * * *"  # Daily at 3 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: cleanup
              image: cockroachdb/cockroach:latest
              command: ["/bin/bash", "-c"]
              args:
                - |
                  cockroach sql --host=cockroachdb-public --insecure -e "
                    DELETE FROM patientdb.outbox_messages WHERE status IN ('Completed','Skipped') AND occurred_on < now() - INTERVAL '30 days';
                    DELETE FROM clinicaldb.outbox_messages WHERE status IN ('Completed','Skipped') AND occurred_on < now() - INTERVAL '30 days';
                  "
          restartPolicy: OnFailure
```

- [ ] **Step 5: Commit**

```bash
git add cockroach/migrations/019-soft-delete-columns.sql
git add src/Shared/Infrastructure/His.Hope.Infrastructure/DataLifecycle/
git add k8s/jobs/outbox-cleanup-cronjob.yaml k8s/jobs/audit-archive-cronjob.yaml
git commit -m "feat(data): implement soft-delete pattern and data lifecycle archival jobs"
```

---

### Task 6: Secret Rotation Automation

**Files:**
- Create: `k8s/infrastructure/secret-rotation-cronjobs.yaml`

**Interfaces:**
- Produces: 3 CronJobs: `rotate-db-passwords` (90d), `rotate-jwt-keys` (90d), `rotate-rabbitmq` (90d)

- [ ] **Step 1: Create secret rotation CronJobs**

```yaml
# k8s/infrastructure/secret-rotation-cronjobs.yaml
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rotate-db-passwords
  namespace: his-hope
spec:
  schedule: "0 2 1 */3 *"  # Every 90 days
  jobTemplate:
    spec:
      template:
        spec:
          serviceAccountName: vault-rotator
          containers:
            - name: rotate
              image: vault:1.16
              command: ["/bin/sh", "-c"]
              args:
                - |
                  # 1. Generate new password
                  NEW_PW=$(vault write -field=password sys/policies/password/patient_db/generate length=32)
                  # 2. Update in CockroachDB
                  cockroach sql -e "ALTER USER patient_service WITH PASSWORD '$NEW_PW'"
                  # 3. Update in Vault
                  vault kv put secret/postgres/patient-service password="$NEW_PW"
                  # 4. Trigger rolling restart
                  kubectl rollout restart deployment/patient-service -n his-hope
          restartPolicy: OnFailure
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rotate-jwt-keys
  namespace: his-hope
spec:
  schedule: "0 3 1 */3 *"
  jobTemplate:
    spec:
      template:
        spec:
          serviceAccountName: vault-rotator
          containers:
            - name: rotate
              image: vault:1.16
              command: ["/bin/sh", "-c"]
              args:
                - |
                  vault write -f transit/keys/jwt-signing/rotate
                  kubectl rollout restart deployment/identity-service -n his-hope
          restartPolicy: OnFailure
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rotate-rabbitmq-credentials
  namespace: his-hope
spec:
  schedule: "0 4 1 */3 *"
  jobTemplate:
    spec:
      template:
        spec:
          serviceAccountName: vault-rotator
          containers:
            - name: rotate
              image: vault:1.16
              command: ["/bin/sh", "-c"]
              args:
                - |
                  NEW_PW=$(vault write -field=password sys/policies/password/rabbitmq/generate length=32)
                  rabbitmqctl change_password his_hope_app "$NEW_PW"
                  vault kv put secret/rabbitmq/credentials password="$NEW_PW"
                  kubectl rollout restart deployment -n his-hope -l app.kubernetes.io/part-of=his-hope
          restartPolicy: OnFailure
```

- [ ] **Step 2: Commit**

```bash
git add k8s/infrastructure/secret-rotation-cronjobs.yaml
git commit -m "feat(security): implement automated 90-day secret rotation for DB, JWT, and RabbitMQ"
```

---

### Task 7: API Abuse Prevention — Per-User Rate Limiting

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/PerUserRateLimitingMiddleware.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/BruteForceProtectionService.cs`
- Create: `cockroach/migrations/020-login-attempts.sql`

**Interfaces:**
- Produces: `PerUserRateLimitingMiddleware` — per-user sliding window rate limiter via Redis
- Produces: `BruteForceProtectionService` — progressive delay, account lockout, IP tracking

- [ ] **Step 1: Create migration**

```sql
-- cockroach/migrations/020-login-attempts.sql
CREATE TABLE IF NOT EXISTS login_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES identitydb.users(id),
    ip_address VARCHAR(45) NOT NULL,
    attempted_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_successful BOOLEAN NOT NULL DEFAULT false,
    INDEX idx_login_attempts_user_time (user_id, attempted_at DESC),
    INDEX idx_login_attempts_ip (ip_address, attempted_at DESC)
);
```

- [ ] **Step 2: Create BruteForceProtectionService**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/BruteForceProtectionService.cs
namespace His.Hope.Infrastructure.Abuse;

public class BruteForceProtectionService
{
    private readonly IConnectionMultiplexer _redis;

    public BruteForceProtectionService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> IsAccountLockedAsync(string username)
    {
        var db = _redis.GetDatabase();
        var key = $"bruteforce:account:{username}";
        return await db.KeyExistsAsync(key);
    }

    public async Task<int> GetFailedAttemptsAsync(string username)
    {
        var db = _redis.GetDatabase();
        var key = $"bruteforce:attempts:{username}";
        var val = await db.StringGetAsync(key);
        return val.TryParse(out int count) ? count : 0;
    }

    public async Task RecordFailedAttemptAsync(string username, string ip)
    {
        var db = _redis.GetDatabase();
        var attemptsKey = $"bruteforce:attempts:{username}";
        var count = await db.StringIncrementAsync(attemptsKey);
        await db.KeyExpireAsync(attemptsKey, TimeSpan.FromMinutes(5));

        if (count >= 10)
        {
            await db.StringSetAsync($"bruteforce:account:{username}", "locked", TimeSpan.FromMinutes(15));
            await db.KeyDeleteAsync(attemptsKey);
        }
    }

    public async Task RecordSuccessAsync(string username)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"bruteforce:attempts:{username}");
    }

    public TimeSpan GetProgressiveDelay(int attempts)
    {
        return attempts switch
        {
            <= 1 => TimeSpan.Zero,
            2 => TimeSpan.FromSeconds(1),
            3 => TimeSpan.FromSeconds(2),
            4 => TimeSpan.FromSeconds(4),
            5 => TimeSpan.FromSeconds(8),
            _ => TimeSpan.FromSeconds(15)
        };
    }
}
```

- [ ] **Step 3: Create PerUserRateLimitingMiddleware**

```csharp
// src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/PerUserRateLimitingMiddleware.cs
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Abuse;

public class PerUserRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;

    public PerUserRateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (userId is null || context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var db = _redis.GetDatabase();
        var key = $"ratelimit:user:{userId}";
        var window = TimeSpan.FromMinutes(1);
        var maxRequests = 200;

        var count = await db.SortedSetLengthAsync(key);
        if (count >= maxRequests)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers.Append("Retry-After", "60");
            await context.Response.WriteAsync("{\"title\":\"Too Many Requests\"}");
            return;
        }

        // Add current timestamp to sorted set
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SortedSetAddAsync(key, now, now);
        await db.KeyExpireAsync(key, window);

        // Clean old entries
        await db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now - window.TotalSeconds);

        await _next(context);
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/
git add cockroach/migrations/020-login-attempts.sql
git commit -m "feat(security): implement per-user rate limiting and brute force protection"
```

---

### Task 8: Synthetic Monitoring — Blackbox + User Journeys

**Files:**
- Create: `k8s/monitoring/blackbox-exporter-config.yaml`
- Create: `k8s/jobs/synthetic-monitor-cronjob.yaml`
- Create: `tests/synthetic/login-journey.spec.ts`
- Create: `tests/synthetic/patient-journey.spec.ts`

**Interfaces:**
- Produces: Blackbox Exporter probes for all 7 services + API Gateway
- Produces: Playwright-based synthetic user journey tests running every 5 minutes

- [ ] **Step 1: Create Blackbox Exporter config**

```yaml
# k8s/monitoring/blackbox-exporter-config.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: blackbox-exporter-config
  namespace: monitoring
data:
  blackbox.yml: |
    modules:
      http_2xx:
        prober: http
        timeout: 5s
        http:
          valid_status_codes: [200, 201]
          method: GET
          headers:
            Authorization: "Bearer ${MONITOR_TOKEN}"
      tcp_connect:
        prober: tcp
        timeout: 5s
```

Create the Prometheus ServiceMonitor or additional scrape config for Blackbox Exporter that probes:
- `http://api-gateway:5000/health`
- `http://patient-service:5002/health`
- `http://identity-service:5001/health`
- `http://clinical-service:5005/health`
- `tcp://patient-service:5006` (gRPC)
- `tcp://rabbitmq:5672`

- [ ] **Step 2: Create Playwright synthetic journey**

```typescript
// tests/synthetic/login-journey.spec.ts
import { test, expect } from '@playwright/test';

const BASE_URL = process.env.BASE_URL || 'http://localhost:5000';

test('Login → Search Patient → Logout', async ({ page }) => {
  await page.goto(`${BASE_URL}/login`);
  await page.fill('input[name="username"]', 'synthetic.user@his-hope.com');
  await page.fill('input[name="password"]', process.env.SYNTHETIC_PASSWORD || '');
  await page.click('button[type="submit"]');
  
  await page.waitForURL('**/dashboard');
  await expect(page.locator('text=Dashboard')).toBeVisible();
  
  // Search for a patient
  await page.fill('input[name="search"]', 'Smith');
  await page.click('button[aria-label="Search"]');
  await expect(page.locator('table tbody tr')).not.toHaveCount(0);
  
  // Logout
  await page.click('button[aria-label="Logout"]');
  await expect(page).toHaveURL(/\/login/);
});
```

- [ ] **Step 3: Create K8s CronJob for synthetic monitoring**

```yaml
# k8s/jobs/synthetic-monitor-cronjob.yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: synthetic-monitor
  namespace: monitoring
spec:
  schedule: "*/5 * * * *"  # Every 5 minutes
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: playwright
              image: mcr.microsoft.com/playwright:v1.44.0-focal
              command: ["npx", "playwright", "test"]
              env:
                - name: BASE_URL
                  value: "http://api-gateway.his-hope.svc.cluster.local:5000"
          restartPolicy: OnFailure
```

- [ ] **Step 4: Commit**

```bash
git add k8s/monitoring/blackbox-exporter-config.yaml
git add k8s/jobs/synthetic-monitor-cronjob.yaml
git add tests/synthetic/
git commit -m "feat(observability): deploy Blackbox Exporter and Playwright synthetic monitoring"
```

---

### Task 9: Statistical Anomaly Detection Rules

**Files:**
- Create: `k8s/monitoring/anomaly-rules.yaml`

**Interfaces:**
- Produces: Prometheus recording rules for seasonal decomposition and dynamic thresholds

- [ ] **Step 1: Create anomaly detection rules**

```yaml
# k8s/monitoring/anomaly-rules.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: anomaly-rules
  namespace: monitoring
data:
  anomaly.rules: |
    groups:
      - name: anomaly_detection
        rules:
          # Rolling average for baseline (1h window)
          - record: anomaly:request_rate_1h_avg
            expr: avg_over_time(rate(http_requests_total[1m])[1h:1m])
          
          # Rolling standard deviation
          - record: anomaly:request_rate_1h_stddev
            expr: stddev_over_time(rate(http_requests_total[1m])[1h:1m])
          
          # Upper bound: mean + 3 sigma
          - record: anomaly:request_rate_upper
            expr: anomaly:request_rate_1h_avg + 3 * anomaly:request_rate_1h_stddev
          
          # Anomaly score: how many sigmas above mean
          - record: anomaly:request_rate_score
            expr: |
              (rate(http_requests_total[1m]) - anomaly:request_rate_1h_avg)
              /
              (anomaly:request_rate_1h_stddev > 0 or 1)

      - name: anomaly_alerts
        rules:
          - alert: RequestRateAnomalyHigh
            expr: anomaly:request_rate_score > 3
            for: 5m
            labels:
              severity: warning
            annotations:
              summary: "Anomalous request rate on {{ $labels.service }}"
              description: "Request rate is {{ $value }} standard deviations above baseline"
          
          - alert: RequestRateAnomalyCritical
            expr: anomaly:request_rate_score > 5
            for: 3m
            labels:
              severity: critical
            annotations:
              summary: "Critical anomaly on {{ $labels.service }}"
```

- [ ] **Step 2: Commit**

```bash
git add k8s/monitoring/anomaly-rules.yaml
git commit -m "feat(observability): add statistical anomaly detection with seasonal baseline"
```

---

### Task 10: Runbooks — Top 10 Missing Scenarios

**Files:**
- Create: `docs/runbooks/crdb-failure.md`
- Create: `docs/runbooks/redis-failure.md`
- Create: `docs/runbooks/rabbitmq-failure.md`
- Create: `docs/runbooks/service-unavailable.md`
- Create: `docs/runbooks/high-latency.md`
- Create: `docs/runbooks/oom-kill.md`
- Create: `docs/runbooks/token-theft.md`
- Create: `docs/runbooks/brute-force.md`
- Create: `docs/runbooks/phi-exfiltration.md`
- Create: `docs/runbooks/failed-deployment-rollback.md`

**Interfaces:**
- Produces: 10 standardized runbooks following the template: Symptoms → Diagnosis → Mitigation → Resolution → Verification

- [ ] **Step 1: Create runbook template header in each file**

```markdown
# [Scenario] Runbook

| Field | Value |
|-------|-------|
| **Severity** | P1 (Critical) |
| **Service** | [service-name] |
| **Owner** | SRE Team |
| **Last Updated** | 2026-07-17 |

## Alert Trigger
[Prometheus alert name]

## Symptoms
- [Symptom 1]
- [Symptom 2]

## Diagnosis
```bash
# Diagnostic commands
```

## Mitigation
1. [Immediate action]
2. [Secondary action]

## Resolution
1. [Root cause fix]
2. [Verification steps]

## Postmortem
Link to postmortem template.
```

**Create the 10 runbooks with specific content for each scenario:**

| # | File | Key Diagnosis Command |
|---|------|----------------------|
| 1 | `crdb-failure.md` | `cockroach node status`, `SHOW RANGES` |
| 2 | `redis-failure.md` | `redis-cli CLUSTER INFO`, `redis-cli INFO memory` |
| 3 | `rabbitmq-failure.md` | `rabbitmqctl list_queues`, `rabbitmqctl list_connections` |
| 4 | `service-unavailable.md` | `kubectl describe pod`, `kubectl logs --tail=100` |
| 5 | `high-latency.md` | Jaeger trace search, `EXPLAIN ANALYZE` slow queries |
| 6 | `oom-kill.md` | `kubectl describe pod \| grep OOM`, memory profile |
| 7 | `token-theft.md` | `redis-cli KEYS blacklist:*`, audit log query |
| 8 | `brute-force.md` | `redis-cli KEYS bruteforce:*`, login_attempts query |
| 9 | `phi-exfiltration.md` | `SELECT * FROM audit_log WHERE action='READ' AND resource LIKE '%patient%'` |
| 10 | `failed-deployment-rollback.md` | `kubectl rollout undo deployment/`, `argocd app rollback` |

- [ ] **Step 2: Commit**

```bash
git add docs/runbooks/
git commit -m "docs(runbooks): add 10 standardized operational runbooks for common failure scenarios"
```

---

## Plan Verification Checklist

- [ ] FHIR Gateway service scaffolded with Clean Architecture
- [ ] Patient and Encounter FHIR adapters functional
- [ ] RedLock distributed locking with atomic Lua scripts
- [ ] L1 in-memory + L2 Redis with stampede prevention
- [ ] Soft-delete interceptor working on all PHI entities
- [ ] Outbox cleanup CronJob deleting 30-day-old messages
- [ ] Secret rotation CronJobs for DB, JWT, RabbitMQ
- [ ] Per-user rate limiting returning 429 with Retry-After
- [ ] Brute force account lockout after 10 failures
- [ ] Blackbox Exporter probing all services
- [ ] Playwright synthetic journeys passing
- [ ] Anomaly detection rules generating alerts
- [ ] 10 standardized runbooks documented
