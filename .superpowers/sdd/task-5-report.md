# Task 5: Full Harness Regression Verification

## Status: DONE_WITH_CONCERNS

## Commits Created

```
commit 531695d (HEAD)
Author: Agent
Date:   Mon Jul 20 2026

    fix(harness): Task 5 — wire RouteLlmTool, fix GetAgentProfileTool DI, seed eval tables
```

## Command Summary and Pass/Fail Status

| Step | Command | Result |
|------|---------|--------|
| **Step 1: Build** | `dotnet build ...AgentHarness.Mcp.csproj` | **PASS** — 0 errors, 2 NuGet advisory warnings |
| **Step 2a: Unit Tests** | `dotnet test ...UnitTests.csproj` | **PASS** — 62/62 passed |
| **Step 2b: Integration Tests** | `dotnet test ...IntegrationTests.csproj` | **PASS** — 39/39 passed |
| **Step 3a: Docker Build** | `docker compose build agentharness` | **PASS** — build succeeded |
| **Step 3b: Docker Restart** | `docker compose up -d agentharness` | **PASS** — container healthy |
| **Step 4a: tools/list** | `curl POST /mcp` → tools/list | **PASS** — 20 tools returned |
| **Step 4b: get-agent-profile** | `curl POST /mcp` → get-agent-profile(dotnet) | **PASS** — AIS 46.47, history with 17 runs |
| **Step 4c: evaluate-agent** | `curl POST /mcp` → evaluate-agent(dotnet-eval, dotnet) | **PASS** — pass@1: 0.67, pass@k: 1.0, persisted run id |
| **Step 4d: compare-models** | `curl POST /mcp` → compare-models(dotnet-eval, dotnet) | **PASS** — 2 rows, winner: deepseek-v4-pro |

## Docker Health Status

All 18 containers running, 16 reported as healthy (kibana has no healthcheck).

## MCP Smoke Result Summary

- ✅ `get-agent-profile` — Returns AIS score (46.47) + history array (17 runs)
- ✅ `evaluate-agent` — Returns pass_at_1 (0.67), pass_at_k (1.0), persisted EvalRunId
- ✅ `compare-models` — Returns 2 model rows sorted by score, winner: deepseek-v4-pro
- ✅ `record-instinct` — Records and persists instinct with confidence score
- ✅ `query-instincts` — Returns ranked matches from stored instincts
- ✅ `route-llm` — (Wired in this task) Routes to cost-effective model model: gpt-5.4-mini

## Fixes Applied (Surgical, Test-Verified)

### 1. RouteLlmTool — Not Wired into MCP Surface (3 places in Program.cs)
- **Added** `services.AddScoped<RouteLlmTool>()` to ConfigureServices
- **Added** `case "route-llm":` to tools/call switch statement
- **Added** route-llm entry to BuildToolList() with full inputSchema

### 2. GetAgentProfileTool — DI Resolution Failure
- **Changed** constructor dependency from concrete `AgentMetricsService` to `IAgentMetricsService` interface
- The interface was already registered in DI but the tool was requesting the concrete class

### 3. Missing eval_suites / eval_runs Tables
- Migration `20260720090000_AddEvalTables.cs` existed but was never applied
- EF Core skipped it because its timestamp precedes the already-applied `AddMemoryEntryConfidenceScore` migration
- **Applied** CREATE TABLE statements + indexes directly via psql to PostgreSQL
- **Inserted** seed eval suite `dotnet-eval` for smoke testing

## Concerns/Blockers

### Concern 1: Migration Gap — CRITICAL (requires fix before next deploy)
Migration `20260720090000_AddEvalTables` has a timestamp preceding the already-applied `20260720105641_AddMemoryEntryConfidenceScore`. EF Core's migration runner will NOT apply migrations whose timestamp is before the last applied migration. This means:
- **Fresh deployments** (empty DB) will work fine — Migrate() runs all 4 migrations in order
- **Existing deployments** (where EnsureCreated ran first) will have the eval tables never created by EF Core

**Recommended fix:** Create a new migration (with a current timestamp) that adds the eval tables, or delete the `20260720090000_AddEvalTables.cs` migration file and regenerate with a later timestamp. The manual SQL application in this task is a patch, not a permanent fix.

### Concern 2: OpenTelemetry Vulnerability
Package `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.12.0 has a known moderate severity vulnerability (GHSA-4625-4j76-fww9). Should be updated.

### Concern 3: BuildServiceProvider Warning in Stdio Mode
Line 504 in Program.cs calls `BuildServiceProvider()` in stdio mode, creating an additional copy of singleton services (ASP0000 warning). Pre-existing, non-blocking.

## Report Path

D:\AI\micro\.superpowers\sdd\task-5-report.md
