# ML Anomaly Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build ML-based anomaly detection system that monitors service metrics from Prometheus, detects anomalous patterns via LSTM autoencoder, and integrates with Alertmanager for false-positive suppression.

**Architecture:** Vertex AI pipeline trains LSTM autoencoder on 4 weeks of Prometheus metrics (request_rate, error_rate, latency_p99) after filtering known incident periods. Trained model deployed to Vertex AI endpoint with REST API. Alertmanager webhook queries model for confirmation before firing alerts.

**Tech Stack:** Python, TensorFlow/Keras, Vertex AI, Prometheus, FastAPI, Flask, Tekton, scikit-learn

## Global Constraints

- Follow existing codebase conventions in `ml/` directory (see `ml/prophet-scaling/` for reference patterns)
- Use same logging format as existing ML code (`logging.basicConfig` with ISO format)
- Config files in YAML with same structure as existing `ml/training/no-show-prediction/config.yaml`
- Training must be reproducible (deterministic seeds, versioned data snapshots)
- All models logged to Vertex AI Model Registry with metrics and artifacts
- Tekton pipeline follows same style as existing `cicd/tekton/pipelines/ci-pipeline.yaml`
- Python 3.11+ compatibility
- Dependency management via `requirements.txt` in each component

---

### Task 1: Training Pipeline (`ml/anomaly-detection/train_pipeline.py`)

**Files:**
- Create: `ml/anomaly-detection/__init__.py` (empty)
- Create: `ml/anomaly-detection/config.yaml`
- Create: `ml/anomaly-detection/train_pipeline.py`

**Interfaces:**
- Consumes: Prometheus metrics (request_rate, error_rate, latency_p99 per service)
- Produces: Saved Keras model artifact and metadata

- [ ] **Step 1: Create `ml/anomaly-detection/` directory and `__init__.py`**

Create the directory structure and init file.

```bash
mkdir -p ml/anomaly-detection
```

- [ ] **Step 2: Create `ml/anomaly-detection/config.yaml`**

```yaml
# =============================================================================
# ML Anomaly Detection — Configuration
# =============================================================================

project: his-hope-analytics
region: us-east1

prometheus_url: "http://prometheus-k8s.monitoring.svc.cluster.local:9090"

display_name: anomaly-detector-v1

dataset:
  source: prometheus
  history_days: 28
  data_interval_minutes: 1
  metrics:
    - name: request_rate
      promql: 'sum(rate(http_requests_total{service="%s"}[1m])) by (service)'
    - name: error_rate
      promql: 'sum(rate(http_requests_total{status=~"5.."}[1m])) by (service) / sum(rate(http_requests_total{}[1m])) by (service)'
    - name: latency_p99
      promql: 'histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket{}[1m])) by (service, le))'

  incident_periods:
    # Known incidents to exclude from training data
    - service: "*"
      start: "2026-07-04T10:00:00Z"
      end: "2026-07-04T14:30:00Z"
      reason: "Database failover incident"

model:
  type: lstm_autoencoder
  sequence_length: 60
  latent_dim: 8
  encoding_layers: [32, 16]
  learning_rate: 0.001
  batch_size: 64
  epochs: 100
  early_stopping_patience: 10
  validation_split: 0.2
  anomaly_threshold_percentile: 95.0

serving:
  machine_type: n1-standard-4
  min_replicas: 1
  max_replicas: 3

monitoring:
  prediction_drift_threshold: 0.15
  feature_drift_threshold: 0.2

schedule:
  frequency: daily
  time: "03:00"
```

- [ ] **Step 3: Create `ml/anomaly-detection/train_pipeline.py`**

