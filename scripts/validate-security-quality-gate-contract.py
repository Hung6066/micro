#!/usr/bin/env python3
"""Prevent raw repository scans from bypassing rendered-manifest policy checks."""

from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[1]
SECURITY_WORKFLOW = ROOT / ".github/workflows/security-quality-gate.yml"
PLATFORM_WORKFLOW = ROOT / ".github/workflows/platform-quality-gates.yml"


def fail(message: str) -> None:
    print(f"Security quality gate contract FAILED: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    security = SECURITY_WORKFLOW.read_text(encoding="utf-8")
    platform = PLATFORM_WORKFLOW.read_text(encoding="utf-8")

    raw_scan = re.search(
        r"- name: Trivy filesystem scan\s+uses: aquasecurity/trivy-action[^\n]*\s+with:(.*?)(?=\n      - name:|\Z)",
        security,
        re.DOTALL,
    )
    if raw_scan is None:
        fail("missing Trivy filesystem scan")
    if not re.search(r"^\s*scanners:\s*vuln,secret\s*$", raw_scan.group(1), re.MULTILINE):
        fail("raw Trivy filesystem scan must use scanners: vuln,secret")
    if 'dockerfile_path="${dockerfile#./}"' not in security:
        fail("container scan must strip the leading ./ before generating a Docker tag")
    if "tr '/._' '-'" not in security:
        fail("container scan must normalize Docker tag path separators")
    if "not -path './docker/sandbox/Dockerfile'" not in security:
        fail("developer-only sandbox must be explicitly excluded from the production image gate")
    if "timeout-minutes: 45" not in security or "timeout --signal=TERM --kill-after=30s 10m docker build" not in security:
        fail("container security gate must have bounded job and image-build timeouts")

    required_platform_contract = (
        "scan-type: config",
        "scan-ref: artifacts/k8s/prod.yaml",
        "python scripts/validate-trivy-production-findings.py",
    )
    for requirement in required_platform_contract:
        if requirement not in platform:
            fail(f"rendered production manifest policy scan missing: {requirement}")

    print("Security quality gate contract passed.")


if __name__ == "__main__":
    main()
