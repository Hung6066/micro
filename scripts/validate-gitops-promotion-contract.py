#!/usr/bin/env python3
"""Fail-closed static contract for the production digest promotion workflow."""

from __future__ import annotations

import pathlib
import re
import sys

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "gitops-release-promotion.yml"
UPDATER = ROOT / "scripts" / "update-gitops-digest.ps1"
RELEASE_UPDATER = ROOT / "scripts" / "update-gitops-release-digests.ps1"


def fail(message: str) -> None:
    print(f"GitOps promotion contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    if not WORKFLOW.is_file():
        fail("missing .github/workflows/gitops-release-promotion.yml")
    raw = WORKFLOW.read_text(encoding="utf-8")
    try:
        document = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        fail(f"invalid YAML: {exc}")

    jobs = document.get("jobs") or {}
    promotion = jobs.get("promotion-pr")
    if not isinstance(promotion, dict):
        fail("promotion job is missing")
    if promotion.get("environment") != "production":
        fail("promotion job must use the protected production environment")
    timeout = promotion.get("timeout-minutes")
    if not isinstance(timeout, int) or timeout <= 0 or timeout > 60:
        fail("promotion job requires timeout-minutes between 1 and 60")

    required_fragments = (
        "update-gitops-release-digests.ps1",
        "Install Harbor trust chain and Cosign",
        "Verify every promoted digest",
        "cosign-linux-amd64",
        "8b24b946dd5809c6bd93de08033bcf6bc0ed7d336b7785787c080f574b89249b",
        "container-release.yml",
        "HARBOR_CA_CHAIN_B64",
        "update-ca-certificates",
        "Create review-required promotion PR",
    )
    for fragment in required_fragments:
        if fragment not in raw:
            fail(f"missing required supply-chain control: {fragment}")
    if not UPDATER.is_file():
        fail("missing scripts/update-gitops-digest.ps1")
    if not RELEASE_UPDATER.is_file():
        fail("missing scripts/update-gitops-release-digests.ps1")
    updater = UPDATER.read_text(encoding="utf-8")
    if "ReleaseSha" not in updater or "newTag:" not in updater:
        fail("digest updater must align the image tag with ReleaseSha when promoting a digest")

    if not re.search(r"\^sha256:\[0-9a-f\]\{64\}\$", raw) and not re.search(
        r"sha256:\[0-9a-f\]\{64\}",
        RELEASE_UPDATER.read_text(encoding="utf-8"),
    ):
        fail("workflow must validate lowercase immutable sha256 input")
    if not re.search(r"\^https://github\.com/\$\{(?:GITHUB_REPOSITORY|env:GITHUB_REPOSITORY)\}/\.github/workflows/container-release\.yml", raw):
        fail("workflow must bind verification to the container-release workflow identity")
    if re.search(r"kubectl\s+(?:apply|create|patch|label)|helm\s+upgrade|ansible-playbook", raw):
        fail("promotion workflow must open a PR and not mutate a cluster")

    print("GitOps promotion contract PASS: protected digest-only review PRs and signed provenance preflight")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
