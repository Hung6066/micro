# His.Hope System Design Upgrade — Full Audit & Roadmap

**Date**: 2026-07-17  
**Status**: Proposed  
**Author**: Lead System Architect  
**Scope**: Full system design upgrade across 7 architectural domains  
**Methodology**: Domain-by-Domain Capability Maturity Assessment (L1 Initial → L5 Optimizing)

---

## Executive Summary

His.Hope has **world-class documentation** (12 ADRs, 2247-line architecture doc, 46+ docs) and a **well-scaffolded microservices architecture**. However, a systematic audit across 100+ patterns in 7 domains reveals a significant **implementation gap**: documentation promises features that code does not deliver. The system's overall maturity is **L2 (Repeatable)**, with pockets of L4 excellence (Outbox, Migration Safety, 3 Pillars, Dashboards) and critical L1 gaps that must be addressed before production deployment.

### Maturity Heatmap

| Domain | Current | Target | Critical Gaps |
|--------|---------|--------|---------------|
| 1. Reliability | L2 | L4 | 3 Critical: Bulkhead dead code, DLQ infinite requeue, Circuit Breaker not wired |
| 2. Data Architecture | L2 | L4 | 4 Critical-to-High: No Schema Registry, Idempotency, Distributed Locking, Archival |
| 3. Security & Compliance | L2 | L4 | 5 Critical: No MFA, No BAA, No Image Signing, No Data Masking, No De-identification |
| 4. Scalability | L2 | L4 | 2 Critical: DB Connection Pool not enforced, No Capacity Model |
| 5. Observability | L3 | L4 | 2 Critical: No Tracing Sampling, SLO rules global not per-service |
| 6. API & Integration | L1-L2 | L4 | 1 Critical: No FHIR/HL7 Interoperability |
| 7. Operational Excellence | L2.5 | L4 | 2 Critical: No Feature Flags, No Capacity Model |

### Top 10 Critical Gaps (Must Fix First)

| # | Domain | Gap | Impact |
|---|--------|-----|--------|
| 1 | API | No FHIR/HL7 interoperability | Cannot integrate with any external healthcare system |
| 2 | Security | No Multi-Factor Authentication | Single-factor auth fails every HIPAA audit |
| 3 | Security | No BAA management | Categorically non-compliant with HIPAA |
| 4 | Security | Image signing not implemented | CI/CD compromise = unrestricted production access |
| 5 | Scalability | DB Connection Pool not enforced | Up to 14,000 potential connections → CRDB meltdown |
| 6 | Reliability | DLQ infinite requeue | Poison messages loop forever, block all processing |
| 7 | Observability | SLO rules are global, not per-service | One service burning budget masked by healthy services |
| 8 | Data | No Schema Registry | Fragile `Type.GetType()` deserialization, no event versioning |
| 9 | Data | No Idempotency Keys | Double-billing, double-booking risk |
| 10 | Security | No Data Masking for non-prod | Real PHI in staging/dev databases |

---

## Domain 1: Reliability

### 1.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| Circuit Breaker | L2 | Defined in `ResilienceConfiguration` but **never wired** into HTTP/gRPC clients |
| Dead Letter Queue | L1 | `BasicNack(requeue: true)` creates infinite poison message loop; no `x-dead-letter-exchange` on any queue |
| Bulkhead | L1 | `BulkheadMaxParallelization = 10` defined but `AddBulkhead()` **never called** — pure dead code |
| Outbox Pattern | L4 | Fully implemented with status lifecycle, retry, distributed locking; lacks cleanup only |
| Saga Orchestration | L2 | Framework (`ISagaStep`, `SagaOrchestrator`) exists but in-memory only, no persistence, no consumers |
| Graceful Degradation | L2 | Cache fallback exists; stale-cache-on-DB-failure documented but not implemented |
| Retry + Jitter | L3 | gRPC handler has proper transient detection; generic pipeline lacks `ShouldHandle` predicate |
| Timeout | L2 | 10s global timeout; no cascading timeout reduction between service layers |
| Health Checks | L3 | 3-probe types, structured response; lacks critical vs non-critical dependency classification |
| Rate Limiting | L4 | Redis-backed sliding window, dual limits, graceful fallback; lacks gRPC coverage |

### 1.2 Proposed Upgrades

#### CRITICAL: Resilience Pipeline Wiring

```
Proposed: IResiliencePipelineFactory with per-dependency named pipelines

Pipeline order (outer → inner):
  Timeout (per-call) → Retry (transient only, with ShouldHandle predicate)
  → Circuit Breaker (per-dependency granularity) → Bulkhead (per priority class)

Implementation:
  - IResiliencePipelineFactory: factory tạo named pipeline per dependency
  - GrpcResilienceInterceptor: Polly pipeline for gRPC calls
  - HTTP: .AddResilienceHandler("dependency-name", cfg => ...)
  - Wire into Program.cs for all service registrations

ADR-013: Per-Dependency Resilience Pipeline Strategy
```

#### CRITICAL: Dead Letter Queue

```
Proposed: DLQ 3-tier architecture

  Primary Queue → (N=3 failures) → Retry Queue (x-message-ttl delay)
  → (M=3 failures) → DLQ (his-hope.dlx)
  
  - DLQ Consumer: persist to DeadLetterMessages table, alert on-call
  - Fix: BasicNack(requeue: false) after max retries
  - Auto-declare DLX on queue creation in RabbitMQEventBus

ADR-014: Dead Letter Queue Strategy
Migration: dead_letter_messages table
```

#### CRITICAL: Bulkhead Implementation

```
Proposed: Bulkhead per priority class + per dependency

  Priority-based:
    P0 (Clinical): 20 parallel, 100 queue
    P1 (Normal):   10 parallel, 50 queue
    P3 (Background): 5 parallel, 20 queue

  Dependency-based:
    DB operations: dedicated bulkhead
    gRPC calls: dedicated bulkhead
    Redis cache: dedicated bulkhead

  Implementation: added to Polly pipeline via ResiliencePipelineFactory

ADR-015: Bulkhead and Resource Isolation Strategy
```

#### HIGH: Persistent Saga Engine

```
Proposed: DB-backed saga with timeout, recovery, idempotency

  - SagaInstance table: Id, SagaType, Status, Data (JSONB), StepIndex, CreatedAt
  - Step timeout: 30s default per step
  - Idempotency: SagaId deduplication
  - Recovery: background job scans stale sagas (>timeout) → compensate
  - Observability: Prometheus counters (started/succeeded/failed/compensated)
  - Distributed lock: prevent dual processing

ADR-016: Persistent Saga Orchestration
Migration: saga_instances table
```

