# Dashboard Phase 1: Performance + Resilience — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce dashboard load time from ~3s to <500ms via parallel queries, memory caching, and graceful degradation.

**Architecture:** Add `IMemoryCache` layer above all four aggregators. Fan out Consul+Prometheus calls via `Task.WhenAll`. Replace in-memory dedup with timestamp cursor in LogStreamBackgroundService. Add `QueryAsync` (instant query) to PrometheusQueryService.

**Tech Stack:** .NET 8, `IMemoryCache`, Polly resilience (already wired), HttpClient with DelegatingHandler

## Global Constraints

- Target framework: .NET 8
- All existing HttpClient resilience (CircuitBreaker + Retry) must remain intact
- No breaking changes to Angular frontend — nullable metrics serialized as `null` or omitted
- Backward-compatible: existing `QueryLogsAsync` callers without `afterTimestamp` work unchanged
- All aggregators return partial data on downstream failure — never 500

---

## Task Dependency Graph

```
Task 1 (CacheKeys + CacheExtensions) ──┐
Task 2 (Model: nullable) ──────────────┤
Task 3 (Prometheus QueryAsync) ────────┤
Task 4 (ES afterTimestamp) ────────────┤
                                        ├── All no deps ── can run in parallel
                                        │
Task 5 (ResourceAggregator) ───────────┼── depends on Task 1, 2, 3
Task 6 (MetricsAggregator) ────────────┼── depends on Task 1
Task 7 (LogsAggregator) ───────────────┼── depends on Task 1
Task 8 (TracesAggregator) ─────────────┼── depends on Task 1
Task 9 (LogStreamBackgroundService) ───┼── depends on Task 4
Task 10 (Program.cs) ──────────────────┼── depends on Task 1 (AddMemoryCache)
Task 11 (Integration tests) ───────────┼── depends on Task 5, 6

BATCH A: Task 1, 2, 3, 4 — parallel (no cross-deps)
BATCH B: Task 5, 6, 7, 8, 9, 10 — parallel (deps on BATCH A only)
BATCH C: Task 11 — after all above pass
```

---

### Task 1: Cache Infrastructure (CacheKeys + CacheExtensions)

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/CacheKeys.cs`
- Create: `src/Bff/SystemDashboard.Bff/Aggregators/CacheExtensions.cs`

**Interfaces:**
- Produces: `CacheKeys` static class with `AllResources`, `ResourceByName(string)`, `Metrics(string,string,string)`, `Logs(string?,string?,int,string?)`, `TraceSearch(string)`, `TraceDetail(string)`; `CacheExtensions.GetOrCreateAsync<T>(IMemoryCache, string, Func<Task<T>>, TimeSpan)`

- [ ] **Step 1: Create CacheKeys.cs**

```csharp
// File: src/Bff/SystemDashboard.Bff/Aggregators/CacheKeys.cs
namespace SystemDashboard.Bff.Aggregators;

public static class CacheKeys
{
    public const string AllResources = "resources:all";
    public static string ResourceByName(string name) => $"resources:{name}";
    public static string Metrics(string service, string metrics, string range) =>
        $"metrics:{service}:{metrics}:{range}";
    public static string Logs(string? service, string? level, int size, string? searchQuery) =>
        $"logs:{service ?? "*"}:{level ?? "*"}:{size}:{searchQuery ?? "*"}";
    public static string TraceSearch(string service) => $"traces:search:{service}";
    public static string TraceDetail(string traceId) => $"traces:{traceId}";
}
```

- [ ] **Step 2: Create CacheExtensions.cs**

```csharp
// File: src/Bff/SystemDashboard.Bff/Aggregators/CacheExtensions.cs
using Microsoft.Extensions.Caching.Memory;

namespace SystemDashboard.Bff.Aggregators;

public static class CacheExtensions
{
    public static async Task<T?> GetOrCreateAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T>> factory,
        TimeSpan absoluteExpiration)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var result = await factory();
        if (result is not null)
            cache.Set(key, result, absoluteExpiration);
        return result;
    }
}
```

- [ ] **Step 3: Build and verify compilation**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Aggregators/CacheKeys.cs src/Bff/SystemDashboard.Bff/Aggregators/CacheExtensions.cs
git commit -m "feat(dashboard): add cache infrastructure (CacheKeys + CacheExtensions)"
```

