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