#### HIGH: Graceful Degradation

```
Proposed: Stale cache serving + degraded response stubs

  Cache TTL model:
    Soft TTL (5m): serve from cache, async refresh from DB
    Hard TTL (1h): if DB fails, still serve stale cache

  Degraded responses per dependency:
    PatientService down → return cached patient summary
    AppointmentService down → return "temporarily unavailable"

  Implementation:
    - StaleCacheService extends ICacheService
    - Polly FallbackPolicy in resilience pipeline
    - IDegradedResponseProvider per service

ADR-017: Graceful Degradation and Stale Cache Strategy
```

### 1.3 Implementation Plan (Domain 1)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-2 | Wire Resilience Pipeline (CB + Retry + Timeout into HTTP/gRPC) |
| 1B | 2-3 | Implement DLQ (DLX + Retry Queue + DLQ Consumer) |
| 1C | 3-4 | Implement Bulkhead (per priority + per dependency) |
| 2 | 5-6 | Persistent Saga Engine |
| 3 | 7-8 | Graceful Degradation (stale cache + degraded responses) |
| 4 | 9-10 | Outbox cleanup, health check criticality, gRPC rate limiting |

---

## Domain 2: Data Architecture

### 2.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| Event Schema Registry | L1 | No registry; `Type.GetType(message.Type)` fragile across assemblies; `TypeNameHandling.All` is security risk |
| Idempotency Design | L1 | "Consumers must handle it" documented but no framework provided; each retry creates new event ID |
| Distributed Locking | L1 | Only crude `LockExpiresAt` advisory lock; no RedLock, no fencing tokens |
| Data Archival / Retention | L1 | SQL comments only ("Consider partitioning by month"); no right-to-erasure; OutboxMessages grows forever |
| CQRS Read Models | L2 | In-process MediatR only; no separate read DB, no materialized views, no projection services |
| Distributed Caching | L2 | Cache-aside only; `RemoveByPrefixAsync` is broken (deletes literal key, not prefix scan) |
| DB Connection Pooling | L2 | Documented "Min 5, Max 100" but not enforced in connection string; no PgBouncer |
| Sharding / Partitioning | L2 | DB-per-service natural sharding only; no explicit table partitioning |
| CDC Strategy | L3 | Only 5 tables captured (patientdb, clinicaldb); billing/pharmacy/lab/identity not included |
| Migration Safety | L4 | Additive-only policy, expand-contract, 3-step deploy; lacks automated backward-compat testing |

### 2.2 Proposed Upgrades

#### HIGH: Event Schema Registry

```
Proposed: Confluent Schema Registry (or Apicurio) with AVRO/Protobuf schemas

  Event envelope update:
    SchemaVersion: "1.2.0"
    SchemaSubject: "his-hope.patient.PatientRegistered"
    SchemaId: registry-assigned

  Producer: validate payload before outbox write
  Consumer: schema lookup + version-aware deserialization
  CI/CD: schema compatibility check in pipeline

  Replace Type.GetType() with EventTypeRegistry (explicit type mapping)

ADR-018: Event Schema Registry and Versioning
CI gate: schema-compatibility-check
```

#### HIGH: Idempotency Layer

```
Proposed: 2-tier idempotency (API + Event Consumer)

  Tier 1 — REST API:
    IdempotencyMiddleware: check Idempotency-Key header
    IdempotencyKeys table: Key, Service, Endpoint, RequestHash, ResponseBody, TTL 24h
    409 Conflict if same key with different body

  Tier 2 — Event Consumer:
    ProcessedEvents table: EventId, Consumer, ProcessedAt
    EventConsumerMiddleware: skip if already processed
    Deterministic event ID from business context (not random GUID)

ADR-019: Idempotency Key and Event Deduplication
Migration: idempotency_keys, processed_events tables
```

#### HIGH: Distributed Locking

```
Proposed: Redis-backed RedLock algorithm

  ILockManager interface:
    AcquireAsync(lockKey, ttl, fencingToken)
    ReleaseAsync(lockKey, fencingToken)
    ExtendAsync(lockKey, fencingToken)
    TTL: 30s default, auto-extend heartbeat every 10s

  Use cases:
    Invoice number generation (sequential, no gap)
    Patient record merge (only one active per patient)
    Prescription fulfillment (prevent double-dispense)
    Outbox processor leader election (replaces LockExpiresAt)

ADR-020: Distributed Locking Strategy
```

#### HIGH: Data Lifecycle & Right-to-Erasure

```
Proposed: Full data lifecycle management

  Soft-Delete: DeletedAt, DeletedBy on all PHI entities
  EF Core global query filter: WHERE DeletedAt IS NULL

  Retention policy per data class:
    Transactional: 7yr active → 3yr archive
    Audit logs: 6yr hot → partition by month → cold storage
    Outbox messages: 30d processed → DELETE

  Right-to-Erasure workflow:
    Patient request → soft-delete → 30d grace → hard-delete
    Backup cleanup via Velero hooks

  Archival jobs (CronJob per service):
    outbox-cleanup, audit-archive, session-cleanup

  Analytics de-identification:
    Dataflow transform to remove 18 HIPAA identifiers before BigQuery

ADR-021: Data Lifecycle and Right-to-Erasure
Migration: DeletedAt, DeletedBy columns on all PHI tables
```

#### HIGH: CQRS Read Models (Phase 1)

```
Proposed: Follower reads + separate read DbContext

  Phase 1 (near-term):
    CockroachDB follower reads: AS OF SYSTEM TIME experimental_follower_read_timestamp()
    Separate read connection string with follower reads enabled
    PatientReadDbContext (read-only optimized)

  Phase 2 (future):
    Projection Service per aggregate
    Event-sourced materialized views
    True CQRS: Commands → Write DB, Queries → Read DB/Projection

ADR-022: CQRS Read Model Separation Strategy
```

### 2.3 Implementation Plan (Domain 2)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-2 | `RemoveByPrefixAsync` fix (quick win) |
| 1B | 2-4 | Idempotency Layer (middleware + DB tables) |
| 2A | 5-7 | Schema Registry + Event envelope upgrade |
| 2B | 5-7 | Distributed Locking (RedLock + locking patterns) |
| 3 | 8-10 | Data Lifecycle (soft-delete, archival jobs, right-to-erasure) |
| 4 | 11-12 | CQRS Read Models Phase 1 (follower reads) |
| 5 | Future | CQRS Read Models Phase 2 (event-sourced projections) |

