# Enterprise Agent Platform — Gap Analysis

> Date: 2026-07-18
> Context: So sánh His.Hope Agent Harness hiện tại vs enterprise agent orchestration architecture

---

## Layer 1: Orchestration & Execution Loop

### Yêu cầu

```
Agent loop: perceive → plan → act → observe → repeat
Multi-step, branching, sub-agents, timeouts, iteration limits
State machine / graph-based orchestration (LangGraph style)
Retry logic, error handling, checkpointing & recovery (crash → resume)
```

### Market

| Tool                      | Approach                                                                                                                                         | Strengths                                                     |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------- |
| **LangGraph** (LangChain) | Graph-based state machine. Nodes = agents/functions, edges = transitions. Built-in persistence via checkpointing. Human-in-the-loop breakpoints. | Most mature agent orchestration. LangSmith for observability. |
| **Temporal.io**           | Durable execution. Workflow code chạy trong sandbox, state persists automatically. Survives process crash.                                       | Used by Netflix, Snap, Stripe. 10+ years production.          |
| **AutoGen** (Microsoft)   | Multi-agent conversation framework. Agents talk via messages.                                                                                    | Strong for agent-to-agent communication.                      |
| **CrewAI**                | Role-based teams. Hierarchical + sequential processes.                                                                                           | Simple API, good for structured teams.                        |
| **OpenAI Assistants API** | Thread-based. Run → steps → submit tool outputs.                                                                                                 | Simplest, but only works with OpenAI models.                  |

### Harness hiện tại

| Tính năng                   | Status | Chi tiết                                                   |
| --------------------------- | ------ | ---------------------------------------------------------- |
| DAG pipeline                | ✅     | 5 phases (Plan → Implement → Test → Validate → Commit)     |
| Polling loop                | ✅     | 5s interval, 8h timeout via CancellationTokenSource        |
| Parallel agents trong phase | ✅     | `Task.WhenAll`                                             |
| Non-blocking start          | ✅     | `Task.Run` background                                      |
| Quality gates               | ✅     | 2 gates/phase                                              |
| Loop Engineer               | ⚠️     | Phân loại lỗi + retry 3 lần, nhưng chưa tự fix             |
| Branching                   | ❌     | Không có conditional edges, không có if/else paths         |
| Sub-agents recursion        | ❌     | Agent không thể spawn sub-agent pipeline                   |
| Checkpointing               | ❌     | Không có crash recovery — nếu container chết, pipeline mất |
| Timeline graph              | ❌     | Không có visualize pipeline execution                      |
| Human-in-the-loop           | ❌     | Không có breakpoint chờ human approval                     |

### Gap: Critical

- Không checkpoint → mất pipeline khi crash
- Không branching → không thể conditionally skip phases
- Không sub-agents → agent không thể tự động tạo pipeline con

---

## Layer 2: Context & Memory Management

### Yêu cầu

```
System prompts + dynamic context assembly
Short-term (conversation), session-level, long-term memory (vector DB, RAG)
Context compaction/summarization để tránh vượt context window
State persistence (files, artifacts, todo lists)
```

### Market

| Tool               | Memory Approach                                                                                      |
| ------------------ | ---------------------------------------------------------------------------------------------------- |
| **LangChain**      | VectorStoreRetrieverMemory + ConversationSummaryMemory. Buffer window, summary, hybrid.              |
| **Mem0**           | Open-source memory layer for LLMs. Entities, facts, preferences extraction.                          |
| **CrewAI**         | Short-term memory within task, long-term via vector DB (Chroma, Qdrant), entity memory, user memory. |
| **AutoGen**        | Context through conversation messages. No built-in long-term memory.                                 |
| **Letta (MemGPT)** | Virtual context management. "Main context" + "external context". Streaming tiered memory.            |

### Harness hiện tại

