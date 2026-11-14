#!/usr/bin/env python3
"""Verify the GitHub image promotion workflow is inert after OpenShip adoption."""
from __future__ import annotations
import pathlib
import sys
import yaml
ROOT = pathlib.Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/gitops-release-promotion.yml"
def fail(message: str) -> None:
    print(f"GitOps retirement contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)
def main() -> int:
    if not WORKFLOW.is_file():
        fail("retired workflow record must remain available for historical DORA runs")
    raw = WORKFLOW.read_text(encoding="utf-8")
    try:
        document = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        fail(f"invalid YAML: {exc}")
    triggers = document.get(True) or document.get("on") or {}
    if "workflow_dispatch" in triggers:
        fail("retired image promotion must not be manually dispatchable")
    if (triggers.get("workflow_run") or {}).get("workflows") != ["Container Release Supply Chain"]:
        fail("legacy trigger must remain disconnected from Source Build and Test")
    retired = (document.get("jobs") or {}).get("retired") or {}
    if retired.get("if") != "${{ false }}":
        fail("retired workflow job must be unconditionally disabled")
    if (document.get("permissions") or {}) != {"contents": "read"}:
        fail("retired workflow must have read-only repository permissions")
    for fragment in ("cosign", "harbor", "update-gitops", "create-pull-request", "kubectl apply", "helm upgrade"):
        if fragment in raw.lower():
            fail(f"retired workflow still contains release behavior: {fragment}")
    print("GitOps retirement contract PASS: no dispatch, artifact verification, or production promotion remains")
    return 0
if __name__ == "__main__":
    raise SystemExit(main())