---

## Domain 3: Security & Compliance

### 3.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| Multi-Factor Authentication | L1 | Listed as "planned" only; single-factor password auth for all clinical users |
| BAA Management | L1 | Gap #1 in HIPAA compliance doc; no signed BAAs with any vendor handling ePHI |
| Image Signing + Digest Pinning | L1 | Planned Q3 2026; all images use `:latest` tags, unsigned |
| Data Masking (non-prod) | L1 | Zero evidence; staging/dev databases contain real PHI |
| PHI De-identification | L1 | CDC pipeline sends raw PHI to BigQuery with no de-identification step |
| Right-to-Erasure | L1 | No process, no soft-delete, no data disposal policy |
| Audit Trail Completeness | L2 | Triggers only on INSERT/UPDATE/DELETE; no SELECT audit; primary audit log is not WORM |
| API Abuse Prevention | L2 | Fixed-window per-IP rate limiting only; no per-user limits, generous brute-force threshold (20/5min) |
| SBOM + Patch SLAs | L2 | Trivy + SonarQube listed as tools; no schedule, no SBOM generation |
| Encryption Key Rotation | L3 | Vault configured; rotation is manual, no automated schedule |
| HIPAA Technical Safeguards | L3 | All 9 §164.312 controls mapped; MFA is the critical coverage gap |
| Session Management | L3 | 15min access, 7d refresh, 8h absolute, 5 concurrent sessions, Redis-backed; no absolute backend enforcement |

### 3.2 Proposed Upgrades

#### CRITICAL: Multi-Factor Authentication

```
Proposed: TOTP (RFC 6238) primary + WebAuthn/FIDO2 for privileged roles

  Enrollment: mandatory on first login (no skip), QR + manual backup
  Recovery: 8 single-use recovery codes, shown once
  Step-up auth: re-verify MFA for sensitive operations

  JWT claim: "amr": ["pwd", "mfa"]
  Policy: MFA required for Provider, Nurse, Admin, Pharmacist

  Grace period: 7 days to enroll, then blocked

ADR-023: Multi-Factor Authentication Strategy
Migration: user_mfa table (UserId, SecretKey, IsEnabled, EnrolledAt, RecoveryCodes)
```

#### CRITICAL: BAA Management

```
Proposed: Vendor ePHI inventory + BAA lifecycle management

  Vendor inventory (who touches ePHI):
    CockroachDB Labs, Google Cloud, Elastic, Redis Labs, RabbitMQ/VMware
    NOTE: Slack and PagerDuty must have NO PHI (scrub alerts)

  BAA lifecycle: sign → annual review → update on scope change → termination export/delete

  Technical controls:
    Log scrubbing pipeline: mask PHI before Elastic
    Redis: cache reference IDs only, not raw PHI
    Slack/PagerDuty: strip PHI from alert content

ADR-024: BAA Management and Vendor ePHI Controls
```

#### CRITICAL: Supply Chain Security

```
Proposed: Cosign + Vault PKI + Gatekeeper + SBOM

  Image signing: Vault PKI key → Tekton cosign-sign task → OCI signature
  Digest pinning: replace :latest with sha256:... in all manifests
  Gatekeeper: ConstraintTemplate requiring signed images + pinned digests
  SBOM: Syft generate CycloneDX JSON in Tekton pipeline

  Vulnerability SLAs:
    Critical CVE: patch within 48h → auto-create ticket
    High CVE: 7 days
    Medium CVE: 30 days

ADR-025: Supply Chain Security Framework (upgrade ADR-011 Proposed → Accepted)
```

#### CRITICAL: Data Masking

```
Proposed: Synthetic data generation + production snapshot masking pipeline

  Non-production environments:
    Bogus/Faker for seed data generation
    Production snapshot → masking pipeline → staging DB

  Masking rules:
    Patient name → random realistic name
    SSN/ID → deterministic hash (preserve uniqueness)
    DOB → shift by random offset (preserve age distribution)

  Log masking: Serilog destructuring policy masks PHI fields → [REDACTED-PHI]

  Analytics de-identification (CDC → BigQuery):
    Dataflow PTransform: remove 18 HIPAA identifiers
    Separate BigQuery datasets: raw_phi (restricted) vs deidentified (analyst access)

ADR-026: Data Masking and De-identification Strategy
```

#### HIGH: Enhanced Audit Trail

```
Proposed: SELECT audit + immutability via hash chaining

  SELECT audit: application-level AuditMiddleware for GET requests with PHI
    Sampling: 100% for single-record, 1% for list queries

  Immutability: hash chain per audit entry
    audit_hash = SHA256(prev_hash + current_data + timestamp)
    Periodic integrity verification with alert on chain break

  Retention tiers:
    Hot (90d in primary DB) → Warm (6yr BigQuery) → Cold (25yr GCS)

ADR-027: Enhanced Audit Trail with Immutability
```

#### HIGH: API Abuse Prevention

```
Proposed: 3-tier rate limiting + brute force protection

  Rate limiting:
    Per-IP: 100 req/min (YARP)
    Per-User: 200 req/min total, 50/min per endpoint
    Per-Endpoint: differentiated limits (POST /login: 5/min, GET /patients: 60/min)

  Brute force protection:
    Progressive delay: 1s→2s→4s→8s after each failure
    Account lockout: 10 failures → 15min lock
    IP lockout: 50 failures → 30min block

  Credential stuffing detection:
    Pattern: many unique usernames from same IP → block + alert

ADR-028: API Abuse Prevention Framework
```

### 3.3 Implementation Plan (Domain 3)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-2 | MFA Implementation (TOTP + enrollment + recovery) |
| 1B | 1-3 | BAA Management (vendor inventory + sign BAAs + log scrubbing) |
| 2A | 4-5 | Image Signing + Digest Pinning + Gatekeeper |
| 2B | 4-5 | Data Masking (synthetic data gen + staging pipeline) |
| 3A | 6-7 | API Abuse Prevention (per-user rate limit + brute force protection) |
| 3B | 6-8 | PHI De-identification (CDC → BigQuery Dataflow transform) |
| 4 | 9-10 | Enhanced Audit (SELECT audit + hash chain + immutability) |
| 5 | 11-12 | SBOM + Vulnerability SLAs + NuGet scanning |

---

## Domain 4: Scalability