```python
#!/usr/bin/env python3
"""
Anomaly Detection Training Pipeline — Vertex AI
=================================================
Queries Prometheus for 4 weeks of service metrics (request_rate, error_rate,
latency_p99) at 1-minute intervals, filters out known incident periods,
trains an LSTM autoencoder on normal patterns, and uploads the model to
Vertex AI Model Registry.

Usage:
    python train_pipeline.py                          # uses config.yaml
    python train_pipeline.py --config /path/to/yaml   # custom config

Requires:  pip install tensorflow google-cloud-aiplatform prometheus-api-client pyyaml pandas numpy
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
import tempfile
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import numpy as np
import pandas as pd
import requests
import yaml

os.environ["TF_CPP_MIN_LOG_LEVEL"] = "2"  # suppress TF info/warnings

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S%z",
)
logger = logging.getLogger("anomaly_train")

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
def load_config(path: str | Path) -> dict[str, Any]:
    """Load YAML configuration file."""
    with open(path, "r") as f:
        cfg = yaml.safe_load(f)
    cfg["prometheus_url"] = os.environ.get(
        "PROMETHEUS_URL",
        cfg.get("prometheus_url", "http://prometheus-k8s.monitoring.svc.cluster.local:9090"),
    )
    return cfg

# ---------------------------------------------------------------------------
# Prometheus data fetching
# ---------------------------------------------------------------------------
SERVICE_LIST = [
    "api-gateway",
    "patient-service",
    "appointment-service",
    "clinical-service",
    "identity-service",
    "lab-service",
    "billing-service",
    "pharmacy-service",
]


def query_prometheus_range(
    prom_url: str,
    query: str,
    start: datetime,
    end: datetime,
    step: str = "1m",
) -> pd.DataFrame:
    """
    Execute a Prometheus range query and return results as a DataFrame
    with columns ``ds`` (datetime) and ``y`` (float value).
    """
    params = {
        "query": query,
        "start": start.timestamp(),
        "end": end.timestamp(),
        "step": step,
    }
    try:
        resp = requests.get(
            f"{prom_url.rstrip('/')}/api/v1/query_range",
            params=params,
            timeout=60,
        )
        resp.raise_for_status()
    except requests.RequestException as exc:
        logger.error("Prometheus query failed: %s", exc)
        return pd.DataFrame(columns=["ds", "y", "service"])

    data = resp.json()
    if data.get("status") != "success":
        logger.error("Prometheus returned error: %s", data.get("error", "unknown"))
        return pd.DataFrame(columns=["ds", "y", "service"])

    results = data.get("data", {}).get("result", [])
    if not results:
        logger.warning("No data returned for query")
        return pd.DataFrame(columns=["ds", "y", "service"])

    records: list[dict] = []
    for series in results:
        metric = series.get("metric", {})
        svc = metric.get("service", "unknown")
        values = series.get("values", [])
        for ts_str, val_str in values:
            records.append({
                "ds": datetime.fromtimestamp(float(ts_str), tz=timezone.utc),
                "y": float(val_str) if val_str not in ("NaN", "+Inf", "-Inf", "") else 0.0,
                "service": svc,
            })

    df = pd.DataFrame(records)
    if df.empty:
        return df

    df = df.sort_values(["service", "ds"]).reset_index(drop=True)
    # Aggregate per service per timestamp (sum for multi-pod services)
    df = df.groupby(["service", "ds"], as_index=False)["y"].sum()

    # Forward-fill small gaps (up to 3 missing intervals = 3 min)
    def _ffill_group(g):
        g = g.set_index("ds").asfreq("1min", method="ffill", limit=3).reset_index()
        return g
    df = df.groupby("service", group_keys=False).apply(_ffill_group)
    df = df.dropna(subset=["y"])
    return df


def fetch_all_metrics(
    config: dict[str, Any],
) -> dict[str, pd.DataFrame]:
    """
    Fetch all configured metrics for all services from Prometheus.

    Returns a dict mapping metric_name -> DataFrame with columns:
        ds, service, y
    """
    prom_url = config["prometheus_url"]
    train_cfg = config.get("dataset", {})
    history_days = train_cfg.get("history_days", 28)
    step = f"{train_cfg.get('data_interval_minutes', 1)}m"
    metrics_cfg = train_cfg.get("metrics", [])

    end = datetime.now(timezone.utc)
    start = end - timedelta(days=history_days)

    logger.info("Fetching %d metrics across %d services...", len(metrics_cfg), len(SERVICE_LIST))
    logger.info("  Query window: %s  →  %s", start.isoformat(), end.isoformat())

    all_data: dict[str, pd.DataFrame] = {}
    for metric_def in metrics_cfg:
        metric_name = metric_def["name"]
        promql_template = metric_def["promql"]
        df_list: list[pd.DataFrame] = []

        for svc in SERVICE_LIST:
            promql = promql_template % svc
            logger.debug("  Querying %s for %s...", metric_name, svc)
            df_svc = query_prometheus_range(prom_url, promql, start, end, step)
            if not df_svc.empty:
                df_list.append(df_svc)

        if df_list:
            combined = pd.concat(df_list, ignore_index=True)
            combined = combined.sort_values(["service", "ds"]).reset_index(drop=True)
            all_data[metric_name] = combined
            logger.info("  ✓ %s: %d data points", metric_name, len(combined))
        else:
            logger.warning("  ✗ %s: no data returned", metric_name)
            all_data[metric_name] = pd.DataFrame(columns=["ds", "service", "y"])

    return all_data


# ---------------------------------------------------------------------------
# Incident period filtering
# ---------------------------------------------------------------------------
def filter_incidents(
    data: dict[str, pd.DataFrame],
    config: dict[str, Any],
) -> dict[str, pd.DataFrame]:
    """
    Remove data points that fall within known incident periods.
    """
    incident_periods = config.get("dataset", {}).get("incident_periods", [])
    if not incident_periods:
        return data

    total_removed = 0
    for metric_name, df in data.items():
        if df.empty:
            continue
        before = len(df)
        for incident in incident_periods:
            svc_filter = incident.get("service", "*")
            start = datetime.fromisoformat(incident["start"])
            end = datetime.fromisoformat(incident["end"])

            mask = (df["ds"] >= start) & (df["ds"] <= end)
            if svc_filter != "*":
                mask = mask & (df["service"] == svc_filter)

            df.drop(df[mask].index, inplace=True)

        after = len(df)
        total_removed += before - after
        data[metric_name] = df.reset_index(drop=True)

    logger.info("Filtered %d data points in known incident periods", total_removed)
    return data


# ---------------------------------------------------------------------------
# Feature engineering
# ---------------------------------------------------------------------------
def build_sequences(
    data: dict[str, pd.DataFrame],
    config: dict[str, Any],
) -> Tuple[np.ndarray, np.ndarray, dict[str, Any]]:
    """
    Convert multi-service, multi-metric time series into sliding-window
    sequences for the LSTM autoencoder.

    Returns:
        - X: numpy array of shape (num_sequences, sequence_length, num_features)
        - y: same as X (autoencoder target)
        - metadata: dict with scaling parameters and feature mapping
    """
    model_cfg = config.get("model", {})
    seq_len = model_cfg.get("sequence_length", 60)

    # Pivot data: for each service, create a matrix of [time, metrics]
    # First, merge all metrics on (service, ds)
    metric_names = list(data.keys())
    merged: Optional[pd.DataFrame] = None

    for metric_name in metric_names:
        df = data[metric_name][["service", "ds", "y"]].rename(
            columns={"y": metric_name}
        )
        if merged is None:
            merged = df
        else:
            merged = merged.merge(df, on=["service", "ds"], how="outer")

    if merged is None or merged.empty:
        raise ValueError("No data available after merging metrics")

    # Sort and fill any remaining NaNs (from partial metric availability)
    merged = merged.sort_values(["service", "ds"]).reset_index(drop=True)
    merged = merged.groupby("service", group_keys=False).apply(
        lambda g: g.fillna(method="ffill").fillna(0)
    )

    # Normalize per service per feature
    feature_cols = metric_names
    scaler_params: dict[str, dict[str, float]] = {}

    for svc in merged["service"].unique():
        svc_mask = merged["service"] == svc
        svc_data = merged.loc[svc_mask, feature_cols]
        scaler_params[svc] = {
            "mean": svc_data.mean().to_dict(),
            "std": svc_data.std().replace(0, 1.0).to_dict(),
        }
        merged.loc[svc_mask, feature_cols] = (
            svc_data - svc_data.mean()
        ) / svc_data.std().replace(0, 1.0)

    # Build sequences
    sequences: list[np.ndarray] = []
    for svc in merged["service"].unique():
        svc_data = merged[merged["service"] == svc][feature_cols].values
        n = len(svc_data)
        if n < seq_len + 1:
            logger.warning("Service %s has insufficient data (%d rows, need %d)", svc, n, seq_len + 1)
            continue
        for i in range(n - seq_len + 1):
            sequences.append(svc_data[i : i + seq_len])

    if not sequences:
        raise ValueError("No sequences could be built from the data")

    X = np.array(sequences)
    y = X.copy()

    meta = {
        "feature_cols": feature_cols,
        "sequence_length": seq_len,
        "scaler_params": scaler_params,
        "num_services": len(merged["service"].unique()),
        "num_sequences": len(sequences),
        "services_trained": merged["service"].unique().tolist(),
    }

    logger.info("Built %d sequences (seq_len=%d, features=%d)", len(sequences), seq_len, len(feature_cols))
    return X, y, meta


# ---------------------------------------------------------------------------
# LSTM Autoencoder model
# ---------------------------------------------------------------------------
def build_lstm_autoencoder(
    sequence_length: int,
    num_features: int,
    model_cfg: dict[str, Any],
) -> Any:
    """
    Build an LSTM autoencoder model.

    Architecture:
        Encoder: LSTM(32) → LSTM(latent_dim)
        Decoder: RepeatVector → LSTM(32) → LSTM(num_features) → TimeDistributed(Dense)
    """
    import tensorflow as tf

    tf.random.set_seed(42)
    np.random.seed(42)

    latent_dim = model_cfg.get("latent_dim", 8)
    encoding_layers = model_cfg.get("encoding_layers", [32, 16])
    learning_rate = model_cfg.get("learning_rate", 0.001)

    # Encoder
    inputs = tf.keras.Input(shape=(sequence_length, num_features), name="input_series")
    x = inputs
    for units in encoding_layers:
        x = tf.keras.layers.LSTM(units, return_sequences=True, name=f"encoder_lstm_{units}")(x)
    x = tf.keras.layers.LSTM(latent_dim, return_sequences=False, name="bottleneck")(x)

    # Decoder
    x = tf.keras.layers.RepeatVector(sequence_length, name="repeat_vector")(x)
    for units in reversed(encoding_layers):
        x = tf.keras.layers.LSTM(units, return_sequences=True, name=f"decoder_lstm_{units}")(x)
    x = tf.keras.layers.TimeDistributed(
        tf.keras.layers.Dense(num_features, name="output_dense"),
        name="decoder_output",
    )(x)

    model = tf.keras.Model(inputs=inputs, outputs=x, name="lstm_autoencoder")
    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=learning_rate),
        loss="mse",
    )

    logger.info(
        "Built LSTM autoencoder: input=(%d, %d), latent=%d, layers=%s",
        sequence_length, num_features, latent_dim, encoding_layers,
    )
    model.summary(print_fn=logger.info)
    return model


# ---------------------------------------------------------------------------
# Training
# ---------------------------------------------------------------------------
def train_model(
    X: np.ndarray,
    y: np.ndarray,
    model_cfg: dict[str, Any],
) -> Tuple[Any, dict[str, Any]]:
    """
    Train the LSTM autoencoder.

    Returns (trained_model, training_history).
    """
    import tensorflow as tf

    batch_size = model_cfg.get("batch_size", 64)
    epochs = model_cfg.get("epochs", 100)
    validation_split = model_cfg.get("validation_split", 0.2)
    patience = model_cfg.get("early_stopping_patience", 10)

    callbacks = [
        tf.keras.callbacks.EarlyStopping(
            monitor="val_loss",
            patience=patience,
            restore_best_weights=True,
            verbose=1,
        ),
        tf.keras.callbacks.ReduceLROnPlateau(
            monitor="val_loss",
            factor=0.5,
            patience=patience // 2,
            min_lr=1e-6,
            verbose=1,
        ),
    ]

    logger.info(
        "Training LSTM autoencoder: X.shape=%s, batch_size=%d, epochs=%d",
        X.shape, batch_size, epochs,
    )

    history = model.fit(
        X, y,
        batch_size=batch_size,
        epochs=epochs,
        validation_split=validation_split,
        callbacks=callbacks,
        verbose=2,
    )

    history_dict = {
        "loss": [float(v) for v in history.history["loss"]],
        "val_loss": [float(v) for v in history.history["val_loss"]],
        "best_val_loss": float(min(history.history["val_loss"])),
        "epochs_trained": len(history.history["loss"]),
    }
    logger.info("Training complete. Best val_loss: %.6f", history_dict["best_val_loss"])
    return model, history_dict


# ---------------------------------------------------------------------------
# Anomaly threshold computation
# ---------------------------------------------------------------------------
def compute_anomaly_threshold(
    model: Any,
    X: np.ndarray,
    percentile: float = 95.0,
) -> float:
    """
    Compute reconstruction error threshold at the given percentile
    over the training data.
    """
    reconstructions = model.predict(X, verbose=0)
    mse = np.mean(np.square(X - reconstructions), axis=(1, 2))
    threshold = float(np.percentile(mse, percentile))
    logger.info(
        "Anomaly threshold (P%.1f): %.6f (min=%.6f, max=%.6f, mean=%.6f)",
        percentile, threshold, mse.min(), mse.max(), mse.mean(),
    )
    return threshold


# ---------------------------------------------------------------------------
# Upload to Vertex AI Model Registry
# ---------------------------------------------------------------------------
def upload_to_vertex_ai(
    model: Any,
    metadata: dict[str, Any],
    history: dict[str, Any],
    threshold: float,
    config: dict[str, Any],
    model_dir: str | Path,
) -> str:
    """
    Upload the trained model to Vertex AI Model Registry.

    Returns the Vertex AI resource name of the uploaded model.
    """
    from google.cloud import aiplatform

    project = config.get("project", "his-hope-analytics")
    region = config.get("region", "us-east1")
    display_name = config.get("display_name", "anomaly-detector-v1")

    aiplatform.init(project=project, location=region)

    # Save model in SavedModel format
    model_path = str(Path(model_dir) / "anomaly-model")
    model.save(model_path)
    logger.info("Model saved to %s", model_path)

    # Save metadata JSON sidecar
    meta_path = Path(model_dir) / "anomaly-metadata.json"
    meta_export = {
        "feature_cols": metadata["feature_cols"],
        "sequence_length": metadata["sequence_length"],
        "scaler_params": metadata["scaler_params"],
        "services_trained": metadata["services_trained"],
        "num_sequences": metadata["num_sequences"],
        "anomaly_threshold": threshold,
        "best_val_loss": history["best_val_loss"],
        "trained_at": datetime.now(timezone.utc).isoformat(),
    }
    with open(meta_path, "w") as f:
        json.dump(meta_export, f, indent=2, default=str)
    logger.info("Metadata saved to %s", meta_path)

    # Upload to Vertex AI
    model_resource = aiplatform.Model.upload(
        display_name=display_name,
        artifact_uri=model_path,
        serving_container_image_uri="us-docker.pkg.dev/vertex-ai/prediction/tf2-cpu.2-15:latest",
        description=f"LSTM autoencoder for service metric anomaly detection. Trained on {metadata['num_sequences']} sequences.",
        labels={
            "model-type": "lstm-autoencoder",
            "project": "anomaly-detection",
        },
    )

    logger.info("Uploaded model to Vertex AI: %s", model_resource.resource_name)
    return model_resource.resource_name


# ---------------------------------------------------------------------------
# Main pipeline
# ---------------------------------------------------------------------------
def run_pipeline(config: dict[str, Any]) -> dict[str, Any]:
    """
    Execute the full training pipeline:
      1. Fetch metrics from Prometheus
      2. Filter incident periods
      3. Build sequences
      4. Train LSTM autoencoder
      5. Compute anomaly threshold
      6. Upload to Vertex AI

    Returns a summary dict.
    """
    import tensorflow as tf

    results: dict[str, Any] = {
        "status": "started",
        "pipeline_start": datetime.now(timezone.utc).isoformat(),
    }

    # Step 1-2: Fetch & filter
    logger.info("=" * 60)
    logger.info("STEP 1/6: Fetching Prometheus metrics...")
    raw_data = fetch_all_metrics(config)

    logger.info("STEP 2/6: Filtering incident periods...")
    clean_data = filter_incidents(raw_data, config)

    # Step 3: Build sequences
    logger.info("STEP 3/6: Building sequences...")
    X, y, metadata = build_sequences(clean_data, config)
    results["num_sequences"] = int(metadata["num_sequences"])
    results["services_trained"] = metadata["services_trained"]

    # Step 4: Train model
    logger.info("STEP 4/6: Training LSTM autoencoder...")
    model_cfg = config.get("model", {})
    model, history = train_model(X, y, model_cfg)
    results["best_val_loss"] = history["best_val_loss"]

    # Step 5: Compute threshold
    logger.info("STEP 5/6: Computing anomaly threshold...")
    threshold_percentile = model_cfg.get("anomaly_threshold_percentile", 95.0)
    threshold = compute_anomaly_threshold(model, X, threshold_percentile)
    results["anomaly_threshold"] = threshold

    # Step 6: Upload
    logger.info("STEP 6/6: Uploading to Vertex AI...")
    with tempfile.TemporaryDirectory(prefix="anomaly-model-") as tmpdir:
        model_resource = upload_to_vertex_ai(
            model, metadata, history, threshold, config, tmpdir,
        )
        results["vertex_model_resource"] = model_resource

    results["status"] = "success"
    results["pipeline_end"] = datetime.now(timezone.utc).isoformat()
    logger.info("=" * 60)
    logger.info("Pipeline complete! Model: %s", results["vertex_model_resource"])
    return results


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Train LSTM autoencoder anomaly detection model"
    )
    parser.add_argument(
        "--config",
        default=os.path.join(os.path.dirname(__file__), "config.yaml"),
        help="Path to configuration YAML (default: ./config.yaml)",
    )
    parser.add_argument(
        "--local",
        action="store_true",
        help="Run locally instead of submitting to Vertex AI (for testing)",
    )
    args = parser.parse_args()

    config = load_config(args.config)

    if args.local:
        logger.info("Running pipeline locally...")
        results = run_pipeline(config)
        print(json.dumps(results, indent=2, default=str))
    else:
        logger.info("Submitting pipeline to Vertex AI...")
        # Vertex AI Pipeline job submission
        from google.cloud import aiplatform
        aiplatform.init(
            project=config.get("project", "his-hope-analytics"),
            location=config.get("region", "us-east1"),
        )

        job = aiplatform.PipelineJob(
            display_name=f"anomaly-detection-{datetime.now().strftime('%Y%m%d-%H%M%S')}",
            template_path="",  # built dynamically or referenced
            pipeline_root=f"gs://{config.get('project', 'his-hope-analytics')}-vertex/pipelines/anomaly",
            parameter_values={
                "config": json.dumps(config),
                "local": False,
            },
            enable_caching=True,
        )
        job.submit()
        logger.info("Pipeline job submitted: %s", job.resource_name)


if __name__ == "__main__":
    main()
```

