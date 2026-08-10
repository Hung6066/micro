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

Requires:  pip install tensorflow google-cloud-aiplatform pyyaml requests numpy pandas
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Optional, Tuple

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
    logger.info("  Query window: %s  ->  %s", start.isoformat(), end.isoformat())

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
            logger.info("  OK %s: %d data points", metric_name, len(combined))
        else:
            logger.warning("  FAIL %s: no data returned", metric_name)
            all_data[metric_name] = pd.DataFrame(columns=["ds", "service", "y"])

    return all_data


# ---------------------------------------------------------------------------
# Incident period filtering
# ---------------------------------------------------------------------------
def filter_incidents(
    data: dict[str, pd.DataFrame],
    config: dict[str, Any],
) -> dict[str, pd.DataFrame]:
    """Remove data points that fall within known incident periods."""
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

    merged = merged.sort_values(["service", "ds"]).reset_index(drop=True)
    merged = merged.groupby("service", group_keys=False).apply(
        lambda g: g.fillna(method="ffill").fillna(0)
    )

    # Normalize per service per feature
    feature_cols = metric_names
    scaler_params: dict[str, dict[str, Any]] = {}

    for svc in merged["service"].unique():
        svc_mask = merged["service"] == svc
        svc_data = merged.loc[svc_mask, feature_cols]
        std = svc_data.std().replace(0, 1.0)
        scaler_params[svc] = {
            "mean": svc_data.mean().to_dict(),
            "std": std.to_dict(),
        }
        merged.loc[svc_mask, feature_cols] = (
            svc_data - svc_data.mean()
        ) / std

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
    Build an LSTM autoencoder model with deterministic seed for reproducibility.

    Architecture:
        Encoder: LSTM(32) -> LSTM(16) -> LSTM(latent_dim)
        Decoder: RepeatVector -> LSTM(16) -> LSTM(32) -> TimeDistributed(Dense)
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
    """Train the LSTM autoencoder with early stopping and LR reduction."""
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
    """Compute reconstruction error threshold at the given percentile."""
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
    """Upload the trained model to Vertex AI Model Registry."""
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
        description=(
            f"LSTM autoencoder for service metric anomaly detection. "
            f"Trained on {metadata['num_sequences']} sequences across "
            f"{len(metadata['services_trained'])} services."
        ),
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
    """Execute the full training pipeline end-to-end."""
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
        from google.cloud import aiplatform
        aiplatform.init(
            project=config.get("project", "his-hope-analytics"),
            location=config.get("region", "us-east1"),
        )

        job = aiplatform.PipelineJob(
            display_name=(
                f"anomaly-detection-"
                f"{datetime.now().strftime('%Y%m%d-%H%M%S')}"
            ),
            template_path="",
            pipeline_root=(
                f"gs://{config.get('project', 'his-hope-analytics')}"
                f"-vertex/pipelines/anomaly"
            ),
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
