# Task 5: ResourceAggregator — Parallel + Cache + Graceful Degradation

**Status:** ✅ Complete

**Commits:**
- Changes already applied in `0135adc` (previous task already included parallel+GRPC+cache refactor)

**Files changed:**
- `src/Bff/SystemDashboard.Bff/Aggregators/ResourceAggregator.cs` — All required changes verified present:
  - `IMemoryCache` dependency injected via constructor
  - `GetAllResourcesAsync` wrapped in `_cache.GetOrCreateAsync` with 15s expiry
  - Consul health + Prometheus CPU/Memory queries launched in parallel via `Task.WhenAll`
  - `GetHealthSafeAsync` helper with try/catch for graceful Consul degradation
  - `QueryLatestMetricValueAsync` uses instant `QueryAsync` instead of range query, returns `double?`

**What was verified:**
1. `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj` — 0 errors, 0 warnings
2. Code review of each stepping stone:
   - [x] `using Microsoft.Extensions.Caching.Memory` present
   - [x] `IMemoryCache _cache` field + `IMemoryCache cache` constructor parameter
   - [x] `QueryLatestMetricValueAsync` uses `_prometheus.QueryAsync(promql, ct)` returning `double?`
   - [x] `GetHealthSafeAsync` wraps `_consul.GetServiceHealthAsync` with try/catch, returns `ConsulServiceHealth?`
   - [x] `GetAllResourcesAsync` uses `_cache.GetOrCreateAsync` with `CacheKeys.AllResources` and 15s TTL
   - [x] All health/CPU/memory queries fire concurrently via `Task.WhenAll`
   - [x] Graceful degradation: failed task results become `null`, not exceptions

**Concerns:**
- Pre-existing build errors in `LogsAggregatorTests.cs` (CS0128: duplicate `cache` variable) from other in-progress tasks — unrelated to ResourceAggregator
- Null-forgiving operator `!` used on `GetOrCreateAsync` return (`result!`) to suppress CS8603; safe because the factory always returns a non-null `List<Resource>`
- Test in `ResourceAggregatorTests.cs` already passes 5 constructor args matching new signature — no test changes needed

**Report file:** `D:\AI\micro\.superpowers\sdd\task-5-report.md`
