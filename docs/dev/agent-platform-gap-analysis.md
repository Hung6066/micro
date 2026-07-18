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

| Tool | Approach | Strengths |
|------|----------|-----------|
| **LangGraph** (LangChain) | Graph-based state machine. Nodes = agents/functions, edges = transitions. Built-in persistence via checkpointing. Human-in-the-loop breakpoints. | Most mature agent orchestration. LangSmith for observability. |
| **Temporal.io** | Durable execution. Workflow code chạy trong sandbox, state persists automatically. Survives process crash. | Used by Netflix, Snap, Stripe. 10+ years production. |
| **AutoGen** (Microsoft) | Multi-agent conversation framework. Agents talk via messages. | Strong for agent-to-agent communication. |
| **CrewAI** | Role-based teams. Hierarchical + sequential processes. | Simple API, good for structured teams. |
| **OpenAI Assistants API** | Thread-based. Run → steps → submit tool outputs. | Simplest, but only works with OpenAI models. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| DAG pipeline | ✅ | 5 phases (Plan → Implement → Test → Validate → Commit) |
| Polling loop | ✅ | 5s interval, 8h timeout via CancellationTokenSource |
| Parallel agents trong phase | ✅ | `Task.WhenAll` |
| Non-blocking start | ✅ | `Task.Run` background |
| Quality gates | ✅ | 2 gates/phase |
| Loop Engineer | ⚠️ | Phân loại lỗi + retry 3 lần, nhưng chưa tự fix |
| Branching | ❌ | Không có conditional edges, không có if/else paths |
| Sub-agents recursion | ❌ | Agent không thể spawn sub-agent pipeline |
| Checkpointing | ❌ | Không có crash recovery — nếu container chết, pipeline mất |
| Timeline graph | ❌ | Không có visualize pipeline execution |
| Human-in-the-loop | ❌ | Không có breakpoint chờ human approval |

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

| Tool | Memory Approach |
|------|-----------------|
| **LangChain** | VectorStoreRetrieverMemory + ConversationSummaryMemory. Buffer window, summary, hybrid. |
| **Mem0** | Open-source memory layer for LLMs. Entities, facts, preferences extraction. |
| **CrewAI** | Short-term memory within task, long-term via vector DB (Chroma, Qdrant), entity memory, user memory. |
| **AutoGen** | Context through conversation messages. No built-in long-term memory. |
| **Letta (MemGPT)** | Virtual context management. "Main context" + "external context". Streaming tiered memory. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| Artifact storage | ✅ | `save-artifact` / `get-artifact` MCP tools, bytea trong PostgreSQL |
| Pipeline metadata | ✅ | `run.AddMetadata(key, value)` |
| Task descriptions | ✅ | Lưu trong AgentRun.TaskDescription |
| System prompts | ❌ | Không có prompt template management |
| Dynamic context | ❌ | Không tự động assemble context từ pipeline state |
| Vector DB | ❌ | Không Chroma, Qdrant, Pinecone |
| RAG | ❌ | Không retrieval-augmented generation |
| Context compaction | ❌ | Không summarization khi context đầy |
| Cross-session memory | ❌ | Không persist memory giữa các pipeline |

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

| Tool | Tool Integration |
|------|------------------|
| **OpenAI Function Calling** | Native JSON schema tool definition. Model tự chọn tool. |
| **MCP (Model Context Protocol)** | Standard protocol for tool servers. Filesystem, DB, API connectors. |
| **LangChain Tools** | 100+ built-in tools. Tavily search, Python REPL, file system, etc. |
| **AutoGen** | Tool registration per agent. Code executor (Docker sandbox). |
| **Claude Code / Codex** | MCP-based tool system. Tự động discover tools từ server config. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| MCP tools | ✅ | 9 tools (start-pipeline, complete-task, get-pending-tasks, save-artifact...) |
| REST API | ✅ | HTTP endpoints for all tools |
| Agent harness access | ⚠️ | Cấu hình MCP bridge trong opencode.json, cần reload session |
| Dynamic tool discovery | ❌ | Tools fixed trong MCP server, không add/remove runtime |
| Code execution | ❌ | Không có sandboxed code interpreter |
| Filesystem sandbox | ❌ | Không isolated environment |
| Tool registry | ❌ | Không centralized tool registry + versioning |
| Rate limiting per tool | ❌ | Chỉ có global backpressure |

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

| Tool | Guardrails |
|------|------------|
| **Guardrails AI** | Input/output validation. PII detection. Policy-as-code. Structured output enforcement. |
| **NVIDIA NeMo Guardrails** | Colang policy language. Action approval flows. LLM self-check. |
| **LangChain Guardrails** | Pydantic output parser, retry with error message. |
| **OpenAI Moderation** | Content filtering. Category-based blocking. |
| **Anthropic Claude** | Constitutional AI. Built-in harmlessness. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| API key auth | ✅ | `ApiKeyMiddleware` với `X-API-Key` header |
| Backpressure | ✅ | `BackpressureController` — pipeline queue capacity |
| Circuit breaker | ✅ | `AgentPoolManager` — circuit breaker per agent type |
| HITL approvals | ❌ | Không flow chờ human approve action |
| PII redaction | ❌ | Không detect/strip PII trong output |
| Policy-as-code | ❌ | Không policy engine (OPA, Cedar) |
| Credential isolation | ❌ | Không Vault integration cho agent credentials |
| Cost budgets | ❌ | Không theo dõi/track LLM costs |

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

