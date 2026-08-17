"""Fail closed when a GitHub Actions workflow references a mutable ref."""

from __future__ import annotations

import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOWS = ROOT / ".github" / "workflows"
USES = re.compile(r"^\s*(?:-\s*)?uses:\s*(?P<repo>[^\s@]+)@(?P<ref>[^\s#]+)")
SHA = re.compile(r"^[0-9a-f]{40}$")


def main() -> int:
    failures: list[str] = []
    references = 0
    for workflow in sorted(WORKFLOWS.glob("*.y*ml")):
        for line_number, line in enumerate(workflow.read_text(encoding="utf-8").splitlines(), 1):
            match = USES.match(line)
            if not match:
                continue
            references += 1
            ref = match.group("ref")
            if not SHA.fullmatch(ref):
                failures.append(f"{workflow.relative_to(ROOT)}:{line_number}: {match.group('repo')}@{ref}")

    if failures:
        print("Workflow action pin gate FAIL: mutable action references found", file=sys.stderr)
        print("\n".join(failures), file=sys.stderr)
        return 1
    print(f"Workflow action pin gate PASS: workflows={len(list(WORKFLOWS.glob('*.y*ml')))} references={references}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