---

### Task 2: Serving Script (`ml/anomaly-detection/serve_model.py`)

**Files:**
- Create: `ml/anomaly-detection/serve_model.py`

**Interfaces:**
- Consumes: Trained model from Vertex AI, Prometheus metrics at inference time
- Produces: REST API: `POST /predict` → `{anomaly_score, is_anomalous, contributing_metrics}`

- [ ] **Step 1: Create `ml/anomaly-detection/serve_model.py`**

```python
#!/usr/bin/env python3
"""
Anomaly Detection Model Serving — Vertex AI Endpoint
======================================================
Deploys trained LSTM autoencoder model to a Vertex AI endpoint and
exposes a REST API for real-time anomaly detection.

API:
    POST /predict
    {
        "service": "patient-service",
        "metrics_window": [
            {"request_rate": 150, "error_rate": 0.02, "latency_p99": 0.45},
            ...
        ]
    }
    → {
        "anomaly_score": 0.87,
        "is_anomalous": true,
        "contributing_metrics": {"error_rate": 0.52},
        "reconstruction_errors": {"request_rate": 0.12, "error_rate": 0.52, "latency_p99": 0.23}
    }

Usage:
    python serve_model.py                          # starts FastAPI server
    python serve_model.py --deploy                 # deploy to Vertex AI endpoint

Requires:  pip install fastapi uvicorn tensorflow google-cloud-aiplatform numpy pandas
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional

import numpy as np
import yaml

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S%z",
)
logger = logging.getLogger("anomaly_serve")

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
def load_config(path: str | Path) -> dict[str, Any]:
    with open(path, "r") as f:
        cfg = yaml.safe_load(f)
    return cfg


# ---------------------------------------------------------------------------
# Model loader
# ---------------------------------------------------------------------------
class AnomalyDetector:
    """
    Wraps the trained LSTM autoencoder model for inference.
    """

    def __init__(
        self,
        model_path: str | None = None,
        metadata_path: str | None = None,
        config: dict[str, Any] | None = None,
    ):
        self.model = None
        self.metadata: dict[str, Any] = {}
        self.config = config or {}
        self._loaded = False

        if model_path:
            self.load(model_path, metadata_path)

    def load(self, model_path: str, metadata_path: str | None = None) -> None:
        """Load model from SavedModel directory and optional metadata."""
        import tensorflow as tf

        logger.info("Loading model from %s...", model_path)
        self.model = tf.keras.models.load_model(model_path)
        self._loaded = True
        logger.info("Model loaded successfully")

        if metadata_path and Path(metadata_path).exists():
            with open(metadata_path, "r") as f:
                self.metadata = json.load(f)
            logger.info("Metadata loaded from %s", metadata_path)

    def predict(
        self,
        service: str,
        metrics_window: list[dict[str, float]],
    ) -> dict[str, Any]:
        """
        Run anomaly detection on a window of metrics for a given service.

        Args:
            service: Name of the service (e.g., "patient-service")
            metrics_window: List of dicts, each with keys matching feature_cols.
                Length must equal sequence_length from training.

        Returns:
            Dict with anomaly_score, is_anomalous, contributing_metrics, etc.
        """
        if not self._loaded or self.model is None:
            raise RuntimeError("Model not loaded. Call load() first.")

        seq_len = self.metadata.get("sequence_length", 60)
        feature_cols = self.metadata.get("feature_cols", [])
        scaler_params = self.metadata.get("scaler_params", {})
        threshold = self.metadata.get("anomaly_threshold", float("inf"))

        if len(metrics_window) != seq_len:
            raise ValueError(
                f"Expected metrics_window of length {seq_len}, got {len(metrics_window)}"
            )

        # Validate and extract features
        X_raw = np.zeros((seq_len, len(feature_cols)))
        for i, point in enumerate(metrics_window):
            for j, col in enumerate(feature_cols):
                X_raw[i, j] = point.get(col, 0.0)

        # Scale using per-service parameters
        svc_params = scaler_params.get(service, scaler_params.get(list(scaler_params.keys())[0], {}))
        mean_vals = np.array([svc_params.get("mean", {}).get(c, 0.0) for c in feature_cols])
        std_vals = np.array([svc_params.get("std", {}).get(c, 1.0) for c in feature_cols])
        std_vals = np.where(std_vals == 0, 1.0, std_vals)

        X_scaled = (X_raw - mean_vals) / std_vals
        X_input = np.expand_dims(X_scaled, axis=0)  # shape: (1, seq_len, num_features)

        # Reconstruct
        reconstruction = self.model.predict(X_input, verbose=0)

        # Compute per-feature reconstruction errors
        mse_per_feature = np.mean(np.square(X_scaled - reconstruction[0]), axis=0)
        total_mse = float(np.mean(mse_per_feature))

        # Feature-level contribution
        total_error = mse_per_feature.sum()
        contributions: dict[str, float] = {}
        per_feature_errors: dict[str, float] = {}
        for j, col in enumerate(feature_cols):
            per_feature_errors[col] = float(mse_per_feature[j])
            if total_error > 0:
                contributions[col] = float(mse_per_feature[j] / total_error)

        # Determine anomaly
        is_anomalous = total_mse > threshold

        # Sort contributions by magnitude
        sorted_contribs = dict(
            sorted(contributions.items(), key=lambda x: x[1], reverse=True)
        )
        sorted_errors = dict(
            sorted(per_feature_errors.items(), key=lambda x: x[1], reverse=True)
        )

        return {
            "service": service,
            "anomaly_score": round(total_mse, 6),
            "anomaly_threshold": round(threshold, 6),
            "is_anomalous": is_anomalous,
            "contributing_metrics": sorted_contribs,
            "reconstruction_errors": sorted_errors,
            "window_size": seq_len,
            "inference_time": datetime.now(timezone.utc).isoformat(),
        }


# ---------------------------------------------------------------------------
# FastAPI server
# ---------------------------------------------------------------------------
# Global detector instance
_detector: AnomalyDetector | None = None


def get_detector() -> AnomalyDetector:
    global _detector
    if _detector is None or not _detector._loaded:
        # Try loading from environment / default paths
        model_path = os.environ.get(
            "ANOMALY_MODEL_PATH",
            "/models/anomaly-model",
        )
        meta_path = os.environ.get(
            "ANOMALY_METADATA_PATH",
            "/models/anomaly-metadata.json",
        )
        _detector = AnomalyDetector(model_path, meta_path)
    return _detector


def create_app(config: dict[str, Any] | None = None) -> Any:
    """Create and configure the FastAPI application."""
    from fastapi import FastAPI, HTTPException
    from pydantic import BaseModel, Field

    app = FastAPI(
        title="Anomaly Detection API",
        description="LSTM autoencoder-based anomaly detection for service metrics",
        version="1.0.0",
    )

    class PredictRequest(BaseModel):
        service: str = Field(..., description="Service name (e.g., patient-service)")
        metrics_window: list[dict[str, float]] = Field(
            ..., description=f"List of metric observations. Expected length={_detector.metadata.get('sequence_length', 60) if _detector else 60}"
        )

    class PredictResponse(BaseModel):
        service: str
        anomaly_score: float
        anomaly_threshold: float
        is_anomalous: bool
        contributing_metrics: dict[str, float]
        reconstruction_errors: dict[str, float]
        window_size: int
        inference_time: str

    class HealthResponse(BaseModel):
        status: str
        model_loaded: bool
        services_trained: list[str]
        timestamp: str

    @app.get("/health", response_model=HealthResponse)
    async def health():
        detector = get_detector()
        return HealthResponse(
            status="ok" if detector._loaded else "degraded",
            model_loaded=detector._loaded,
            services_trained=detector.metadata.get("services_trained", []),
            timestamp=datetime.now(timezone.utc).isoformat(),
        )

    @app.post("/predict", response_model=PredictResponse)
    async def predict(request: PredictRequest):
        detector = get_detector()
        if not detector._loaded:
            raise HTTPException(
                status_code=503,
                detail="Model not loaded. The service may still be initializing.",
            )

        try:
            result = detector.predict(request.service, request.metrics_window)
            return PredictResponse(**result)
        except ValueError as e:
            raise HTTPException(status_code=400, detail=str(e))
        except Exception as e:
            logger.exception("Prediction failed")
            raise HTTPException(status_code=500, detail=str(e))

    return app


# ---------------------------------------------------------------------------
# Vertex AI deployment
# ---------------------------------------------------------------------------
def deploy_to_vertex(model_resource_name: str, config: dict[str, Any]) -> str:
    """
    Deploy a model from Vertex AI Model Registry to a Vertex AI endpoint.

    Returns the endpoint resource name.
    """
    from google.cloud import aiplatform

    project = config.get("project", "his-hope-analytics")
    region = config.get("region", "us-east1")
    serving_cfg = config.get("serving", {})

    aiplatform.init(project=project, location=region)

    model = aiplatform.Model(model_resource_name)

    endpoint_name = f"anomaly-detection-endpoint"
    endpoints = aiplatform.Endpoint.list(
        filter=f"display_name={endpoint_name}",
        order_by="create_time",
    )
    if endpoints:
        endpoint = endpoints[0]
        logger.info("Reusing existing endpoint: %s", endpoint.resource_name)
    else:
        endpoint = aiplatform.Endpoint.create(
            display_name=endpoint_name,
            description="Endpoint for LSTM autoencoder anomaly detection",
        )
        logger.info("Created endpoint: %s", endpoint.resource_name)

    # Deploy model to endpoint
    model.deploy(
        endpoint=endpoint,
        machine_type=serving_cfg.get("machine_type", "n1-standard-4"),
        min_replica_count=serving_cfg.get("min_replicas", 1),
        max_replica_count=serving_cfg.get("max_replicas", 3),
        traffic_split={"0": 100},
    )

    logger.info("Deployed model %s to endpoint %s", model_resource_name, endpoint.resource_name)
    return endpoint.resource_name


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main() -> None:
    parser = argparse.ArgumentParser(
        description="Serve or deploy anomaly detection model"
    )
    parser.add_argument(
        "--config",
        default=os.path.join(os.path.dirname(__file__), "config.yaml"),
        help="Path to configuration YAML",
    )
    parser.add_argument(
        "--deploy",
        metavar="MODEL_RESOURCE_NAME",
        default=None,
        help="Deploy a Vertex AI model resource to an endpoint (e.g., projects/.../models/123)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8080,
        help="Port for the FastAPI server (default: 8080)",
    )
    parser.add_argument(
        "--model-path",
        default=None,
        help="Path to SavedModel directory (for local serving)",
    )
    parser.add_argument(
        "--metadata-path",
        default=None,
        help="Path to metadata JSON (for local serving)",
    )
    args = parser.parse_args()

    config = load_config(args.config)

    if args.deploy:
        endpoint = deploy_to_vertex(args.deploy, config)
        print(json.dumps({"endpoint": endpoint}, indent=2))
        return

    # Local serving mode
    import uvicorn

    global _detector
    model_path = args.model_path or os.environ.get(
        "ANOMALY_MODEL_PATH", "/models/anomaly-model"
    )
    meta_path = args.metadata_path or os.environ.get(
        "ANOMALY_METADATA_PATH", "/models/anomaly-metadata.json"
    )

    if Path(model_path).exists():
        _detector = AnomalyDetector(model_path, meta_path, config)
    else:
        _detector = AnomalyDetector(config=config)
        logger.warning("Model not found at %s. Model will load on first request.", model_path)

    app = create_app(config)
    logger.info("Starting anomaly detection server on port %d...", args.port)
    uvicorn.run(app, host="0.0.0.0", port=args.port, log_level="info")


if __name__ == "__main__":
    main()
```

