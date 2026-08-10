# Advanced Capabilities — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement advanced system capabilities: persistent saga orchestration, request QoS/prioritization across all layers, real user monitoring (RUM), auto-remediation K8s operator, graceful degradation with stale cache fallback, and adaptive backpressure.

**Architecture:** Build a persistent saga engine with CockroachDB-backed state and timeout recovery. Implement 5-tier request priority (P0-P4) propagating from HTTP header through gRPC metadata to RabbitMQ queues and K8s PriorityClasses. Deploy RUM via OpenTelemetry Web SDK in Angular frontend. Create a custom K8s operator for auto-remediation triggered by Prometheus alerts.

**Tech Stack:** .NET 8, CockroachDB, Redis, Angular 17, OpenTelemetry Web SDK, Kubernetes Operator SDK, Prometheus Alertmanager, Polly

## Global Constraints

- All database migrations must be backward-compatible (additive-only)
- All secrets managed via HashiCorp Vault — never hardcoded
- JWT auth required on all endpoints (except /health)
- Distroless containers only, non-root user (UID 1654)
- Conventional Commits: `feat(domain): description`

---

## File Structure Map

```
src/Shared/His.Hope.Infrastructure/
├── Saga/
│   ├── SagaInstance.cs                     [CREATE]  — saga entity
│   ├── ISagaStateStore.cs                  [CREATE]  — saga persistence interface
│   ├── PersistentSagaOrchestrator.cs       [CREATE]  — DB-backed saga engine
│   └── SagaRecoveryService.cs              [CREATE]  — stale saga recovery
├── Qos/
│   ├── PriorityHeaderMiddleware.cs         [CREATE]  — X-Priority header propagation
│   ├── PriorityAdmissionMiddleware.cs       [CREATE]  — queue-depth based admission
│   └── PriorityConstants.cs                [CREATE]  — P0-P4 definitions
├── Degradation/
│   ├── IDegradedResponseProvider.cs        [CREATE]  — fallback interface
│   └── StaleCacheFallbackPolicy.cs         [CREATE]  — Polly fallback for stale cache
├── Backpressure/
│   └── AdaptiveConcurrencyLimiter.cs       [CREATE]  — self-tuning concurrency

src/Frontend/his-hope-app/src/app/
├── monitoring/
│   ├── rum.service.ts                      [CREATE]  — OpenTelemetry Web SDK init
│   └── web-vitals.ts                       [CREATE]  — LCP, INP, CLS collection

k8s/
├── priority/
│   └── priority-classes.yaml               [CREATE]  — P0-P4 PriorityClass resources
├── operator/
│   ├── hishope-remediation-deployment.yaml [CREATE]  — operator deployment
│   ├── remediation-policy-crd.yaml         [CREATE]  — CRD definition
│   └── remediation-rbac.yaml               [CREATE]  — operator RBAC

src/Services/RemediationOperator/           [CREATE]  — new K8s operator service

cockroach/migrations/
├── 021-saga-instances.sql                  [CREATE]  — saga state table

docker/
├── docker-compose.yml                      [MODIFY] — add remediation operator
```

---

### Task 1: Persistent Saga Engine

