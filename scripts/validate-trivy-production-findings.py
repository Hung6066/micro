#!/usr/bin/env python3
"""Fail closed on rendered-production Trivy findings, with narrow reviewed exceptions.

KSV-0108 treats any ExternalName as an internet-exposure risk.  Production uses
two aliases only to preserve legacy service names while resolving to services
inside the cluster.  KSV-0109 also flags the runtime contract ConfigMap even
though it holds Vault references and fixed placeholders rather than values.
This verifier keeps the scanner's output authoritative and accepts only those
three exact findings after independently validating their safety invariants.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml


EXPECTED_EXTERNAL_NAMES = {
    "his-hope-redis": "his-hope-redis.his-hope-data.svc.cluster.local",
    "his-hope-rabbitmq": "his-hope-rabbitmq.his-hope-data.svc.cluster.local",
}
RUNTIME_CONFIG_NAME = "his-hope-runtime-contract-config"
PLACEHOLDER_KEYS = {
    "SECRET_POSTGRES_PASSWORD",
    "SECRET_RABBITMQ_PASSWORD",
    "SECRET_REDIS_PASSWORD",
    "SECRET_OIDC_CLIENT_SECRET",
}
REFERENCE_KEYS = {f"{key}_REF" for key in PLACEHOLDER_KEYS}


def fail(message: str) -> None:
    print(f"FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def normalized_id(finding: dict) -> str:
    return re.sub(r"[^A-Z0-9]", "", str(finding.get("ID", "")).upper())


def finding_text(finding: dict) -> str:
    return " ".join(
        str(finding.get(key, ""))
        for key in ("Title", "Message", "Description", "Resolution")
    ).lower()


def load_documents(path: Path) -> list[dict]:
    return [doc for doc in yaml.safe_load_all(path.read_text(encoding="utf-8")) if isinstance(doc, dict)]


def validate_rendered_contract(documents: list[dict]) -> None:
    external_names: dict[str, str] = {}
    runtime_config: dict | None = None
    for document in documents:
        kind = document.get("kind")
        metadata = document.get("metadata") or {}
        name = metadata.get("name")
        spec = document.get("spec") or {}
        if kind == "Service":
            if spec.get("externalIPs"):
                fail(f"Service [{name}] declares externalIPs.")
            if spec.get("type") == "ExternalName":
                external_names[str(name)] = str(spec.get("externalName", ""))
        if kind == "ConfigMap" and name == RUNTIME_CONFIG_NAME:
            runtime_config = document

    if external_names != EXPECTED_EXTERNAL_NAMES:
        fail(f"ExternalName Services must equal {EXPECTED_EXTERNAL_NAMES}, found {external_names}.")
    if runtime_config is None:
        fail(f"Missing runtime ConfigMap [{RUNTIME_CONFIG_NAME}].")

    data = runtime_config.get("data") or {}
    for key in PLACEHOLDER_KEYS:
        if data.get(key) != "__FROM_SECRET_PROVIDER__":
            fail(f"Runtime ConfigMap key [{key}] must be the fixed Vault placeholder.")
    for key in REFERENCE_KEYS:
        value = str(data.get(key, ""))
        if not re.fullmatch(r"kv/data/his-hope/production/[a-z0-9-]+#[a-z0-9-]+", value):
            fail(f"Runtime ConfigMap key [{key}] must contain only a production Vault reference.")
    if data.get("HIS_HOPE_SECRET_PROVIDER") != "vault":
        fail("Runtime ConfigMap must declare Vault as its secret provider.")
    if data.get("HIS_HOPE_SECRET_PROVIDER_REF") != "kv/data/his-hope/production/runtime":
        fail("Runtime ConfigMap must use the approved Vault runtime reference.")

    for key, value in data.items():
        if "CONNECTIONSTRING" in key.upper() and re.search(r"(?i)(?:password|pwd)=[^;\s]+", str(value)):
            fail(f"Runtime ConfigMap connection string [{key}] contains a literal password.")


def allowed_finding(finding: dict) -> bool:
    finding_id = normalized_id(finding)
    text = finding_text(finding)
    resource = str(finding.get("Resource", "")).strip()
    if finding_id == "KSV0108":
        # Bind the exception to the exact rendered Service and target name.
        # Free-form titles/messages are not an authorization boundary.
        return (
            resource in EXPECTED_EXTERNAL_NAMES
            and EXPECTED_EXTERNAL_NAMES[resource].lower() in text
        )
    if finding_id == "KSV0109":
        return resource == RUNTIME_CONFIG_NAME
    return False


def main() -> None:
    if len(sys.argv) != 3:
        fail("Usage: validate-trivy-production-findings.py <trivy-json> <rendered-manifest>")
    report_path = Path(sys.argv[1])
    rendered_path = Path(sys.argv[2])
    if not report_path.is_file() or not rendered_path.is_file():
        fail("Trivy report and rendered production manifest must exist.")

    validate_rendered_contract(load_documents(rendered_path))
    report = json.loads(report_path.read_text(encoding="utf-8"))
    findings = [
        finding
        for result in report.get("Results", [])
        for finding in result.get("Misconfigurations") or []
    ]
    rejected = [finding for finding in findings if not allowed_finding(finding)]
    if rejected:
        details = ", ".join(f"{finding.get('ID')}:{finding.get('Title')}" for finding in rejected)
        fail(f"Unexpected production Trivy finding(s): {details}")
    allowed_resources = [
        str(finding.get("Resource", "")).strip()
        for finding in findings
        if normalized_id(finding) in {"KSV0108", "KSV0109"}
    ]
    expected_resources = set(EXPECTED_EXTERNAL_NAMES) | {RUNTIME_CONFIG_NAME}
    unexpected_allowed = [resource for resource in allowed_resources if resource not in expected_resources]
    if unexpected_allowed:
        fail(f"Reviewed Trivy exceptions reference unexpected resource(s): {unexpected_allowed}")
    duplicate_resources = sorted({resource for resource in allowed_resources if allowed_resources.count(resource) > 1})
    if duplicate_resources:
        fail(f"Reviewed Trivy exceptions exceed one finding per resource: {duplicate_resources}")
    print(f"PASS: reviewed production Trivy exception contract accepted {len(findings)} finding(s).")


if __name__ == "__main__":
    main()
