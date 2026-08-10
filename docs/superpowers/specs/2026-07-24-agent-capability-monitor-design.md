# Agent Capability Monitor — Design Spec

**Date:** 2026-07-24  
**Status:** Draft  
**Author:** @architect  
**Approach:** C — Git-based, Registry-driven

---

## 1. Motivation

Hiện tại, khi một agent học được kỹ năng mới (ví dụ: `@dotnet` lần đầu dùng Redis caching, Circuit Breaker, hay Outbox pattern), không có cơ chế nào tự động ghi nhận và cập nhật capability của agent đó. Dispatcher và Orchestrator vẫn routing dựa trên description tĩnh trong `opencode.json`, dẫn đến:

- **Missed routing**: Dispatcher không biết `@dotnet` đã có khả năng dùng Redis → không route task Redis cho nó
- **Stale descriptions**: Agent description không phản ánh đúng năng lực thực tế
- **Knowledge silos**: Pattern hay bị lặp lại vì agent khác không biết đồng nghiệp đã giải quyết được

**Giải pháp:** Agent Capability Monitor — script nhẹ, chạy khi có PR mới, phân tích code output để phát hiện capability mới, cập nhật registry và agent description.

---

## 2. Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    AGENT LÀM VIỆC                          │
│  @dotnet tạo PR: thêm Circuit Breaker + Redis cache       │
└─────────────────────┬────────────────────────────────────┘
                      │ PR created / merged
                      ▼
┌──────────────────────────────────────────────────────────┐
│              CAPABILITY MONITOR                           │
│                                                           │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────┐  │
│  │ 1. Analyze  │───→│ 2. Compare   │───→│ 3. Update   │  │
│  │  PR diff    │    │  vs Registry │    │  Registry +  │  │
│  │  (patterns) │    │              │    │  Description │  │
│  └─────────────┘    └──────────────┘    └─────────────┘  │
│                                                           │
│  Input:                          Output:                  │
│  - PR diff (code changes)        - agent-capabilities.json│
│  - capability-rules.json         - opencode.json (updated)│
│  - agent-capabilities.json       - PR cập nhật capability │
└──────────────────────────────────────────────────────────┘
```

### Design Principles

| Principle | Applied as |
|-----------|------------|
| **Simplicity First** | 1 script + 2 JSON files. Không DB, không service mới |
| **Surgical Changes** | Chỉ sửa `agent-capabilities.json` + agent description trong `opencode.json` |
| **Registry-based** | So sánh với known capabilities → chỉ flag cái mới |
| **Git-native** | Git hook trigger, git history là audit trail |

---

## 3. Components

### 3.1 File Structure

```
His.Hope/
├── agent-capabilities.json          # Registry: known capabilities per agent
├── scripts/
│   ├── capability-monitor.ps1       # Main script: analyze → compare → update
│   └── capability-rules.json        # Rule engine: pattern → capability mapping
│   └── tests/
│       └── capability-monitor.tests.ps1  # Pester unit tests
└── .git/hooks/
    └── post-merge                   # Trigger monitor on PR merge