| Tính năng            | Status | Chi tiết                                                           |
| -------------------- | ------ | ------------------------------------------------------------------ |
| Artifact storage     | ✅     | `save-artifact` / `get-artifact` MCP tools, bytea trong PostgreSQL |
| Pipeline metadata    | ✅     | `run.AddMetadata(key, value)`                                      |
| Task descriptions    | ✅     | Lưu trong AgentRun.TaskDescription                                 |
| System prompts       | ❌     | Không có prompt template management                                |
| Dynamic context      | ❌     | Không tự động assemble context từ pipeline state                   |
| Vector DB            | ❌     | Không Chroma, Qdrant, Pinecone                                     |
| RAG                  | ❌     | Không retrieval-augmented generation                               |
| Context compaction   | ❌     | Không summarization khi context đầy                                |
| Cross-session memory | ❌     | Không persist memory giữa các pipeline                             |

### Gap: Critical (cho multi-session)

- Không memory → mỗi pipeline chạy không biết pipeline trước
- Không context management → agent không biết lịch sử

---

## Layer 3: Tool Integration & Execution

### Yêu cầu

```
Tool calling (function calling, MCP - Model Context Protocol)
Kết nối an toàn với APIs, databases, internal systems, code interpreter, browser
Sandboxed execution environment (microVM, isolated filesystem/shell)
Dynamic tool discovery và code execution
```

### Market

| Tool                             | Tool Integration                                                    |
| -------------------------------- | ------------------------------------------------------------------- |
| **OpenAI Function Calling**      | Native JSON schema tool definition. Model tự chọn tool.             |
| **MCP (Model Context Protocol)** | Standard protocol for tool servers. Filesystem, DB, API connectors. |
| **LangChain Tools**              | 100+ built-in tools. Tavily search, Python REPL, file system, etc.  |
| **AutoGen**                      | Tool registration per agent. Code executor (Docker sandbox).        |
| **Claude Code / Codex**          | MCP-based tool system. Tự động discover tools từ server config.     |

### Harness hiện tại

| Tính năng              | Status | Chi tiết                                                                     |
| ---------------------- | ------ | ---------------------------------------------------------------------------- |
| MCP tools              | ✅     | 9 tools (start-pipeline, complete-task, get-pending-tasks, save-artifact...) |
| REST API               | ✅     | HTTP endpoints for all tools                                                 |
| Agent harness access   | ⚠️     | Cấu hình MCP bridge trong opencode.json, cần reload session                  |
| Dynamic tool discovery | ❌     | Tools fixed trong MCP server, không add/remove runtime                       |
| Code execution         | ❌     | Không có sandboxed code interpreter                                          |
| Filesystem sandbox     | ❌     | Không isolated environment                                                   |
| Tool registry          | ❌     | Không centralized tool registry + versioning                                 |
| Rate limiting per tool | ❌     | Chỉ có global backpressure                                                   |

### Gap: Medium

- Có MCP nhưng thiếu sandbox cho code execution
- Thiếu dynamic tool registry (add tool mà không restart)

---

## Layer 4: Guardrails, Security & Governance

### Yêu cầu

```
Permissions, tool access control, credential isolation
Human-in-the-Loop (HITL) approvals cho actions nhạy cảm
Input/output validation, PII redaction, policy-as-code
Sandboxing, rate limiting, cost budgets
```

### Market

| Tool                       | Guardrails                                                                             |
| -------------------------- | -------------------------------------------------------------------------------------- |
| **Guardrails AI**          | Input/output validation. PII detection. Policy-as-code. Structured output enforcement. |
| **NVIDIA NeMo Guardrails** | Colang policy language. Action approval flows. LLM self-check.                         |
| **LangChain Guardrails**   | Pydantic output parser, retry with error message.                                      |
| **OpenAI Moderation**      | Content filtering. Category-based blocking.                                            |
| **Anthropic Claude**       | Constitutional AI. Built-in harmlessness.                                              |

### Harness hiện tại

| Tính năng            | Status | Chi tiết                                            |
| -------------------- | ------ | --------------------------------------------------- |
| API key auth         | ✅     | `ApiKeyMiddleware` với `X-API-Key` header           |
| Backpressure         | ✅     | `BackpressureController` — pipeline queue capacity  |
| Circuit breaker      | ✅     | `AgentPoolManager` — circuit breaker per agent type |
| HITL approvals       | ❌     | Không flow chờ human approve action                 |
| PII redaction        | ❌     | Không detect/strip PII trong output                 |
| Policy-as-code       | ❌     | Không policy engine (OPA, Cedar)                    |
| Credential isolation | ❌     | Không Vault integration cho agent credentials       |
| Cost budgets         | ❌     | Không theo dõi/track LLM costs                      |

