# Task 3: Prometheus Instant Query (QueryAsync) — Report

## Status

**Complete** ✅

## Changes

### `IPrometheusQueryService.cs`
- Added `Task<MetricDataPoint?> QueryAsync(string query, CancellationToken ct = default)` to the interface (line 15)

### `PrometheusQueryService.cs`
- Added `QueryAsync` method (lines 67-96) that calls Prometheus `/api/v1/query` (instant query) endpoint
- Added 3 private sealed record types for deserialization:
  - `PromInstantResponse` (lines 126-133)
  - `PromInstantData` (lines 135-142)
  - `PromInstantResult` (lines 144-151)

## Design Notes

- Follows same pattern as existing `QueryRangeAsync` — catches exceptions, logs warning, returns `null` on failure
- Instant query response has `value` (single `[timestamp, "value"]` array) instead of `values` (array of arrays)
- Uses `FirstOrDefault()` on results — instant queries typically return 0 or 1 result series
- Reuses existing `MetricDataPoint` model for the return type

## Commit

```
202bb37 feat(dashboard): add Prometheus instant query (QueryAsync) for single-value lookups
```

## Build Verification

- `dotnet build` — **succeeded**, 0 warnings, 0 errors
- No existing tests for this method; no test run performed

## Concerns

- None
