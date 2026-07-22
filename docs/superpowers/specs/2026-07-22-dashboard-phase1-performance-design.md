# Dashboard Phase 1: Performance + Resilience — Design Spec

**Date:** 2026-07-22  
**Status:** Draft  
**Author:** Lead System Architect  

## 1. Problem Statement

The SystemDashboard BFF (`SystemDashboard.Bff`) loads resources and metrics in ~3 seconds due to sequential network calls to Consul, Prometheus, Elasticsearch, and Jaeger. No caching exists — every API call triggers full recomputation. The LogStreamBackgroundService uses an in-memory `HashSet` for dedup, losing state on restart.

## 2. Goals

| Goal | Metric | Target |
|------|--------|--------|
| Resource list load time (cached) | P95 latency | < 50ms |
| Resource list load time (cold) | P95 latency | < 800ms |
| Metrics query (cold) | P95 latency | < 500ms |
| Graceful degradation | Partial results returned | Never empty on partial failure |
| LogStream restart resilience | Duplicate logs on restart | 0 (cursor-based) |

## 3. Non-Goals (Phase 1)

- Reactive/SignalR push for metrics (Phase 2)
- ES log retention / rotation policies (Phase 2)
- RBAC dashboard access control (Phase 3)
- Docker build cache fix (Phase 3)
- Frontend lazy-loading/UX (Phase 3)
- Multi-region failover

## 4. Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    HTTP Controllers                      │
│  ResourcesController  MetricsController  LogsController  │
└──────────┬───────────┬──────────┬───────────┬───────────┘
           │           │          │           │
     ┌─────▼─────┐ ┌───▼───┐ ┌───▼───┐ ┌────▼─────┐
     │ Resource  │ │Metrics│ │ Logs  │ │ Traces   │
     │Aggregator │ │Aggr.  │ │Aggr.  │ │Aggr.     │
     └─┬──┬──┬───┘ └──┬───┘ └──┬───┘ └────┬─────┘
       │  │  │        │        │           │
       │  │  │    ┌───▼────────▼───────────▼─────┐
       │  │  │    │      IMemoryCache            │
       │  │  │    │  TTL: 10s–120s by data type │
       │  │  │    └─────────────────────────────┘
       │  │  │
  ┌────▼──▼──▼────────────────────────────┐
  │      Task.WhenAll fan-out             │
  │  (Consul ×7 + Prometheus ×14)         │
  └──┬──────┬─────────┬──────────────────┘
     │      │         │
  ┌──▼──┐ ┌─▼────┐ ┌─▼──────┐
  │Consul│ │Prom  │ │ES/Jaeger│
  └─────┘ └──────┘ └────────┘
