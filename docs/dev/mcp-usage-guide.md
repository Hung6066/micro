# His.Hope MCP Usage Guide

> **Hướng dẫn sử dụng 16 MCP servers cho AI agents trong His.Hope**
>
> Version: 1.0 | Last updated: 2026-07-17 | Maintainer: @architect

---

## Table of Contents

1. [What is MCP?](#1-what-is-mcp)
2. [How MCP Works in His.Hope](#2-how-mcp-works-in-hishop)
3. [Complete MCP Server Reference](#3-complete-mcp-server-reference)
4. [Usage Examples by Role/Task](#4-usage-examples-by-roletask)
5. [Troubleshooting Common Issues](#5-troubleshooting-common-issues)
6. [Adding New MCP Servers](#6-adding-new-mcp-servers)
7. [Best Practices](#7-best-practices)
8. [Appendix: opencode.json MCP Config](#8-appendix-opencodejson-mcp-config)

---

## 1. What is MCP?

**Model Context Protocol (MCP)** is an open standard that connects AI agents to external tools and data sources. Think of it as a "USB-C port for AI" — instead of each AI agent implementing custom integrations for databases, Kubernetes, Redis, etc., MCP provides a unified protocol.

### Key Concepts

| Concept | Description |
|---|---|
| **MCP Server** | A lightweight server that exposes **tools** (actions) and **resources** (data) via the MCP protocol |
| **Tool** | An action the AI agent can invoke (e.g., "run a SQL query", "list pods", "get Redis value") |
| **Resource** | Data the AI agent can read (e.g., database schemas, container logs, Prometheus metrics) |
| **Transport** | MCP servers run as local subprocesses (stdio) or remote (SSE). His.Hope uses **stdio** exclusively |

### What MCP Enables

- **Direct database queries** — AI agent can run `SELECT` against any of the 7 PostgreSQL databases
- **Infrastructure control** — AI agent can `kubectl get pods`, check RabbitMQ queues, query Prometheus
- **File operations** — AI agent can read/write/search any file within the workspace
- **Browser automation** — AI agent can drive Playwright for E2E testing
- **Reasoning** — AI agent can use structured step-by-step thinking for complex problems

Without MCP, the AI agent would be limited to reading code files and making educated guesses about runtime state. With MCP, the agent can **interact with live infrastructure** just like a human developer using CLI tools.

---

## 2. How MCP Works in His.Hope

### Architecture

```
┌──────────────────────────────────────────────────────────┐
│                  OpenCode (AI Agent Host)                 │
│  ┌────────────────────────────────────────────────────┐  │
│  │              LLM (DeepSeek V4)                     │  │
│  │  "Check why patient creation fails"                │  │
│  └──────────┬─────────────────────────────────────────┘  │
│             │                                            │
│    ┌────────┴────────┐                                   │
│    │   MCP Client    │ ← Reads opencode.json for config   │
│    └────────┬────────┘                                   │
└─────────────┼────────────────────────────────────────────┘
              │
    ┌─────────┴──────────────────────────────────────────────┐
    │  stdio (subprocess)                                     │
    │                                                         │
    │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │
    │  │  MCP     │ │  MCP     │ │  MCP     │ │  MCP     │  │
    │  │ Server A │ │ Server B │ │ Server C │ │ Server D │  │
    │  │ (db-*)   │ │ (k8s)    │ │ (redis)  │ │ (...16)  │  │
    │  └──────────┘ └──────────┘ └──────────┘ └──────────┘  │
    │         │             │           │                    │
    ▼         ▼             ▼           ▼                    ▼
  PostgreSQL  kubectl    Redis CLI     RabbitMQ API
  (Docker)    (~/.kube)  (localhost)  (localhost:15672)
```

### How AI Agents Interact

1. **User asks a question** (e.g., "Why is patient creation failing?")
2. **Architect agent delegates** to appropriate agent (e.g., `@dotnet`)
3. **Agent uses MCP tools** to investigate:
   - `db-patient` → `db-patient_query` → `SELECT * FROM patients WHERE ...`
   - `kubernetes` → `kubectl get pods his-hope-patient` to check status
   - `docker` → `docker exec his-hope-patient -- journalctl` for logs
   - `filesystem` → read source code files
4. **Agent synthesizes findings** and reports to the user

### Lifecycle

MCP servers are **lazy-started** — they only launch when the AI agent first uses a tool from that server. If an agent never uses `db-lab`, the lab MCP server never starts. This conserves resources and avoids unnecessary network connections.

---

## 3. Complete MCP Server Reference

### 3.1 Database Servers (7 servers)

Each microservice has its own PostgreSQL database. All share the same `his-hope-postgres` container on port 5433.

| Server | Database | Service | Connection String |
|---|---|---|---|
| `db-identity` | `identitydb` | IdentityService | `postgresql://postgres:postgres@localhost:5433/identitydb` |
| `db-patient` | `patientdb` | PatientService | `postgresql://postgres:postgres@localhost:5433/patientdb` |
| `db-appointment` | `appointmentdb` | AppointmentService | `postgresql://postgres:postgres@localhost:5433/appointmentdb` |
| `db-clinical` | `clinicaldb` | ClinicalService | `postgresql://postgres:postgres@localhost:5433/clinicaldb` |
| `db-lab` | `labdb` | LabService | `postgresql://postgres:postgres@localhost:5433/labdb` |
| `db-billing` | `billingdb` | BillingService | `postgresql://postgres:postgres@localhost:5433/billingdb` |
| `db-pharmacy` | `pharmacydb` | PharmacyService | `postgresql://postgres:postgres@localhost:5433/pharmacydb` |

**Package**: `@modelcontextprotocol/server-postgres` (same package, different connection strings)

**Tools provided**:
- `db-<service>_query` — Execute a read-only SQL query (the `_query` tool name is auto-generated by the MCP server name)

**Example prompts**:
```
"List all tables in patientdb"
"Show the schema for the patients table in identitydb"
"Find all appointments for patient with ID 1001 in appointmentdb"
"Count how many prescriptions are active in pharmacydb"
```

**What AI agents should NOT do**:
- Never run `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, or `TRUNCATE` — these MCP servers are **read-only** by default
- Never try to connect to databases not in the allowed list

### 3.2 Filesystem Server

| Server | Package | Scope |
|---|---|---|
| `filesystem` | `@modelcontextprotocol/server-filesystem` | `D:\AI\micro` (entire workspace) |

**Tools provided**:
- Read files
- Write files
- Edit files
- Search files (glob patterns)
- List directories
- Move/rename files
- Create directories
- Get file info

**Example prompts**:
```
"Read the Program.cs in the IdentityService project"
"Find all files matching *.proto in src/Shared/Protos"
"Search for 'PatientCreatedEvent' across all C# files"
"Show me the directory structure of src/Services/PatientService"
```

### 3.3 Playwright Server

| Server | Package | Purpose |
|---|---|---|
| `playwright` | `@playwright/mcp` | Browser automation for E2E testing |

**Tools provided**:
- `browser_navigate` — Navigate to URL
- `browser_snapshot` — Get accessibility snapshot of current page
- `browser_take_screenshot` — Capture screenshot
- `browser_click` — Click elements
- `browser_type` — Type text into elements
- `browser_fill_form` — Fill multiple form fields
- `browser_select_option` — Select dropdown options
- `browser_hover` — Hover over elements
- `browser_press_key` — Keyboard actions
- `browser_network_requests` — Inspect network requests
- `browser_console_messages` — Check console errors
- `browser_find` — Search page text
- `browser_evaluate` — Run JS in page context

**Example prompts**:
```
"Navigate to the His.Hope login page and take a screenshot"
"Fill in the patient registration form with test data"
"Check if the appointment booking dialog displays validation errors"
"Capture all network requests when submitting a prescription"
```

### 3.4 GitHub Server

| Server | Package | Auth |
|---|---|---|
| `github` | `github-mcp-server-mcp` (global npm install) | Auto-detected from `gh` CLI |

**Invocation**: Uses `cmd /c` to call the globally-installed binary (Windows workaround).

**Tools provided**:
- Repository management (create, fork, search repos)
- Issue management (create, read, update, list, search issues)
- Pull request management (create, review, merge, list)
- File operations (create, read, update, delete files via GitHub API)
- Branch management
- Commit history and diffs
- GitHub Actions workflow management
- Release management

**Example prompts**:
```
"List all open issues tagged as bug"
"Create a PR from branch feature/patient-allergy-check to main"
"Check the status of the latest GitHub Actions workflow run"
"Show the commit history for the last 10 commits to src/Services/"
```

### 3.5 Kubernetes Server

| Server | Package | Config |
|---|---|---|
| `kubernetes` | `mcp-server-kubernetes` | Auto-detects from `~/.kube/config` |

**Tools provided**:
- `kubectl_get` — List/get resources (pods, deployments, services, configmaps, etc.)
- `kubectl_describe` — Detailed resource information
- `kubectl_logs` — Get pod/container logs
- `kubectl_apply` — Apply YAML manifests
- `kubectl_delete` — Delete resources
- `kubectl_create` — Create resources
- `kubectl_scale` — Scale deployments
- `kubectl_rollout` — Manage rollouts (status, history, restart, undo)
- `kubectl_patch` — Patch existing resources
- `kubectl_exec` — Execute commands in pods
- `kubectl_port_forward` — Forward local ports to pod ports
- `kubernetes_install_helm_chart` — Deploy Helm charts
- `kubernetes_uninstall_helm_chart` — Remove Helm releases
- `kubernetes_upgrade_helm_chart` — Upgrade Helm releases

**Note for local dev**: The `desktop-linux` context is used with Docker Desktop's Linux engine via named pipe.

**Example prompts**:
```
"Check the status of all pods in the his-hope namespace"
"Show the logs for the his-hope-patient pod"
"Describe the his-hope-identity service"
"Restart the his-hope-appointment deployment"
"Scale his-hope-patient to 3 replicas"
```

### 3.6 RabbitMQ Server

| Server | Package | Connection |
|---|---|---|
| `rabbitmq` | `rabbitmq-mcp` | `http://admin:admin@localhost:15672` |

**Environment variables**:
```
RABBITMQ_HOST=localhost
RABBITMQ_USERNAME=admin
RABBITMQ_PASSWORD=admin
RABBITMQ_MANAGEMENT_PORT=15672
RABBITMQ_PROTOCOL=http
```

**Tools provided**:
- Queue management: list, get, create, delete, purge, pause, resume queues
- Exchange management: list, get, create, delete exchanges
- Binding management: bind/unbind queues and exchanges
- Connection management: list connections, close connections (by name or username)
- Channel management: list channels
- User management: list, create, delete users, set permissions
- Vhost management: list, create, delete vhosts
- Policy management: list, create, delete policies and operator policies
- Node management: list nodes, get node memory breakdown
- Health checks: alarms, certificate expiration, port listeners, protocol listeners, virtual hosts

**Example prompts**:
```
"List all queues and their message counts"
"Show the bindings for the 'patient' exchange"
"Check how many messages are in the dead-letter queue"
"List all connections for the IdentityService"
"Get the node memory breakdown for the RabbitMQ cluster"
```

### 3.7 Redis Server

| Server | Package | Connection |
|---|---|---|
| `redis` | `@modelcontextprotocol/server-redis` | `redis://localhost:6379` |

**Tools provided**:
- `redis_get` — Get value by key
- `redis_set` — Set key-value (with optional TTL)
- `redis_delete` — Delete one or more keys
- `redis_list` — List keys matching a pattern

**Typical Redis data in His.Hope** (per ADR-010):
- JWT refresh tokens: `refresh_token:{token_hash}`
- User sessions: `session:{user_id}`
- Rate limit counters: `ratelimit:{ip}:{endpoint}`
- Distributed locks: `lock:{resource_id}`
- Cache entries: `cache:{entity_type}:{id}`

**Example prompts**:
```
"List all Redis keys matching 'refresh_token:*'"
"Get the value for a specific refresh token key"
"Show all active sessions for user ID 42"
"Check the TTL on cache entry for patient:1001"
"Count how many keys match 'ratelimit:*'"
```

### 3.8 Prometheus Server

| Server | Package | URL |
|---|---|---|
| `prometheus` | `mcp-prometheus` | `http://localhost:9090` |

**Tools provided**:
- `prometheus_query` — Execute instant PromQL query
- `prometheus_queryRange` — Execute range query over time period
- `prometheus_getTargets` — List scrape targets and their health
- `prometheus_getRules` — List alerting/recording rules
- `prometheus_getPrometheusStatus` — Get Prometheus server status
- `prometheus_getClusterHealthOverview` — Get cluster health overview
- `prometheus_diagnoseNamespace` — Diagnose namespace health
- `prometheus_diagnoseNode` — Diagnose node health
- `prometheus_getTopResourceConsumers` — Top CPU/memory/network consumers
- `prometheus_investigatePod` — Deep pod investigation
- `prometheus_compareTimeRanges` — Compare metrics between time periods

**Example prompts**:
```
"Query the current CPU usage of all his-hope services"
"Show the HTTP request rate for the API Gateway over the last hour"
"Check if any alerting rules are firing"
"List all Prometheus scrape targets and their health"
"Compare the error rate in PatientService between today and yesterday"
```

### 3.9 Docker Server

| Server | Package | Access |
|---|---|---|
| `docker` | `mcp-server-docker` | `docker exec` via Docker Desktop named pipe |

**Allowed containers** (16 containers configured):
- `his-hope-postgres`, `his-hope-redis`, `his-hope-rabbitmq`
- `his-hope-elasticsearch`, `his-hope-jaeger`, `his-hope-prometheus`, `his-hope-grafana`
- `his-hope-identity`, `his-hope-patient`, `his-hope-appointment`
- `his-hope-clinical`, `his-hope-lab`, `his-hope-billing`, `his-hope-pharmacy`
- `his-hope-gateway`, `his-hope-frontend`

**Note**: On Windows, this works by calling `docker.exe` which connects via named pipe to Docker Desktop's Linux engine.

**Tools provided**:
- `docker_exec` — Execute commands inside a container (restricted to ALLOWED_CONTAINERS)
- `docker_run_command` — Run command via Docker Compose service

**Example prompts**:
```
"Show the logs from his-hope-identity container"
"Check the environment variables in his-hope-patient"
"Verify that his-hope-postgres is accepting connections"
"Run 'dotnet --list-runtimes' in his-hope-gateway"
"Check disk usage in his-hope-frontend container"
```

### 3.10 Sequential Thinking Server

| Server | Package | Purpose |
|---|---|---|
| `sequential-thinking` | `@modelcontextprotocol/server-sequential-thinking` | Structured reasoning |

**Tool provided**:
- `sequentialthinking` — A single tool that walks through a problem step-by-step, with the ability to revise, branch, and backtrack

This is a **meta-cognitive tool** — it doesn't interact with infrastructure. It helps the AI agent think through complex problems systematically.

**When to use**:
- Decomposing a large refactoring task
- Analyzing cross-service impacts
- Debugging a non-trivial production issue
- Designing architecture for a new feature
- Planning database migration strategies

**Example prompts**:
```
"Analyze the impact of migrating IdentityService from PostgreSQL to CockroachDB"
"Design a strategy for de-duplicating patient records across the system"
"Trace the complete flow of an appointment booking request through all services"
"Plan the steps needed to add a new 'Lab Order' feature end-to-end"
"Debug why the dead-letter queue in RabbitMQ is accumulating messages"
```

---

## 4. Usage Examples by Role/Task

### 4.1 Debug a Database Issue

**Scenario**: A patient record can't be found, or an appointment query is slow.

**MCP servers**: `db-patient`, `db-appointment`, `kubernetes`, `docker`

```
AI agent should:

1. Use db-patient_query to check the database:
   "SELECT * FROM patients WHERE id = 1001"

2. Use db-appointment_query to check appointments:
   "SELECT * FROM appointments WHERE patient_id = 1001"

3. Explain a query plan for slow queries:
   "EXPLAIN ANALYZE SELECT * FROM appointments WHERE patient_id = 1001"

4. Check if the database container is healthy:
   → Use docker: docker exec his-hope-postgres pg_isready -U postgres

5. Check service logs for connection errors:
   → Use kubernetes: kubectl logs his-hope-patient -n his-hope

6. Query Prometheus for database error rates:
   → Use prometheus: prometheus_query "rate(pg_stat_database_tup_fetched[5m])"
```

### 4.2 Debug RabbitMQ Event Flow

**Scenario**: Events are not reaching the expected consumer, or messages are piling up in a queue.

**MCP servers**: `rabbitmq`, `kubernetes`

```
AI agent should:

1. List all queues to see message counts:
   → Use rabbitmq: list queues

2. Examine a specific queue:
   → Use rabbitmq: get queue details

3. Check bindings between exchanges and queues:
   → Use rabbitmq: list bindings for the exchange

4. Peek at messages stuck in a queue:
   → Use rabbitmq: get messages from queue

5. Check if consumer connections exist:
   → Use rabbitmq: list consumers for the queue
   → Use rabbitmq: list connections

6. Check the publishing service's health:
   → Use kubernetes: kubectl logs his-hope-identity -n his-hope

7. Check the consuming service's logs:
   → Use kubernetes: kubectl logs his-hope-appointment -n his-hope
```

### 4.3 Check Kubernetes Deployment Status

**Scenario**: A new deployment doesn't seem to be running correctly.

**MCP servers**: `kubernetes`

```
AI agent should:

1. List all pods and their status:
   → Use kubernetes: kubectl get pods -n his-hope

2. Get detailed pod info for the failing pod:
   → Use kubernetes: kubectl describe pod his-hope-patient-xxx -n his-hope

3. Check pod logs:
   → Use kubernetes: kubectl logs his-hope-patient-xxx -n his-hope

4. Check the deployment rollout status:
   → Use kubernetes: rollout status deployment his-hope-patient -n his-hope

5. Check rollout history:
   → Use kubernetes: rollout history deployment his-hope-patient -n his-hope

6. If needed, restart the deployment:
   → Use kubernetes: rollout restart deployment his-hope-patient -n his-hope

7. Check events in the namespace:
   → Use kubernetes: kubectl get events -n his-hope --sort-by=.lastTimestamp
```

### 4.4 Inspect Redis Cache / Tokens

**Scenario**: Users are being logged out unexpectedly, or cache needs inspection.

**MCP servers**: `redis`

```
AI agent should:

1. List all session keys:
   → Use redis: redis_list "session:*"

2. Get a specific session's data:
   → Use redis: redis_get "session:42"

3. Check for refresh token keys:
   → Use redis: redis_list "refresh_token:*"

4. Check key TTL (expiration):
   → Use redis: redis_get "refresh_token:abc123"
   (TTL info typically included in the value or use `TTL key` if available)

5. List cache entries for a specific entity:
   → Use redis: redis_list "cache:patient:*"

6. Count total keys:
   → Use redis: redis_list "*"
```

### 4.5 View Container Logs

**Scenario**: A service is crashing at startup or producing errors.

**MCP servers**: `docker`, `kubernetes`

```
AI agent should:

For Docker (local dev):
   → Use docker: docker_run_command {
       "service": "his-hope-identity",
       "command": "tail -100 /app/logs/app.log"
     }
   → Or use: docker exec his-hope-identity -- journalctl -u app --no-pager -n 100

For Kubernetes (deployed):
   → Use kubernetes: kubectl logs deployment/his-hope-identity -n his-hope --tail=100
   → Use kubernetes: kubectl logs pod/his-hope-identity-xxx -n his-hope --previous
   → Use kubernetes: kubectl logs deployment/his-hope-identity -n his-hope --all-containers

For infrastructure containers:
   → Use docker: docker exec his-hope-postgres -- pg_stat_activity
   → Use docker: docker exec his-hope-rabbitmq -- rabbitmqctl list_queues
```

### 4.6 Query Prometheus Metrics

**Scenario**: Investigating performance degradation or setting up monitoring.

**MCP servers**: `prometheus`

```
AI agent should:

1. Check overall cluster health:
   → Use prometheus: get cluster health overview

2. Get CPU usage for all services:
   → Use prometheus: query "rate(process_cpu_seconds_total{job=~'his-hope-.*'}[5m])"

3. Check HTTP request rates at the API Gateway:
   → Use prometheus: query "rate(http_requests_total{service='apigateway'}[5m])"

4. Check error rates:
   → Use prometheus: query "rate(http_requests_total{service='apigateway',status=~'5..'}[5m])"

5. Get top resource consumers:
   → Use prometheus: get top resource consumers (cpu, limit=10)

6. Compare today vs yesterday:
   → Use prometheus: compare time ranges (offset 24h)

7. Diagnose a specific namespace:
   → Use prometheus: diagnose namespace "his-hope"

8. Investigate a specific pod:
   → Use prometheus: investigate pod "his-hope-patient" namespace "his-hope"
```

### 4.7 Debug a Git Issue

**Scenario**: A commit is stuck, branch needs cleanup, or PR review is needed.

**MCP servers**: `github`

```
AI agent should:

1. List recent commits on the current branch:
   → Use github: list commits on the current branch (uses Git tooling)

2. Check open PRs:
   → Use github: list pull requests

3. View PR details and diff:
   → Use github: get pull request details

4. Check CI status:
   → Use github: list workflow runs

5. If merging is blocked:
   → Use github: check branch protection rules

6. Create or update issues:
   → Use github: create issue with labels

7. Resolve merge conflicts (read PR diff):
   → Use github: get pull request files and diffs
```

### 4.8 Plan a Complex Refactor

**Scenario**: Redesigning a service boundary, adding a new entity, or migrating data.

**MCP servers**: `sequential-thinking`, `filesystem`, all `db-*` servers

```
AI agent should:

1. Use sequential-thinking to decompose the problem:
   → "I need to refactor the PatientService to extract Allergy management into a separate bounded context"

2. Use filesystem to read all relevant source files:
   → Read PatientAggregate, Allergy entity, existing migrations

3. Use db-* servers to understand current schema:
   → Query patientdb for existing allergy tables/columns

4. Use sequential-thinking to design the migration strategy:
   → Step-by-step: new schema → dual-write → backfill → cutover → cleanup

5. Use filesystem to write the changes:
   → New Domain, Application, Infrastructure, Api projects
   → New migration files
   → Updated proto files

6. Final sequential-thinking verification:
   → Verify no backward-incompatible changes
   → Verify all references are updated
   → Verify no data loss
```

### 4.10 Debug Error Alerts with the Error Infrastructure

**Scenario**: An alert fires (HighErrorRate, ServiceDown, DeadLetterQueueGrowth) and you need to trace the root cause from alert to code.

**MCP servers**: `prometheus`, `rabbitmq`, `redis`, `kubernetes`, `docker`, all `db-*` servers, `filesystem`, `sequential-thinking`

```
AI agent should:

1. Start with Prometheus to check what's firing:
   → Use prometheus: query "ALERTS{alertstate='firing'}"
   → Use prometheus: get rules (filter by type=alert)
   → Use prometheus: query "rate(http_requests_total{status=~'5..'}[5m])" to see 5xx rate

2. Check RabbitMQ dead-letter queues (if DLQ alert):
   → Use rabbitmq: list queues (filter for deadletter queues)
   → Use rabbitmq: get queue details for the DLQ
   → Use rabbitmq: get messages from DLQ to inspect payload

3. Find the correlation ID in recent errors:
   → Use kubernetes: kubectl logs deployment/his-hope-{service} -n his-hope --tail=100
   → Extract correlationId from structured log entries

4. Trace the specific failing request with db-*:
   → Use db-{service}_query: Query error tracking tables if available
   → Use db-{service}_query: "SELECT * FROM audit_log WHERE correlation_id = 'abc123'"

5. Cross-reference with Redis for token/auth issues:
   → Use redis: redis_list "session:*" to check active sessions
   → Use redis: redis_get "refresh_token:{token_hash}" to check token validity

6. Check Jaeger trace via docker (if traceId is known):
   → Use docker: docker exec his-hope-jaeger -- cli query traces --trace-id {traceId}

7. Read the relevant source code:
   → Use filesystem: search for the endpoint/handler mentioned in the error
   → Use filesystem: read the command handler that's failing

8. Use sequential-thinking to correlate findings:
   → "Alert X fired → found Y errors in logs → traced to Z handler → root cause is..."

Key metrics to query in Prometheus for error debugging:
```
# Error rate by service and status code
rate(http_requests_total{status=~"5.."}[5m])

# gRPC error rate by method
sum(rate(grpc_server_handled_total{grpc_code!="OK"}[5m])) by (grpc_method)

# Circuit breaker status
circuit_breaker_state{state="open"}

# Outbox backlog
rate(outbox_messages_total{status="pending"}[15m])

# Memory pressure (often causes 500s)
container_memory_usage_bytes / container_spec_memory_limit_bytes
```

---

### 4.11 Read/Search Code Files

**Scenario**: Need to understand how a specific feature is implemented.

**MCP servers**: `filesystem`

```
AI agent should:

1. Search for a class or interface:
   → Use filesystem: search for files matching "IEventBus*"
   → Or grep for "class PatientAggregate"

2. Read the directory structure:
   → Use filesystem: list directory "src/Services/PatientService"

3. Read specific files:
   → Use filesystem: read file "src/Services/PatientService/Api/Program.cs"

4. Search for patterns:
   → Use filesystem: search for "outbox_pattern_enabled" across all JSON files

5. Glob for specific file types:
   → Use filesystem: glob "**/*.proto"
   → Use filesystem: glob "**/*Migration*.cs"

6. Edit files (with review gates):
   → Use filesystem: edit file with exact text replacement
```

---

## 5. Troubleshooting Common Issues

### 5.1 MCP Server Not Starting / Error 32000

**Symptoms**:
- Error code `32000` when the AI agent tries to use a tool
- Server status shows as "failed" or "disconnected"
- The AI agent reports "tool not found"

**Root causes and fixes**:

| Cause | Likely Server | Fix |
|---|---|---|
| **npm package not installed** | Any `npx`-based server | Run `npm install -g <package>` or let `npx` auto-install (first run may be slow) |
| **npx resolution issue on Windows** | `github` (cmd /c) | Verify `github-mcp-server-mcp` is globally installed: `npm list -g` |
| **PostgreSQL not running** | All `db-*` servers | Check `docker ps \| findstr postgres`. Start via `docker compose up his-hope-postgres -d` |
| **Prometheus not running** | `prometheus` | Check `docker ps \| findstr prometheus`. Start via `docker compose up his-hope-prometheus -d` |
| **RabbitMQ not running** | `rabbitmq` | Check `docker ps \| findstr rabbitmq` |
| **Redis not running** | `redis` | Check `docker ps \| findstr redis` |
| **Kubeconfig missing** | `kubernetes` | Verify `~/.kube/config` exists. For local dev, k8s may not be available |
| **GitHub auth expired** | `github` | Run `gh auth login` or `gh auth status` to check |
| **Port conflict** | Any server | Check if the required port is already in use. Run `netstat -ano \| findstr :PORT` |
| **MCP server process crashed** | Any | Restart OpenCode. Permanent fix: check the server's logs for errors |

**General debug steps**:
```
1. Check if the required service/docker container is running:
   docker ps --format "table {{.Names}}\t{{.Status}}"

2. Test the MCP server manually:
   npx -y @modelcontextprotocol/server-postgres "postgresql://postgres:postgres@localhost:5433/identitydb"

3. Verify network reachability:
   Test-NetConnection -ComputerName localhost -Port 5433

4. Restart all Docker services:
   docker compose -f docker/docker-compose.yml restart

5. Relaunch OpenCode (MCP servers restart on launch)
```

### 5.2 npx Resolution Issues on Windows

**Problem**: `npx` may fail to find or install packages on Windows.

**Symptoms**:
```
Error: spawn npx ENOENT
Cannot find module '@modelcontextprotocol/server-postgres'
```

**Solutions**:
1. **Pre-install the package globally**:
   ```
   npm install -g @modelcontextprotocol/server-postgres
   npm install -g @modelcontextprotocol/server-filesystem
   npm install -g @modelcontextprotocol/server-sequential-thinking
   npm install -g @playwright/mcp
   npm install -g mcp-server-kubernetes
   npm install -g mcp-prometheus
   npm install -g mcp-server-docker
   npm install -g rabbitmq-mcp
   npm install -g @modelcontextprotocol/server-redis
   ```

2. **Clear npm cache** (if packages are corrupted):
   ```
   npm cache clean --force
   ```

3. **Ensure Node.js is in PATH**:
   ```
   where node
   where npx
   ```

4. **Use full path to npx** in `opencode.json` if needed (currently configured correctly)

### 5.3 Token/Auth Issues

**Symptoms**:
- GitHub operations fail with "Not authenticated"
- RabbitMQ returns 401
- Vault (if configured) returns permission errors

**Fixes**:
```
GitHub:
  gh auth status
  gh auth login           # Re-authenticate
  gh auth refresh         # Refresh token

RabbitMQ:
  # Verify credentials in opencode.json match the running instance
  docker exec his-hope-rabbitmq rabbitmqctl list_users

PostgreSQL:
  # Default credentials: postgres / postgres
  docker exec his-hope-postgres psql -U postgres -c "\du"
```

### 5.4 Container Not Running

**Symptoms**: MCP server connects, but queries return empty or connection refused.

**Check container status**:
```
docker ps -a --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

**Start all containers**:
```
docker compose -f docker/docker-compose.yml up -d
```

**Check specific container**:
```
docker inspect his-hope-postgres --format '{{.State.Status}}'
```

### 5.5 Port Conflicts

**Symptoms**: MCP server starts but tools return connection refused.

**Common ports**:
| Port | Service | Container |
|---|---|---|
| 5433 | PostgreSQL (mapped from 5432) | `his-hope-postgres` |
| 6379 | Redis | `his-hope-redis` |
| 15672 | RabbitMQ Management API | `his-hope-rabbitmq` |
| 9090 | Prometheus | `his-hope-prometheus` |
| 3000 | Grafana | `his-hope-grafana` |

**Debug**:
```
netstat -ano | findstr :5433
netstat -ano | findstr :6379
netstat -ano | findstr :15672
netstat -ano | findstr :9090
```

### 5.6 Using MCP Servers with the Error Infrastructure

The new error management infrastructure (see [Error Management Guide](error-management-guide.md)) exposes cross-cutting data that can be queried via MCP servers:

| Data | MCP Server | How to Query |
|---|---|---|
| **Correlation IDs in logs** | `docker` / `kubernetes` | `kubectl logs deploy/his-hope-{svc} -n his-hope --tail=100 \| findstr "correlationId"` |
| **Error tables in databases** | `db-*` (all 7 servers) | `db-{service}_query "SELECT * FROM error_log WHERE created_at > now() - interval '1 hour'"` |
| **Audit log with correlation ID** | `db-*` | `db-{service}_query "SELECT * FROM audit_log WHERE correlation_id = 'abc123'"` |
| **Error rate metrics** | `prometheus` | `prometheus_query "rate(http_requests_total{status=~'5..'}[5m])"` |
| **AlertManager status** | `prometheus` | `prometheus_query "ALERTS{alertstate='firing'}"` |
| **Dead-letter queues** | `rabbitmq` | `rabbitmq_list_queues` then filter for `deadletter` queues |
| **Outbox message backlog** | `db-*` | `db-{service}_query "SELECT count(*) FROM OutboxMessages WHERE processed_at IS NULL"` |
| **JWT token blacklist** | `redis` | `redis_list "token:blacklist:*"` |

**Typical error investigation workflow using MCP servers:**

```
1. prometheus_query: "ALERTS{alertstate='firing'}"          → Which alert?
2. rabbitmq: list queues with "deadletter" in name          → DLQ details?
3. db-patient_query: "SELECT * FROM error_log LIMIT 10"     → Recent errors?
4. kubernetes: kubectl logs deploy/his-hope-patient -n his-hope --tail=50
                                                              → Container logs?
5. filesystem: read source code of the failing handler       → Code fix?
```

### 5.7 MCP Server Timeout

**Symptoms**: Tool call hangs and eventually times out.

**Cause**: The MCP server process is busy or blocked. Some queries (e.g., large DB results) may take longer than the default timeout.

**Fixes**:
- Use more specific queries with `LIMIT` clauses
- Retry the operation
- Check if the server process has enough memory
- The OpenCode timeout is ~120s for tool calls

---

## 6. Adding New MCP Servers

To add a new MCP server to the project, edit `opencode.json` in the `mcp` section.

### Step 1: Identify the Package

Find an MCP-compatible server package. Common sources:
- [MCP GitHub Organization](https://github.com/modelcontextprotocol) — official servers
- [MCP Awesome List](https://github.com/punkpeye/awesome-mcp-servers) — community servers
- npm registry — search for `@modelcontextprotocol/server-*` or `mcp-server-*`

### Step 2: Add to opencode.json

```jsonc
"mcp": {
  // ... existing servers ...

  "my-new-server": {
    "type": "local",
    "command": ["npx", "-y", "mcp-server-package-name", "--arg1", "value1"],
    "enabled": true,
    "env": {
      "API_KEY": "your-key-here",
      "HOST": "localhost"
    }
  }
}
```

### Step 3: Determine the Command

| Pattern | Example |
|---|---|
| **Simple** (no args) | `["npx", "-y", "@modelcontextprotocol/server-sequential-thinking"]` |
| **With URL/DSN** | `["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://user:pass@host:port/db"]` |
| **With flags** | `["npx", "-y", "mcp-prometheus", "--url", "http://localhost:9090"]` |
| **With environment** | Add `"env": { "KEY": "value" }` (more secure than CLI args) |
| **Windows global binary** | `["cmd", "/c", "global-package-name"]` (for globally installed npm packages) |

### Step 4: Test the Server

1. Restart OpenCode (MCP servers are loaded at startup)
2. Ask the AI agent to list the tools from the new server
3. Run a simple tool invocation
4. Check OpenCode logs for errors

### Step 5: Document

Add the new server to this guide (Section 3) and notify the team.

---

## 7. Best Practices

### 7.1 For AI Agents

1. **Use the right server for the job**: Don't query `db-lab` for patient data. Each microservice has its own database for a reason.

2. **Prefer precise queries**: Use `SELECT specific_columns` instead of `SELECT *`. Add `LIMIT 100` to avoid overwhelming the agent context.

3. **Chain MCP tools strategically**: When debugging, start broad then narrow:
   ```
   ✅ Good: docker logs → kubernetes describe pod → db-* query → filesystem read code
   ❌ Bad: Reading 20 source files randomly before checking runtime state
   ```

4. **Use sequential-thinking for complex investigations**: Before diving into code changes, think through the problem step by step.

5. **Don't assume write access**: Most database MCP servers are read-only. Never attempt destructive operations without explicit confirmation from the architect agent.

6. **Handle timeouts gracefully**: If a tool call times out, retry with a more specific query or smaller scope.

7. **Verify preconditions**: Before using DB tools, verify the container is running. Before using k8s tools, verify a cluster exists.

### 7.2 For Developers

1. **Keep containers running**: MCP servers for databases, Redis, RabbitMQ, and Prometheus all depend on Docker containers. Keep `docker compose up -d` running in a terminal.

2. **Don't change default credentials**: Many MCP servers hardcode connection strings in `opencode.json`. If you change the PostgreSQL password in `docker-compose.yml`, update `opencode.json` accordingly.

3. **Monitor MCP server health**: If the AI agent reports an error like "tool not found", an MCP server may have crashed silently. Restart OpenCode to re-launch all servers.

4. **Global npm installs**: For stability, pre-install MCP packages globally:
   ```
   npm install -g @modelcontextprotocol/server-postgres
   npm install -g @playwright/mcp
   npm install -g mcp-server-kubernetes
   npm install -g mcp-prometheus
   npm install -g mcp-server-docker
   npm install -g rabbitmq-mcp
   npm install -g @modelcontextprotocol/server-redis
   npm install -g @modelcontextprotocol/server-filesystem
   npm install -g @modelcontextprotocol/server-sequential-thinking
   ```

5. **Security awareness**: MCP servers run as subprocesses of OpenCode with the same permissions as your user. The database servers connect with admin (`postgres:postgres`). In production, use restricted credentials.

6. **Use the `explore` agent for file searches**: The `@explore` agent is specialized for fast codebase exploration using `filesystem` and `glob` tools. For complex runtime investigations, delegate to specialized agents (`@dba`, `@devops`, etc.) who have the right MCP context.

### 7.3 Common Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|---|---|---|
| Querying all 7 databases to find a record | Wastes time and context | Know which service owns the data |
| `SELECT *` without `LIMIT` | Overwhelms agent context | Be selective with columns and rows |
| Reading entire files before checking runtime | Misses the obvious (service might be down) | Check logs and status first |
| Using DB tools when you need the filesystem | Schema is in EF Core configs, not just the DB | Check migrations + DBQuery |
| Editing `opencode.json` without restarting | Changes don't take effect until restart | Restart OpenCode after config changes |
| Chaining too many MCP tools in one prompt | Exceeds context window | Be systematic: investigate, report, then investigate more |

---

## 8. Appendix: opencode.json MCP Config

The full MCP configuration block from `opencode.json`:

```jsonc
"mcp": {
  "playwright": {
    "type": "local",
    "command": ["npx", "-y", "@playwright/mcp"],
    "enabled": true
  },
  "github": {
    "type": "local",
    "command": ["cmd", "/c", "github-mcp-server-mcp"],
    "enabled": true
  },
  "filesystem": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-filesystem", "D:\\AI\\micro"],
    "enabled": true
  },
  "db-identity": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/identitydb"],
    "enabled": true
  },
  "db-patient": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/patientdb"],
    "enabled": true
  },
  "db-appointment": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/appointmentdb"],
    "enabled": true
  },
  "db-clinical": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/clinicaldb"],
    "enabled": true
  },
  "db-lab": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/labdb"],
    "enabled": true
  },
  "db-billing": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/billingdb"],
    "enabled": true
  },
  "db-pharmacy": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-postgres", "postgresql://postgres:postgres@localhost:5433/pharmacydb"],
    "enabled": true
  },
  "sequential-thinking": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-sequential-thinking"],
    "enabled": true
  },
  "kubernetes": {
    "type": "local",
    "command": ["npx", "-y", "mcp-server-kubernetes"],
    "enabled": true
  },
  "rabbitmq": {
    "type": "local",
    "command": ["npx", "-y", "rabbitmq-mcp"],
    "enabled": true,
    "env": {
      "RABBITMQ_HOST": "localhost",
      "RABBITMQ_USERNAME": "admin",
      "RABBITMQ_PASSWORD": "admin",
      "RABBITMQ_MANAGEMENT_PORT": "15672",
      "RABBITMQ_PROTOCOL": "http"
    }
  },
  "redis": {
    "type": "local",
    "command": ["npx", "-y", "@modelcontextprotocol/server-redis", "redis://localhost:6379"],
    "enabled": true
  },
  "prometheus": {
    "type": "local",
    "command": ["npx", "-y", "mcp-prometheus", "--url", "http://localhost:9090"],
    "enabled": true
  },
  "docker": {
    "type": "local",
    "command": ["npx", "-y", "mcp-server-docker"],
    "enabled": true,
    "env": {
      "ALLOWED_CONTAINERS": "his-hope-postgres:his-hope-postgres,his-hope-redis:his-hope-redis,his-hope-rabbitmq:his-hope-rabbitmq,his-hope-elasticsearch:his-hope-elasticsearch,his-hope-jaeger:his-hope-jaeger,his-hope-prometheus:his-hope-prometheus,his-hope-grafana:his-hope-grafana,his-hope-identity:his-hope-identity,his-hope-patient:his-hope-patient,his-hope-appointment:his-hope-appointment,his-hope-clinical:his-hope-clinical,his-hope-lab:his-hope-lab,his-hope-billing:his-hope-billing,his-hope-pharmacy:his-hope-pharmacy,his-hope-gateway:his-hope-gateway,his-hope-frontend:his-hope-frontend",
      "DEFAULT_SERVICE": "his-hope-identity",
      "COMMAND_TIMEOUT": "300000"
    }
  }
}
```

### MCP Server Dependency Map

```
┌──────────────────────────────────────────────────────────────────┐
│                    MCP Server dependency on running services      │
│                                                                   │
│  Self-contained (no external deps):                               │
│    filesystem, sequential-thinking, github, playwright            │
│                                                                   │
│  Requires Docker container(s):                                    │
│    7x db-*  ────► his-hope-postgres (port 5433)                  │
│    redis     ────► his-hope-redis (port 6379)                    │
│    rabbitmq  ────► his-hope-rabbitmq (port 15672)                │
│    prometheus───► his-hope-prometheus (port 9090)                 │
│    docker    ────► Docker Desktop (named pipe)                    │
│                                                                   │
│  Requires external config files:                                  │
│    kubernetes───► ~/.kube/config                                  │
│    github    ────► gh CLI auth token                              │
└──────────────────────────────────────────────────────────────────┘
```

### Quick Reference Card

| I want to... | MCP Server | Tool / Action |
|---|---|---|
| Query patient data | `db-patient` | `db-patient_query` |
| Query identity data | `db-identity` | `db-identity_query` |
| Query appointment data | `db-appointment` | `db-appointment_query` |
| Query clinical data | `db-clinical` | `db-clinical_query` |
| Query lab data | `db-lab` | `db-lab_query` |
| Query billing data | `db-billing` | `db-billing_query` |
| Query pharmacy data | `db-pharmacy` | `db-pharmacy_query` |
| Read/write files | `filesystem` | Read, Write, Edit, Glob, Grep |
| Browse web UI | `playwright` | `browser_navigate`, `browser_snapshot` |
| Git/GitHub ops | `github` | PR, issues, commits, workflows |
| Kubernetes ops | `kubernetes` | `kubectl_get`, `kubectl_logs`, rollout |
| RabbitMQ ops | `rabbitmq` | List queues, bindings, exchanges |
| Redis inspection | `redis` | `redis_get`, `redis_list`, `redis_delete` |
| Prometheus queries | `prometheus` | `prometheus_query`, queryRange |
| Container commands | `docker` | `docker_exec`, `docker_run_command` |
| Complex reasoning | `sequential-thinking` | Step-by-step problem decomposition |
| Query error rates | `prometheus` | `prometheus_query "rate(http_requests_total{status=~'5..'}[5m])"` |
| Check firing alerts | `prometheus` | `prometheus_query "ALERTS{alertstate='firing'}"` |
| Inspect dead-letter queues | `rabbitmq` | List queues with "deadletter" in name |
| Query error tables | `db-*` | `db-{service}_query "SELECT * FROM error_log WHERE ..."` |
| Find correlation IDs in logs | `kubernetes` / `docker` | `kubectl logs` / `docker exec` with `correlationId` filter |
| Check outbox backlog | `db-*` | `db-{service}_query "SELECT count(*) FROM OutboxMessages WHERE processed_at IS NULL"` |

---

> **Last updated**: 2026-07-17 | **Maintainer**: @architect | **Next review**: 2026-08-17
