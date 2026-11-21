#!/usr/bin/env python3
"""Ensure GitHub Actions validates source without releasing application images."""

from __future__ import annotations

import pathlib
import sys

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKFLOW_PATH = ROOT / ".github" / "workflows" / "source-ci.yml"
RETIRED_RELEASE_WORKFLOWS = (
    "container-release.yml",
    "gitops-promotion.yml",
    "azure-identity-acr.yml",
)


def fail(message: str) -> None:
    print(f"Source CI boundary FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    for name in RETIRED_RELEASE_WORKFLOWS:
        if (ROOT / ".github" / "workflows" / name).exists():
            fail(f"application release workflow remains enabled: {name}")

    if not WORKFLOW_PATH.is_file():
        fail("missing .github/workflows/source-ci.yml")

    raw = WORKFLOW_PATH.read_text(encoding="utf-8")
    try:
        document = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        fail(f"invalid YAML: {exc}")

    triggers = document.get(True) or document.get("on") or {}
    if not isinstance(triggers, dict):
        fail("source CI triggers must be a mapping")
    required_branches = {"main", "develop", "developer"}
    for event in ("push", "pull_request"):
        branches = set((triggers.get(event) or {}).get("branches") or [])
        if not required_branches.issubset(branches):
            fail(f"{event} must validate main, develop, and developer")

    jobs = document.get("jobs") or {}
    backend = jobs.get("backend") or {}
    frontend = jobs.get("frontend") or {}
    required = (
        ("backend", "dotnet restore His.Hope.sln"),
        ("backend", "dotnet build His.Hope.sln"),
        ("backend", "run-non-integration-tests.ps1"),
        ("frontend", "npm run build:frontend:production"),
        ("frontend", "npm --workspace @his-hope/frontend-foundation run test"),
    )
    job_text = {"backend": str(backend), "frontend": str(frontend)}
    for job, fragment in required:
        if fragment not in job_text[job]:
            fail(f"{job} job is missing source validation: {fragment}")

    forbidden = (
        "docker/build-push-action@",
        "docker push",
        "cosign sign",
        "cosign attest",
        "azure/login@",
        "kubectl apply",
        "helm upgrade",
    )
    for fragment in forbidden:
        if fragment in raw:
            fail(f"source CI must not publish/sign/deploy application artifacts: {fragment}")

    permissions = document.get("permissions") or {}
    if permissions.get("contents") != "read":
        fail("source CI must use read-only repository contents permission")
    if permissions.get("packages") or permissions.get("id-token"):
        fail("source CI must not receive package-publish or OIDC signing permissions")

    print("Source CI boundary PASS: source builds/tests only; OpenShip owns image build, signing, and K3s rollout")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