---

### Task 3: Alertmanager Webhook (`ml/anomaly-detection/anomaly_webhook.py`)

**Files:**
- Create: `ml/anomaly-detection/anomaly_webhook.py`

**Interfaces:**
- Consumes: Alertmanager webhook payload with alert details
- Produces: Suppression decision + webhook response

- [ ] **Step 1: Create `ml/anomaly-detection/anomaly_webhook.py`**

```python
#!/usr/bin/env python3
"""
Alertmanager Webhook — Anomaly Detection Confirmation
=======================================================
Receives Alertmanager webhook alerts, queries the trained anomaly detection
model for confirmation, and suppresses false positives.

The webhook acts as a "sanity check" layer: before an alert fires, it asks
the ML model whether the current metrics window actually looks anomalous.

Flow:
    Alertmanager → this webhook → anomaly model → confirm/suppress

Usage:
    python anomaly_webhook.py                          # uses config.yaml
    python anomaly_webhook.py --port 9095              # custom port

Requires:  pip install flask requests pyyaml numpy
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import requests
import yaml

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S%z",
)
logger = logging.getLogger("anomaly_webhook")

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
def load_config(path: str | Path) -> dict[str, Any]:
    with open(path, "r") as f:
        cfg = yaml.safe_load(f)
    return cfg


# ---------------------------------------------------------------------------
# Prometheus metric fetcher (for webhook to build metrics_window)
# ---------------------------------------------------------------------------
SERVICE_LIST = [
    "api-gateway",
    "patient-service",
    "appointment-service",
    "clinical-service",
    "identity-service",
    "lab-service",
    "billing-service",
    "pharmacy-service",
]


def fetch_metrics_window(
    prom_url: str,
    service: str,
    window_minutes: int = 60,
    step: str = "1m",
) -> list[dict[str, float]] | None:
    """
    Fetch the last ``window_minutes`` of metrics for a service from Prometheus.

    Returns a list of dicts suitable for the anomaly model's predict endpoint,
    or None if the fetch fails.
    """
    now = datetime.now(timezone.utc)
    start = now - timedelta(minutes=window_minutes)

    metric_queries = {
        "request_rate": f'sum(rate(http_requests_total{{service="{service}"}}[1m]))',
        "error_rate": f'sum(rate(http_requests_total{{service="{service}",status=~"5.."}}[1m])) / sum(rate(http_requests_total{{service="{service}"}}[1m]))',
        "latency_p99": f'histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket{{service="{service}"}}[1m])) by (le))',
    }

    # Fetch each metric
    metric_data: dict[str, dict[str, float]] = {}
    for metric_name, promql in metric_queries.items():
        params = {
            "query": promql,
            "start": start.timestamp(),
            "end": now.timestamp(),
            "step": step,
        }
        try:
            resp = requests.get(
                f"{prom_url.rstrip('/')}/api/v1/query_range",
                params=params,
                timeout=30,
            )
            resp.raise_for_status()
            data = resp.json()
            if data.get("status") != "success":
                continue

            results = data.get("data", {}).get("result", [])
            if not results:
                continue

            values = results[0].get("values", [])
            for ts_str, val_str in values:
                ts = datetime.fromtimestamp(float(ts_str), tz=timezone.utc)
                ts_key = ts.isoformat()
                if ts_key not in metric_data:
                    metric_data[ts_key] = {"ds": ts_key}
                metric_data[ts_key][metric_name] = (
                    float(val_str) if val_str not in ("NaN", "+Inf", "-Inf", "") else 0.0
                )
        except requests.RequestException as exc:
            logger.warning("Failed to fetch metric %s: %s", metric_name, exc)

    if not metric_data:
        return None

    # Sort by timestamp and build window
    sorted_ts = sorted(metric_data.keys())
    window = []
    for ts in sorted_ts:
        entry = metric_data[ts]
        if all(m in entry for m in metric_queries):
            window.append({
                "request_rate": entry.get("request_rate", 0.0),
                "error_rate": entry.get("error_rate", 0.0),
                "latency_p99": entry.get("latency_p99", 0.0),
            })

    if not window:
        return None

    # Trim to exact window size if needed
    seq_len = 60  # default, will match training config
    return window[-seq_len:] if len(window) > seq_len else window


# ---------------------------------------------------------------------------
# Model query client
# ---------------------------------------------------------------------------
class AnomalyModelClient:
    """Client for querying the deployed anomaly detection model."""

    def __init__(self, endpoint_url: str, timeout: int = 10):
        self.endpoint_url = endpoint_url.rstrip("/")
        self.timeout = timeout

    def predict(
        self,
        service: str,
        metrics_window: list[dict[str, float]],
    ) -> dict[str, Any] | None:
        """
        Query the anomaly model for prediction.

        Returns the prediction response dict, or None on failure.
        """
        try:
            resp = requests.post(
                f"{self.endpoint_url}/predict",
                json={
                    "service": service,
                    "metrics_window": metrics_window,
                },
                timeout=self.timeout,
                headers={"Content-Type": "application/json"},
            )
            resp.raise_for_status()
            return resp.json()
        except requests.RequestException as exc:
            logger.error("Failed to query anomaly model: %s", exc)
            return None


# ---------------------------------------------------------------------------
# Alertmanager webhook handler
# ---------------------------------------------------------------------------
def create_webhook_app(config: dict[str, Any]) -> Any:
    """Create Flask application for handling Alertmanager webhooks."""
    from flask import Flask, jsonify, request

    app = Flask(__name__)

    # Configure model client
    model_endpoint = os.environ.get(
        "ANOMALY_ENDPOINT_URL",
        config.get("anomaly_endpoint_url", "http://anomaly-detection:8080"),
    )
    model_client = AnomalyModelClient(model_endpoint)

    prom_url = os.environ.get(
        "PROMETHEUS_URL",
        config.get("prometheus_url", "http://prometheus-k8s.monitoring.svc.cluster.local:9090"),
    )

    # Suppression configuration
    suppression_cfg = config.get("suppression", {})
    min_anomaly_score = suppression_cfg.get("min_anomaly_score", 0.5)
    max_suppression_count = suppression_cfg.get("max_suppression_count", 50)

    # Tracking suppression count per alert to avoid infinite suppression
    _suppression_counters: dict[str, int] = {}

    def _extract_service(labels: dict[str, str]) -> str | None:
        """Extract service name from alert labels."""
        for key in ("service", "service_name", "job"):
            if key in labels:
                return labels[key]
        # Try to infer from alertname
        alertname = labels.get("alertname", "")
        for svc in SERVICE_LIST:
            if svc.replace("-", "_") in alertname or svc in alertname:
                return svc
        return None

    @app.route("/webhook", methods=["POST"])
    def webhook():
        """
        Receive Alertmanager webhook payload.

        Expected format (Alertmanager v6+):
        {
            "version": "4",
            "groupKey": "...",
            "status": "firing",
            "alerts": [
                {
                    "status": "firing",
                    "labels": {...},
                    "annotations": {...},
                    "startsAt": "...",
                    "endsAt": "...",
                    ...
                }
            ],
            ...
        }
        """
        payload = request.get_json(silent=True)
        if not payload:
            return jsonify({"error": "invalid payload"}), 400

        alerts = payload.get("alerts", [])
        logger.info("Received %d alert(s) from Alertmanager", len(alerts))

        decisions: list[dict[str, Any]] = []

        for alert in alerts:
            labels = alert.get("labels", {})
            status = alert.get("status", "firing")
            alertname = labels.get("alertname", "unknown")
            service = _extract_service(labels)

            decision = {
                "alertname": alertname,
                "service": service,
                "status": status,
                "action": "pass",  # default: let alert through
            }

            if status != "firing" or service is None:
                # Only evaluate firing alerts with known services
                decisions.append(decision)
                continue

            # Check suppression counter
            alert_key = f"{service}/{alertname}"
            suppress_count = _suppression_counters.get(alert_key, 0)
            if suppress_count >= max_suppression_count:
                logger.warning(
                    "Alert %s suppressed %d times. Allowing through.",
                    alert_key, suppress_count,
                )
                decisions.append(decision)
                continue

            # Fetch current metrics window
            metrics_window = fetch_metrics_window(prom_url, service)
            if metrics_window is None:
                logger.warning("Could not fetch metrics for %s. Passing alert.", service)
                decisions.append(decision)
                continue

            # Query anomaly model
            prediction = model_client.predict(service, metrics_window)

            if prediction is None:
                logger.warning("Model unavailable for %s. Passing alert.", service)
                decisions.append(decision)
                continue

            anomaly_score = prediction.get("anomaly_score", 0.0)
            is_anomalous = prediction.get("is_anomalous", False)

            if is_anomalous and anomaly_score >= min_anomaly_score:
                # Model confirms anomaly — let alert through
                decision["action"] = "pass"
                decision["anomaly_score"] = anomaly_score
                decision["reason"] = "Model confirms anomalous pattern"
                logger.info(
                    "ALERT CONFIRMED: %s/%s (score=%.4f, threshold=%.4f)",
                    service, alertname, anomaly_score,
                    prediction.get("anomaly_threshold", 0),
                )
                # Reset suppression counter
                _suppression_counters[alert_key] = 0
            else:
                # Model says not anomalous — likely false positive
                decision["action"] = "suppress"
                decision["anomaly_score"] = anomaly_score
                decision["reason"] = "Model did not confirm anomaly (likely false positive)"
                _suppression_counters[alert_key] = suppress_count + 1
                logger.info(
                    "ALERT SUPPRESSED: %s/%s (score=%.4f < threshold=%.4f, suppression #%d)",
                    service, alertname, anomaly_score,
                    prediction.get("anomaly_threshold", 0),
                    _suppression_counters[alert_key],
                )

            decisions.append(decision)

        # Log summary
        suppressed = sum(1 for d in decisions if d["action"] == "suppress")
        passed = sum(1 for d in decisions if d["action"] == "pass")
        logger.info("Webhook processed: %d passed, %d suppressed", passed, suppressed)

        return jsonify({
            "received": len(alerts),
            "passed": passed,
            "suppressed": suppressed,
            "decisions": decisions,
        })

    @app.route("/health", methods=["GET"])
    def health():
        return jsonify({
            "status": "ok",
            "suppression_counters": dict(_suppression_counters),
            "timestamp": datetime.now(timezone.utc).isoformat(),
        })

    return app


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main() -> None:
    parser = argparse.ArgumentParser(
        description="Alertmanager webhook for anomaly detection confirmation"
    )
    parser.add_argument(
        "--config",
        default=os.path.join(os.path.dirname(__file__), "config.yaml"),
        help="Path to configuration YAML",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=9095,
        help="Port to listen on (default: 9095)",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug mode",
    )
    args = parser.parse_args()

    config = load_config(args.config)
    app = create_webhook_app(config)

    logger.info("Starting anomaly webhook on port %d...", args.port)
    app.run(host="0.0.0.0", port=args.port, debug=args.debug)


if __name__ == "__main__":
    main()
```