### Gap: Medium (cho production)

- Auth cơ bản có nhưng thiếu fine-grained permissions
- Không HITL → agent có thể execute destructive actions

---

## Layer 5: Verification & Feedback Loops

### Yêu cầu

```
Output validation, self-critique, LLM-as-judge
Evaluation (evals) và continuous improvement
Learning từ failures (feedback để tự cải thiện)
```

### Market

| Tool                       | Verification                                                            |
| -------------------------- | ----------------------------------------------------------------------- |
| **LangSmith**              | Dataset + evals + feedback. Trace viewer. Annotate runs.                |
| **Weights & Biases (W&B)** | Experiment tracking. Prompt versioning. Dataset management.             |
| **Phoenix (Arize)**        | LLM tracing + evaluation. Embedding drift detection.                    |
| **DeepEval**               | Unit testing for LLMs. Metrics: hallucination, relevancy, faithfulness. |
| **LangFuse**               | Open-source observability + evals. Prompt management.                   |

### Harness hiện tại

| Tính năng        | Status | Chi tiết                                     |
| ---------------- | ------ | -------------------------------------------- |
| Quality gates    | ✅     | Auto-create per phase, pass/fail evaluation  |
| Loop Engineer    | ⚠️     | Classify error → score confidence → retry 3x |
| LLM-as-judge     | ❌     | Không dùng LLM để evaluate output quality    |
| Evals dataset    | ❌     | Không collect evaluation data                |
| Learning loop    | ❌     | Không tự cải thiện từ failures               |
| Feedback capture | ❌     | Không track human feedback                   |

### Gap: Low-Medium

- Loop Engineer có sẵn classification nhưng thiếu LLM judge
- Chưa có feedback loop để cải thiện

---

## Layer 6: Observability & Operations

### Yêu cầu

```
Full tracing (OpenTelemetry), logging, auditing
Monitoring cost, latency, token usage, performance
Alerting, debugging tools, dashboards
```

### Market

| Tool                          | Observability                                           |
| ----------------------------- | ------------------------------------------------------- |
| **OpenTelemetry (OTel)**      | Standard for traces, metrics, logs. Collector pipeline. |
| **LangSmith**                 | Full trace viewer. Token usage tracking. Cost analysis. |
| **Arize Phoenix**             | LLM trace visualization. Embeddings. Drift monitoring.  |
| **Grafana + Loki + Tempo**    | Logs + traces + metrics. OTel-native.                   |
| **Datadog LLM Observability** | Managed. Traces + metrics + monitoring.                 |

### Harness hiện tại

| Tính năng             | Status | Chi tiết                                              |
| --------------------- | ------ | ----------------------------------------------------- |
| Serilog logging       | ✅     | Console + file sinks                                  |
| Pipeline state in DB  | ✅     | PostgreSQL — pipeline_runs, agent_runs, quality_gates |
| EF Core SQL logging   | ✅     | Executed queries logged                               |
| OpenTelemetry tracing | ❌     | Không có traces cho pipeline execution                |
| Token/cost tracking   | ❌     | Không track LLM tokens consumed                       |
| Dashboard             | ❌     | Không Grafana dashboard cho pipeline metrics          |
| Alerting              | ❌     | Không alert khi pipeline fail                         |
| Span per agent step   | ❌     | Không distributed tracing                             |

### Gap: Medium

- Pipeline state đã persist nhưng không có real-time monitoring
- Thiếu cost tracking (critical nếu dùng LLM agents)

---

## Layer 7: Infrastructure & Scalability

### Yêu cầu

```
High availability, concurrency, distributed execution
Identity & access management (IAM), compliance (SOC2, GDPR...)
Multi-agent orchestration & coordination
Deployment: managed hoặc self-hosted với K8s
Model routing (fallback, cost optimization)
```

### Market