---

### Task 2: Model Change — Nullable Metrics

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Models/Resource.cs:33-34`

**Interfaces:**
- Consumes: (none)
- Produces: `ServiceResource.CpuPercent` → `double?`, `ServiceResource.MemoryUsedMb` → `double?`

- [ ] **Step 1: Change CpuPercent and MemoryUsedMb to nullable**

Open `src/Bff/SystemDashboard.Bff/Models/Resource.cs`, find the `ServiceResource` record (lines 27-38). Change lines 33-34:

```csharp
// BEFORE:
    public double CpuPercent { get; init; }
    public double MemoryUsedMb { get; init; }

// AFTER:
    public double? CpuPercent { get; init; }
    public double? MemoryUsedMb { get; init; }
```

- [ ] **Step 2: Fix compile errors in ResourceAggregator.cs (nullability)**

ResourceAggregator currently assigns `double` values to now-`double?` properties. This is fine — C# implicitly converts `double` to `double?`. No code changes needed in ResourceAggregator for this step (the actual aggregator refactor is Task 5).

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded, no nullability warnings (or only pre-existing ones).

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Models/Resource.cs
git commit -m "feat(dashboard): make CpuPercent and MemoryUsedMb nullable for graceful degradation"
```

---

### Task 3: Prometheus Instant Query (QueryAsync)

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Services/IPrometheusQueryService.cs:13-14`
- Modify: `src/Bff/SystemDashboard.Bff/Services/PrometheusQueryService.cs` (add method + response types)

**Interfaces:**
- Consumes: (none)
- Produces: `Task<MetricDataPoint?> QueryAsync(string query, CancellationToken ct = default)`

- [ ] **Step 1: Add QueryAsync to IPrometheusQueryService**

```csharp
// Add after line 14 in IPrometheusQueryService.cs:
    Task<MetricDataPoint?> QueryAsync(string query, CancellationToken ct = default);
```

- [ ] **Step 2: Implement QueryAsync in PrometheusQueryService**

Add the method to `PrometheusQueryService` class (before the `#region` or after `QueryRangeAsync`):

```csharp
public async Task<MetricDataPoint?> QueryAsync(
    string query, CancellationToken ct = default)
{
    try
    {
        var requestUri = $"/api/v1/query?query={Uri.EscapeDataString(query)}";

        var response = await _httpClient.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var promResponse = await response.Content.ReadFromJsonAsync<PromInstantResponse>(ct);
        var result = promResponse?.Data?.Result?.FirstOrDefault();
        if (result?.Value is null)
            return null;

        var valElement = (JsonElement)result.Value[1];
        return new MetricDataPoint
        {
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(
                ((JsonElement)result.Value[0]).GetInt64()).UtcDateTime,
            Value = double.TryParse(valElement.GetString(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0
        };
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to query Prometheus instant: {Query}", query);
        return null;
    }
}
```

Also add these private nested records inside `PrometheusQueryService` (after `PromResult` on line 93):

```csharp
private sealed record PromInstantResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("data")]
    public PromInstantData? Data { get; init; }
}

private sealed record PromInstantData
{
    [JsonPropertyName("resultType")]
    public string? ResultType { get; init; }

    [JsonPropertyName("result")]
    public PromInstantResult[]? Result { get; init; }
}

private sealed record PromInstantResult
{
    [JsonPropertyName("metric")]
    public Dictionary<string, string>? Metric { get; init; }

    [JsonPropertyName("value")]
    public List<object>? Value { get; init; }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Services/IPrometheusQueryService.cs src/Bff/SystemDashboard.Bff/Services/PrometheusQueryService.cs
git commit -m "feat(dashboard): add Prometheus instant query (QueryAsync) for single-value lookups"
```

---

### Task 4: ES Timestamp Filter (afterTimestamp)

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Services/IElasticsearchQueryService.cs:14-17`
- Modify: `src/Bff/SystemDashboard.Bff/Services/ElasticsearchQueryService.cs:23-27` (signature) and request body (add range filter)

**Interfaces:**
- Consumes: (none)
- Produces: `IElasticsearchQueryService.QueryLogsAsync` gains `DateTime? afterTimestamp = null` parameter

- [ ] **Step 1: Add afterTimestamp to interface**

```csharp
// IElasticsearchQueryService.cs — new signature:
Task<List<LogEntry>> QueryLogsAsync(
    string? service = null, string? level = null,
    int? from = null, int size = 100,
    string? searchQuery = null,
    DateTime? afterTimestamp = null,
    CancellationToken ct = default);
