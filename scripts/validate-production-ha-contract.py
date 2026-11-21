#!/usr/bin/env python3
"""Fail-closed static HA contract for the production application overlay."""

from __future__ import annotations

import pathlib
import subprocess
import sys

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[1]
REQUIRED = {
    "admin-app", "api-gateway", "appointment-service", "billing-bff",
    "billing-service", "clinical-bff", "clinical-service", "commerce-service",
    "content-service", "dashboard-app", "dashboard-bff", "frontend",
    "identity-service", "lab-bff", "lab-service", "manufacturing-service",
    "patient-bff", "patient-service", "pharmacy-bff", "pharmacy-service",
    "systemdashboard-bff",
}


def fail(message: str) -> None:
    print(f"Production HA contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    try:
        rendered = subprocess.run(
            ["kubectl", "kustomize", str(ROOT / "k8s/overlays/prod"), "--load-restrictor", "LoadRestrictionsNone"],
            check=True, capture_output=True, text=True,
        ).stdout
    except (OSError, subprocess.CalledProcessError) as exc:
        fail(f"unable to render production overlay: {exc}")

    documents = [document for document in yaml.safe_load_all(rendered) if document]
    deployments = {
        document["metadata"]["name"].removeprefix("his-hope-"): document
        for document in documents if document.get("kind") == "Deployment"
    }
    missing = sorted(REQUIRED - deployments.keys())
    if missing:
        fail(f"required HA deployment(s) are missing: {', '.join(missing)}")

    pdbs = [d for d in documents if d.get("kind") == "PodDisruptionBudget"]
    hpas = [d for d in documents if d.get("kind") == "HorizontalPodAutoscaler"]
    errors: list[str] = []
    for name in sorted(REQUIRED):
        deployment = deployments[name]
        replicas = int(deployment.get("spec", {}).get("replicas", 1))
        if replicas < 2:
            errors.append(f"{name} has replicas={replicas}, expected at least 2")
        spread = deployment.get("spec", {}).get("template", {}).get("spec", {}).get("topologySpreadConstraints", [])
        if not any(constraint.get("topologyKey") == "kubernetes.io/hostname" for constraint in spread):
            errors.append(f"{name} has no node topology spread constraint")

        matching_pdb = [
            pdb for pdb in pdbs
            if pdb.get("spec", {}).get("selector", {}).get("matchLabels", {}).get("app.kubernetes.io/name") == name
        ]
        if not matching_pdb:
            errors.append(f"{name} has no PodDisruptionBudget")
        else:
            preserves_one = False
            for pdb in matching_pdb:
                spec = pdb.get("spec", {})
                if int(spec.get("minAvailable", 0)) >= 1:
                    preserves_one = True
                elif int(spec.get("maxUnavailable", replicas)) < replicas:
                    preserves_one = True
            if not preserves_one:
                errors.append(f"{name} PDB does not preserve one available pod")

        matching_hpa = [
            hpa for hpa in hpas
            if hpa.get("metadata", {}).get("name") == f"his-hope-{name}"
            or hpa.get("spec", {}).get("scaleTargetRef", {}).get("name") == f"his-hope-{name}"
        ]
        if matching_hpa and min(int(hpa["spec"].get("minReplicas", 1)) for hpa in matching_hpa) < 2:
            errors.append(f"{name} HPA minReplicas is below 2")

    if errors:
        fail("; ".join(errors))
    print(f"Production HA contract PASS: {len(REQUIRED)} deployments have >=2 replicas and PDB coverage")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
