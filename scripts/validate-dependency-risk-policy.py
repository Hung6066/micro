#!/usr/bin/env python3
"""Validate the repository dependency-remediation SLA and exception policy."""

from __future__ import annotations

import datetime as dt
import json
import pathlib
import sys


ROOT = pathlib.Path(__file__).resolve().parents[1]
POLICY_PATH = ROOT / "config/dependency-risk-policy.v1.json"
SEVERITIES = ("critical", "high", "moderate", "low")


def fail(message: str) -> None:
    print(f"Dependency risk policy FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def parse_date(value: object, field: str) -> dt.date:
    if not isinstance(value, str):
        fail(f"{field} must be an ISO-8601 date")
    try:
        return dt.date.fromisoformat(value)
    except ValueError as exc:
        fail(f"{field} must be an ISO-8601 date: {exc}")


def main() -> int:
    if not POLICY_PATH.is_file():
        fail(f"missing {POLICY_PATH.relative_to(ROOT)}")
    try:
        policy = json.loads(POLICY_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"unable to read policy: {exc}")

    if policy.get("version") != "1.0":
        fail("policy version must be 1.0")
    if not policy.get("approvedBy"):
        fail("policy must identify approving functions")
    cadence = policy.get("scanCadenceDays")
    if not isinstance(cadence, int) or cadence <= 0 or cadence > 31:
        fail("scanCadenceDays must be between 1 and 31")

    slos = policy.get("serviceLevelObjectivesDays")
    if not isinstance(slos, dict) or set(slos) != set(SEVERITIES):
        fail("SLOs must define exactly critical, high, moderate and low")
    for severity in SEVERITIES:
        days = slos[severity]
        if not isinstance(days, int) or days <= 0:
            fail(f"{severity} SLO must be a positive number of days")
    if not (slos["critical"] <= slos["high"] <= slos["moderate"] <= slos["low"]):
        fail("SLO windows must be non-decreasing by severity")

    requirements = policy.get("exceptionRequirements")
    exceptions = policy.get("exceptions")
    if not isinstance(requirements, list) or not requirements:
        fail("exceptionRequirements must be a non-empty list")
    if not isinstance(exceptions, list):
        fail("exceptions must be a list")

    required = set(requirements)
    seen_ids: set[str] = set()
    today = dt.datetime.now(dt.timezone.utc).date()
    for exception in exceptions:
        if not isinstance(exception, dict):
            fail("each exception must be an object")
        missing = sorted(required - exception.keys())
        if missing:
            fail(f"exception is missing required fields: {', '.join(missing)}")
        exception_id = exception["id"]
        if not isinstance(exception_id, str) or not exception_id.strip() or exception_id in seen_ids:
            fail("exception ids must be non-empty and unique")
        seen_ids.add(exception_id)
        severity = exception["severity"]
        if severity not in SEVERITIES:
            fail(f"{exception_id} has unsupported severity {severity!r}")
        for field in ("package", "ecosystem", "reason", "owner", "compensatingControl", "trackingIssue", "approvedBy"):
            if not isinstance(exception[field], str) or not exception[field].strip():
                fail(f"{exception_id}.{field} must be non-empty")
        expiry = parse_date(exception["expiresAt"], f"{exception_id}.expiresAt")
        if expiry <= today:
            fail(f"{exception_id} expires on or before today")

    print(
        "Dependency risk policy PASS: "
        f"scan cadence={cadence}d, SLOs={slos['critical']}/{slos['high']}/{slos['moderate']}/{slos['low']}d, "
        f"active exceptions={len(exceptions)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