**Files:**
- Create: `cockroach/migrations/021-saga-instances.sql`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/SagaInstance.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/ISagaStateStore.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/PersistentSagaOrchestrator.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/SagaRecoveryService.cs`

**Interfaces:**
- Produces: `ISagaStateStore` — persists saga state to DB
- Produces: `PersistentSagaOrchestrator` — executes saga steps with per-step timeout, compensation, idempotency

- [ ] **Step 1: Create migration**

```sql
-- cockroach/migrations/021-saga-instances.sql
CREATE TABLE IF NOT EXISTS saga_instances (
    saga_id UUID PRIMARY KEY,
    saga_type VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Running, Completed, Failed, Compensating, Compensated
    step_index INT NOT NULL DEFAULT 0,
    data JSONB NOT NULL,
    error_message TEXT,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,
    last_heartbeat TIMESTAMPTZ NOT NULL DEFAULT now(),
    INDEX idx_saga_status (status, started_at),
    INDEX idx_saga_heartbeat (last_heartbeat) WHERE status IN ('Running', 'Compensating')
);
```

- [ ] **Step 2: Create SagaInstance entity and ISagaStateStore**

```csharp
// SagaInstance.cs
public class SagaInstance
{
    public Guid SagaId { get; set; }
    public string SagaType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int StepIndex { get; set; }
    public string Data { get; set; } = "{}"; // JSONB
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
}
```

- [ ] **Step 3: Create PersistentSagaOrchestrator**

Wraps the existing `SagaOrchestrator` with:
- Save saga state to DB on each step transition
- Heartbeat update every 5s during execution
- Per-step timeout (30s default) — if exceeded, trigger compensation
- Idempotency: check SagaId before starting
- Distributed lock via ILockManager (from Plan 2) to prevent dual execution

- [ ] **Step 4: Create SagaRecoveryService**

Background service that:
- Scans Running/Compensating sagas with last_heartbeat > 60s (stale)
- Re-acquires distributed lock on stale saga
- Resumes from StepIndex with compensation if needed
- Logs recovery events and exposes Prometheus metrics

- [ ] **Step 5: Commit**

```bash
git add cockroach/migrations/021-saga-instances.sql src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/
git commit -m "feat(saga): implement persistent saga orchestration with timeout recovery"
```

---

### Task 2: Request QoS — Priority Classes + Propagation

**Files:**
- Create: `k8s/priority/priority-classes.yaml`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Qos/PriorityConstants.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Qos/PriorityHeaderMiddleware.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Qos/PriorityAdmissionMiddleware.cs`

**Interfaces:**
- Produces: 5 K8s PriorityClass resources (P0-P4)
- Produces: HTTP middleware that injects and propagates X-Priority header
- Produces: Admission middleware that checks queue depth per priority before routing

- [ ] **Step 1: Create K8s PriorityClasses**

```yaml
# k8s/priority/priority-classes.yaml
apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: his-hope-p0-clinical-critical
value: 1000000
globalDefault: false
preemptionPolicy: PreemptLowerPriority
description: "Clinical critical — vitals, emergency access"
---
apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: his-hope-p1-clinical-normal
value: 100000
description: "Clinical normal — appointments, prescriptions, lab"
---
apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: his-hope-p2-administrative
value: 10000
description: "Administrative — registration, billing, inventory"
---
apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: his-hope-p3-reporting
value: 1000
description: "Reporting — analytics, exports, batch jobs"
---
apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: his-hope-p4-background
value: 100
description: "Background — sync, training, archival"
```

- [ ] **Step 2: Create PriorityConstants + PriorityHeaderMiddleware**

```csharp
// PriorityConstants.cs
public static class PriorityConstants
{
    public const string HeaderName = "X-Priority";
    public const string P0 = "P0"; // Clinical Critical
    public const string P1 = "P1"; // Clinical Normal
    public const string P2 = "P2"; // Administrative
    public const string P3 = "P3"; // Reporting
    public const string P4 = "P4"; // Background
}
```

PriorityHeaderMiddleware: reads X-Priority from request, propagates to downstream gRPC calls and RabbitMQ message headers.

- [ ] **Step 3: Create PriorityAdmissionMiddleware**

Checks queue depth per priority bucket before allowing request through. P0-P1 always admitted. P2 admitted if total queue < 70%. P3-P4 admitted if total queue < 50%.

- [ ] **Step 4: Commit**

```bash
git add k8s/priority/ src/Shared/Infrastructure/His.Hope.Infrastructure/Qos/
git commit -m "feat(qos): implement 5-tier request priority with K8s PriorityClasses and propagation"
```

---

### Task 3: Real User Monitoring (RUM)

**Files:**
- Create: `src/Frontend/his-hope-app/src/app/monitoring/rum.service.ts`
- Create: `src/Frontend/his-hope-app/src/app/monitoring/web-vitals.ts`

**Interfaces:**
- Produces: RUM data pipeline — Web Vitals → OTEL Collector → Prometheus + Jaeger
- Consumes: @opentelemetry/web SDK