### 4.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| DB Connection Pool | L1 | No PgBouncer; connection limits documented but not enforced; up to 14K potential connections |
| Capacity Planning | L1 | Targets stated (1B patients, 10M req/s) but zero model or growth projection |
| Caching Hierarchy | L2 | Single-level Redis; no L1 in-memory cache; no stampede prevention; `RemoveByPrefixAsync` bug |
| Backpressure | L2 | Bulkhead queue of 50 only; no adaptive limits; no Retry-After headers |
| Request QoS | L1 | No priority classes; clinical writes compete equally with analytics exports |
| HPA | L2 | CPU/Memory only; no custom metrics (request rate, queue depth, connection count) |
| Read Scaling | L2 | No read replicas; no follower reads; no read-through caching |
| Cold Start | L1 | No pre-warming; no AOT compilation; 200s startup window |
| VPA | L1 | `updateMode: Off` — recommendations only, no automated resource adjustment |
| Cluster Autoscaling | L2 | Basic scale-up/down; 15-min node provision time; no spot instances |
| Multi-Region | L3 | 3-region active-active, CockroachDB zone configs, global LB; best covered area |

### 4.2 Proposed Upgrades

#### CRITICAL: DB Connection Pool Management

```
Proposed: PgBouncer sidecar per service pod

  Pool mode: Transaction pooling
  Pool size: 20 per PgBouncer instance
  Total CRDB connections: ~400 (not 14,000)

  Per-service tuning:
    PatientService: 30 pool, 5s timeout
    IdentityService: 15 pool, 2s timeout (auth-critical)
    ClinicalService: 20 pool, 10s timeout
    BillingService: 10 pool, 30s timeout (complex queries)

  Read/Write splitting: separate connection string for follower reads
  Monitoring: pool_size, pool_waiting, pool_wait_time_ms metrics
  Alert: PoolWaitTimeP99 > 100ms → scale warning

ADR-029: Database Connection Pool Management
```

#### CRITICAL: Capacity Planning Framework

```
Proposed: Load baseline → capacity model → forecasting pipeline

  Load baseline: k6 load test to determine max throughput per service
  Resource formula: pods = ceil(target_rps / (rps_per_pod × 0.7))
  Growth model: phased from 100 → 1K → 10K → 100K → 1M → 10M req/s

  Forecasting: Prophet model predicting traffic 30 days ahead
  Weekly capacity review meeting with automated CCR tickets
  Quarterly stress test to 2x projected 6-month peak

ADR-030: Capacity Planning Framework
```

#### HIGH: Multi-Level Caching

```
Proposed: L1 in-memory + L2 Redis + stampede prevention

  L1 (IMemoryCache): reference data, user permissions, 5min TTL, max 500 items
  L2 (Redis): patient data, appointment slots, session tokens, 5-10min TTL

  Stampede prevention: XFetch algorithm (probabilistic early recomputation)
  Cache warming: pre-load ICD-10, CPT catalog, role permissions on startup
  Cross-service invalidation: PatientUpdated event → invalidate downstream caches

  Fix: RemoveByPrefixAsync using SCAN instead of single key delete

ADR-031: Multi-Level Caching Strategy
```

#### HIGH: Adaptive Backpressure & Load Shedding

```
Proposed: Netflix-style adaptive concurrency limits

  Algorithm: measure latency gradient → adjust concurrency limits
  Auto-range: min=5, max=100 concurrent, self-tuning

  Priority-based load shedding: drop P4 first, preserve P0
  Deadline-aware: prioritize requests nearing timeout
  Retry-After headers on 429 responses
  Frontend backpressure: debounce user actions, merge duplicate requests

ADR-032: Adaptive Backpressure and Load Shedding
```

#### HIGH: Request QoS

```
Proposed: 5-tier priority system across all layers

  P0 (Clinical Critical): vitals, emergency access — preempts P3-P4
  P1 (Clinical Normal): appointments, prescriptions, lab orders
  P2 (Administrative): registration, billing, inventory
  P3 (Reporting): analytics, exports, batch jobs
  P4 (Background): data sync, ML training, archival

  Propagation: X-Priority HTTP header → gRPC metadata → RabbitMQ priority queues
  K8s: Guaranteed QoS for P0-P1, Burstable for P2, BestEffort for P3-P4
  Admission control: API Gateway checks queue depth per priority before routing

ADR-033: Quality of Service and Request Prioritization
```

#### HIGH: Advanced HPA

```
Proposed: Custom metrics + predictive scaling

  Custom metrics per service:
    PatientService: requests_per_second (target 500/s per pod)
    IdentityService: auth_requests_per_second (target 1000/s)
    Outbox processor: outbox_backlog_depth (target < 100)

  Scaling behavior tuning:
    Scale-up stabilization: 30s (fast)
    Scale-down stabilization: 300s (slow, prevent flapping)

  Phase 2: Prophet predictive scaling (pre-scale before spike)
  Phase 3: KEDA event-driven autoscaling (scale to zero when idle)

ADR-034: Advanced Horizontal Pod Autoscaling
```

### 4.3 Implementation Plan (Domain 4)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-3 | PgBouncer sidecar + connection pool tuning |
| 1B | 1-2 | Load test baseline + initial capacity model |
| 2A | 4-5 | Multi-level caching (L1 + stampede prevention + warming) |
| 2B | 4-6 | Request QoS (PriorityClass + per-priority bulkhead) |
| 3A | 7-8 | Adaptive backpressure + load shedding |
| 3B | 7-9 | HPA custom metrics + scaling behavior tuning |
| 4 | 10-12 | Predictive scaling + KEDA integration |
| 5 | Future | VPA Auto mode, spot instances, CDN immutable assets |

---

## Domain 5: Observability

### 5.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| Tracing Sampling | L1 | No strategy at all; 100% sampling → Jaeger unusable at 10M req/s target |
| SLO Recording Rules | L2 | All use `sum(...)` without `by (service)` — global aggregate masks per-service issues |
| Synthetic Monitoring | L1 | Completely absent; no external uptime checks or user journey simulations |
| RUM | L1 | No Web Vitals (LCP, INP, CLS); no frontend performance data |
| ML Anomaly Detection | L2 | LSTM Autoencoder listed in model inventory but not deployed; all thresholds static |
| Saturation (Golden Signal) | L2 | CPU/memory only; no queue depth, connection pool, or thread pool saturation metrics |
| USE Metrics | L2 | No systematic USE methodology; network and I/O saturation missing |
| On-Call Rotation | L2 | Escalation paths defined but no follow-the-sun rotation, no primary/secondary design |
| DB Query Performance | L3 | Manual `crdb_internal` queries; no auto-EXPLAIN capture, no index monitoring |
| Three Pillars | L4 | OpenTelemetry + Serilog ELK + Jaeger — all well-implemented |
| Dashboards | L4 | 15+ comprehensive dashboards across all services and infrastructure |
| Log Aggregation | L4 | Structured JSON, correlation ID, 8+ Kibana saved queries |

