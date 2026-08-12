#!/usr/bin/env python3
"""Fail-closed static contract for the production digest promotion workflow."""

from __future__ import annotations

import pathlib
import re
import sys



ROOT = pathlib.Path(__file__).resolve().parents[1]
RELEASE_WORKFLOW = ROOT / ".github" / "workflows" / "gitops-release-promotion.yml"
RELEASE_UPDATER = ROOT / "scripts" / "update-gitops-release-digests.ps1"
MIRROR_WORKFLOW = ROOT / ".github" / "workflows" / "gitops-mirror-verify.yml"
MIRROR_SYNC_WORKFLOW = ROOT / ".github" / "workflows" / "gitops-mirror-sync.yml"
ARGOCD_BOOTSTRAP_WORKFLOW = ROOT / ".github" / "workflows" / "argocd-bootstrap.yml"


def fail(message: str) -> None:
    print(f"GitOps promotion contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    legacy_workflow = ROOT / ".github" / "workflows" / "gitops-promotion.yml"
    if legacy_workflow.exists():
        fail("legacy single-digest gitops-promotion.yml must be removed; use the 19-digest release promotion workflow")
    if not RELEASE_WORKFLOW.is_file():
        fail("missing .github/workflows/gitops-release-promotion.yml")
    if not RELEASE_UPDATER.is_file():
        fail("missing scripts/update-gitops-release-digests.ps1")
    if not MIRROR_WORKFLOW.is_file():
        fail("missing .github/workflows/gitops-mirror-verify.yml")
    if not MIRROR_SYNC_WORKFLOW.is_file():
        fail("missing .github/workflows/gitops-mirror-sync.yml")
    if not ARGOCD_BOOTSTRAP_WORKFLOW.is_file():
        fail("missing .github/workflows/argocd-bootstrap.yml")
    release_raw = RELEASE_WORKFLOW.read_text(encoding="utf-8")
    required_release_fragments = (
        "workflow_run:",
        "Container Release Supply Chain",
        "release_run_id:",
        "gh run download",
        "image-ref-*",
        "verify-image-attestations.ps1",
        "update-gitops-release-digests.ps1",
        "Create review-required promotion PR",
        "GITOPS_PRODUCTION_BRANCH",
        "HARBOR_CA_CHAIN_B64",
        "jq -r '.head_repository.full_name'",
        "jq -r '.head_branch'",
        "tags/v",
    )
    for fragment in required_release_fragments:
        if fragment not in release_raw:
            fail(f"release promotion is missing required control: {fragment}")
    if not re.search(r"\^https://github\.com/\$\{env:GITHUB_REPOSITORY\}/\.github/workflows/container-release\.yml", release_raw):
        fail("release promotion must bind verification to the container-release workflow identity")
    if not re.search(r"tags/v\[0-9\]", release_raw):
        fail("release promotion must restrict attestation identity to version tags")
    if re.search(r"kubectl\s+(?:apply|create|patch|label)|helm\s+upgrade|ansible-playbook", release_raw):
        fail("release promotion must open a review PR and not mutate a cluster")

    mirror_raw = MIRROR_WORKFLOW.read_text(encoding="utf-8")
    for fragment in ("verify-git-mirror.ps1", "KUBECONFIG_PRODUCTION_B64", "workflow_run:", "head_sha", "RequireSynced"):
        if fragment not in mirror_raw:
            fail(f"mirror verification is missing required control: {fragment}")
    for fragment in ("runs-on: [self-hosted, linux, gitops-mirror]", "EXPECTED_REPO_URL", "GITOPS_MIRROR_REPO_URL"):
        if fragment not in mirror_raw:
            fail(f"mirror verification is missing required self-hosted HTTPS control: {fragment}")

    mirror_sync_raw = MIRROR_SYNC_WORKFLOW.read_text(encoding="utf-8")
    for fragment in (
        "branches: [production]",
        "GITOPS_MIRROR_REPO_URL",
        "GITOPS_MIRROR_USERNAME",
        "GITOPS_MIRROR_TOKEN",
        "fetch --no-tags production-mirror",
        "--force-with-lease=\"refs/heads/production:$expected_remote_revision\"",
        "environment: production",
        "runs-on: [self-hosted, linux, gitops-mirror]",
        "https://",
    ):
        if fragment not in mirror_sync_raw:
            fail(f"mirror synchronization is missing required control: {fragment}")
    if re.search(r"git\s+push\s+[^\n]*--force(?!-with-lease)", mirror_sync_raw):
        fail("mirror synchronization must use force-with-lease, never unconditional force")

    bootstrap_raw = ARGOCD_BOOTSTRAP_WORKFLOW.read_text(encoding="utf-8")
    for fragment in ("change_reference:", "Approved change/ticket reference", "change_reference }}' -notmatch", "Apply reviewed GitOps bootstrap applications"):
        if fragment not in bootstrap_raw:
            fail(f"Argo bootstrap cutover is missing required approval control: {fragment}")

    print("GitOps promotion contract PASS: protected digest-only review PRs and signed provenance preflight")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