- [ ] **Step 1: Create web-vitals collector**

```typescript
// src/app/monitoring/web-vitals.ts
import { onLCP, onINP, onCLS, onFCP, onTTFB } from 'web-vitals';

export function initWebVitals(sendMetric: (name: string, value: number, rating: string) => void) {
  onLCP(metric => sendMetric('LCP', metric.value, metric.rating));
  onINP(metric => sendMetric('INP', metric.value, metric.rating));
  onCLS(metric => sendMetric('CLS', metric.value, metric.rating));
  onFCP(metric => sendMetric('FCP', metric.value, metric.rating));
  onTTFB(metric => sendMetric('TTFB', metric.value, metric.rating));
}
```

- [ ] **Step 2: Create RUM service with OTEL Web SDK**

```typescript
// src/app/monitoring/rum.service.ts
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { Resource } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';

@Injectable({ providedIn: 'root' })
export class RumService {
  constructor() {
    const provider = new WebTracerProvider({
      resource: new Resource({ [ATTR_SERVICE_NAME]: 'his-hope-frontend' }),
    });
    
    provider.addSpanProcessor(new BatchSpanProcessor(
      new OTLPTraceExporter({ url: '/api/otel/v1/traces' })
    ));
    provider.register();
    
    initWebVitals((name, value, rating) => {
      // Report Web Vitals as custom spans
      const tracer = provider.getTracer('web-vitals');
      const span = tracer.startSpan(`web-vital-${name}`);
      span.setAttribute('web_vital.name', name);
      span.setAttribute('web_vital.value', value);
      span.setAttribute('web_vital.rating', rating);
      span.end();
    });
  }
}
```

- [ ] **Step 3: Register in app.module.ts**

```typescript
providers: [RumService] // providedIn: 'root' already handles this
```

- [ ] **Step 4: Commit**

```bash
git add src/Frontend/his-hope-app/src/app/monitoring/
git commit -m "feat(observability): implement Real User Monitoring with Web Vitals and OpenTelemetry"
```

---

### Task 4: Auto-Remediation K8s Operator

**Files:**
- Create: `src/Services/RemediationOperator/` (new .NET project — K8s operator)
- Create: `k8s/operator/remediation-policy-crd.yaml`
- Create: `k8s/operator/remediation-rbac.yaml`
- Create: `k8s/operator/hishope-remediation-deployment.yaml`

**Interfaces:**
- Produces: `RemediationPolicy` CRD — declarative remediation rules
- Produces: HisHopeRemediation operator — watches alerts, executes remediation

- [ ] **Step 1: Create RemediationPolicy CRD**

```yaml
# k8s/operator/remediation-policy-crd.yaml
apiVersion: apiextensions.k8s.io/v1
kind: CustomResourceDefinition
metadata:
  name: remediationpolicies.remediation.his-hope.io
spec:
  group: remediation.his-hope.io
  names:
    kind: RemediationPolicy
    plural: remediationpolicies
    singular: remediationpolicy
    shortNames: [rp, rempol]
  scope: Namespaced
  versions:
    - name: v1
      served: true
      storage: true
      schema:
        openAPIV3Schema:
          type: object
          properties:
            spec:
              type: object
              required: [alertName, severity, actions]
              properties:
                alertName:
                  type: string
                severity:
                  type: string
                  enum: [P0, P1, P2, P3]
                actions:
                  type: array
                  items:
                    type: object
                    properties:
                      type:
                        type: string
                        enum: [scale, restart, rollback, notify]
                      target:
                        type: string
                      params:
                        type: object
```

- [ ] **Step 2: Create operator project**

Create `src/Services/RemediationOperator/` using .NET 8 with K8s client library. The operator:
- Watches for Prometheus Alertmanager webhook calls
- Matches alert name → RemediationPolicy CRD
- Executes actions: scale deployment, restart pod, rollback ArgoCD, notify Slack
- Creates RemediationAction CRD for audit trail
- Safety: max 3 actions/service/hour, 5-min cooldown, anti-loop

- [ ] **Step 3: Create RBAC and Deployment**