### 5.2 Proposed Upgrades

#### CRITICAL: Tracing Sampling Strategy

```
Proposed: 3-tier sampling: head-based + tail-based + adaptive

  Head-based (OTEL Collector):
    Probabilistic: 10% normal, 100% errors, 100% P0 priority
    Rate-limiting: max 100 spans/s per service

  Tail-based (OTEL Collector + Jaeger):
    Sample if: latency > 500ms OR error=true OR phi.access=true
    30s decision wait for full trace completion

  Adaptive (Phase 2):
    High traffic (day): 1-5% sample rate
    Low traffic (night): 100% sample rate

  Storage tiering:
    Hot (Elasticsearch): 7 days, full detail
    Warm (BigQuery): 90 days, sampled traces
    Cold (GCS): 1 year, aggregated metrics only

ADR-035: Distributed Tracing Sampling Strategy
```

#### CRITICAL: Per-Service SLO Framework Fix

```
Proposed: Fix recording rules + add per-service SLOs for all 7 services

  Fix: ALL recording rules must include `by (service)`:
    job:slo_availability_30d:ratio{service="patient-service"}
    job:slo_availability_30d:ratio{service="identity-service"}
    ... (all 7 services)

  Per-service SLO targets:
    IdentityService: 99.95% availability, p99 < 300ms
    PatientService: 99.9%, p99 < 500ms
    ClinicalService: 99.95%, p99 < 300ms (critical)
    AppointmentService: 99.9%, p99 < 1s
    BillingService: 99.5%, p99 < 2s (batch-oriented)
    LabService: 99.5%, p99 < 2s
    PharmacyService: 99.9%, p99 < 500ms

  Expand SLO exporter from 4 to 7 services
  Per-service error budget policy: Green/Yellow/Orange/Red thresholds

ADR-036: Per-Service SLO Framework
```

#### HIGH: Synthetic Monitoring

```
Proposed: Blackbox Exporter + Playwright user journeys

  External health: HTTP/HTTPS/DNS/TCP probes from 3 regions
  User journeys (5-min schedule per region):
    Journey 1: Login → Search Patient → View Record → Logout
    Journey 2: Register Patient → Book Appointment → Check In
    Journey 3: Create Prescription → Verify in Pharmacy Service
    Journey 4: Lab Order → Result Upload → Result View
    Journey 5: Generate Invoice → Process Payment → Verify

  SLA verification from synthetic data, monthly auto-generated report
  External provider (GCP Uptime Check) for independent verification

ADR-037: Synthetic Monitoring Strategy
```

#### HIGH: Real User Monitoring

```
Proposed: Angular + OpenTelemetry Web SDK

  Web Vitals: LCP, INP, CLS via web-vitals library + @opentelemetry/web
  Frontend metrics: page load time, API latency, NgRx action timing
  Data pipeline: OTEL Web SDK → OTEL Collector HTTP endpoint → Prometheus + Jaeger
  Performance budget alerts: LCP > 2.5s P95, CLS > 0.1 P95, INP > 200ms P95

ADR-038: Real User Monitoring Strategy
```

#### HIGH: ML Anomaly Detection

```
Proposed: 2-phase approach: statistical first, ML second

  Phase 1 — Statistical baseline (immediate):
    Seasonal decomposition (daily + weekly cycles)
    Dynamic threshold: μ ± 3σ within seasonally-adjusted window
    Alert when outside 99.7% confidence band

  Phase 2 — LSTM Autoencoder (when training data sufficient):
    Vertex AI training pipeline, retrain weekly
    Anomaly score 0-100; > 70 Warning, > 90 Critical
    Feedback loop: operator labels → model improvement

ADR-039: Anomaly Detection Framework
```

### 5.3 Implementation Plan (Domain 5)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-2 | **SLO Rules Fix** (per-service `by (service)` recording rules) |
| 1B | 1-3 | **Tracing Sampling Strategy** (OTEL Collector config + storage tiering) |
| 2A | 4-5 | Blackbox Exporter + Basic Synthetic Monitoring (HTTP checks) |
| 2B | 5-6 | Statistical Anomaly Detection (seasonal + dynamic thresholds) |
| 3A | 7-8 | RUM Implementation (Web Vitals + frontend traces) |
| 3B | 7-8 | Advanced Synthetic (Playwright user journeys) |
| 4 | 9-10 | ML Anomaly Detection (Vertex AI pipeline + serving) |
| 5 | 11-12 | USE Metrics Dashboard + On-Call Rotation + DB Auto-EXPLAIN |

---

## Domain 6: API & Integration

### 6.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| FHIR/HL7 Interoperability | L1 | No FHIR resources, no HL7v2 parsing, no SMART on FHIR — cannot integrate with any external healthcare system |
| Idempotency Keys | L1 | No `Idempotency-Key` header support; double-billing/double-booking risk |
| gRPC Contract Evolution | L1 | No versioning in proto packages, no `reserved` fields, no buf linter |
| Pagination | L2 | Offset-based only; no cursor-based pagination; offset breaks at 1B scale |
| Webhook/Push Notifications | L1 | Only internal RabbitMQ events; no external push delivery mechanism |
| API Versioning | L2 | `/api/v1/` convention only; no deprecation policy, no sunset headers |
| Long-Running Operations | L1 | All sync request-response; no 202 Accepted + operation status pattern |
| Filtering/Sorting | L1 | Basic keyword search only; no sort params, no sparse fieldsets, no query language |
| gRPC Deadlines | L1 | No deadline propagation; no cancellation token flow across services |
| Gateway Rate Limiting | L2 | Per-IP fixed window only; no per-user, no per-endpoint limits |
| Error Standardization | L3 | RFC 7807 for REST; gRPC error codes not documented |
| API Documentation | L3 | Thorough manual docs; no auto-generated OpenAPI spec published |

### 6.2 Proposed Upgrades

#### CRITICAL: FHIR Interoperability

