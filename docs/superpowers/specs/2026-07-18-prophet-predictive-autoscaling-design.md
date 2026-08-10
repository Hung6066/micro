# Prophet-Based Predictive Auto-Scaling

**Date:** 2026-07-18
**Status:** Approved
**Author:** ML/AI Engineering

## Problem Statement

Current HPA scaling is purely reactive — it responds to CPU/memory utilization and request-rate metrics with a lag. During rapid traffic surges (e.g., clinic opening hours, post-lunch rush, batch report generation), pods take minutes to spin up, causing latency spikes and potential timeouts. We need _predictive_ scaling that anticipates demand and pre-warms replicas before the load arrives.

## Design Overview

Train Facebook Prophet time-series models on historical request-rate data for each backend service, predict the next hour's expected peak, and proactively adjust HPA `minReplicas` to ensure sufficient capacity.

### Architecture

```
┌─────────────────────┐      ┌────────────────────────────┐
│     Prometheus      │◄─────│  ServiceMonitors (all svc) │
│  (http_requests_    │      └────────────────────────────┘
│   total)            │
└────────┬────────────┘
         │ 14d history @ 5m intervals
         ▼
┌─────────────────────┐
│  train_model.py     │  ← Manual / scheduled Job
│  (Prophet per svc)  │
└────────┬────────────┘
         │ .pkl files
         ▼
┌─────────────────────┐
│   Models PVC        │
│  (8 .pkl files)     │
└────────┬────────────┘
         │ loaded hourly
         ▼
┌─────────────────────┐      ┌────────────────────────────┐
│  predict.py         │─────►│  K8s API (patch HPA)       │
│  (CronJob, hourly)  │      └────────────────────────────┘
└─────────────────────┘
```

## Components

### 1. Configuration (`ml/prophet-scaling/config.yaml`)

Central configuration file defining:
- `prometheus_url`: Prometheus server address
- `services`: List of 8 backend services, each with:
  - `name`: Service name (matches `service` label in Prometheus)
  - `target_rps_per_pod`: Request rate target per replica
  - `min_replicas_lower_bound`: Safety floor
  - `max_replicas_upper_bound`: Ceiling
- `model_dir`: Path to model storage
- `training`: Prophet hyperparameters (seasonality mode, changepoint prior, etc.)
- `prediction`: Forecast horizon, interval width

### 2. Training Script (`ml/prophet-scaling/train_model.py`)

- Queries Prometheus for 14 days of 5-min interval `http_requests_total` rates
- Groups by `service` label, trains one Prophet model per service
- Model configuration:
  - Daily seasonality (auto-detected fourier order)
  - Weekly seasonality (captures weekday vs weekend patterns)
  - Additive seasonality (stable pattern, no multiplicative growth)
  - `changepoint_prior_scale=0.05` (allows moderate trend adaptation)
- Saves model + metadata as pickle to `models/<service>_prophet_model.pkl`
- Handles missing data gracefully (forward-fill gaps)

### 3. Prediction Script (`ml/prophet-scaling/predict.py`)

- Loads all 8 Prophet models from PVC
- Generates future dataframe for 60 minutes (12 steps × 5 min)
- Predicts with 80% prediction interval
- Formula:
  ```
  predicted_peak = max(yhat_upper[0:12])
  recommended    = ceil(predicted_peak / target_rps_per_pod)
  minReplicas    = clamp(recommended, lower_bound, upper_bound)
  ```
- Patches HPA `minReplicas` via Kubernetes Python client
- Outputs JSON summary with per-service details

### 4. Kubernetes CronJob (`k8s/autoscaling/predictive-scaler-cronjob.yaml`)

- Schedule: `0 * * * *` (top of every hour)
- Namespace: `his-hope`
- Single container: `python:3.11-slim` with Prophet + kubernetes client
- PVC mount for model storage
- ServiceAccount with RBAC to `patch` HPAs
- Security context: non-root, seccomp RuntimeDefault, all capabilities dropped

### 5. PersistentVolumeClaim

- Name: `predictive-scaler-models`
- AccessMode: `ReadWriteOnce`
- Storage: 1Gi (8 Prophet models ~ 8 × 200KB = 1.6MB, with headroom)

## Target Services & Parameters

| Service | Target RPS/Pod | Lower Bound | Upper Bound |
|---------|---------------|-------------|-------------|
| api-gateway | 1000 | 2 | 10 |
| patient-service | 500 | 3 | 30 |
| appointment-service | 500 | 3 | 20 |
| clinical-service | 500 | 2 | 15 |
| identity-service | 500 | 2 | 10 |
| lab-service | 500 | 2 | 15 |
| billing-service | 500 | 2 | 10 |
| pharmacy-service | 500 | 2 | 10 |

## Data Flow

1. **Training**: Prometheus → `train_model.py` → 8 × `.pkl` → PVC
2. **Prediction (hourly)**: PVC → `predict.py` → K8s API (HPA patch)
3. **Observation**: Standard Prometheus metrics + structured JSON logging

## Error Handling

- **Prometheus unreachable**: Log error, use existing `minReplicas` unchanged
- **Model file missing**: Trigger auto-retrain if missing, else skip service
- **HPA patch failure**: Log error, continue to next service
- **Invalid prediction (NaN)**: Fall back to lower bound

## Security

- ServiceAccount with least-privilege RBAC: only `patch` HPAs, `get` deployments
- No secrets in config — Prometheus address passed as env var
- Read-only root filesystem (models PVC is the only write mount)

## Files

| # | File | Description |
|---|------|-------------|
| 1 | `ml/prophet-scaling/config.yaml` | Central configuration |
| 2 | `ml/prophet-scaling/train_model.py` | Prophet model training |
| 3 | `ml/prophet-scaling/predict.py` | Prediction + HPA patching |
| 4 | `k8s/autoscaling/predictive-scaler-cronjob.yaml` | CronJob + SA + RBAC + PVC |

## Future Considerations

- **Online training**: Retrain incrementally as new data arrives
- **Anomaly detection**: Graceful handling of holiday spikes (Tet, Lunar New Year)
- **Multi-step horizon**: Predict 2-4 hours ahead for slow-scaling services
- **Vertex AI integration**: Move training to Vertex AI Pipelines for managed orchestration
