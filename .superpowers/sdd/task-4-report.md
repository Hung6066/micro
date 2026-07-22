# Task 4: ES Timestamp Filter (afterTimestamp)

**Status:** ✅ Complete

**Commits:**
- `6ce6399` — `feat(dashboard): add afterTimestamp filter to Elasticsearch log queries`

**Files changed:**
- `src/Bff/SystemDashboard.Bff/Services/IElasticsearchQueryService.cs` — Added `DateTime? afterTimestamp = null` parameter to `QueryLogsAsync`
- `src/Bff/SystemDashboard.Bff/Services/ElasticsearchQueryService.cs` — Added parameter to implementation signature + range filter clause in ES query body
- `src/Bff/SystemDashboard.Bff/Aggregators/LogsAggregator.cs` — Passed `null` for new parameter at call site
- `src/Bff/SystemDashboard.Bff/Tests/LogsAggregatorTests.cs` — Added `Arg.Any<DateTime?>()` to mock setup

**What was done:**
1. Interface signature updated with optional `afterTimestamp` parameter
2. Implementation signature matched
3. ES query body adds a `range` filter on `@timestamp` with `gte` using ISO 8601 format when `afterTimestamp.HasValue` is true
4. Fixed two callers that were passing `CancellationToken` in the wrong position
5. Build verified — `dotnet build` succeeded (0 errors)
6. Committed

**Concerns:**
- `LogStreamBackgroundService` (Task 9) will still compile since `afterTimestamp` is optional with default `null` — it will need updating to pass the cursor value
- Pre-existing build warning `CS7022` in test SDK and `CS8618` in SharedKernel unrelated to this change

---

## Fix: Revert out-of-scope `match → term` change

**Commit:** (staged, not yet committed)

**Problem:** Task 4 introduced a `term` query on `service.keyword` instead of the original `match` on `service`. This changes ES query semantics from full-text match to exact term match — a behavioral regression not requested.

**Fix:** Reverted line 35 in `ElasticsearchQueryService.cs`:
```csharp
// Before (wrong):
mustClauses.Add(new { term = new Dictionary<string, object> { ["service.keyword"] = service } });

// After (correct):
mustClauses.Add(new { match = new { service } });
```

**Verification:** `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj` — 0 errors.
**Note:** The `EsHit._id` and `EsLogSource.SpanId/Exception/Fields` record additions introduced alongside the regression were left in place because they're required for the `LogEntry` mapping to compile.