```
Proposed: New FHIR Gateway service with HL7.Fhir.R4 SDK (Firely)

  FHIR R4 Resources to implement:
    Patient, Practitioner, Encounter, Observation, Condition
    MedicationRequest, MedicationDispense, Procedure, Claim, Appointment

  SMART on FHIR: OAuth2 + OpenID Connect, EHR Launch + Standalone Launch
  FHIR Endpoint: /fhir/r4/{resource}
  Search: _id, identifier, name, birthdate, gender; _include, chained search

  HL7v2 Support: MLLP listener, ADT/ORM/ORU message types
  HL7v2 → FHIR conversion engine for legacy integration

  Mapping layer: per-service FHIR Adapter (domain entity ↔ FHIR resource)
  FHIR operations: $validate, $match, $everything

ADR-041: FHIR and HL7 Interoperability Strategy
```

#### HIGH: Idempotency Keys

```
Proposed: Middleware-based idempotency for all mutating endpoints

  IdempotencyMiddleware:
    Check Idempotency-Key header on POST/PUT/PATCH
    Lookup in IdempotencyKeys table
    Same key + same hash → return cached response (idempotent)
    Same key + different hash → 409 Conflict

  IdempotencyKeys table:
    IdempotencyKey (PK), ServiceName, Endpoint, HttpMethod
    RequestHash (SHA256), ResponseStatusCode, ResponseBody
    Status: Processing | Completed | Conflict
    TTL: 24h

ADR-019: Idempotency Key and Event Deduplication (shared with Domain 2)
```

#### HIGH: gRPC Contract Evolution

```
Proposed: buf linting + CI enforcement + backward compatibility policy

  Proto packages: his.hope.patient.v1 (versioned)
  Backward compatibility rules:
    ADD field → safe
    REMOVE field → mark as reserved, DO NOT reuse field number
    ADD RPC → safe
    REMOVE RPC → VIOLATION (keep stub returning UNIMPLEMENTED)

  CI enforcement: buf breaking check in pipeline (fail on violations)
  buf lint: enforce style guide
  EventTypeRegistry: replace Type.GetType() with explicit mapping

ADR-042: gRPC Contract Evolution and Compatibility
```

#### MEDIUM-HIGH: Cursor-Based Pagination

```
Proposed: Dual-mode pagination (cursor + backward-compatible offset)

  Cursor-based (recommended):
    Response: { items: [], nextCursor: "base64json" }
    Request: GET /patients?cursor={nextCursor}&limit=20
    Cursor: base64(JSON { lastId, lastSortField })
    Performance: WHERE id > @cursorId ORDER BY id LIMIT @limit (index scan)

  Offset (backward compat):
    Max offset: 1000 (10,000 records)
    Deprecate after 6 months

  Total count: opt-in only (?includeTotal=true)
  gRPC consistency: cursor + page_size in all list RPC messages

ADR-043: Pagination Strategy
```

#### MEDIUM-HIGH: Webhook Engine

```
Proposed: Webhook subscription API + delivery engine

  Registration: POST/GET/DELETE /webhooks
  Security: HMAC-SHA256 signature (X-Hope-Signature header)
  Delivery: exponential backoff retry (1s, 5s, 25s, 125s, 625s)
  Circuit breaker per webhook URL

  Event catalog:
    patient.{created,updated,deleted}
    appointment.{booked,cancelled}
    encounter.{created,finalized}
    lab.{order_created,result_available}
    billing.{invoice_generated,payment_received}
    pharmacy.{prescription_created,medication_dispensed}

  Monitoring: delivery_success_rate, delivery_latency_p95 metrics
  Alert: success rate < 90%

ADR-044: Webhook and External Push Notification Strategy
```

### 6.3 Implementation Plan (Domain 6)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-2 | gRPC Contract Evolution (buf + proto versioning + CI enforcement) |
| 1B | 2-3 | Idempotency Keys (API middleware) |
| 2 | 4-7 | **FHIR Gateway Service** (new service: FHIR + SMART + HL7v2 adapter) |
| 3A | 8-9 | Cursor-Based Pagination (dual-mode + keyset optimization) |
| 3B | 8-10 | Webhook Engine (subscription API + delivery worker + monitoring) |
| 4 | 11-12 | API Versioning Strategy (deprecation headers + sunset automation) |
| 5 | 13-14 | Long-Running Operations (async pattern + operation status) |

---

## Domain 7: Operational Excellence

### 7.1 Current State Assessment

| Pattern | Maturity | Key Finding |
|---------|----------|-------------|
| Feature Flags | L1 | Not implemented; no LaunchDarkly, Unleash, or custom solution |
| Capacity Planning | L1 | Targets only; no model, no forecasting, no growth projection |
| Auto-Remediation | L2 | Config skeleton exists; no actual remediation engine or K8s operator |
| Secret Rotation Automation | L2 | Manual Vault commands; no CronJob automation for DB, JWT, RabbitMQ |
| Tenant Isolation | L2 | Row-level security views only; no schema or database per tenant |
| Build/Deployment SLAs | L1 | No DORA metrics; no CI/CD pipeline performance targets |
| Runbook Completeness | L2 | Only 1 runbook (error-response); missing 15+ critical scenarios |
| Deployment Safety | L3 | Canary defined (5%→25%→50%→100%); no canary analysis criteria documented |
| Chaos Engineering | L3 | 14 experiment definitions; Game Day scheduled |
| Disaster Recovery | L3 | Comprehensive plan; RPO 60s, RTO 30s per service |
| Migration Safety | L4 | Additive-only, expand-contract, 3-step deploy — best practice |
| Incident Management | L3 | P0-P5 classification, postmortem template; no follow-the-sun rotation |

### 7.2 Proposed Upgrades

#### CRITICAL: Feature Flags

```
Proposed: Unleash (self-hosted) for progressive delivery

  Flag types:
    Release Toggle: hide WIP features
    Ops Toggle: instant kill switch (< 1s propagation)
    Permission Toggle: premium/preview user features
    Experiment Toggle: A/B testing

  Context: User ID, Tenant ID, Role, Region
  Lifecycle: Created → 10% Canary → 50% Beta → 100% GA → Deprecated → Removed
  Max lifetime: 60 days (avoid flag creep)
  Auto-reminder: alert after 30 days to clean up

  Integration:
    .NET: IFeatureFlagService in Shared Infrastructure
    Angular: FeatureFlagService + featureFlag directive
    NgRx: flag state in store for reactive UI

ADR-047: Feature Flag and Progressive Delivery Strategy
```

#### CRITICAL: Capacity Planning Operations

