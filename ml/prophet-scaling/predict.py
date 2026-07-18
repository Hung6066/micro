#!/usr/bin/env python3
"""
Prophet Prediction — Predictive Auto-Scaler
=============================================
Loads trained Prophet models, forecasts request rate for the next 60
minutes, calculates recommended HPA minReplicas per service, and patches
the corresponding HorizontalPodAutoscaler resources via the Kubernetes API.

Intended to be run as a Kubernetes CronJob (hourly).

Usage:
    python predict.py                           # uses config.yaml in same dir

Requires:  pip install prophet joblib pyyaml requests kubernetes
"""

from __future__ import annotations

import argparse
import json
import logging
import math
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S%z",
)
logger = logging.getLogger("predict")


# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
def load_config(path: str | Path) -> dict[str, Any]:
    with open(path, "r") as f:
        cfg = yaml.safe_load(f)
    cfg["prometheus_url"] = os.environ.get(
        "PROMETHEUS_URL",
        cfg.get("prometheus_url", "http://prometheus-k8s.monitoring.svc.cluster.local:9090"),
    )
    return cfg


# ---------------------------------------------------------------------------
# Model loading
# ---------------------------------------------------------------------------
def load_model(service_name: str, model_dir: str | Path) -> Any | None:
    """
    Load a Prophet model from ``{model_dir}/{service}_prophet_model.pkl``.

    Returns ``None`` and logs a warning if the file doesn't exist or is
    corrupt.
    """
    import joblib

    path = Path(model_dir) / f"{service_name}_prophet_model.pkl"
    if not path.exists():
        logger.warning("Model file not found for %s: %s", service_name, path)
        return None

    try:
        payload = joblib.load(path)
        model = payload["model"] if isinstance(payload, dict) else payload
        logger.info("Loaded model for %s from %s", service_name, path)
        return model
    except Exception:
        logger.exception("Failed to load model for %s", service_name)
        return None


# ---------------------------------------------------------------------------
# Forecasting
# ---------------------------------------------------------------------------
def predict_peak(
    model: Any,
    service_name: str,
    forecast_steps: int = 12,
    interval_width: float = 0.80,
) -> dict[str, Any]:
    """
    Generate a forecast for the next ``forecast_steps`` intervals (each
    5 minutes) and return the predicted peak and confidence intervals.

    Returns a dict with keys:
      - predicted_peak:     max yhat_upper value (float)
      - predicted_mean:     mean of yhat values (float)
      - forecast_timestamps: ISO timestamps for each step (list[str])
      - yhat_values:        point forecast values (list[float])
      - yhat_upper_values:  upper-bound forecast values (list[float])
      - forecast_generated_at: ISO timestamp
    """
    future = model.make_future_dataframe(
        periods=forecast_steps,
        freq="5min",
        include_history=False,
    )
    forecast = model.predict(future)

    yhat = forecast["yhat"].values
    # Determine the upper column name (depends on interval_width)
    upper_col = "yhat_upper"
    if upper_col not in forecast.columns:
        # Prophet uses a dynamic column name like yhat_upper_0.8
        upper_col = [c for c in forecast.columns if c.startswith("yhat_upper")][0]
    yhat_upper = forecast[upper_col].values

    predicted_peak = float(max(yhat_upper))
    predicted_mean = float(yhat.mean())

    return {
        "predicted_peak_rps": round(predicted_peak, 2),
        "predicted_mean_rps": round(predicted_mean, 2),
        "forecast_timestamps": [t.isoformat() for t in forecast["ds"]],
        "yhat_values": [round(float(v), 2) for v in yhat],
        "yhat_upper_values": [round(float(v), 2) for v in yhat_upper],
        "forecast_generated_at": datetime.now(timezone.utc).isoformat(),
    }


# ---------------------------------------------------------------------------
# Replica calculation
# ---------------------------------------------------------------------------
def calculate_min_replicas(
    predicted_peak_rps: float,
    target_rps_per_pod: float,
    lower_bound: int,
    upper_bound: int,
) -> int:
    """
    Determine the recommended ``minReplicas`` for the next hour.

    Formula:
        recommended = ceil(predicted_peak_rps / target_rps_per_pod)
        minReplicas = clamp(recommended, lower_bound, upper_bound)

    Invalid / NaN predictions fall back to ``lower_bound``.
    """
    if math.isnan(predicted_peak_rps) or predicted_peak_rps < 0:
        logger.warning("Invalid predicted peak (%.2f), falling back to lower bound %d",
                       predicted_peak_rps, lower_bound)
        return lower_bound

    if target_rps_per_pod <= 0:
        logger.error("target_rps_per_pod must be > 0, got %f", target_rps_per_pod)
        return lower_bound

    recommended = math.ceil(predicted_peak_rps / target_rps_per_pod)
    clamped = max(lower_bound, min(recommended, upper_bound))

    logger.info(
        "  peak=%.2f rps → target=%d rps/pod → recommended=%d → clamped=[%d..%d] → %d",
        predicted_peak_rps, int(target_rps_per_pod), recommended,
        lower_bound, upper_bound, clamped,
    )
    return clamped