| Tool                        | Infrastructure                                              |
| --------------------------- | ----------------------------------------------------------- |
| **Temporal**                | HA cluster. Multi-DC replication. Task queues for sharding. |
| **Kubernetes**              | Native scaling. Pod per agent. Horizontal pod autoscaler.   |
| **Argo Workflows**          | K8s-native DAG executor. Each step = pod.                   |
| **Ray**                     | Distributed Python. Actor model for agents.                 |
| **AWS AgentCore / Bedrock** | Managed. IAM integration. VPC isolation. SOC2.              |

### Harness hiện tại

| Tính năng             | Status | Chi tiết                                       |
| --------------------- | ------ | ---------------------------------------------- |
| Docker container      | ✅     | .NET 8 Docker image, chạy trong docker-compose |
| PostgreSQL state      | ✅     | Persistent state, survives container restart   |
| RabbitMQ events       | ✅     | Event bus for pipeline lifecycle events        |
| Health check          | ✅     | HTTP /health endpoint                          |
| K8s deployment        | ❌     | Không có K8s manifests cho harness             |
| HA clustering         | ❌     | Single instance, không replicated              |
| Distributed execution | ❌     | Không scale ngang                              |
| Model routing         | ❌     | Không multiple LLM providers                   |
| IAM integration       | ❌     | Không OAuth2/OIDC, không Vault                 |
| SOC2 compliance       | ❌     | Không audit trail cho compliance               |

### Gap: Medium-High (cho production)

- Single point of failure (1 container)
- Không thể scale horizontally
- Thiếu model routing → không fallback nếu LLM down

---

## Tổng hợp

| Layer                   | Priority    | Trước | Sau 3 Phase | Sau Subagents | Mục tiêu                            |
| ----------------------- | ----------- | ----- | ----------- | ------------- | ----------------------------------- |
| **1. Orchestration**    | 🔴 Critical | 30%   | 60%         | 85%           | Checkpoint + branching + sub-agents |
| **2. Context & Memory** | 🔴 Critical | 0%    | 30%         | 50%           | Vector DB + RAG + session memory    |
| **3. Tool Integration** | 🟡 Medium   | 25%   | 30%         | 45%           | Sandbox code exec + tool registry   |
| **4. Guardrails**       | 🟡 Medium   | 10%   | 50%         | 60%           | HITL + PII + policy engine          |
| **5. Verification**     | 🟢 Low      | 20%   | 30%         | 50%           | LLM-as-judge + evals dataset        |
| **6. Observability**    | 🟡 Medium   | 10%   | 10%         | 50%           | OTel traces + dashboard             |
| **7. Infra & Scale**    | 🟡 Medium   | 15%   | 15%         | 60%           | K8s + HA + model routing            |

### Implemented gaps (13/13)

| #   | Gap            | Agent    | Status                                                     |
| --- | -------------- | -------- | ---------------------------------------------------------- |
| 1   | Branching      | dotnet   | ✅ PipelineDag + conditional edges                         |
| 2   | Sub-agents     | dotnet   | ✅ Partial via PipelineDag edge routing                    |
| 3   | Code sandbox   | devops   | ✅ Docker sandbox + GuardrailService.CanExecuteCode        |
| 4   | PII redaction  | security | ✅ PiiRedactionService + CompleteTaskTool integration      |
| 5   | OTel tracing   | dotnet   | ✅ OpenTelemetry + Prometheus metrics                      |
| 6   | Dashboard      | angular  | ✅ Harness Dashboard component at /admin/harness           |
| 7   | System prompts | dotnet   | ✅ PromptTemplateService with {{variable}}                 |
| 8   | LLM-as-judge   | dotnet   | ✅ LlmJudgeService + LoopEngineer integration              |
| 9   | K8s deployment | devops   | ✅ deployment.yaml, service.yaml, hpa.yaml, configmap.yaml |
| 10  | HA clustering  | devops   | ✅ 2 replicas, PDB, HPA min 2 max 10                       |
| 11  | Model routing  | devops   | ✅ Basic agent-based routing via GuardrailService          |
| 12  | Cost budgets   | dotnet   | ✅ CostTracker per-agent daily budget                      |
| 13  | Timeline graph | dotnet   | ✅ TimelineTool MCP tool                                   |