```
Proposed: Full capacity management lifecycle (shared with Domain 4)

  Weekly capacity review: utilization vs capacity per service, Prophet forecast
  Auto-triggers:
    >70% capacity in 14 days → auto Jira ticket
    >85% in 7 days → P2 alert + Slack
    >95% in 3 days → P1 incident + emergency scale

  Quarterly stress test: 2x projected 6-month peak, document bottlenecks
  Annual budget planning: growth model + 30% buffer
```

#### HIGH: Auto-Remediation Engine

```
Proposed: Custom K8s operator (HisHopeRemediation)

  Watch: Prometheus alerts via Alertmanager webhook
  Match: alert → RemediationPolicy CRD
  Execute: remediation action (scale, restart, rollback)
  Record: RemediationAction CRD (audit trail)

  Policy tiers:
    P0 - Auto (instant): pod crash → restart; OOM → increase memory
    P1 - Auto with notify: CB open → scale; latency high → scale
    P2 - Suggest: backlog high → recommend scale (human approval)
    P3-P4 - Log only: slow query → log for DBA review

  Safety guards: max 3 actions/service/hour, 5min cooldown, anti-loop circuit breaker
  Weekly review: review all auto-remediation actions, improve policies

ADR-048: Auto-Remediation Framework
```

#### HIGH: Secret Rotation Automation

```
Proposed: K8s CronJobs for each rotation type

  rotate-db-passwords (90d):
    Vault generate → update CRDB user → update K8s secret → rolling restart
    Grace period: 24h dual-password validity

  rotate-jwt-keys (90d):
    Vault transit rotate → increment key version
    Old keys: keep 1 version back for active token validation

  rotate-rabbitmq (90d):
    Vault generate → RabbitMQ API → update secret → rolling restart
    Connection drain: wait 30s before removing old user

  Safety: pre/post-rotation health check, auto-rollback on failure
  Monitoring: rotation age alert (>100 days = overdue)

ADR-049: Automated Secret Rotation Strategy
```

#### HIGH: Tenant Isolation

```
Proposed: 3-tier tenant isolation (soft → schema → database)

  Tier 1 — Soft (current, < 100 tenants):
    Row-level security views ✅
    Add per-tenant rate limiting and resource monitoring

  Tier 2 — Schema (> 100 tenants):
    CockroachDB schema per tenant
    Physical index separation, independent backup

  Tier 3 — Database (enterprise):
    Database per tenant
    Separate connection pool, independent scaling

  Tenant management API:
    POST /tenants: provision (schema, user, seed data)
    GET /tenants/{id}/health: per-tenant health
    DELETE /tenants/{id}: deprovision (GDPR right-to-erasure)

ADR-050: Tenant Isolation Strategy
```

#### MEDIUM: Build & Deployment SLAs

```
Proposed: DORA metrics dashboard with pipeline optimization

  Targets:
    Deployment frequency: 10/day (on-demand)
    Lead time: < 1 hour (commit → production)
    Change failure rate: < 5%
    MTTR: < 15 minutes

  Pipeline stage timing targets:
    Build: < 10min | Unit Test: < 5min | Integration: < 15min
    Security Scan: < 10min | Container Build: < 5min
    Deploy Staging: < 5min | Canary→Prod: 30min (observation)

  Optimization: Bazel caching, parallel test execution, incremental deployment

ADR-051: CI/CD Performance SLAs
```

#### MEDIUM: Runbook Completeness

```
Proposed: 20+ runbooks across infrastructure, application, security, deployment, data

  Standard template:
    Metadata (severity, service, owner) + Alert Trigger
    Symptoms → Diagnosis → Mitigation → Resolution → Verification → Postmortem link

  Prioritized runbook creation:
    1. CockroachDB Failure (node down, replication lag)
    2. Redis Cluster Failure (OOM, split brain)
    3. RabbitMQ Failure (queue overflow, connection exhaustion)
    4. Service Unavailable (liveness probe failure)
    5. High Latency (per-service diagnosis tree)
    6. Memory Leak / OOM Kill
    7. Token Theft / Session Hijacking
    8. Brute Force Attack Detected
    9. PHI Exfiltration Detected
    10. Failed Deployment Rollback

ADR-052: Runbook Standards and Coverage
```

### 7.3 Implementation Plan (Domain 7)

| Phase | Week | Tasks |
|-------|------|-------|
| 1A | 1-3 | Feature Flags (Unleash deploy + IFeatureFlagService + Angular directive) |
| 1B | 2-4 | Runbook Creation (top 10 missing runbooks) |
| 2A | 5-7 | Secret Rotation Automation (DB + JWT + RabbitMQ CronJobs) |
| 2B | 5-7 | Tenant Isolation (per-tenant rate limiting + health dashboard) |
| 3A | 8-10 | Auto-Remediation Engine (K8s Operator + remediation policies) |
| 3B | 8-10 | Build & Deployment SLAs (DORA metrics dashboard) |
| 4 | 11-13 | Capacity Planning Operations (weekly review + forecasting pipeline) |
| 5 | 14-16 | Runbook Coverage Completion (all 20+ runbooks) |

---

## Master Implementation Roadmap

### Giai đoạn 1: Foundation Hardening (Tuần 1–8)

| Week | Domain | Task | Type |
|------|--------|------|------|
| 1-2 | Reliability | Wire Resilience Pipeline (CB + Retry + Timeout) | **CRITICAL** |
| 1-2 | Observability | **SLO Rules Fix** (per-service recording rules) | **CRITICAL** |
| 1-2 | API | gRPC Contract Evolution (buf + proto versioning) | HIGH |
| 1-2 | Data | Fix `RemoveByPrefixAsync` | QUICK WIN |
| 2-3 | Reliability | Dead Letter Queue implementation | **CRITICAL** |
| 2-3 | Observability | **Tracing Sampling Strategy** | **CRITICAL** |
| 2-4 | Data | Idempotency Layer (middleware + DB) | HIGH |
| 3-4 | Reliability | Bulkhead implementation | **CRITICAL** |
| 3-4 | Scalability | PgBouncer sidecar deployment | **CRITICAL** |
| 4-6 | Security | MFA Implementation | **CRITICAL** |
| 4-6 | Security | BAA Management | **CRITICAL** |
| 5-7 | Data | Schema Registry + Event envelope | HIGH |
| 5-7 | Ops | Feature Flags deployment | **CRITICAL** |
| 7-8 | Scalability | Load test baseline + capacity model | **CRITICAL** |
| 7-8 | Security | Image Signing + Digest Pinning + Gatekeeper | **CRITICAL** |

