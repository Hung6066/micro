"""Validate repository container inputs before any image build is attempted."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FROM_RE = re.compile(r"^\s*FROM\s+(?P<image>mcr\.microsoft\.com/dotnet/[^\s]+)")


def main() -> int:
    errors: list[str] = []
    dockerfiles = sorted(
        path
        for path in ROOT.rglob("Dockerfile")
        if ".git" not in path.parts and ".worktrees" not in path.parts
    )
    if not dockerfiles:
        errors.append("No Dockerfiles were found.")

    base_images = 0
    for path in dockerfiles:
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            match = FROM_RE.match(line)
            if not match:
                continue
            base_images += 1
            image = match.group("image")
            if not re.search(r"@sha256:[0-9a-f]{64}(?:\s|$)", image):
                errors.append(f"{path.relative_to(ROOT)}:{line_number}: unpinned .NET base image")

    global_json = json.loads((ROOT / "global.json").read_text(encoding="utf-8"))
    sdk = global_json.get("sdk", {})
    if sdk.get("version") != "8.0.319":
        errors.append(f"global.json SDK version must remain 8.0.319, found {sdk.get('version')!r}")
    if sdk.get("rollForward") != "latestFeature":
        errors.append("global.json rollForward must be latestFeature for the pinned .NET 8 container base")

    if errors:
        for error in errors:
            print(f"CONTAINER_BUILD_CONTRACT_FAIL|{error}", file=sys.stderr)
        return 1

    print(f"Container build contract PASS: dockerfiles={len(dockerfiles)} dotnetBaseImages={base_images}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