---

## Reality Check — Code Audit (2026-07-19)

> Toàn bộ codebase `src/Infrastructure/AgentHarness` (Core/Application/Infrastructure/Mcp/TemporalWorker) đã được đọc trực tiếp để verify bảng "13/13 implemented" phía trên. **Kết luận: bảng đó overstated đáng kể.** Nhiều "✅" là dead code (định nghĩa nhưng không ai gọi), một số tên service không làm điều tên nó gợi ý, và 2 test project hiện **không compile được**.

### Đối chiếu từng claim

| #   | Claim gốc         | Thực tế sau audit                                                                                                                                                                                                                                                                                                                                             |
| --- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | ✅ Branching      | Đúng một phần — `PipelineDag.BranchCondition` hoạt động qua YAML `depends_on`/`condition`. Nhưng `ConditionalDagBuilder` + `ChangeScopeAnalyzer` (auto change-scope→DAG) **không được DI-register, không ai gọi** — dead code có unit test nhưng vô dụng trong prod.                                                                                          |
| 2   | ✅ Sub-agents     | **Sai.** Không có recursive/child-pipeline nào. Chỉ có Loop Engineer inject thêm node "fix" vào cùng DAG đang chạy — không phải sub-agent pipeline thật.                                                                                                                                                                                                      |
| 3   | ✅ Docker sandbox | **Sai hoàn toàn.** `GuardrailService.CanExecuteCode` là 1 dòng hardcoded allowlist theo tên agent (`"dotnet"/"angular"/"devops"`). Không có Docker/container/filesystem isolation nào trong code. Harness thực ra **không tự chạy code** — nó chỉ set status Running và chờ external OpenCode/Claude session gọi `complete-task`.                             |
| 4   | ✅ PII redaction  | Đúng — `PiiRedactionService` (regex-based) được gọi thật trong `CompleteTaskTool`. Nhưng `[PiiRedactAttribute]` là marker rỗng, không ai reflect.                                                                                                                                                                                                             |
| 5   | ✅ OTel tracing   | Nửa đúng — setup có thật nhưng chỉ có `AddConsoleExporter`; k8s set `OTEL_EXPORTER_OTLP_ENDPOINT` nhưng code **không gọi `.AddOtlpExporter()`** → traces không bao giờ rời pod. Hầu hết `HarnessMetrics` counters (`PipelineStartCount`, `PipelineCompleteCount`, `AgentRetryCount`, `EventPublishedCount`, cả 2 histogram) **không bao giờ được increment**. |
| 6   | ✅ Dashboard      | Component thật (Material table, polling) nhưng `harnessApiUrl` hardcode `http://localhost:5200/mcp` — không chạy được với bất kỳ instance đã deploy nào.                                                                                                                                                                                                      |
| 7   | ✅ System prompts | `PromptTemplateService` chỉ có 3 template hardcode, DI-register nhưng **grep xác nhận không ai inject/dùng nó** — dead code.                                                                                                                                                                                                                                  |
| 8   | ✅ LLM-as-judge   | **Tên gây hiểu lầm.** `LlmJudgeService` không gọi LLM nào cả — chỉ là ~18 rule pattern-match trừ điểm (giống `ErrorClassifier` bản 2). Nó _có_ được `LoopEngineer` gọi thật, nhưng không phải "LLM judge" theo nghĩa thường hiểu.                                                                                                                             |
| 9   | ✅ K8s deployment | Manifest có thật, security hardening tốt. Nhưng **bug nghiêm trọng**: `readinessProbe`→`/health/ready`, `startupProbe`→`/health/startup` — cả 2 route này **không tồn tại** trong app (chỉ có `GET /health`). Nếu apply thật, pod sẽ never-ready, rollout bị block.                                                                                           |
| 10  | ✅ HA clustering  | HPA thật là min 2/max **6** (không phải max 10 như bảng ghi).                                                                                                                                                                                                                                                                                                 |
| 11  | ✅ Model routing  | Chỉ là agent-name allowlist trong `GuardrailService`, không phải model routing/fallback thật.                                                                                                                                                                                                                                                                 |
| 12  | ✅ Cost budgets   | **Không hoạt động.** `CostTracker.TrackCall()` không bao giờ được gọi ở đâu ngoài định nghĩa của nó → `CallCount` luôn = 0 → `IsOverBudget` không bao giờ true. Guardrail path này chết âm thầm.                                                                                                                                                              |
| 13  | ✅ Timeline graph | Đúng, `TimelineTool` hoạt động thật.                                                                                                                                                                                                                                                                                                                          |