```yaml
# Operator RBAC: get/list/watch deployments, pods, create events, patch deployments
# Deployment: 1 replica, his-hope-p1 priority, Vault sidecar for secrets
```

- [ ] **Step 4: Commit**

```bash
git add src/Services/RemediationOperator/ k8s/operator/
git commit -m "feat(ops): implement auto-remediation K8s operator with remediation policies"
```

---

### Task 5: Graceful Degradation — Stale Cache Fallback

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Degradation/IDegradedResponseProvider.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Degradation/StaleCacheFallbackPolicy.cs`

**Interfaces:**
- Produces: `IDegradedResponseProvider` — provides fallback response when downstream unavailable
- Produces: `StaleCacheFallbackPolicy` — Polly fallback serving stale cache on DB failure

- [ ] **Step 1: Create IDegradedResponseProvider**

```csharp
public interface IDegradedResponseProvider
{
    Task<T?> GetDegradedResponseAsync<T>(string cacheKey, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create StaleCacheFallbackPolicy**

Polly `FallbackStrategyOptions` that:
- On DB/Redis failure → serves stale cached data from Redis ignoring TTL
- Returns degraded response with X-Degraded-Data: true header
- Logs degradation events to Prometheus counter

- [ ] **Step 3: Register in ResiliencePipelineFactory**

Add fallback as the last (outermost) layer in the pipeline chain:
```
Fallback → Timeout → Retry → Circuit Breaker → Bulkhead → Execute
```

- [ ] **Step 4: Commit**

```bash
git add src/Shared/Infrastructure/His.Hope.Infrastructure/Degradation/
git commit -m "feat(resilience): implement stale cache fallback for graceful degradation"
```

---

### Task 6: Adaptive Backpressure

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Backpressure/AdaptiveConcurrencyLimiter.cs`

**Interfaces:**
- Produces: `AdaptiveConcurrencyLimiter` — self-tuning concurrency limiter based on latency gradient

- [ ] **Step 1: Create AdaptiveConcurrencyLimiter**

Implementation inspired by Netflix Concurrency Limits:
- Measures p99 latency over rolling 1-min window
- If latency increases > 20% → reduce concurrency by 10%
- If latency stable or decreasing → increase concurrency by 5% (up to max)
- Min concurrency: 5, Max: 100
- Exposed via Prometheus gauge

```csharp
public class AdaptiveConcurrencyLimiter
{
    private int _currentLimit;
    private readonly int _minLimit;
    private readonly int _maxLimit;
    private readonly SlidingWindow _latencyWindow;

    public int CurrentLimit => _currentLimit;

    public void RecordLatency(TimeSpan latency)
    {
        _latencyWindow.Add(latency.TotalMilliseconds);
        AdjustLimit();
    }

    private void AdjustLimit()
    {
        var currentP99 = _latencyWindow.GetPercentile(99);
        var baselineP99 = _latencyWindow.GetBaselinePercentile(99);
        
        if (currentP99 > baselineP99 * 1.2)
            _currentLimit = Math.Max(_minLimit, _currentLimit - (int)(_currentLimit * 0.1));
        else if (currentP99 < baselineP99 * 0.9)
            _currentLimit = Math.Min(_maxLimit, _currentLimit + (int)(_currentLimit * 0.05));
    }
}
```

- [ ] **Step 2: Integrate with ResiliencePipelineFactory**

Replace static ConcurrencyLimiter with AdaptiveConcurrencyLimiter in pipeline builder.

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Infrastructure/His.Hope.Infrastructure/Backpressure/
git commit -m "feat(resilience): implement adaptive concurrency limiter with latency-based self-tuning"
```

---

## Plan Verification Checklist

- [ ] Saga instances persisted to DB with heartbeat recovery
- [ ] 5 K8s PriorityClasses deployed and assigned to deployments
- [ ] X-Priority header propagated through HTTP → gRPC → RabbitMQ
- [ ] RUM Web Vitals flowing to OTEL Collector
- [ ] RemediationPolicy CRDs and operator responding to alerts
- [ ] Stale cache served on DB failure with X-Degraded-Data header
- [ ] Adaptive concurrency limiter adjusting limits based on latency
