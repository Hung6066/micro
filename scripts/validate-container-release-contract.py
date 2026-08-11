#!/usr/bin/env python3
"""Fail-closed contract for the Harbor container release workflow."""

from __future__ import annotations

import pathlib
import sys

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "container-release.yml"


def fail(message: str) -> None:
    print(f"Container release contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    if not WORKFLOW.is_file():
        fail("missing .github/workflows/container-release.yml")
    raw = WORKFLOW.read_text(encoding="utf-8")
    try:
        document = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        fail(f"invalid YAML: {exc}")

    jobs = document.get("jobs") or {}
    trigger = document.get(True) or document.get("on") or {}
    dispatch = trigger.get("workflow_dispatch") if isinstance(trigger, dict) else None
    publish = (dispatch.get("inputs") or {}).get("publish") if isinstance(dispatch, dict) else None
    if publish is None or publish.get("type") != "boolean" or publish.get("default") is not False:
        fail("workflow_dispatch.publish must be boolean and default to false")
    preflight = jobs.get("quality-security")
    release = jobs.get("release")
    if not isinstance(preflight, dict) or not isinstance(release, dict):
        fail("quality-security and release jobs are required")
    if release.get("needs") != "quality-security":
        fail("release job must depend on quality-security")
    if release.get("environment") != "production":
        fail("release job must use the protected production environment for Harbor credentials")
    timeout = preflight.get("timeout-minutes")
    if not isinstance(timeout, int) or timeout <= 0 or timeout > 60:
        fail("quality-security requires timeout-minutes between 1 and 60")

    required = (
        "dotnet restore His.Hope.sln",
        "dotnet build His.Hope.sln",
        "npm audit --omit=dev --audit-level=high",
        "aquasecurity/trivy-action@",
        "validate-kustomize-release.ps1 -Environment prod",
        "validate-kustomize-runtime.ps1 -Overlay prod",
        "verify-admission-policy.ps1",
        "validate-manifest-secret-contract.py",
        "validate-container-build-contract.py",
        "skip-dirs: k8s,docker/spire",
        "docker/build-push-action@",
        "cosign sign --yes",
        "cosign attest --yes",
        "HARBOR_CA_CHAIN_B64",
        "update-ca-certificates",
    )
    for fragment in required:
        if fragment not in raw:
            fail(f"missing release control: {fragment}")

    if "push: true" not in raw or "if: ${{ github.event_name != 'workflow_dispatch' || inputs.publish == true }}" not in raw:
        fail("Harbor push must remain explicitly gated")
    print("Container release contract PASS: quality/security preflight precedes digest push, signing and attestation")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