# ---------------------------------------------------------------------------
# Kubernetes HPA patching
# ---------------------------------------------------------------------------
def patch_hpa_min_replicas(
    service_name: str,
    min_replicas: int,
    namespace: str = "his-hope",
    dry_run: bool = False,
) -> bool:
    """
    Patch the HPA ``minReplicas`` for a given service via the Kubernetes API.

    Returns ``True`` on success.
    """
    if dry_run:
        logger.info("  [DRY-RUN] Would patch HPA %s → minReplicas=%d", service_name, min_replicas)
        return True

    try:
        from kubernetes import client, config  # noqa: PLC0415
    except ImportError:
        logger.error(
            "The 'kubernetes' package is not installed. "
            "Run: pip install kubernetes"
        )
        return False

    try:
        # Load in-cluster config (when running inside a pod)
        config.load_incluster_config()
    except config.ConfigException:
        try:
            # Fall back to kubeconfig for local development
            config.load_kube_config()
        except config.ConfigException as exc:
            logger.error("Failed to load Kubernetes config: %s", exc)
            return False

    api = client.AutoscalingV2Api()

    patch_body = {
        "spec": {
            "minReplicas": min_replicas,
        },
    }

    try:
        api.patch_namespaced_horizontal_pod_autoscaler(
            name=service_name,
            namespace=namespace,
            body=patch_body,
        )
        logger.info("  ✓ Patched HPA %s → minReplicas=%d", service_name, min_replicas)
        return True
    except client.exceptions.ApiException as exc:
        if exc.status == 404:
            logger.warning("  HPA not found for %s in namespace %s", service_name, namespace)
        else:
            logger.error("  Failed to patch HPA %s: %s", service_name, exc)
        return False
    except Exception:
        logger.exception("  Unexpected error patching HPA %s", service_name)
        return False


# ---------------------------------------------------------------------------
# Main prediction loop
# ---------------------------------------------------------------------------
def predict_for_service(
    service_cfg: dict[str, Any],
    config: dict[str, Any],
    dry_run: bool = False,
) -> dict[str, Any] | None:
    """
    Run prediction and HPA patching for a single service.

    Returns a result dict (or ``None`` on failure).
    """
    svc_name = service_cfg["name"]
    model_dir = config.get("model_dir", "/models")
    pred_cfg = config.get("prediction", {})
    forecast_steps = pred_cfg.get("forecast_steps", 12)
    interval_width = pred_cfg.get("interval_width", 0.80)

    logger.info("=== Predicting %s ===", svc_name)

    model = load_model(svc_name, model_dir)
    if model is None:
        logger.warning("  Skipping %s: no model available", svc_name)
        return None

    try:
        forecast = predict_peak(
            model,
            svc_name,
            forecast_steps=forecast_steps,
            interval_width=interval_width,
        )
    except Exception:
        logger.exception("  Prediction failed for %s", svc_name)
        return None

    min_replicas = calculate_min_replicas(
        predicted_peak_rps=forecast["predicted_peak_rps"],
        target_rps_per_pod=service_cfg["target_rps_per_pod"],
        lower_bound=service_cfg["min_replicas_lower_bound"],
        upper_bound=service_cfg["max_replicas_upper_bound"],
    )

    patched = patch_hpa_min_replicas(
        svc_name,
        min_replicas,
        dry_run=dry_run,
    )

    result = {
        "service": svc_name,
        "predicted_peak_rps": forecast["predicted_peak_rps"],
        "predicted_mean_rps": forecast["predicted_mean_rps"],
        "target_rps_per_pod": service_cfg["target_rps_per_pod"],
        "recommended_min_replicas": min_replicas,
        "lower_bound": service_cfg["min_replicas_lower_bound"],
        "upper_bound": service_cfg["max_replicas_upper_bound"],
        "hpa_patched": patched,
        "forecast_generated_at": forecast["forecast_generated_at"],
    }
    logger.info(
        "  ✓ %s → minReplicas=%d (peak=%.1f rps, target=%d rps/pod, patched=%s)",
        svc_name, min_replicas,
        forecast["predicted_peak_rps"],
        service_cfg["target_rps_per_pod"],
        patched,
    )
    return result


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Predict request rate and patch HPA minReplicas"
    )
    parser.add_argument(
        "--config",
        default=os.path.join(os.path.dirname(__file__), "config.yaml"),
        help="Path to configuration YAML (default: ./config.yaml)",
    )
    parser.add_argument(
        "--service",
        default=None,
        help="Predict for a single service only (default: all)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Log predictions but do not patch HPAs",
    )
    parser.add_argument(
        "--namespace",
        default="his-hope",
        help="Kubernetes namespace for HPA resources (default: his-hope)",
    )
    args = parser.parse_args()

    config = load_config(args.config)
    services = config.get("services", [])

    if args.service:
        services = [s for s in services if s["name"] == args.service]
        if not services:
            logger.error("Service '%s' not found in config", args.service)
            sys.exit(1)

    logger.info(
        "Prophet predictive scaler run — %d service(s), dry_run=%s",
        len(services), args.dry_run,
    )

    results: list[dict[str, Any]] = []
    for svc in services:
        result = predict_for_service(svc, config, dry_run=args.dry_run)
        if result:
            results.append(result)

    # Print JSON summary to stdout (consumed by logging / monitoring)
    summary = {
        "run_timestamp": datetime.now(timezone.utc).isoformat(),
        "dry_run": args.dry_run,
        "services_processed": len(results),
        "services": results,
    }
    print(json.dumps(summary, indent=2))

    logger.info(
        "Prediction run complete: %d services processed",
        len(results),
    )


if __name__ == "__main__":
    main()