```

**Key pattern:** Each aggregator fires all downstream calls in parallel via `Task.WhenAll`, caches results in `IMemoryCache`, and returns partial data on any failure.

## 5. Component Designs

### 5.1 ResourceAggregator

**File:** `src/Bff/SystemDashboard.Bff/Aggregators/ResourceAggregator.cs`

#### 5.1.1 Parallel Query Fan-Out

Current sequential loop (lines 116-148) replaced with parallel fan-out:

```csharp
public async Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default)
{
    return await _cache.GetOrCreateAsync(CacheKeys.AllResources, async () =>
    {
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
        await Task.WhenAll(
            healthTasks.Values
                .Concat(cpuTasks.Values)
                .Concat(memoryTasks.Values));

        // Phase 2: Assemble results
        var resources = new List<Resource>();
        foreach (var (name, (httpPort, grpcPort, _, databases)) in _serviceMap)
        {
            var health = healthTasks[name].Result; // safe: already awaited
            var cpuPercent = cpuTasks.TryGetValue(name, out var cpuT) ? cpuT.Result : null;
            var memoryMb = memoryTasks.TryGetValue(name, out var memT) ? memT.Result : null;

            var (stateStr, healthStr, checks) = MapHealthResult(health);
            // If health is null but we got metrics, it's "Degraded" not "Stopped"

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
                CpuPercent = cpuPercent,     // nullable now
                MemoryUsedMb = memoryMb,      // nullable now
                Databases = databases.ToList(),
            });
        }

        resources.AddRange(_infraResources);
        resources.AddRange(_databaseResources);
        return resources;
    }, TimeSpan.FromSeconds(15));
}
```

#### 5.1.2 Graceful Degradation

`GetHealthSafeAsync` wraps Consul call with error handling so one failed service doesn't block others:

```csharp
private async Task<ConsulServiceHealth?> GetHealthSafeAsync(string serviceName, CancellationToken ct)
{
    try
    {
        return await _consul.GetServiceHealthAsync(serviceName, ct);
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Consul health check failed for {Service}", serviceName);
        return null; // caller falls back to direct HTTP health check
    }
}
```

#### 5.1.3 Model Change: Nullable Metrics

`CpuPercent` and `MemoryUsedMb` become `double?` — `null` means "unknown" (not 0). Frontend renders `null` as "N/A" badge.

### 5.2 MetricsAggregator

**File:** `src/Bff/SystemDashboard.Bff/Aggregators/MetricsAggregator.cs`

#### 5.2.1 Parallel Metric Queries

Current sequential `foreach` loop (lines 70-123) replaced with `Task.WhenAll`:

```csharp
public async Task<List<MetricSnapshot>> GetMetricsAsync(
    string service, string[] metricNames, string range, CancellationToken ct = default)
{
    var cacheKey = CacheKeys.Metrics(service, string.Join(",", metricNames), range);
    return await _cache.GetOrCreateAsync(cacheKey, async () =>
    {
        var rangeConfig = RangeConfig.GetValueOrDefault(range, RangeConfig["5m"]);
        var end = DateTime.UtcNow;
        var start = end - rangeConfig.duration;

        // Launch all metric queries in parallel
        var tasks = metricNames.Select(async metricName =>
        {
            // ... per-metric logic (same as before) ...
            try
            {
                var dataPoints = await _prometheus.QueryRangeAsync(promql, start, end, rangeConfig.step, ct);
                return BuildSnapshot(metricName, displayName, unit, dataPoints);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metrics query failed: {Metric}", metricName);
                return MetricSnapshot.Empty(metricName, displayName, unit);
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }, GetMetricsTtl(range));
}

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

### 5.3 LogsAggregator

**File:** `src/Bff/SystemDashboard.Bff/Aggregators/LogsAggregator.cs`

Add cache wrapper with 5-second TTL for log queries:

```csharp
public async Task<List<LogEntry>> QueryLogsAsync(...)
{
    var cacheKey = CacheKeys.Logs(service, level, size, searchQuery);
    return await _cache.GetOrCreateAsync(cacheKey, async () =>
    {
        // existing query logic
    }, TimeSpan.FromSeconds(5));
}
```

### 5.4 TracesAggregator

**File:** `src/Bff/SystemDashboard.Bff/Aggregators/TracesAggregator.cs`

Same pattern — cache with appropriate TTL:

| Data | TTL |
|------|-----|
| Search results | 15s |
| Single trace detail | 30s (traces are immutable) |

### 5.5 PrometheusQueryService — Instant Query

**New method on `IPrometheusQueryService`:**

```csharp
/// <summary>
/// Executes an instant PromQL query (current value).
/// Faster than QueryRangeAsync for single-point lookups.
/// </summary>
Task<MetricDataPoint?> QueryAsync(string promql, CancellationToken ct = default);
```

Implementation in `PrometheusQueryService`:

```csharp
public async Task<MetricDataPoint?> QueryAsync(string promql, CancellationToken ct = default)
{
    var url = $"{_baseUrl}/api/v1/query?query={Uri.EscapeDataString(promql)}";
    var response = await _httpClient.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadFromJsonAsync<PrometheusInstantResponse>(_jsonOptions, ct);
    return json?.Data?.Result?.FirstOrDefault()?.Value;
}
```

### 5.6 LogStreamBackgroundService — Timestamp Cursor

**File:** `src/Bff/SystemDashboard.Bff/Services/LogStreamBackgroundService.cs`

Replace in-memory HashSet with timestamp-based cursor:

```csharp
public sealed class LogStreamBackgroundService : BackgroundService
{
    private DateTime _lastPushedTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var esQuery = scope.ServiceProvider.GetRequiredService<IElasticsearchQueryService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<LogStreamHub>>();

                // Query logs after the last pushed timestamp
                var logs = await esQuery.QueryLogsAsync(
                    size: 50,
                    afterTimestamp: _lastPushedTimestamp,
                    ct: stoppingToken);

                if (logs.Count > 0)
                {
                    foreach (var log in logs)
                    {
                        await hubContext.Clients
                            .Group(log.Service ?? "*")
                            .SendAsync("LogEntry", log, stoppingToken);
                    }

                    _lastPushedTimestamp = logs.Max(l => l.Timestamp.GetValueOrDefault(_lastPushedTimestamp));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LogStream poll failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

**Note:** Requires adding `afterTimestamp` parameter to `IElasticsearchQueryService.QueryLogsAsync` and its ES query builder.

### 5.7 New Files

| File | Namespace | Purpose |
|------|-----------|---------|
| `Aggregators/CacheKeys.cs` | `SystemDashboard.Bff.Aggregators` | Static cache key constants |
| `Aggregators/CacheExtensions.cs` | `SystemDashboard.Bff.Aggregators` | `GetOrCreateAsync<T>` extension on `IMemoryCache` |

### 5.7a ElasticsearchQueryService Signature Change

`IElasticsearchQueryService.QueryLogsAsync` adds optional `afterTimestamp` parameter:

```csharp
Task<List<LogEntry>> QueryLogsAsync(
    string? service = null, string? level = null,
    int? from = null, int size = 100,
    string? searchQuery = null,
    DateTime? afterTimestamp = null,   // NEW
    CancellationToken ct = default);
```

When `afterTimestamp` is specified, ES range query filters `@timestamp >= afterTimestamp`. When null, existing behavior unchanged.

### 5.8 Program.cs Changes

```csharp
// Add memory cache
builder.Services.AddMemoryCache();

// Add resilience pipelines
builder.Services.AddResiliencePolicies();
```

## 6. Cache TTL Summary

| Data | TTL | Key Format |
|------|-----|-----------|
| All resources | 15s | `resources:all` |
| Single resource | 15s | `resources:{name}` |
| Metrics (5m) | 10s | `metrics:{service}:{metric}:5m` |
| Metrics (1h) | 30s | `metrics:{service}:{metric}:1h` |
| Metrics (24h) | 120s | `metrics:{service}:{metric}:24h` |
| Log search | 5s | `logs:{svc}:{level}:{size}:{query}` |
| Trace search | 15s | `traces:search:{svc}` |
| Trace detail | 30s | `traces:{traceId}` |

## 7. Error Handling Matrix

| Scenario | ResourceAggregator | MetricsAggregator | LogsAggregator |
|----------|-------------------|-------------------|----------------|
| Consul down | Fallback to direct HTTP health check | N/A | N/A |
| Prometheus down | `CpuPercent=null` (show "N/A") | Return `MetricSnapshot.Empty` per metric | N/A |
| ES down | N/A | N/A | Return empty list |
| Jaeger down | N/A | N/A | Return empty list |
| All down | Returns resources with unknown status, not 500 | Returns metrics with zero values | Returns empty list |

**Rule:** Never return 500 when downstream is unhealthy. Return partial data with null/empty signals.

## 8. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Cache staleness shows wrong health | Low | Medium | 15s TTL is short enough for ops dashboards |
| Memory pressure from cache entries | Low | Low | Fixed-size cache keys, bounded by service count |
| Parallel queries overwhelm ES/Prom | Low | Low | Each HttpClient already has circuit breaker; Prometheus scrapes are cheap |
| Timestamp cursor misses logs on clock skew | Medium | Low | Use 30s lookback window on restart |
| Nullable model breaks Angular frontend | Medium | Low | Coordinate with frontend team, null-coalesce to 0 in BFF if needed |

## 9. Testing Strategy

| Test Type | What | Tool |
|-----------|------|------|
| Unit | Aggregator parallel query assembly, null handling | xUnit |
| Integration | PrometheusQueryService.QueryAsync, ES timestamp filter | Testcontainers |
| E2E | Dashboard load time < 500ms, "N/A" display on failure | Playwright |
| Chaos | Kill Prometheus pod, verify graceful degradation | Chaos Mesh |

## 10. Rollout Plan

1. **Dev:** Merge to `develop`, verify with Docker Compose
2. **Staging:** Deploy to staging cluster, run k6 load test (target: P95 < 800ms cold)
3. **Canary:** 10% traffic for 1 hour, monitor error rate + latency
4. **Full:** Roll to 100%, monitor for 24h
5. **Rollback:** Revert to sequential mode via feature flag `Dashboard:ParallelQueries:Enabled=false`