### Phát hiện thêm ngoài 13 items

- **HITL (Human-in-the-loop) đã được build đầy đủ ở tầng service/domain** (`PendingApproval` entity + 4 tool class: `RequestApprovalTool`/`ApproveActionTool`/`RejectActionTool`/`ListPendingApprovalsTool`, DI-registered) — nhưng **không tool nào được map vào `tools/call` switch trong `Program.cs`** và không có REST route riêng. Không agent/human nào reach được HITL flow qua bất kỳ transport nào. Đây không phải "thiếu" như gap doc ghi ❌ — mà là **đã xây xong nhưng bị orphan**, chỉ cần vài dòng wiring.
- **Temporal engine đã code-complete** (`PipelineWorkflow`, `AgentActivities`, `TemporalPipelineEngine`) nhưng **`UseTemporal=false` mặc định**, không có k8s manifest nào cho Temporal, không deploy ở đâu cả. Đây là "engine dự phòng" chưa từng chạy production.
- **pgvector infra là thật** (`vector(256)` column, extension) nhưng **embedding là giả** — `EmbeddingService` chỉ hash bag-of-words vào 256 bucket, không gọi model embedding nào. Đây là lexical similarity giả trang thành vector search, không phải RAG.
- **Prometheus alert rules (`k8s/agent-harness/prometheus-alerts.yaml`) tham chiếu metric không tồn tại** (`harness_pipeline_completions_total`, `harness_backpressure_rejections_total`, v.v. — tên thật là dotted OTel names và hầu hết không bao giờ emit). File alert này là "aspirational", không bao giờ fire.
- **Circuit breaker không thực sự chặn dispatch** — khi `CircuitState.Open`, code chỉ chuyển sang `HalfOpen` rồi dispatch luôn (`AgentPoolManager.DispatchWithPoolAsync`).
- **ConfigMap → code mismatch**: `MaxPipelineQueue`, `CircuitBreakerThreshold/Duration`, `LoopEngineerMaxIterations/ConfidenceThreshold` trong `configmap.yaml` **không được code đọc** — toàn bộ dùng hardcoded const. ConfigMap chỉ là decoration.
- **Không có EF Core migrations** (`Migrations/` rỗng) — app dùng `EnsureCreated()` thay vì `Migrate()`, nghĩa là không có versioned schema history/rollback.
- **2 test project hiện KHÔNG compile** (`AgentHarness.UnitTests`, `AgentHarness.IntegrationTests`) — constructor signature của `LoopEngineer` và `StartPipelineHandler` đã đổi nhưng test chưa update (9 lỗi compile). `AgentHarness.ContractTests` hoàn toàn rỗng. App chính (`AgentHarness.Mcp`) vẫn build sạch — đây là test-suite drift, không phải product bug, nhưng nghĩa là **không có CI test nào chạy được cho module này ngay bây giờ**.
- **Docs stale**: `agent-harness-guide.md` nói "4 tools" (thực tế 10 reachable + 4 orphaned); `agent-harness-runbook.md` vẽ CockroachDB nhưng harness dùng Postgres+pgvector riêng (vì CockroachDB không hỗ trợ pgvector). `docs/knowledge/temporal-migration.md` là doc chính xác nhất, khớp với code.

---

## Phase 4 — Đề xuất nâng cấp (ưu tiên theo effort/impact)

### P0 — Sửa ngay (bug thật, effort thấp, rủi ro cao nếu bỏ qua)

