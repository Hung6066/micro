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