```

### 3.2 `agent-capabilities.json` — Capability Registry

Tracks all known capabilities per agent. Acts as the source of truth for comparison.

```json
{
  "version": "1",
  "last_updated": "2026-07-24T10:30:00Z",
  "agents": {
    "@dotnet": {
      "capabilities": [
        {
          "id": "redis-caching",
          "category": "infrastructure",
          "detected_at": "2026-07-20",
          "source_pr": "#342",
          "evidence": "StackExchange.Redis, AddStackExchangeRedisCache",
          "confidence": "high"
        }
      ]
    }
  }
}
```

**Schema:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique capability identifier (kebab-case) |
| `category` | enum | infrastructure, resilience, communication, messaging, security, data, testing, frontend |
| `detected_at` | date | First detection date |
| `source_pr` | string | PR number where capability was first observed |
| `evidence` | string | Key patterns found (comma-separated) |
| `confidence` | enum | `high` (used 3+ times), `medium` (first use), `low` (only import, not used) |

**Confidence lifecycle:**
```
low (import only) → medium (first usage) → high (3+ PRs with pattern)
```

### 3.3 `capability-rules.json` — Rule Engine

Maps code patterns to capability IDs. Extensible — add new rules without changing script.

```json
{
  "version": "1",
  "rules": [
    {
      "id": "redis-caching",
      "category": "infrastructure",
      "patterns": [
        "StackExchange\\.Redis",
        "AddStackExchangeRedisCache",
        "IDistributedCache"
      ],
      "require_all": false,
      "min_matches": 2,
      "description": "Sử dụng Redis để caching"
    },
    {
      "id": "circuit-breaker",
      "category": "resilience",
      "patterns": [
        "Polly.*CircuitBreaker",
        "AddPolicyHandler.*CircuitBreaker",
        "ICircuitBreakerPolicy"
      ],
      "require_all": false,
      "min_matches": 1,
      "description": "Circuit Breaker pattern với Polly"
    },
    {
      "id": "grpc-client",
      "category": "communication",
      "patterns": [
        "Grpc\\.Net\\.Client",
        "AddGrpcClient",
        "GrpcChannel",
        "\\.proto"
      ],
      "require_all": false,
      "min_matches": 2,
      "description": "gRPC client implementation"
    },
    {
      "id": "outbox-pattern",
      "category": "messaging",
      "patterns": [
        "OutboxMessage",
        "IOutboxDispatcher",
        "ProcessOutboxMessages"
      ],
      "require_all": false,
      "min_matches": 2,
      "description": "Transactional Outbox pattern"
    },
    {
      "id": "ngrx-signals",
      "category": "frontend",
      "patterns": [
        "@ngrx/signals",
        "signalStore",
        "patchState",
        "withMethods"
      ],
      "require_all": false,
      "min_matches": 2,
      "description": "NgRx SignalStore state management"
    },
    {
      "id": "material-dialog",
      "category": "frontend",
      "patterns": [
        "MatDialog",
        "MAT_DIALOG_DATA",
        "dialog\\.open"
      ],
      "require_all": false,
      "min_matches": 2,
      "description": "Angular Material Dialog"
    }
  ]
}
```

**Rule schema:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Must match capability id in registry |
| `category` | enum | Same categories as registry |
| `patterns` | string[] | Regex patterns to grep in PR diff |
| `require_all` | bool | If true, ALL patterns must match |
| `min_matches` | int | Minimum pattern matches to trigger (if require_all=false) |
| `description` | string | Human-readable for PR description |

### 3.4 `scripts/capability-monitor.ps1` — Main Script

**Flow:**

```
function Invoke-CapabilityMonitor {
    param($PrNumber, $AgentName, $TriggerMode = "auto")

    # Step 1: Get PR diff
    $diff = gh pr diff $PrNumber --color=never

    # Step 2: Load rules & registry
    $rules = Get-Content "$PSScriptRoot/capability-rules.json" -Raw | ConvertFrom-Json
    $registry = Get-Content "$PSScriptRoot/../agent-capabilities.json" -Raw | ConvertFrom-Json

    # Step 3: Detect capabilities from diff
    $detected = @()
    foreach ($rule in $rules.rules) {
        $matchCount = 0
        foreach ($pattern in $rule.patterns) {
            if ($diff -match $pattern) { $matchCount++ }
        }
        if ($matchCount -ge $rule.min_matches) {
            $detected += @{ id = $rule.id; category = $rule.category; evidence = $pattern }
        }
    }

    # Step 4: Compare with registry → find NEW capabilities
    $agentEntry = $registry.agents.$AgentName
    $known = if ($agentEntry) { $agentEntry.capabilities.id } else { @() }
    $newCapabilities = $detected | Where-Object { $_.id -notin $known }

    # Step 5: No new → exit
    if ($newCapabilities.Count -eq 0) {
        Write-Output "No new capabilities detected for $AgentName in PR #$PrNumber"
        return
    }

    # Step 6: Update registry
    foreach ($cap in $newCapabilities) {
        $registry.agents.$AgentName.capabilities += @{
            id = $cap.id
            category = $cap.category
            detected_at = (Get-Date -Format "yyyy-MM-dd")
            source_pr = "#$PrNumber"
            evidence = $cap.evidence
            confidence = "medium"
        }
    }
    $registry | ConvertTo-Json -Depth 4 | Set-Content "agent-capabilities.json"

    # Step 7: Update agent description in opencode.json
    # (append new capabilities to agent's description field)

    # Step 8: Create PR for capability update
    if ($TriggerMode -eq "auto") {
        gh pr create --title "feat(agents): $AgentName new capabilities detected" `
                     --body "Detected: $($newCapabilities.id -join ', ')" `
                     --base main
    }

    Write-Output "Updated $AgentName with $($newCapabilities.Count) new capabilities"
}
```

### 3.5 Git Hook — `.git/hooks/post-merge`

```bash
#!/bin/bash
# Trigger capability monitor on merge of PRs that change src/ or tests/

MERGE_HEAD=$(git rev-parse MERGE_HEAD 2>/dev/null)
if [ -z "$MERGE_HEAD" ]; then exit 0; fi

# Only check if code files changed
CHANGED=$(git diff --name-only HEAD@{1} HEAD -- 'src/' 'tests/')
if [ -z "$CHANGED" ]; then exit 0; fi

# Extract PR number from merge commit message
PR_NUMBER=$(git log -1 --pretty=%B | grep -oP '#\K\d+' | head -1)
if [ -z "$PR_NUMBER" ]; then exit 0; fi

# Determine agent from PR author or label
AGENT=$(gh pr view $PR_NUMBER --json author --jq '.author.login')