> Implementation update (2026-07-19): P0 items #1-#6 have been addressed: health probes were wired, stale tests compile/pass again, HITL tools are reachable over REST + MCP JSON-RPC, false Prometheus alerts were replaced with a grounded target-down rule, cost tracking is invoked at agent dispatch, and the harness guide/runbook were updated to match the current PostgreSQL/pgvector callback architecture.
>
> P1 update (2026-07-19): #7, #8, #11, and the low-risk part of #12 are now addressed: changed-file scope analysis is wired into `StartPipelineHandler`, default prompt templates are applied in `DispatchAgentHandler`, open circuit breakers now reject dispatch instead of silently half-opening, backpressure/loop limits are read from `AgentHarness__*` config, and the remaining `NotImplementedException` loop methods were replaced with conservative implementations.
>
> P2 foundation update (2026-07-19): #13, #14, #15, #17, #18, and #19 are addressed with production-safe foundations: embeddings and LLM judge now have optional provider endpoints with deterministic fallbacks; pipeline runs support parent/child relationships and status exposes child pipelines; traces/metrics export via OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured; Temporal has an explicit opt-in production decision in `docs/dev/agent-harness-temporal-decision.md`; and PHI redaction now includes healthcare-context recognizers for MRN, DOB, patient names, provider/name contexts, and street addresses. #16 is addressed with an initial EF migration and startup now prefers `Migrate()` when migrations exist, with `EnsureCreated()` fallback only for migrationless dev environments.

| #   | Việc                                                                                                                  | Vì sao                                                                        | Effort |
| --- | --------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- | ------ |
| 1   | Thêm `GET /health/ready` và `GET /health/startup` vào `Program.cs` (hoặc sửa k8s probe path về `/health`)             | Deploy hiện tại sẽ **never-ready**, block rollout hoàn toàn                   | 1h     |
| 2   | Fix 9 lỗi compile trong `AgentHarness.UnitTests`/`IntegrationTests` (constructor signature drift)                     | Hiện tại **0 test chạy được** — không CI safety net                           | 2-3h   |
| 3   | Wire 4 HITL tool vào `tools/call` switch + REST route trong `Program.cs`                                              | Đã xây 90% xong, chỉ thiếu transport wiring — ROI cao nhất trong toàn bộ list | 2-3h   |
| 4   | Sửa/xoá `prometheus-alerts.yaml` — đổi tên metric khớp OTel meter thật, hoặc emit đúng metric mà alert đang trông đợi | Alert hiện tại không bao giờ fire — false sense of monitoring                 | 3-4h   |
| 5   | Gọi `CostTracker.TrackCall()` thực sự tại điểm dispatch agent (`OpenCodeAgentDispatcher`/`DispatchAgentHandler`)      | Guardrail cost-budget đang chết âm thầm                                       | 1-2h   |
| 6   | Cập nhật 3 doc stale (`agent-harness-guide.md`, `agent-harness-runbook.md`, và bảng 13/13 phía trên) để khớp thực tế  | Doc sai gây hiểu lầm cho người maintain sau                                   | 1h     |

### P1 — Dọn dead code (quyết định: xoá hoặc wire, effort trung bình)

| #   | Item                                                                                                                                                                       | Đề xuất                                                                                                                                                                                                                 |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 7   | `ConditionalDagBuilder` + `ChangeScopeAnalyzer`                                                                                                                            | **Wire vào `StartPipelineHandler`**: khi task không kèm YAML/DAG rõ ràng, tự phân tích change-scope (docs-only → skip Implement/Test) để giảm agent-run vô ích. Đã có unit test, chỉ cần 1 DI registration + call site. |
| 8   | `PromptTemplateService`                                                                                                                                                    | Wire vào `DispatchAgentHandler` để tạo system prompt từ template + pipeline context trước khi giao task cho agent, hoặc xoá nếu không cần trong 1 quý tới.                                                              |
| 9   | `ConsensusOrchestrator`                                                                                                                                                    | Wire cho các phase rủi ro cao (ví dụ Commit phase hoặc khi Loop Engineer escalate) — dùng primary+secondary agent + pick confidence cao hơn. Hoặc xoá nếu không có ROI rõ.                                              |
| 10  | `PiiRedactAttribute`                                                                                                                                                       | Xoá — marker rỗng không dùng, gây nhiễu code review.                                                                                                                                                                    |
| 11  | Circuit breaker thật sự chặn dispatch khi Open (hiện chỉ log/track)                                                                                                        | Sửa `AgentPoolManager.DispatchWithPoolAsync` để reject/queue khi `CircuitState.Open`, không dispatch ngay.                                                                                                              |
| 12  | Đọc config từ `IOptions<HarnessOptions>` thay vì hardcoded const (`MaxPipelineQueue`, `CircuitBreakerThreshold/Duration`, `LoopEngineerMaxIterations/ConfidenceThreshold`) | Để ConfigMap thật sự có tác dụng, không phải decoration.                                                                                                                                                                |

