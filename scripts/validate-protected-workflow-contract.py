#!/usr/bin/env python3
"""Fail-closed contract for workflows that can mutate protected clusters."""

from __future__ import annotations

import pathlib
import re
import sys

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKFLOWS = ROOT / ".github" / "workflows"
PROTECTED = {
    "alertmanager-e2e.yml",
    "control-plane-rebuild-drill.yml",
    "application-restore-smoke.yml",
    "argocd-bootstrap.yml",
    "cnpg-azure-backup-bootstrap.yml",
    "cnpg-restore-drill.yml",
    "database-continuity-pvc-migration.yml",
    "harbor-clean-node-drill.yml",
    "vault-recovery-drill.yml",
    "k3s-backup-agent-rollout.yml",
    "pod-security-production-rollout.yml",
    "sigstore-policy-controller-bootstrap.yml",
    "k3s-secrets-encryption-rotation.yml",
    "linkerd-mtls-policy-e2e.yml",
    "k3s-production-go-live-gate.yml",
}
REQUIRED_EVIDENCE_WORKFLOWS = {"k3s-devsecops-gate.yml"}


def fail(message: str) -> None:
    print(f"Protected workflow contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    for name in sorted(REQUIRED_EVIDENCE_WORKFLOWS):
        path = WORKFLOWS / name
        if not path.is_file():
            fail(f"missing required evidence workflow {name}")
        raw = path.read_text(encoding="utf-8")
        if "artifacts/evidence" in raw and "uses: actions/upload-artifact@" in raw:
            if "if-no-files-found: ignore" in raw:
                fail(f"{name} must not ignore missing evidence artifacts")
            if "if-no-files-found: error" not in raw:
                fail(f"{name} must fail when required evidence artifact is missing")
    for name in sorted(PROTECTED):
        path = WORKFLOWS / name
        if not path.is_file():
            fail(f"missing workflow {name}")
        raw = path.read_text(encoding="utf-8")
        if "artifacts/evidence" in raw and "uses: actions/upload-artifact@" in raw:
            if "if-no-files-found: error" not in raw:
                fail(f"{name} must fail when required evidence artifact is missing")
        try:
            document = yaml.safe_load(raw) or {}
        except yaml.YAMLError as exc:
            fail(f"invalid YAML in {name}: {exc}")
        jobs = document.get("jobs") or {}
        if not jobs:
            fail(f"{name} has no jobs")
        trigger = document.get(True) or document.get("on") or {}
        dispatch = trigger.get("workflow_dispatch") if isinstance(trigger, dict) else None
        inputs = dispatch.get("inputs") if isinstance(dispatch, dict) else {}
        for guarded_input in ("apply", "run_test"):
            definition = inputs.get(guarded_input) if isinstance(inputs, dict) else None
            if definition is None:
                continue
            if definition.get("type") != "boolean":
                fail(f"{name} input {guarded_input} must be boolean")
            if definition.get("default") is not False:
                fail(f"{name} input {guarded_input} must default to false")
        if not re.search(r"(?m)^\s*concurrency:\s*$", raw):
            fail(f"{name} must declare concurrency")
        for job_name, job in jobs.items():
            if not isinstance(job, dict):
                fail(f"{name}:{job_name} is not a job mapping")
            if not job.get("environment"):
                fail(f"{name}:{job_name} lacks a protected environment")
            timeout = job.get("timeout-minutes")
            if not isinstance(timeout, int) or timeout <= 0 or timeout > 60:
                fail(f"{name}:{job_name} requires timeout-minutes between 1 and 60")
        mutation_markers = (
            r"kubectl\s+(?:apply|create|patch|label)",
            r"helm\s+upgrade",
            r"ansible-playbook",
            r"(?:^|\s)-Apply(?:\s|$)",
        )
        if any(re.search(marker, raw, flags=re.MULTILINE) for marker in mutation_markers) and "inputs.apply" not in raw:
            fail(f"{name} contains mutation tooling without an inputs.apply guard")

        if name == "pod-security-production-rollout.yml":
            boundary = raw.find("bootstrap-k3s-security-boundaries.ps1")
            rollout = raw.find("rollout-pod-security-production.ps1")
            if boundary < 0 or rollout < 0 or boundary > rollout:
                fail("pod-security-production-rollout.yml must bootstrap security boundaries before Pod Security rollout")
            if "bootstrap-k3s-security-boundaries.ps1 -Kubeconfig" not in raw:
                fail("pod-security-production-rollout.yml must invoke the boundary bootstrap script")
            if "bootstrap-k3s-security-boundaries.ps1 -Kubeconfig '${{ steps.kubeconfig.outputs.path }}' -Apply -AllowProduction" not in raw:
                fail("pod-security-production-rollout.yml must guard the production boundary apply with -AllowProduction")
            if "validate-live-image-drift.ps1" not in raw:
                fail("pod-security-production-rollout.yml must block enforcement while live images drift from reviewed digests")

        if name == "argocd-bootstrap.yml":
            if "-HighAvailability" not in raw:
                fail("argocd-bootstrap.yml must install the reviewed HA manifest")
            if "inputs.apply" not in raw or "-WhatIf" not in raw:
                fail("argocd-bootstrap.yml must retain an explicit dry-run path")
        if name == "longhorn-storage-bootstrap.yml":
            if "ANSIBLE_SSH_PRIVATE_KEY" not in raw or "prepare-longhorn-nodes.ps1" not in raw:
                fail("longhorn-storage-bootstrap.yml must run the protected read-only host prerequisite and labeling wrapper")
            if "inputs.environment == 'production'" not in raw:
                fail("longhorn-storage-bootstrap.yml must scope the host prerequisite audit to production")
        if name == "alertmanager-e2e.yml":
            if "inputs.run_test" not in raw or "-AllowProduction" not in raw:
                fail("alertmanager-e2e.yml must have an explicit run_test and production guard")
            if "ALERTMANAGER_E2E_RECEIVER_URL" not in raw:
                fail("alertmanager-e2e.yml must use a protected dedicated receiver URL")
            if "-BearerToken" in raw:
                fail("alertmanager-e2e.yml must inject the bearer token through the environment, not command-line arguments")
        if name == "application-restore-smoke.yml":
            if "APP_RESTORE_BEARER_TOKEN" not in raw or "-AllowProduction" not in raw:
                fail("application-restore-smoke.yml must use a protected bearer token and production guard")
            if "-BearerToken" in raw:
                fail("application-restore-smoke.yml must inject the bearer token through the environment, not command-line arguments")
        if name == "control-plane-rebuild-drill.yml":
            if "VaultPasswordPath" not in raw or "ANSIBLE_VAULT_PASSWORD" not in raw or "inputs.apply" not in raw:
                fail("control-plane-rebuild-drill.yml must use protected Ansible vault input and an explicit apply guard")
        if name == "k3s-secrets-encryption-rotation.yml":
            if "ANSIBLE_SSH_PRIVATE_KEY" not in raw or "ANSIBLE_VAULT_PASSWORD" not in raw or "inputs.apply" not in raw:
                fail("k3s-secrets-encryption-rotation.yml must use protected Ansible inputs and an explicit apply guard")
            if "rotate-k3s-secrets-encryption.ps1" not in raw or "-AllowProduction" not in raw:
                fail("k3s-secrets-encryption-rotation.yml must invoke the guarded rotation script")
        if name == "linkerd-mtls-policy-e2e.yml":
            if "inputs.apply" not in raw or "-AllowProduction" not in raw or "grpcurl_image" not in raw:
                fail("linkerd-mtls-policy-e2e.yml must use an explicit apply guard and digest-pinned probe image input")
            if "test-linkerd-mtls-policy.ps1" not in raw:
                fail("linkerd-mtls-policy-e2e.yml must invoke the guarded mTLS policy probe")
        if name == "k3s-production-go-live-gate.yml":
            if "Where-Object { $_.outcome -ne 'success' }" not in raw:
                fail("k3s-production-go-live-gate.yml must fail when any required step is skipped or unsuccessful")
            if "Production go-live gate failed after evidence collection" not in raw:
                fail("k3s-production-go-live-gate.yml must aggregate and report failed evidence steps")
    print(f"Protected workflow contract PASS: workflows={len(PROTECTED)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
