#!/usr/bin/env python3
"""
Alertmanager Webhook - Anomaly Detection Confirmation
======================================================
Receives Alertmanager webhook alerts, queries the trained anomaly detection
model for confirmation, and suppresses false positives.

The webhook acts as a "sanity check" layer: before an alert fires, it asks
the ML model whether the current metrics window actually looks anomalous.

Flow:
    Alertmanager -> this webhook -> anomaly model -> confirm/suppress

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
from datetime import datetime, timedelta, timezone
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
        "request_rate": (
            f'sum(rate(http_requests_total{{service="{service}"}}[1m]))'
        ),
        "error_rate": (
            f'sum(rate(http_requests_total{{service="{service}",status=~"5.."}}[1m]))'
            f' / '
            f'sum(rate(http_requests_total{{service="{service}"}}[1m]))'
        ),
        "latency_p99": (
            f'histogram_quantile(0.99, '
            f'sum(rate(http_request_duration_seconds_bucket{{service="{service}"}}[1m])) '
            f'by (le))'
        ),
    }

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
                    float(val_str)
                    if val_str not in ("NaN", "+Inf", "-Inf", "")
                    else 0.0
                )
        except requests.RequestException as exc:
            logger.warning("Failed to fetch metric %s: %s", metric_name, exc)

    if not metric_data:
        return None

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

    seq_len = 60
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
        """Query the anomaly model for prediction."""
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

    _suppression_counters: dict[str, int] = {}

    def _extract_service(labels: dict[str, str]) -> str | None:
        """Extract service name from alert labels."""
        for key in ("service", "service_name", "job"):
            if key in labels:
                return labels[key]
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
                    ...
                }
            ]
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
                "action": "pass",
            }

            if status != "firing" or service is None:
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
                decision["action"] = "pass"
                decision["anomaly_score"] = anomaly_score
                decision["reason"] = "Model confirms anomalous pattern"
                logger.info(
                    "ALERT CONFIRMED: %s/%s (score=%.4f, threshold=%.4f)",
                    service, alertname, anomaly_score,
                    prediction.get("anomaly_threshold", 0),
                )
                _suppression_counters[alert_key] = 0
            else:
                decision["action"] = "suppress"
                decision["anomaly_score"] = anomaly_score
                decision["reason"] = "Model did not confirm anomaly (likely false positive)"
                _suppression_counters[alert_key] = suppress_count + 1
                logger.info(
                    "ALERT SUPPRESSED: %s/%s (score=%.4f, suppression #%d)",
                    service, alertname, anomaly_score,
                    _suppression_counters[alert_key],
                )

            decisions.append(decision)

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
