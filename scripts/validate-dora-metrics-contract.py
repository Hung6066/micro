#!/usr/bin/env python3
"""Fail-closed static contract for DORA metric production and dashboard use."""

from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[1]
collector = (ROOT / "scripts/collect-dora-metrics.py").read_text(encoding="utf-8")
workflow = (ROOT / ".github/workflows/dora-metrics.yml").read_text(encoding="utf-8")
dashboard = (ROOT / "k8s/monitoring/dora-metrics-dashboard.yaml").read_text(encoding="utf-8")
overlay = "\n".join(
    (
        ROOT / "k8s/observability/overlays/prod-shared-storage/kustomization.yaml"
    ).read_text(encoding="utf-8")
    .splitlines()
    + (ROOT / "k8s/observability/overlays/prod/kustomization.yaml").read_text(encoding="utf-8").splitlines()
)

required_metrics = [
    "pipeline_deployment_frequency_per_day",
    "pipeline_lead_time_seconds",
    "pipeline_change_failure_rate_ratio",
    "pipeline_mttr_seconds",
]
errors: list[str] = []
for metric in required_metrics:
    if metric not in collector:
        errors.append(f"collector does not emit {metric}")
    if metric not in dashboard:
        errors.append(f"dashboard does not query {metric}")
if "pipeline_deployments_total" not in collector:
    errors.append("collector does not emit pipeline_deployments_total")

for required in ["collect-dora-metrics.py", "GITHUB_TOKEN", "artifacts/dora", "schedule:", "workflow_dispatch:"]:
    if required not in workflow:
        errors.append(f"DORA workflow missing {required}")

if "dora-metrics-dashboard.yaml" not in overlay:
    errors.append("production observability overlay does not include the DORA dashboard")
if re.search(r"(?:ghp_|github_pat_|token\s*[:=]\s*['\"])[A-Za-z0-9_\-]{20,}", collector + workflow, re.I):
    errors.append("possible hard-coded GitHub credential in DORA collector/workflow")

if errors:
    print("DORA metrics contract FAIL")
    for error in errors:
        print(f"- {error}")
    raise SystemExit(80)
print(f"DORA metrics contract PASS: metrics={len(required_metrics)} producer=GitHub Actions dashboard=production")
