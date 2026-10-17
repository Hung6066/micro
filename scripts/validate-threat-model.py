#!/usr/bin/env python3
"""Fail-closed validation for the repository threat-model catalog."""
from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "config" / "threat-model.v1.json"
OUTPUT = ROOT / "artifacts" / "evidence" / "threat-model-validation.json"


def fail(message: str) -> None:
    raise SystemExit(f"Threat model validation FAIL: {message}")


def require_text(value: object, path: str) -> str:
    if not isinstance(value, str) or not value.strip():
        fail(f"{path} must be a non-empty string")
    return value.strip()


def validate_evidence(entries: object, path: str) -> None:
    if not isinstance(entries, list) or not entries:
        fail(f"{path} must contain evidence paths")
    for index, item in enumerate(entries):
        relative = require_text(item, f"{path}[{index}]")
        if not (ROOT / relative).exists():
            fail(f"missing evidence path: {relative}")


def main() -> None:
    if not SOURCE.is_file():
        fail(f"missing source: {SOURCE.relative_to(ROOT)}")
    data = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    if data.get("schemaVersion") != "threat-model.v1":
        fail("unsupported schemaVersion")
    require_text(data.get("system"), "system")
    require_text(data.get("securityOwner"), "securityOwner")
    require_text(data.get("clinicalSafetyOwner"), "clinicalSafetyOwner")
    if not isinstance(data.get("reviewCadenceDays"), int) or data["reviewCadenceDays"] <= 0:
        fail("reviewCadenceDays must be positive")

    flows = data.get("flows")
    abuses = data.get("abuseCases")
    if not isinstance(flows, list) or len(flows) < 5:
        fail("at least five data flows are required")
    if not isinstance(abuses, list) or len(abuses) < 5:
        fail("at least five abuse cases are required")

    flow_ids: set[str] = set()
    for index, flow in enumerate(flows):
        path = f"flows[{index}]"
        if not isinstance(flow, dict):
            fail(f"{path} must be an object")
        flow_id = require_text(flow.get("id"), f"{path}.id")
        if flow_id in flow_ids:
            fail(f"duplicate flow id: {flow_id}")
        flow_ids.add(flow_id)
        for field in ("name", "source", "destination", "trustBoundary", "dataClass"):
            require_text(flow.get(field), f"{path}.{field}")
        if not isinstance(flow.get("controls"), list) or not flow["controls"]:
            fail(f"{path}.controls must be non-empty")
        validate_evidence(flow.get("evidence"), f"{path}.evidence")

    abuse_ids: set[str] = set()
    for index, abuse in enumerate(abuses):
        path = f"abuseCases[{index}]"
        if not isinstance(abuse, dict):
            fail(f"{path} must be an object")
        abuse_id = require_text(abuse.get("id"), f"{path}.id")
        if abuse_id in abuse_ids:
            fail(f"duplicate abuse-case id: {abuse_id}")
        abuse_ids.add(abuse_id)
        for field in ("category", "scenario", "owner", "status", "mitigation"):
            require_text(abuse.get(field), f"{path}.{field}")
        if abuse["status"] not in {"mitigated", "accepted", "open"}:
            fail(f"{path}.status must be mitigated, accepted or open")
        validate_evidence(abuse.get("evidence"), f"{path}.evidence")

    if not any(flow["dataClass"] == "PHI" for flow in flows):
        fail("a PHI data flow is required")
    if not any(abuse["status"] == "open" for abuse in abuses):
        # Explicitly require security review to account for residual risk.
        fail("at least one residual open abuse case is required")

    result = {
        "status": "pass",
        "schemaVersion": data["schemaVersion"],
        "flowCount": len(flows),
        "abuseCaseCount": len(abuses),
        "generatedAtUtc": __import__("datetime").datetime.now(__import__("datetime").timezone.utc).isoformat(),
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"Threat model validation PASS: {len(flows)} flows, {len(abuses)} abuse cases; artifact={OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
