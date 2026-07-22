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