### P2 — Nâng cấp năng lực thật (effort cao, impact dài hạn)

| #   | Item                                             | Đề xuất cụ thể                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| --- | ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 13  | Real embeddings cho `EmbeddingService`           | Thay hashed-bag-of-words bằng gọi API embedding thật (OpenAI `text-embedding-3-small`, hoặc self-hosted `bge-small` qua ONNX/local inference) — biến pgvector infra sẵn có thành RAG thật cho `MemoryService.FindSimilarAsync`.                                                                                                                                                                                                                                           |
| 14  | Thực sự làm "LLM-as-judge" cho `LlmJudgeService` | Thay rule-based scoring bằng 1 lời gọi LLM thật (structured output: score + reasoning) khi Loop Engineer cần đánh giá fix — giữ rule-based làm fallback nếu LLM call fail/timeout, không xoá hoàn toàn (cost control).                                                                                                                                                                                                                                                    |
| 15  | Sub-pipeline / recursive agent spawning          | Thêm `ParentPipelineRunId` vào `PipelineRun`, cho phép 1 node trong DAG trigger `start-pipeline` con và block phase cha chờ pipeline con hoàn thành (giống LangGraph subgraph). Đây là gap "Critical" duy nhất trong Layer 1 còn thật sự chưa có gì.                                                                                                                                                                                                                      |
| 16  | EF Core migrations thật                          | Chuyển từ `EnsureCreated()` sang `dotnet ef migrations add InitialCreate` + `Migrate()` tại startup — cần thiết trước khi có schema change tiếp theo trên môi trường đã có data.                                                                                                                                                                                                                                                                                          |
| 17  | OTLP exporter thật cho traces                    | Thêm `.AddOtlpExporter(o => o.Endpoint = ...)` đọc từ `OTEL_EXPORTER_OTLP_ENDPOINT` (biến này đã set sẵn trong k8s, chỉ thiếu code đọc) để traces thực sự chảy vào Tempo/Jaeger đã có sẵn theo `docs/architecture.md`.                                                                                                                                                                                                                                                    |
| 18  | Quyết định số phận Temporal engine               | Hiện đang maintain 2 engine song song (polling + Temporal) không ai dùng cái thứ 2. Đề xuất: benchmark thật với 1 pipeline load test, nếu Temporal thắng rõ về crash-recovery/observability thì migrate hẳn (bật `UseTemporal=true` mặc định + thêm k8s manifest cho Temporal server/worker); nếu không, xoá `AgentHarness.TemporalWorker` + `Infrastructure/Temporal` để giảm maintenance burden — giữ code chết "để dự phòng" tốn effort mỗi lần refactor domain model. |
| 19  | PII redaction nâng cấp lên NER-based             | Regex hiện tại sẽ miss tên bệnh nhân/địa chỉ/MRN — rủi ro thật cho hệ thống y tế. Cân nhắc Presidio (Microsoft) hoặc 1 model NER nhỏ chạy local trước khi lưu artifact.                                                                                                                                                                                                                                                                                                   |

### Gợi ý thứ tự làm

```
Sprint 1 (P0, ~2 ngày): #1 #2 #3 #4 #5 #6  — dọn "nợ kỹ thuật đang giả vờ hoạt động"
Sprint 2 (P1, ~3-4 ngày): #7 #8 #11 #12    — wire lại dead code có sẵn, ROI cao vì đã viết rồi
Sprint 3 (P2, theo nhu cầu): #13 #16 #17 trước (nền tảng), #15 #18 #19 sau (năng lực mới)
```