### Giai đoạn 2: Structural Resilience (Tuần 9–16)

| Week | Domain | Task | Type |
|------|--------|------|------|
| 9-10 | Security | Data Masking (synthetic data + staging pipeline) | **CRITICAL** |
| 9-11 | Scalability | Multi-level caching (L1 + stampede prevention) | HIGH |
| 9-11 | API | **FHIR Gateway Service** (FHIR + SMART + HL7v2) | **CRITICAL** |
| 10-12 | Data | Distributed Locking (RedLock) | HIGH |
| 11-13 | Observability | Statistical Anomaly Detection | HIGH |
| 11-13 | Ops | Secret Rotation Automation | HIGH |
| 12-14 | Data | Data Lifecycle (soft-delete + archival + erasure) | HIGH |
| 13-15 | Security | API Abuse Prevention (per-user rate limit) | HIGH |
| 14-16 | Ops | Runbook Creation (10 missing runbooks) | MEDIUM |
| 14-16 | Observability | Synthetic Monitoring (Blackbox + HTTP checks) | HIGH |

### Giai đoạn 3: Advanced Capabilities (Tuần 17–24)

| Week | Domain | Task | Type |
|------|--------|------|------|
| 17-19 | Reliability | Persistent Saga Engine | HIGH |
| 17-19 | Scalability | Request QoS (PriorityClass + per-priority bulkhead) | HIGH |
| 18-20 | Observability | RUM Implementation (Web Vitals) | HIGH |
| 19-21 | API | Webhook Engine | MEDIUM |
| 20-22 | Security | PHI De-identification (CDC → BigQuery) | HIGH |
| 20-22 | Security | Enhanced Audit (SELECT audit + hash chain) | HIGH |
| 21-23 | Ops | Auto-Remediation Engine (K8s Operator) | HIGH |
| 21-23 | Reliability | Graceful Degradation (stale cache + fallback) | HIGH |
| 22-24 | Scalability | Adaptive Backpressure | HIGH |

### Giai đoạn 4: Optimization & Scale (Tuần 25+)

| Domain | Task | Type |
|--------|------|------|
| Scalability | HPA custom metrics + KEDA integration | HIGH |
| Scalability | Predictive scaling (Prophet model) | MEDIUM |
| Data | CQRS Read Models (event-sourced projections) | HIGH |
| Ops | Runbook Coverage Completion (20+ runbooks) | MEDIUM |
| Ops | Capacity Planning Operations (weekly review) | MEDIUM |
| Ops | Build & Deployment SLAs (DORA metrics) | MEDIUM |
| Observability | ML Anomaly Detection (Vertex AI pipeline) | HIGH |
| API | Long-Running Operations (async pattern) | MEDIUM |
| API | Cursor-Based Pagination | MEDIUM |

---

## New ADRs Required (40)

| ADR | Title | Domain |
|-----|-------|--------|
| 013 | Per-Dependency Resilience Pipeline Strategy | Reliability |
| 014 | Dead Letter Queue Strategy | Reliability |
| 015 | Bulkhead and Resource Isolation Strategy | Reliability |
| 016 | Persistent Saga Orchestration | Reliability |
| 017 | Graceful Degradation and Stale Cache Strategy | Reliability |
| 018 | Event Schema Registry and Versioning | Data |
| 019 | Idempotency Key and Event Deduplication | Data / API |
| 020 | Distributed Locking Strategy | Data |
| 021 | Data Lifecycle and Right-to-Erasure | Data |
| 022 | CQRS Read Model Separation Strategy | Data |
| 023 | Multi-Factor Authentication Strategy | Security |
| 024 | BAA Management and Vendor ePHI Controls | Security |
| 025 | Supply Chain Security Framework | Security |
| 026 | Data Masking and De-identification Strategy | Security |
| 027 | Enhanced Audit Trail with Immutability | Security |
| 028 | API Abuse Prevention Framework | Security |
| 029 | Database Connection Pool Management | Scalability |
| 030 | Capacity Planning Framework | Scalability |
| 031 | Multi-Level Caching Strategy | Scalability |
| 032 | Adaptive Backpressure and Load Shedding | Scalability |
| 033 | Quality of Service and Request Prioritization | Scalability |
| 034 | Advanced Horizontal Pod Autoscaling | Scalability |
| 035 | Distributed Tracing Sampling Strategy | Observability |
| 036 | Per-Service SLO Framework | Observability |
| 037 | Synthetic Monitoring Strategy | Observability |
| 038 | Real User Monitoring Strategy | Observability |
| 039 | Anomaly Detection Framework | Observability |
| 040 | USE Metrics Framework | Observability |
| 041 | FHIR and HL7 Interoperability Strategy | API |
| 042 | GRPC Contract Evolution and Compatibility | API |
| 043 | Pagination Strategy | API |
| 044 | Webhook and External Push Notification Strategy | API |
| 045 | API Versioning and Deprecation Policy | API |
| 046 | Long-Running Operation Pattern | API |
| 047 | Feature Flag and Progressive Delivery Strategy | Ops |
| 048 | Auto-Remediation Framework | Ops |
| 049 | Automated Secret Rotation Strategy | Ops |
| 050 | Tenant Isolation Strategy | Ops |
| 051 | CI/CD Performance SLAs | Ops |
| 052 | Runbook Standards and Coverage | Ops |

---

## Success Metrics

After completing all 4 phases, the system should achieve:

| Metric | Current | Target |
|--------|---------|--------|
| Overall Maturity | L2 (Repeatable) | L4 (Measured) |
| Critical Gaps Closed | 0/10 | 10/10 |
| High Gaps Closed | 0/30+ | 25/30+ |
| HIPAA Audit Readiness | Partial | Full (with MFA + BAA + Audit) |
| Production Deployment Safety | Low | High (Feature Flags + Auto-Remediation) |
| Interoperability | None | FHIR R4 + HL7v2 |
| Observability Completeness | 60% | 95% (SLOs + Sampling + Synthetic + RUM) |
| Automated Operations | 20% | 80% (Rotation + Remediation + Scaling) |

---

## References

- `docs/architecture.md` — System Architecture (2247 lines)
- `docs/adr/` — 12 existing Architecture Decision Records
- `docs/security/hipaa-compliance.md` — HIPAA Technical Safeguards mapping
- `docs/enterprise-roadmap.md` — 5-phase roadmap to 1B patients
- `docs/api/` — REST, gRPC, Events API references
