"""Fail closed on literal Kubernetes secret values without printing their values."""

from pathlib import Path
import re
import sys


KEY = re.compile(r"^\s*(?:password|token|privateKey|clientSecret)\s*:\s*(?P<value>\S+)", re.I)
PLACEHOLDER = re.compile(r"^['\"]?\$\{[A-Za-z_][A-Za-z0-9_]*\}['\"]?(?:#.*)?$")
SAFE = re.compile(r"^['\"]?(?:<[^>]+>|REDACTED|vault:[^\s]+)['\"]?(?:#.*)?$", re.I)


def main() -> int:
    violations = []
    for path in sorted(Path("k8s").rglob("*.y*ml")):
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            match = KEY.match(line)
            if not match:
                continue
            value = match.group("value")
            if PLACEHOLDER.match(value) or SAFE.match(value):
                continue
            violations.append(f"{path}:{number}")
    if violations:
        print(f"Manifest secret contract FAIL: {len(violations)} literal value(s) found")
        print("\n".join(violations))
        return 1
    print("Manifest secret contract PASS: no literal Kubernetes secret values")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
