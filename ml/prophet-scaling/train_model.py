#!/usr/bin/env python3
"""
Prophet Model Training — Predictive Auto-Scaling
=================================================
Queries Prometheus for 14 days of http_requests_total data at 5-minute
intervals, trains one Prophet model per backend service, and serialises
the model + metadata to disk.

Usage:
    python train_model.py                          # uses config.yaml
    python train_model.py --config /path/to.yaml   # custom config path

Requires:  pip install prophet requests pyyaml pandas
"""

from __future__ import annotations

import argparse
import logging
import math
import os
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

import pandas as pd
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
logger = logging.getLogger("train_model")


# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
def load_config(path: str | Path) -> dict[str, Any]:
    """Load YAML configuration file."""
    with open(path, "r") as f:
        cfg = yaml.safe_load(f)
    # Override prometheus_url from env if set
    cfg["prometheus_url"] = os.environ.get(
        "PROMETHEUS_URL", cfg.get("prometheus_url", "http://prometheus-k8s.monitoring.svc.cluster.local:9090")
    )
    return cfg


# ---------------------------------------------------------------------------
# Prometheus query
# ---------------------------------------------------------------------------
def query_prometheus_range(
    prom_url: str,
    query: str,
    start: datetime,
    end: datetime,
    step: str = "5m",
) -> pd.DataFrame:
    """
    Execute a Prometheus range query and return results as a DataFrame
    with columns ``ds`` (datetime) and ``y`` (float value).

    Returns an empty DataFrame if the query fails or returns no data.
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
            timeout=30,
        )
        resp.raise_for_status()
    except requests.RequestException as exc:
        logger.error("Prometheus query failed: %s", exc)
        return pd.DataFrame(columns=["ds", "y"])

    data = resp.json()
    if data.get("status") != "success":
        logger.error("Prometheus returned non-success status: %s", data.get("error", "unknown"))
        return pd.DataFrame(columns=["ds", "y"])

    results = data.get("data", {}).get("result", [])
    if not results:
        logger.warning("No data returned for query: %s", query)
        return pd.DataFrame(columns=["ds", "y"])

    records: list[dict] = []
    for series in results:
        metric = series.get("metric", {})
        values = series.get("values", [])
        for ts_str, val_str in values:
            records.append({
                "ds": datetime.fromtimestamp(float(ts_str), tz=timezone.utc),
                "y": float(val_str) if val_str != "NaN" else 0.0,
                # Preserve labels for potential multi-series aggregation
                **{k: v for k, v in metric.items() if k != "__name__"},
            })

    df = pd.DataFrame(records)
    if df.empty:
        return df

    df = df.sort_values("ds").reset_index(drop=True)

    # Aggregate if multiple series (e.g., multiple pods) — sum per timestamp
    grouping_cols = [c for c in df.columns if c not in ("ds", "y")]
    if grouping_cols:
        df = df.groupby("ds", as_index=False)[["y"]].sum()
    else:
        # Only one series — still ensure ds is unique
        df = df.groupby("ds", as_index=False)["y"].sum()

    # Forward-fill small gaps (up to 2 missing intervals = 10 min)
    df = df.set_index("ds").asfreq("5min", method="ffill", limit=2).reset_index()
    df = df.dropna(subset=["y"])

    return df


# ---------------------------------------------------------------------------
# Data preparation
# ---------------------------------------------------------------------------
def prepare_training_data(
    df: pd.DataFrame, min_valid_points: int = 50
) -> pd.DataFrame | None:
    """
    Validate and prepare time-series data for Prophet.

    Returns ``None`` if the series has insufficient data points.
    """
    if df.empty or len(df) < min_valid_points:
        logger.warning(
            "Insufficient data points: got %d, need at least %d",
            len(df),
            min_valid_points,
        )
        return None

    # Ensure we have enough recent data (at least 3 days)
    date_range = df["ds"].max() - df["ds"].min()
    if date_range < timedelta(days=3):
        logger.warning(
            "Data span too short: %.1f days (need >= 3 days)", date_range.total_seconds() / 86400
        )
        return None

    # Clip extreme outliers (99.9th percentile)
    upper_bound = df["y"].quantile(0.999)
    df["y"] = df["y"].clip(upper=upper_bound)

    # Ensure no negative values
    df["y"] = df["y"].clip(lower=0.0)

    return df[["ds", "y"]].copy()


# ---------------------------------------------------------------------------
# Prophet training wrapper
# ---------------------------------------------------------------------------
def train_prophet(
    df: pd.DataFrame,
    service_name: str,
    config: dict[str, Any],
) -> Any:
    """
    Train a Prophet model on the prepared DataFrame.

    Returns the trained Prophet model instance.
    """
    train_cfg = config.get("training", {})

    try:
        from prophet import Prophet
    except ImportError:
        logger.error(
            "The 'prophet' package is not installed. "
            "Run: pip install prophet"
        )
        sys.exit(1)

    model = Prophet(
        seasonality_mode=train_cfg.get("seasonality_mode", "additive"),
        changepoint_prior_scale=train_cfg.get("changepoint_prior_scale", 0.05),
        seasonality_prior_scale=train_cfg.get("seasonality_prior_scale", 10.0),
        daily_seasonality=train_cfg.get("daily_seasonality", True),
        weekly_seasonality=train_cfg.get("weekly_seasonality", True),
        yearly_seasonality=train_cfg.get("yearly_seasonality", False),
        uncertainty_samples=train_cfg.get("uncertainty_samples", 1000),
        interval_width=train_cfg.get("interval_width", 0.80),
    )

    logger.info("Training Prophet model for %s (%d data points)...", service_name, len(df))
    model.fit(df)
    logger.info("Model for %s trained successfully.", service_name)

    return model


# ---------------------------------------------------------------------------
# Save model
# ---------------------------------------------------------------------------
def save_model(
    model: Any,
    service_name: str,
    model_dir: str | Path,
    metadata: dict[str, Any] | None = None,
) -> Path:
    """
    Serialise a Prophet model to ``{model_dir}/{service}_prophet_model.pkl``
    along with a sidecar metadata dict.
    """
    import joblib  # import only when needed; not a core dep

    model_dir = Path(model_dir)
    model_dir.mkdir(parents=True, exist_ok=True)

    model_path = model_dir / f"{service_name}_prophet_model.pkl"
    meta_path = model_dir / f"{service_name}_metadata.pkl"

    payload = {
        "model": model,
        "metadata": metadata or {},
    }
    joblib.dump(payload, model_path)
    logger.info("Saved model to %s", model_path)

    # Write a lightweight metadata sidecar for quick inspection without
    # loading the full model.
    import json  # noqa: PLC0415  (import inside function)
    meta_export = {
        "service": service_name,
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "data_points": (metadata or {}).get("data_points"),
        "data_start": (metadata or {}).get("data_start"),
        "data_end": (metadata or {}).get("data_end"),
        "mean_rps": (metadata or {}).get("mean_rps"),
        "peak_rps": (metadata or {}).get("peak_rps"),
    }
    with open(meta_path, "w") as f:
        json.dump(meta_export, f, indent=2, default=str)
    logger.info("Saved metadata to %s", meta_path)

    return model_path


# ---------------------------------------------------------------------------
# Main training loop
# ---------------------------------------------------------------------------
def train_for_service(
    service_name: str,
    config: dict[str, Any],
) -> bool:
    """
    Train and save a Prophet model for a single service.

    Returns ``True`` on success, ``False`` on failure.
    """
    prom_url = config["prometheus_url"]
    train_cfg = config.get("training", {})
    history_days = train_cfg.get("history_days", 14)
    step = f"{train_cfg.get('data_interval_minutes', 5)}m"
    model_dir = config.get("model_dir", "/models")

    # PromQL: request rate per second, aggregated across all pods
    promql = (
        f'sum(rate(http_requests_total{{service="{service_name}"}}[{step}])) '
        f'by (service)'
    )

    end = datetime.now(timezone.utc)
    start = end - timedelta(days=history_days)

    logger.info("=== Training %s ===", service_name)
    logger.info("  Query range: %s  →  %s", start.isoformat(), end.isoformat())
    logger.info("  PromQL: %s", promql)

    df = query_prometheus_range(prom_url, promql, start, end, step)
    df = prepare_training_data(df)

    if df is None:
        logger.error("  ✗ Skipping %s: insufficient training data", service_name)
        return False

    metadata = {
        "data_points": len(df),
        "data_start": df["ds"].min().isoformat(),
        "data_end": df["ds"].max().isoformat(),
        "mean_rps": float(df["y"].mean()),
        "peak_rps": float(df["y"].max()),
    }
    logger.info(
        "  Data: %d points, mean=%.2f rps, peak=%.2f rps",
        metadata["data_points"],
        metadata["mean_rps"],
        metadata["peak_rps"],
    )

    try:
        model = train_prophet(df, service_name, config)
        save_model(model, service_name, model_dir, metadata)
        logger.info("  ✓ %s model saved successfully", service_name)
        return True
    except Exception:
        logger.exception("  ✗ Failed to train model for %s", service_name)
        return False


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Train Prophet models for predictive auto-scaling"
    )
    parser.add_argument(
        "--config",
        default=os.path.join(os.path.dirname(__file__), "config.yaml"),
        help="Path to configuration YAML (default: ./config.yaml)",
    )
    parser.add_argument(
        "--service",
        default=None,
        help="Train only a single service (default: all services)",
    )
    args = parser.parse_args()

    config = load_config(args.config)
    services = config.get("services", [])

    if args.service:
        services = [s for s in services if s["name"] == args.service]
        if not services:
            logger.error("Service '%s' not found in config", args.service)
            sys.exit(1)

    logger.info("Starting Prophet training for %d service(s) ...", len(services))

    successes = 0
    failures = 0
    for svc in services:
        ok = train_for_service(svc["name"], config)
        if ok:
            successes += 1
        else:
            failures += 1

    logger.info(
        "Training complete: %d succeeded, %d failed", successes, failures
    )
    sys.exit(0 if failures == 0 else 1)


if __name__ == "__main__":
    main()