---

### Task 4: Tekton CI/CD Pipeline (`cicd/tekton/pipelines/ml-train-deploy.yaml`)

**Files:**
- Create: `cicd/tekton/pipelines/ml-train-deploy.yaml`

**Interfaces:**
- Consumes: Git source, model config
- Produces: Trained model in Vertex AI Model Registry, endpoint deployed

- [ ] **Step 1: Create `cicd/tekton/pipelines/ml-train-deploy.yaml`**

```yaml
apiVersion: tekton.dev/v1beta1
kind: Pipeline
metadata:
  name: ml-train-deploy
  labels:
    app.kubernetes.io/version: "0.1"
  annotations:
    tekton.dev/pipelines.minVersion: "0.17.0"
    tekton.dev/tags: ml, anomaly-detection, vertex-ai
    tekton.dev/displayName: ML Train & Deploy Pipeline
spec:
  description: >-
    Pipeline to train an LSTM autoencoder anomaly detection model using
    Prometheus metrics, evaluate it, upload to Vertex AI Model Registry,
    and deploy to a Vertex AI endpoint.

  params:
    - name: ML_COMPONENT
      type: string
      description: ML component directory (e.g., anomaly-detection)
      default: anomaly-detection
    - name: CONFIG_PATH
      type: string
      description: Path to config YAML relative to ml component dir
      default: config.yaml
    - name: GIT_REVISION
      type: string
      description: Git commit SHA or tag
    - name: MODEL_DISPLAY_NAME
      type: string
      description: Display name for the model in Vertex AI
      default: anomaly-detector
    - name: PROJECT_ID
      type: string
      description: GCP project ID
      default: his-hope-analytics
    - name: REGION
      type: string
      description: GCP region
      default: us-east1
    - name: PROMETHEUS_URL
      type: string
      description: Prometheus server URL
      default: http://prometheus-k8s.monitoring.svc.cluster.local:9090

  workspaces:
    - name: source
      description: Repository source code
    - name: output
      description: Build outputs, model artifacts, and test results
    - name: gcp-secret
      description: GCP service account key for Vertex AI access
      optional: false

  results:
    - name: MODEL_RESOURCE_NAME
      description: Vertex AI model resource name
    - name: ENDPOINT_RESOURCE_NAME
      description: Vertex AI endpoint resource name
    - name: EVALUATION_METRICS
      description: JSON string of evaluation metrics

  tasks:
    - name: fetch-prometheus-data
      taskRef:
        name: python-script
        kind: ClusterTask
      params:
        - name: SCRIPT
          value: |
            python ml/$(params.ML_COMPONENT)/train_pipeline.py \
              --config ml/$(params.ML_COMPONENT)/$(params.CONFIG_PATH) \
              --local
        - name: PROMETHEUS_URL
          value: $(params.PROMETHEUS_URL)
      workspaces:
        - name: source
          workspace: source
        - name: output
          workspace: output

    - name: train-model
      taskRef:
        name: python-script
        kind: ClusterTask
      runAfter:
        - fetch-prometheus-data
      params:
        - name: SCRIPT
          value: |
            pip install tensorflow google-cloud-aiplatform pyyaml requests numpy pandas && \
            python ml/$(params.ML_COMPONENT)/train_pipeline.py \
              --config ml/$(params.ML_COMPONENT)/$(params.CONFIG_PATH) \
              --local
        - name: PROMETHEUS_URL
          value: $(params.PROMETHEUS_URL)
      workspaces:
        - name: source
          workspace: source
        - name: output
          workspace: output
        - name: gcp-secret
          workspace: gcp-secret

    - name: evaluate-model
      taskRef:
        name: python-script
        kind: ClusterTask
      runAfter:
        - train-model
      params:
        - name: SCRIPT
          value: |
            python -c "
            import json, os
            metrics_path = '/workspace/output/evaluation.json'
            # Read training metrics from output
            result = {
                'status': 'evaluated',
                'pipeline_run': os.environ.get('PIPELINE_RUN_ID', 'unknown'),
                'timestamp': '$(context.pipelineRun.uid)'
            }
            # Check model artifact exists
            model_dir = '/workspace/output/models/anomaly-model'
            if os.path.exists(model_dir):
                result['model_saved'] = True
                result['model_path'] = model_dir
            else:
                result['model_saved'] = False
            with open(metrics_path, 'w') as f:
                json.dump(result, f, indent=2)
            print(json.dumps(result, indent=2))
            "
      workspaces:
        - name: source
          workspace: source
        - name: output
          workspace: output

    - name: upload-model
      taskRef:
        name: python-script
        kind: ClusterTask
      runAfter:
        - evaluate-model
      params:
        - name: SCRIPT
          value: |
            python -c "
            from google.cloud import aiplatform
            import os, json
            
            project = '$(params.PROJECT_ID)'
            region = '$(params.REGION)'
            display_name = '$(params.MODEL_DISPLAY_NAME)-$(params.GIT_REVISION)'
            
            aiplatform.init(project=project, location=region)
            
            model_dir = '/workspace/output/models/anomaly-model'
            if not os.path.exists(model_dir):
                print(f'Model not found at {model_dir}')
                exit(1)
            
            model = aiplatform.Model.upload(
                display_name=display_name,
                artifact_uri=model_dir,
                serving_container_image_uri='us-docker.pkg.dev/vertex-ai/prediction/tf2-cpu.2-15:latest',
                description=f'LSTM autoencoder - trained at {os.path.join(model_dir, \"anomaly-metadata.json\")}',
                labels={
                    'model-type': 'lstm-autoencoder',
                    'component': 'anomaly-detection',
                },
            )
            
            result = {'model_resource_name': model.resource_name}
            with open('/workspace/output/model-resource.json', 'w') as f:
                json.dump(result, f)
            print(json.dumps(result))
            "
      workspaces:
        - name: source
          workspace: source
        - name: output
          workspace: output
        - name: gcp-secret
          workspace: gcp-secret

    - name: deploy-endpoint
      taskRef:
        name: python-script
        kind: ClusterTask
      runAfter:
        - upload-model
      params:
        - name: SCRIPT
          value: |
            python -c "
            from google.cloud import aiplatform
            import json, os
            
            project = '$(params.PROJECT_ID)'
            region = '$(params.REGION)'
            
            aiplatform.init(project=project, location=region)
            
            # Read model resource name
            with open('/workspace/output/model-resource.json') as f:
                data = json.load(f)
            model_resource_name = data['model_resource_name']
            
            model = aiplatform.Model(model_resource_name)
            
            # Find or create endpoint
            endpoint_name = 'anomaly-detection-endpoint'
            endpoints = aiplatform.Endpoint.list(
                filter=f'display_name={endpoint_name}',
                order_by='create_time',
            )
            if endpoints:
                endpoint = endpoints[0]
                print(f'Reusing endpoint: {endpoint.resource_name}')
            else:
                endpoint = aiplatform.Endpoint.create(
                    display_name=endpoint_name,
                    description='LSTM autoencoder anomaly detection',
                )
                print(f'Created endpoint: {endpoint.resource_name}')
            
            # Deploy
            model.deploy(
                endpoint=endpoint,
                machine_type='n1-standard-4',
                min_replica_count=1,
                max_replica_count=3,
                traffic_split={'0': 100},
            )
            
            result = {
                'model_resource_name': model_resource_name,
                'endpoint_resource_name': endpoint.resource_name,
            }
            with open('/workspace/output/deployment-result.json', 'w') as f:
                json.dump(result, f)
            print(json.dumps(result))
            "
      workspaces:
        - name: source
          workspace: source
        - name: output
          workspace: output
        - name: gcp-secret
          workspace: gcp-secret

  finally:
    - name: collect-results
      taskRef:
        name: collect-results
        kind: ClusterTask
      workspaces:
        - name: output
          workspace: output
      params:
        - name: RESULT_FILES
          value: |
            /workspace/output/model-resource.json
            /workspace/output/evaluation.json
            /workspace/output/deployment-result.json

    - name: notify-ml-status
      taskRef:
        name: send-slack-message
        kind: ClusterTask
      params:
        - name: MESSAGE
          value: |
            ML Train/Deploy Pipeline completed for $(params.ML_COMPONENT)@$(params.GIT_REVISION)
            Status: $(tasks.status)
            Model: $(results.MODEL_RESOURCE_NAME)
            Endpoint: $(results.ENDPOINT_RESOURCE_NAME)
            PipelineRun: $(context.pipelineRun.name)
```

---

### Task 5: Commit

**Files:**
- All files created in Tasks 1-4

- [ ] **Step 1: Commit all changes**

```bash
git add \
  ml/anomaly-detection/__init__.py \
  ml/anomaly-detection/config.yaml \
  ml/anomaly-detection/train_pipeline.py \
  ml/anomaly-detection/serve_model.py \
  ml/anomaly-detection/anomaly_webhook.py \
  cicd/tekton/pipelines/ml-train-deploy.yaml

git commit -m "feat(ml): implement ML-based anomaly detection with Vertex AI pipeline"
```
