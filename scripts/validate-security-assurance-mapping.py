#!/usr/bin/env python3
"""Validate ASVS/CWE control mapping against the generated endpoint inventory."""

from __future__ import annotations

import json
import pathlib
import re
import sys


ROOT = pathlib.Path(__file__).resolve().parents[1]
MAPPING_PATH = ROOT / "config/security-assurance-mapping.v1.json"


def fail(message: str) -> None:
    print(f"Security assurance mapping FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def load_json(path: pathlib.Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"unable to read {path.relative_to(ROOT)}: {exc}")


def main() -> int:
    mapping = load_json(MAPPING_PATH)
    if not isinstance(mapping, dict) or mapping.get("schemaVersion") != "security-assurance-mapping.v1":
        fail("unsupported mapping schema")
    controls = mapping.get("controls")
    if not isinstance(controls, list) or not controls:
        fail("controls must be a non-empty list")

    control_ids: set[str] = set()
    for control in controls:
        if not isinstance(control, dict):
            fail("each control must be an object")
        control_id = control.get("id")
        if not isinstance(control_id, str) or not re.fullmatch(r"ASVS-V\d+(?:\.\d+)+", control_id):
            fail(f"invalid ASVS control id: {control_id!r}")
        if control_id in control_ids:
            fail(f"duplicate control id: {control_id}")
        control_ids.add(control_id)
        cwe = control.get("cwe")
        if not isinstance(cwe, list) or not cwe or any(
            not isinstance(item, str) or not re.fullmatch(r"CWE-\d+", item) for item in cwe
        ):
            fail(f"{control_id} must include valid CWE ids")
        if control.get("status") not in {"implemented", "partial", "planned"}:
            fail(f"{control_id} has invalid status")
        for field in ("owner", "objective", "evidence", "verificationCommands"):
            if not control.get(field):
                fail(f"{control_id} is missing {field}")
        for relative in [*control["evidence"]]:
            if not isinstance(relative, str) or not (ROOT / relative).is_file():
                fail(f"{control_id} references missing evidence: {relative}")
        if any(not isinstance(command, str) or not command.strip() for command in control["verificationCommands"]):
            fail(f"{control_id} has an invalid verification command")

    inventory_relative = mapping.get("inventoryPath")
    if not isinstance(inventory_relative, str):
        fail("inventoryPath is required")
    inventory_path = ROOT / inventory_relative
    if not inventory_path.is_file():
        fail(f"missing endpoint inventory: {inventory_relative}")
    inventory = load_json(inventory_path)
    if not isinstance(inventory, list):
        fail("endpoint inventory must be a JSON array")

    classification_controls = mapping.get("classificationControls")
    if not isinstance(classification_controls, dict):
        fail("classificationControls is required")
    for classification, required in classification_controls.items():
        if not isinstance(required, list) or not required or any(item not in control_ids for item in required):
            fail(f"{classification} references unknown or empty controls")
    for row in inventory:
        if not isinstance(row, dict):
            fail("endpoint inventory row must be an object")
        classification = row.get("classification")
        if classification not in classification_controls:
            fail(f"endpoint has unmapped classification: {classification!r}")

    mapped_inventory = []
    for row in inventory:
        classification = row["classification"]
        mapped_inventory.append(
            {
                **row,
                "assuranceControls": classification_controls[classification],
                "mappingEvidence": "config/security-assurance-mapping.v1.json",
            }
        )
    mapped_relative = mapping.get("mappedInventoryPath")
    if not isinstance(mapped_relative, str) or not mapped_relative.strip():
        fail("mappedInventoryPath is required")
    mapped_path = ROOT / mapped_relative
    try:
        mapped_path.parent.mkdir(parents=True, exist_ok=True)
        mapped_path.write_text(json.dumps(mapped_inventory, indent=2) + "\n", encoding="utf-8")
    except OSError as exc:
        fail(f"unable to write mapped inventory: {exc}")

    print(
        "Security assurance mapping PASS: "
        f"{len(control_ids)} ASVS controls, {len(inventory)} endpoints, "
        f"{len(classification_controls)} endpoint classifications mapped; "
        f"artifact={mapped_relative}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
