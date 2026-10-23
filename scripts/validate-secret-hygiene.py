#!/usr/bin/env python3
"""Fail closed when captured authentication material is reintroduced."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FORBIDDEN_PATHS = (
    "auth_headers.txt",
    "authorize_result.bin",
    "cookie*.txt",
    "*_cookie.txt",
    "tests/e2e/inspect*.js",
    "tests/e2e/fixtures/*-auth.json",
    ".tools/dev-render.yaml",
    "docker/temporal/.env",
)
FORBIDDEN_MARKERS = (
    b"#HttpOnly_",
    b"BEGIN RSA PRIVATE KEY",
    b"BEGIN OPENSSH PRIVATE KEY",
)


def tracked_paths() -> list[Path]:
    result = subprocess.run(
        ["git", "-C", str(ROOT), "ls-files", "-z"],
        check=True,
        stdout=subprocess.PIPE,
    )
    return [ROOT / item for item in result.stdout.decode().split("\0") if item]


def main() -> int:
    violations: list[str] = []
    paths = tracked_paths()
    for pattern in FORBIDDEN_PATHS:
        for path in ROOT.glob(pattern):
            if path.is_file():
                violations.append(f"forbidden captured-artifact path: {path.relative_to(ROOT)}")

    for path in paths:
        if path.resolve() == Path(__file__).resolve():
            continue
        if not path.is_file() or path.stat().st_size > 5 * 1024 * 1024:
            continue
        data = path.read_bytes()
        for marker in FORBIDDEN_MARKERS:
            if marker in data:
                violations.append(
                    f"forbidden authentication/secret marker {marker.decode(errors='replace')!r}: "
                    f"{path.relative_to(ROOT)}"
                )

    if violations:
        print("SECRET_HYGIENE_FAIL")
        print("\n".join(sorted(set(violations))))
        return 80

    print(f"SECRET_HYGIENE_PASS tracked_files={len(paths)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