| Tool | Verification |
|------|--------------|
| **LangSmith** | Dataset + evals + feedback. Trace viewer. Annotate runs. |
| **Weights & Biases (W&B)** | Experiment tracking. Prompt versioning. Dataset management. |
| **Phoenix (Arize)** | LLM tracing + evaluation. Embedding drift detection. |
| **DeepEval** | Unit testing for LLMs. Metrics: hallucination, relevancy, faithfulness. |
| **LangFuse** | Open-source observability + evals. Prompt management. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| Quality gates | ✅ | Auto-create per phase, pass/fail evaluation |
| Loop Engineer | ⚠️ | Classify error → score confidence → retry 3x |
| LLM-as-judge | ❌ | Không dùng LLM để evaluate output quality |
| Evals dataset | ❌ | Không collect evaluation data |
| Learning loop | ❌ | Không tự cải thiện từ failures |
| Feedback capture | ❌ | Không track human feedback |

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

| Tool | Observability |
|------|---------------|
| **OpenTelemetry (OTel)** | Standard for traces, metrics, logs. Collector pipeline. |
| **LangSmith** | Full trace viewer. Token usage tracking. Cost analysis. |
| **Arize Phoenix** | LLM trace visualization. Embeddings. Drift monitoring. |
| **Grafana + Loki + Tempo** | Logs + traces + metrics. OTel-native. |
| **Datadog LLM Observability** | Managed. Traces + metrics + monitoring. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| Serilog logging | ✅ | Console + file sinks |
| Pipeline state in DB | ✅ | PostgreSQL — pipeline_runs, agent_runs, quality_gates |
| EF Core SQL logging | ✅ | Executed queries logged |
| OpenTelemetry tracing | ❌ | Không có traces cho pipeline execution |
| Token/cost tracking | ❌ | Không track LLM tokens consumed |
| Dashboard | ❌ | Không Grafana dashboard cho pipeline metrics |
| Alerting | ❌ | Không alert khi pipeline fail |
| Span per agent step | ❌ | Không distributed tracing |

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

| Tool | Infrastructure |
|------|---------------|
| **Temporal** | HA cluster. Multi-DC replication. Task queues for sharding. |
| **Kubernetes** | Native scaling. Pod per agent. Horizontal pod autoscaler. |
| **Argo Workflows** | K8s-native DAG executor. Each step = pod. |
| **Ray** | Distributed Python. Actor model for agents. |
| **AWS AgentCore / Bedrock** | Managed. IAM integration. VPC isolation. SOC2. |

### Harness hiện tại

| Tính năng | Status | Chi tiết |
|-----------|--------|----------|
| Docker container | ✅ | .NET 8 Docker image, chạy trong docker-compose |
| PostgreSQL state | ✅ | Persistent state, survives container restart |
| RabbitMQ events | ✅ | Event bus for pipeline lifecycle events |
| Health check | ✅ | HTTP /health endpoint |
| K8s deployment | ❌ | Không có K8s manifests cho harness |
| HA clustering | ❌ | Single instance, không replicated |
| Distributed execution | ❌ | Không scale ngang |
| Model routing | ❌ | Không multiple LLM providers |
| IAM integration | ❌ | Không OAuth2/OIDC, không Vault |
| SOC2 compliance | ❌ | Không audit trail cho compliance |

### Gap: Medium-High (cho production)
- Single point of failure (1 container)
- Không thể scale horizontally
- Thiếu model routing → không fallback nếu LLM down

---

## Tổng hợp

| Layer | Priority | Trước | Sau 3 Phase | Sau Subagents | Mục tiêu |
|-------|----------|-------|-------------|---------------|----------|
| **1. Orchestration** | 🔴 Critical | 30% | 60% | 85% | Checkpoint + branching + sub-agents |
| **2. Context & Memory** | 🔴 Critical | 0% | 30% | 50% | Vector DB + RAG + session memory |
| **3. Tool Integration** | 🟡 Medium | 25% | 30% | 45% | Sandbox code exec + tool registry |
| **4. Guardrails** | 🟡 Medium | 10% | 50% | 60% | HITL + PII + policy engine |
| **5. Verification** | 🟢 Low | 20% | 30% | 50% | LLM-as-judge + evals dataset |
| **6. Observability** | 🟡 Medium | 10% | 10% | 50% | OTel traces + dashboard |
| **7. Infra & Scale** | 🟡 Medium | 15% | 15% | 60% | K8s + HA + model routing |

### Implemented gaps (13/13)

| # | Gap | Agent | Status |
|---|-----|-------|--------|
| 1 | Branching | dotnet | ✅ PipelineDag + conditional edges |
| 2 | Sub-agents | dotnet | ✅ Partial via PipelineDag edge routing |
| 3 | Code sandbox | devops | ✅ Docker sandbox + GuardrailService.CanExecuteCode |
| 4 | PII redaction | security | ✅ PiiRedactionService + CompleteTaskTool integration |
| 5 | OTel tracing | dotnet | ✅ OpenTelemetry + Prometheus metrics |
| 6 | Dashboard | angular | ✅ Harness Dashboard component at /admin/harness |
| 7 | System prompts | dotnet | ✅ PromptTemplateService with {{variable}} |
| 8 | LLM-as-judge | dotnet | ✅ LlmJudgeService + LoopEngineer integration |
| 9 | K8s deployment | devops | ✅ deployment.yaml, service.yaml, hpa.yaml, configmap.yaml |
| 10 | HA clustering | devops | ✅ 2 replicas, PDB, HPA min 2 max 10 |
| 11 | Model routing | devops | ✅ Basic agent-based routing via GuardrailService |
| 12 | Cost budgets | dotnet | ✅ CostTracker per-agent daily budget |
| 13 | Timeline graph | dotnet | ✅ TimelineTool MCP tool |