```

- [ ] **Step 2: Add afterTimestamp to implementation signature**

Change the method signature in `ElasticsearchQueryService.cs` (line 23-26) to match the interface.

- [ ] **Step 3: Add range filter to ES query body**

In the `QueryLogsAsync` method body, inside the `mustClauses` list building section, add after the `searchQuery` clause (after line 39):

```csharp
if (afterTimestamp.HasValue)
{
    mustClauses.Add(new
    {
        range = new Dictionary<string, object>
        {
            ["@timestamp"] = new Dictionary<string, object>
            {
                ["gte"] = afterTimestamp.Value.ToString("o")
            }
        }
    });
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded. Note: `LogStreamBackgroundService` will need updating in Task 9 to pass the new parameter.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Services/IElasticsearchQueryService.cs src/Bff/SystemDashboard.Bff/Services/ElasticsearchQueryService.cs
git commit -m "feat(dashboard): add afterTimestamp filter to Elasticsearch log queries"
```

---

### Task 5: ResourceAggregator — Parallel + Cache + Graceful Degradation

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Aggregators/ResourceAggregator.cs`

**Interfaces:**
- Consumes: `CacheKeys` (Task 1), `CacheExtensions` (Task 1), `IPrometheusQueryService.QueryAsync` (Task 3), nullable `CpuPercent`/`MemoryUsedMb` (Task 2)
- Produces: (none — existing public API unchanged)

- [ ] **Step 1: Add using and IMemoryCache dependency**

Add to top of file:
```csharp
using Microsoft.Extensions.Caching.Memory;
```

Add field and constructor parameter:
```csharp
private readonly IMemoryCache _cache;
```

In constructor signature, add `IMemoryCache cache` parameter and assign `_cache = cache;`.

- [ ] **Step 2: Replace QueryLatestMetricValueAsync to use QueryAsync**

Replace the `QueryLatestMetricValueAsync` method (lines 308-323) with a version that uses instant query:

```csharp
private async Task<double?> QueryLatestMetricValueAsync(string promql, CancellationToken ct)
{
    try
    {
        var point = await _prometheus.QueryAsync(promql, ct);
        return point?.Value;
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Failed to query Prometheus for metric: {Query}", promql);
        return null;
    }
}
```

- [ ] **Step 3: Add GetHealthSafeAsync helper**

Add new private method:

```csharp
private async Task<ConsulServiceHealth?> GetHealthSafeAsync(
    string serviceName, CancellationToken ct)
{
    try
    {
        return await _consul.GetServiceHealthAsync(serviceName, ct);
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Consul health check failed for {Service}", serviceName);
        return null;
    }
}
```

- [ ] **Step 4: Refactor GetAllResourcesAsync — parallel fan-out + cache**

Replace the entire `GetAllResourcesAsync` method body with:

```csharp
public async Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default)
{
    return await _cache.GetOrCreateAsync(CacheKeys.AllResources, async () =>
    {
        // Fetch Consul services (eager — needed for health lookup)
        List<string> consulServices;
        try
        {
            consulServices = await _consul.GetServiceNamesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get service names from Consul");
            consulServices = [];
        }

        // Phase 1: Launch all queries simultaneously
        var healthTasks = new Dictionary<string, Task<ConsulServiceHealth?>>();
        var cpuTasks = new Dictionary<string, Task<double?>>();
        var memoryTasks = new Dictionary<string, Task<double?>>();

        foreach (var (name, _) in _serviceMap)
        {
            healthTasks[name] = GetHealthSafeAsync(name, ct);
            if (ServiceToJobMap.TryGetValue(name, out var job))
            {
                cpuTasks[name] = QueryLatestMetricValueAsync(
                    CpuPromqlTemplate.Replace("{job}", job), ct);
                memoryTasks[name] = QueryLatestMetricValueAsync(
                    MemoryPromqlTemplate.Replace("{job}", job), ct);
            }
        }

        // Await all at once
        var allTasks = healthTasks.Values
            .Concat<object>(cpuTasks.Values)
            .Concat<object>(memoryTasks.Values)
            .Cast<Task>();
        await Task.WhenAll(allTasks);

        // Phase 2: Assemble results
        var resources = new List<Resource>();
        foreach (var (name, (httpPort, grpcPort, _, databases)) in _serviceMap)
        {
            var consulHealth = healthTasks.TryGetValue(name, out var hTask)
                ? (hTask.IsCompletedSuccessfully ? hTask.Result : null)
                : null;

            var (stateStr, healthStr, checks) = consulHealth is not null
                ? MapFromConsul(consulHealth)
                : await CheckDirectHealthAsync(name, httpPort, ct);

            double? cpuPercent = cpuTasks.TryGetValue(name, out var cTask)
                && cTask.IsCompletedSuccessfully ? cTask.Result : null;
            double? memoryMb = memoryTasks.TryGetValue(name, out var mTask)
                && mTask.IsCompletedSuccessfully ? mTask.Result : null;

            resources.Add(new ServiceResource
            {
                Name = name,
                DisplayName = FormatServiceName(name),
                Status = stateStr,
                HealthStatus = healthStr,
                Type = "service",
                HealthChecks = checks,
                HttpPort = httpPort,
                GrpcPort = grpcPort,
                CpuPercent = cpuPercent,
                MemoryUsedMb = memoryMb,
                Databases = databases.ToList(),
            });
        }

        resources.AddRange(_infraResources);
        resources.AddRange(_databaseResources);
        return resources;
    }, TimeSpan.FromSeconds(15));
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Aggregators/ResourceAggregator.cs
git commit -m "feat(dashboard): parallelize ResourceAggregator queries with cache and graceful degradation"
```

---

### Task 6: MetricsAggregator — Parallel + Cache

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Aggregators/MetricsAggregator.cs`
- Modify: `src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs` (add `Empty` factory)

**Interfaces:**
- Consumes: `CacheKeys` (Task 1), `CacheExtensions` (Task 1)
- Produces: `MetricSnapshot.Empty(string, string, string)` factory method

- [ ] **Step 1: Add Empty factory to MetricSnapshot**

Add to `MetricSnapshot` record in `src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs`:

```csharp
public static MetricSnapshot Empty(string name, string displayName, string unit) => new()
{
    Name = name,
    DisplayName = displayName,
    Unit = unit,
    CurrentValue = 0,
    DataPoints = []
};
```

- [ ] **Step 2: Add IMemoryCache to MetricsAggregator**

Add using and field:
```csharp
using Microsoft.Extensions.Caching.Memory;
private readonly IMemoryCache _cache;
```

Update constructor to accept `IMemoryCache cache`.

- [ ] **Step 3: Add GetMetricsTtl helper**

```csharp
private static TimeSpan GetMetricsTtl(string range) => range switch
{
    "5m" => TimeSpan.FromSeconds(10),
    "15m" => TimeSpan.FromSeconds(15),
    "1h" => TimeSpan.FromSeconds(30),
    "6h" => TimeSpan.FromSeconds(60),
    "24h" => TimeSpan.FromSeconds(120),
    _ => TimeSpan.FromSeconds(10)
};
```

- [ ] **Step 4: Refactor GetMetricsAsync — parallel + cache**

Replace the method body (lines 56-126) with:

```csharp
public async Task<List<MetricSnapshot>> GetMetricsAsync(
    string service, string[] metricNames, string range, CancellationToken ct = default)
{
    var metricsKey = string.Join(",", metricNames.OrderBy(m => m));
    var cacheKey = CacheKeys.Metrics(service, metricsKey, range);

    return await _cache.GetOrCreateAsync(cacheKey, async () =>
    {
        if (!RangeConfig.TryGetValue(range, out var rangeConfig))
        {
            _logger.LogWarning("Invalid range requested: {Range}. Using default 5m.", range);
            rangeConfig = RangeConfig["5m"];
        }

        var end = DateTime.UtcNow;
        var start = end - rangeConfig.duration;

        // Launch all metric queries in parallel
        var tasks = metricNames.Select(async metricName =>
        {
            if (!MetricPromqlTemplates.TryGetValue(metricName, out var template))
            {
                _logger.LogWarning("Unknown metric name: {MetricName}", metricName);
                return null;
            }

            var (displayName, unit) = MetricConfig.TryGetValue(metricName, out var cfg)
                ? cfg
                : (metricName, "");

            var job = ServiceToJobMap.TryGetValue(service, out var mappedJob) ? mappedJob : service;
            var promql = template.Replace("{job}", job);

            try
            {
                var dataPoints = await _prometheus.QueryRangeAsync(promql, start, end, rangeConfig.step, ct);
                var values = dataPoints.Select(dp => dp.Value).ToList();
                return new MetricSnapshot
                {
                    Name = metricName,
                    DisplayName = displayName,
                    Unit = unit,
                    CurrentValue = values.Count > 0 ? values[^1] : 0.0,
                    PreviousValue = values.Count > 1 ? (double?)values[^2] : null,
                    Min = values.Count > 0 ? (double?)values.Min() : null,
                    Max = values.Count > 0 ? (double?)values.Max() : null,
                    Avg = values.Count > 0 ? (double?)values.Average() : null,
                    DataPoints = dataPoints
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metrics query failed: {Metric} for {Service}", metricName, service);
                return MetricSnapshot.Empty(metricName, displayName, unit);
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<MetricSnapshot>().ToList();
    }, GetMetricsTtl(range));
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Aggregators/MetricsAggregator.cs src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs
git commit -m "feat(dashboard): parallelize MetricsAggregator with cache and Empty factory"
```

---

### Task 7: LogsAggregator — Cache Wrapper

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Aggregators/LogsAggregator.cs`

**Interfaces:**
- Consumes: `CacheKeys` (Task 1), `CacheExtensions` (Task 1)
- Produces: (none — existing public API unchanged)

- [ ] **Step 1: Add IMemoryCache to LogsAggregator**

Add using and field:
```csharp
using Microsoft.Extensions.Caching.Memory;
private readonly IMemoryCache _cache;
```

Update constructor to accept `IMemoryCache cache`.

- [ ] **Step 2: Wrap QueryLogsAsync with cache**

Replace `QueryLogsAsync` body with:

```csharp
public async Task<List<LogEntry>> QueryLogsAsync(
    string? service = null, string? level = null,
    int? from = null, int size = 100,
    string? searchQuery = null, CancellationToken ct = default)
{
    var cacheKey = CacheKeys.Logs(service, level, size, searchQuery);
    return await _cache.GetOrCreateAsync(cacheKey, async () =>
    {
        try
        {
            return await _esService.QueryLogsAsync(service, level, from, size, searchQuery, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogsAggregator failed to query logs for {Service}", service);
            return [];
        }
    }, TimeSpan.FromSeconds(5));
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Aggregators/LogsAggregator.cs
git commit -m "feat(dashboard): add cache wrapper to LogsAggregator (5s TTL)"
```

---

### Task 8: TracesAggregator — Cache Wrapper

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Aggregators/TracesAggregator.cs`

**Interfaces:**
- Consumes: `CacheKeys` (Task 1), `CacheExtensions` (Task 1)
- Produces: (none — existing public API unchanged)

- [ ] **Step 1: Add IMemoryCache to TracesAggregator**

Add using and field:
```csharp
using Microsoft.Extensions.Caching.Memory;
private readonly IMemoryCache _cache;
```

Update constructor to accept `IMemoryCache cache`.

- [ ] **Step 2: Wrap SearchTracesAsync with cache**

Replace `SearchTracesAsync` body:

```csharp
public async Task<List<TraceSummary>> SearchTracesAsync(
    string service, DateTime? from, DateTime? to,
    long? minDurationMs, int limit = 20, CancellationToken ct = default)
{
    var cacheKey = CacheKeys.TraceSearch(service);
    return await _cache.GetOrCreateAsync(cacheKey, async () =>
    {
        try
        {
            return await _jaeger.SearchTracesAsync(service, from, to, minDurationMs, limit, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TracesAggregator failed to search traces for {Service}", service);
            return [];
        }
    }, TimeSpan.FromSeconds(15));
}
```

- [ ] **Step 3: Wrap GetTraceAsync with cache**

Replace `GetTraceAsync` body:

```csharp
public async Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default)
{
    var cacheKey = CacheKeys.TraceDetail(traceId);
    return await _cache.GetOrCreateAsync(cacheKey, async () =>
    {
        try
        {
            return await _jaeger.GetTraceAsync(traceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TracesAggregator failed to get trace {TraceId}", traceId);
            return null;
        }
    }, TimeSpan.FromSeconds(30));
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Aggregators/TracesAggregator.cs
git commit -m "feat(dashboard): add cache wrapper to TracesAggregator (15s/30s TTL)"
```

---

### Task 9: LogStreamBackgroundService — Timestamp Cursor

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Services/LogStreamBackgroundService.cs`

**Interfaces:**
- Consumes: `IElasticsearchQueryService.QueryLogsAsync` with `afterTimestamp` (Task 4)
- Produces: (none)

- [ ] **Step 1: Remove HashSet, add timestamp field**

Remove line 11:
```csharp
private readonly HashSet<string> _pushedIds = new();
```

Add field:
```csharp
private DateTime _lastPushedTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(30);
```

- [ ] **Step 2: Replace ExecuteAsync body**

Replace the entire `ExecuteAsync` method body with:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("LogStreamBackgroundService started (cursor: {Cursor})", _lastPushedTimestamp.ToString("o"));
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var esQuery = scope.ServiceProvider.GetRequiredService<IElasticsearchQueryService>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<LogStreamHub>>();

            var logs = await esQuery.QueryLogsAsync(
                size: 50,
                afterTimestamp: _lastPushedTimestamp,
                ct: stoppingToken);

            if (logs.Count > 0)
            {
                _logger.LogDebug("Pushing {Count} new log entries via SignalR", logs.Count);
                foreach (var log in logs)
                {
                    await hubContext.Clients
                        .Group(log.Service ?? "*")
                        .SendAsync("LogEntry", log, stoppingToken);
                }

                var maxTs = logs.Max(l => l.Timestamp);
                if (maxTs > _lastPushedTimestamp)
                    _lastPushedTimestamp = maxTs;
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogStreamBackgroundService poll failed");
        }

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Services/LogStreamBackgroundService.cs
git commit -m "feat(dashboard): replace in-memory dedup with timestamp cursor in LogStreamBackgroundService"
```

---

### Task 10: Program.cs — Register MemoryCache + Resilience

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Program.cs`

**Interfaces:**
- Consumes: (none)
- Produces: `IMemoryCache` available via DI for all aggregators

- [ ] **Step 1: Add AddMemoryCache**

After line 59 (`builder.Services.AddHealthChecks();`), add:

```csharp
// Memory cache for aggregator responses
builder.Services.AddMemoryCache();
```

- [ ] **Step 2: Add AddResiliencePolicies (if not already present)**

Verify line 62 (`builder.Services.AddResiliencePolicies();`) is present. If the `using` for `His.Hope.Infrastructure.Resilience` is at line 4, it should resolve. If the `AddResiliencePolicies` extension method doesn't exist, check the shared infrastructure.

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Program.cs
git commit -m "feat(dashboard): register IMemoryCache and resilience policies in DI"
```

---

### Task 11: Integration Tests

**Files:**
- Create: `src/Bff/SystemDashboard.Bff.Tests/Aggregators/ResourceAggregatorTests.cs`
- Create: `src/Bff/SystemDashboard.Bff.Tests/Aggregators/MetricsAggregatorTests.cs`

**Interfaces:**
- Consumes: ResourceAggregator (Task 5), MetricsAggregator (Task 6), IMemoryCache
- Produces: (test files only)

- [ ] **Step 1: Verify test project exists**

Run: `Test-Path "src/Bff/SystemDashboard.Bff.Tests"`

If it doesn't exist, create it with `dotnet new xunit`.

- [ ] **Step 2: Write ResourceAggregator parallel query test**

```csharp
// File: src/Bff/SystemDashboard.Bff.Tests/Aggregators/ResourceAggregatorTests.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Tests.Aggregators;

public class ResourceAggregatorTests
{
    [Fact]
    public async Task GetAllResourcesAsync_ReturnsCachedResult_OnSecondCall()
    {
        // Arrange
        var consul = new Mock<IConsulDiscoveryService>();
        consul.Setup(c => c.GetServiceNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["identity-service"]);
        consul.Setup(c => c.GetServiceHealthAsync("identity-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsulServiceHealth
            {
                ServiceName = "identity-service",
                Status = "passing"
            });

        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricDataPoint { Timestamp = DateTime.UtcNow, Value = 42.0 });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<ResourceAggregator>.Instance;

        var aggregator = new ResourceAggregator(
            consul.Object, httpClientFactory.Object, prometheus.Object, cache, logger);

        // Act — first call
        var resources1 = await aggregator.GetAllResourcesAsync();

        // Reset mock counts — second call should hit cache, not downstream
        consul.Invocations.Clear();
        prometheus.Invocations.Clear();

        var resources2 = await aggregator.GetAllResourcesAsync();

        // Assert — second call was cached (no downstream calls)
        Assert.NotEmpty(resources1);
        Assert.Equal(resources1.Count, resources2.Count);
        consul.Verify(c => c.GetServiceNamesAsync(It.IsAny<CancellationToken>()), Times.Never);
        prometheus.Verify(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllResourcesAsync_ReturnsNullMetrics_WhenPrometheusFails()
    {
        // Arrange
        var consul = new Mock<IConsulDiscoveryService>();
        consul.Setup(c => c.GetServiceNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["identity-service"]);
        consul.Setup(c => c.GetServiceHealthAsync("identity-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsulServiceHealth
            {
                ServiceName = "identity-service",
                Status = "passing"
            });

        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated failure"));

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<ResourceAggregator>.Instance;

        var aggregator = new ResourceAggregator(
            consul.Object, httpClientFactory.Object, prometheus.Object, cache, logger);

        // Act
        var resources = await aggregator.GetAllResourcesAsync();

        // Assert
        var svc = resources.OfType<ServiceResource>().FirstOrDefault(r => r.Name == "identity-service");
        Assert.NotNull(svc);
        Assert.Equal("Running", svc.Status);
        Assert.Null(svc.CpuPercent);
        Assert.Null(svc.MemoryUsedMb);
    }
}
```

- [ ] **Step 3: Write MetricsAggregator parallel batch test**

```csharp
// File: src/Bff/SystemDashboard.Bff.Tests/Aggregators/MetricsAggregatorTests.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Tests.Aggregators;

public class MetricsAggregatorTests
{
    [Fact]
    public async Task GetMetricsAsync_ReturnsEmptySnapshot_OnPrometheusFailure()
    {
        // Arrange
        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryRangeAsync(It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated failure"));

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<MetricsAggregator>.Instance;

        var aggregator = new MetricsAggregator(prometheus.Object, cache, logger);

        // Act
        var results = await aggregator.GetMetricsAsync(
            "identity-service", ["cpu", "memory"], "5m");

        // Assert — returns Empty snapshots, not throws
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(0, r.CurrentValue));
        Assert.All(results, r => Assert.Empty(r.DataPoints ?? []));
    }

    [Fact]
    public async Task GetMetricsAsync_QueriesAllMetricsInParallel()
    {
        // Arrange — use a slow mock to verify parallelism
        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryRangeAsync(It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100); // simulate network latency
                return new List<MetricDataPoint>
                {
                    new() { Timestamp = DateTime.UtcNow, Value = 50.0 }
                };
            });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<MetricsAggregator>.Instance;

        var aggregator = new MetricsAggregator(prometheus.Object, cache, logger);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await aggregator.GetMetricsAsync(
            "identity-service", ["cpu", "memory", "requests", "errors"], "5m");
        sw.Stop();

        // Assert — 4 × 100ms = 400ms sequential, should be ~100ms parallel
        Assert.Equal(4, results.Count);
        Assert.True(sw.ElapsedMilliseconds < 300, $"Expected <300ms parallel, got {sw.ElapsedMilliseconds}ms");
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test src/Bff/SystemDashboard.Bff.Tests/SystemDashboard.Bff.Tests.csproj --no-restore`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/SystemDashboard.Bff.Tests/
git commit -m "test(dashboard): add integration tests for parallel queries and graceful degradation"
```

---

## Verification Checklist (Post-Implementation)

- [ ] `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj` — succeeds
- [ ] `dotnet test src/Bff/SystemDashboard.Bff.Tests/SystemDashboard.Bff.Tests.csproj` — all pass
- [ ] Docker Compose: `docker compose up -d` and dashboard loads at http://localhost:5700
- [ ] Resource list API returns in < 500ms (cold), < 50ms (cached)
- [ ] Kill Prometheus container — dashboard still loads with "N/A" for metrics
- [ ] Kill Consul container — dashboard falls back to HTTP health checks
- [ ] Restart BFF — LogStreamBackgroundService picks up from timestamp cursor, no duplicates