# Run monitor
pwsh scripts/capability-monitor.ps1 -PrNumber $PR_NUMBER -AgentName "@$AGENT"
```

---

## 4. Agent Description Update Format

When a new capability is detected, the agent's description in `opencode.json` is updated with a structured suffix:

**Before:**
```
Senior .NET backend engineer for His.Hope microservices
(Clean Architecture, CQRS, DDD, gRPC, EF Core).
Uses MCP: db-* (7 databases), rabbitmq, docker, agent-harness for pipeline tasks.
```

**After (with new capabilities appended):**
```
Senior .NET backend engineer for His.Hope microservices
(Clean Architecture, CQRS, DDD, gRPC, EF Core).
Uses MCP: db-* (7 databases), rabbitmq, docker, agent-harness for pipeline tasks.
Detected capabilities: redis-caching (high), circuit-breaker (high), outbox-pattern (medium).
```

---

## 5. Error Handling

| Scenario | Behavior |
|----------|----------|
| `capability-rules.json` malformed | Exit 1, write error to stderr |
| `agent-capabilities.json` malformed | Exit 1, suggest manual fix |
| `gh` CLI not available | Skip, log warning |
| PR author not a known agent | Skip, log "Unknown agent: $author" |
| Diff too large (>10MB) | Sample first 5000 lines only |
| No code changes in PR | Skip, exit 0 |

---

## 6. Test Strategy

### Unit Tests (Pester — `scripts/tests/capability-monitor.tests.ps1`)

| # | Test Case | Input | Expected |
|---|-----------|-------|----------|
| 1 | `Detects-Redis-Pattern` | Diff with `StackExchange.Redis` + `AddStackExchangeRedisCache` | Rule `redis-caching` matched |
| 2 | `Ignores-Single-Match-When-Min-2` | Diff with only `IDistributedCache` | No match |
| 3 | `Detects-Multiple-Capabilities` | Diff with Redis + Circuit Breaker | Both rules matched |
| 4 | `Filters-Out-Comments` | Pattern in `// StackExchange.Redis` comment | No match |
| 5 | `No-False-Positive-On-String` | `"redis"` in JSON string literal | No match |
| 6 | `Registry-Update-Adds-New` | New capability not in registry | Added to registry |
| 7 | `Registry-Update-Skips-Known` | Capability already in registry | Skipped, no duplicate |
| 8 | `Empty-Diff` | PR with no code changes | "No changes detected", exit 0 |
| 9 | `Malformed-JSON` | Broken capability-rules.json | Exit 1, error message |
| 10 | `New-Agent-First-PR` | Agent not yet in registry | Creates new agent entry |

### Integration Test

| # | Test | Description |
|---|------|-------------|
| 11 | `End-to-End-PR-Flow` | Mock PR → run monitor → verify registry + description updated |
| 12 | `Multi-Agent-PR` | PR with code from @dotnet + @angular → each gets own capabilities |
| 13 | `Hook-Integration` | Verify post-merge hook invokes script correctly |

---

## 7. Rollout Plan

### Phase 1: Core (1 session)
- [ ] Create `agent-capabilities.json` (empty registry, with schema)
- [ ] Create `capability-rules.json` (6 initial rules)
- [ ] Create `scripts/capability-monitor.ps1`
- [ ] Create `scripts/tests/capability-monitor.tests.ps1`
- [ ] All tests passing

### Phase 2: Integration (1 session)
- [ ] Install `.git/hooks/post-merge` trigger
- [ ] Run against last 10 merged PRs to backfill registry
- [ ] Manual review of detected capabilities
- [ ] Update `@dispatcher` and `@orchestrator` descriptions with backfilled data

### Phase 3: Observability (future)
- [ ] Add capability usage metrics (which capabilities are actually used)
- [ ] Confidence promotion (medium → high after 3+ uses)
- [ ] Auto-suggest new rules based on detected patterns

---

## 8. Open Questions

1. **PR trigger: post-merge hay pre-commit?** — Post-merge an toàn hơn (tránh false positive trên WIP code), nhưng pre-commit cho phản hồi nhanh hơn. Đề xuất: post-merge cho Phase 1.
2. **Manual mode:** Cần flag `-TriggerMode manual` để chạy thủ công cho backfill?
3. **Rule contributions:** Ai được phép thêm rule mới vào `capability-rules.json`? Đề xuất: PR review bởi @architect.

---

## 9. Decision Log

| Decision | Rationale |
|----------|-----------|
| Approach C over A | User preference: đơn giản, git-native, không cần AgentHarness |
| Registry-based over LLM-only | Deterministic, auditable, không phụ thuộc LLM quality |
| Post-merge trigger over pre-commit | Tránh false positive trên code chưa hoàn thiện |
| PowerShell over Python | His.Hope đã dùng pwsh cho scripting (generate-index.ps1, mcp-harness.cmd) |
| JSON files over DB | Không cần migration, git diff được luôn |
