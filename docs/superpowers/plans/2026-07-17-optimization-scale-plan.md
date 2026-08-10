# Optimization & Scale — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) to implement this plan task-by-task.

**Goal:** Implement advanced optimization capabilities: ML anomaly detection via Vertex AI, predictive auto-scaling with Prophet model, CQRS event-sourced read projections, long-running async operations, and HPA custom metrics.

**Tech Stack:** Vertex AI, Prophet, Prometheus Adapter, KEDA, CockroachDB, MediatR, .NET 8

## Global Constraints

- All database migrations must be backward-compatible (additive-only)
- All secrets managed via HashiCorp Vault — never hardcoded
- JWT auth required on all endpoints (except /health)
- Conventional Commits: `feat(domain): description`

---

### Task 1: HPA Custom Metrics + KEDA Integration

**Files:**
- Create: `k8s/autoscaling/keda-scaledobjects.yaml`
- Create: `k8s/autoscaling/prometheus-adapter-config.yaml`
- Modify: `k8s/base/*-service.yaml` — update HPA with custom metrics

**Steps:**
1. Create KEDA ScaledObjects for outbox processor (scale on RabbitMQ queue depth) and patient-service (scale on request rate)
2. Create Prometheus Adapter config exposing custom metrics to HPA
3. Update all service HPAs with multi-metric: max(cpu, memory, custom_metric)
4. Commit: `feat(scalability): add HPA custom metrics and KEDA event-driven autoscaling`

---

### Task 2: Predictive Scaling — Prophet Model

**Files:**
- Create: `ml/prophet-scaling/train_model.py`
- Create: `ml/prophet-scaling/predict.py`
- Create: `k8s/autoscaling/predictive-scaler-cronjob.yaml`

**Steps:**
1. Create Prophet training script using historical Prometheus request rate data
2. Create prediction script outputting recommended minReplicas for next hour
3. Create K8s CronJob that runs predict script hourly, patches HPA minReplicas
4. Commit: `feat(scalability): implement Prophet-based predictive auto-scaling`

---

### Task 3: CQRS Read Models — Event-Sourced Projections

**Files:**
- Create: `src/Services/PatientService/PatientService.Infrastructure/Projections/PatientProjection.cs`
- Create: `src/Services/PatientService/PatientService.Infrastructure/Projections/PatientProjector.cs`
- Create: `cockroach/migrations/022-patient-read-models.sql`

**Steps:**
1. Create materialized view or separate table for patient read model (denormalized for query performance)
2. Create PatientProjector consuming PatientRegistered/Updated events to rebuild read model
3. Add EF Core DbContext for read model with no tracking
4. Commit: `feat(data): implement CQRS event-sourced read projections for patient queries`

---

### Task 4: Long-Running Async Operations

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/AsyncOperations/OperationStatus.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/AsyncOperations/AsyncOperationMiddleware.cs`
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/AsyncOperations/IAsyncOperationHandler.cs`

**Steps:**
1. Create OperationStatus entity and DB table (Id, Status, Progress, Result JSON, CreatedAt, CompletedAt, TTL 24h)
2. Create AsyncOperationMiddleware: 202 Accepted + Location header pattern
3. Create IAsyncOperationHandler<TRequest, TResult> interface
4. Create GET /operations/{id} endpoint returning status/progress/result
5. Commit: `feat(api): implement async request-reply pattern for long-running operations`

---

### Task 5: ML Anomaly Detection — Vertex AI Pipeline

**Files:**
- Create: `ml/anomaly-detection/train_pipeline.py`
- Create: `ml/anomaly-detection/serve_model.py`
- Create: `cicd/tekton/pipelines/ml-train-deploy.yaml`

**Steps:**
1. Create Vertex AI training pipeline: LSTM autoencoder on multi-metric time series
2. Create model serving endpoint exposing anomaly score API
3. Create Alertmanager webhook integration: forward alerts to anomaly service for verification
4. Commit: `feat(ml): implement ML-based anomaly detection with Vertex AI pipeline`

---

### Task 6: Build & Deployment SLAs — DORA Metrics

**Files:**
- Create: `k8s/monitoring/dora-metrics-dashboard.yaml`
- Create: `cicd/tekton/tasks/pipeline-metrics-exporter.yaml`

**Steps:**
1. Create DORA metrics Grafana dashboard: Deployment Frequency, Lead Time, Change Failure Rate, MTTR
2. Create Tekton task exporting pipeline metrics to Prometheus
3. Set SLAs: deploy < 10/day, lead time < 1h, failure rate < 5%, MTTR < 15min
4. Commit: `feat(ops): implement DORA metrics dashboard for CI/CD performance tracking`

---

## Plan Verification

- [ ] KEDA ScaledObjects scaling outbox processors on queue depth
- [ ] Prophet model generating hourly predictions
- [ ] Patient read model serving queries without hitting write DB
- [ ] Long-running operations returning 202 + status endpoint
- [ ] Vertex AI anomaly detection endpoint serving predictions
- [ ] DORA metrics visible in Grafana dashboard
