#!/usr/bin/env python3
"""
Anomaly Detection Model Serving - Vertex AI Endpoint
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
    -> {
        "anomaly_score": 0.87,
        "is_anomalous": true,
        "contributing_metrics": {"error_rate": 0.52},
        "reconstruction_errors": {
            "request_rate": 0.12, "error_rate": 0.52, "latency_p99": 0.23
        }
    }

Usage:
    python serve_model.py                          # starts FastAPI server
    python serve_model.py --deploy <model-resource> # deploy to Vertex AI

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
from typing import Any, Optional

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
    """Wraps the trained LSTM autoencoder model for inference."""

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
            metrics_window: List of dicts with keys matching feature_cols.
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
        svc_params = scaler_params.get(
            service,
            scaler_params.get(list(scaler_params.keys())[0], {}),
        )
        mean_vals = np.array([svc_params.get("mean", {}).get(c, 0.0) for c in feature_cols])
        std_vals = np.array([svc_params.get("std", {}).get(c, 1.0) for c in feature_cols])
        std_vals = np.where(std_vals == 0, 1.0, std_vals)

        X_scaled = (X_raw - mean_vals) / std_vals
        X_input = np.expand_dims(X_scaled, axis=0)

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

        is_anomalous = total_mse > threshold

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
_detector: AnomalyDetector | None = None


def get_detector() -> AnomalyDetector:
    global _detector
    if _detector is None or not _detector._loaded:
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
        service: str = Field(
            ..., description="Service name (e.g., patient-service)"
        )
        metrics_window: list[dict[str, float]] = Field(
            ..., description="List of metric observations"
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
        except Exception:
            logger.exception("Prediction failed")
            raise HTTPException(status_code=500, detail="Internal prediction error")

    return app


# ---------------------------------------------------------------------------
# Vertex AI deployment
# ---------------------------------------------------------------------------
def deploy_to_vertex(model_resource_name: str, config: dict[str, Any]) -> str:
    """Deploy a model from Vertex AI Model Registry to an endpoint."""
    from google.cloud import aiplatform

    project = config.get("project", "his-hope-analytics")
    region = config.get("region", "us-east1")
    serving_cfg = config.get("serving", {})

    aiplatform.init(project=project, location=region)

    model = aiplatform.Model(model_resource_name)

    endpoint_name = "anomaly-detection-endpoint"
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

    model.deploy(
        endpoint=endpoint,
        machine_type=serving_cfg.get("machine_type", "n1-standard-4"),
        min_replica_count=serving_cfg.get("min_replicas", 1),
        max_replica_count=serving_cfg.get("max_replicas", 3),
        traffic_split={"0": 100},
    )

    logger.info(
        "Deployed model %s to endpoint %s",
        model_resource_name, endpoint.resource_name,
    )
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
        help="Deploy a Vertex AI model resource to an endpoint",
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
        logger.warning(
            "Model not found at %s. Model will load on first request.",
            model_path,
        )

    app = create_app(config)
    logger.info("Starting anomaly detection server on port %d...", args.port)
    uvicorn.run(app, host="0.0.0.0", port=args.port, log_level="info")


if __name__ == "__main__":
    main()
