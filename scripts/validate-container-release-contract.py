#!/usr/bin/env python3
"""Compatibility check that prevents the old GitHub image release returning."""
from __future__ import annotations
import pathlib
import sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
def main() -> int:
    if (ROOT / ".github/workflows/container-release.yml").exists():
        print("Application image release must be owned by OpenShip, not GitHub Actions.", file=sys.stderr)
        return 1
    if not (ROOT / ".github/workflows/source-ci.yml").is_file():
        print("Source CI workflow is missing.", file=sys.stderr)
        return 1
    print("Container release boundary PASS: GitHub Actions no longer builds, signs, or publishes application images")
    return 0
if __name__ == "__main__":
    raise SystemExit(main())
