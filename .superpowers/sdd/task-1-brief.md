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

