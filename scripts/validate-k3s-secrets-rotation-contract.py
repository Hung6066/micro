#!/usr/bin/env python3
"""Static contract for the protected K3s Secrets Encryption rotation path."""

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PLAYBOOK = ROOT / "ansible/enterprise-k3s/playbooks/45-rotate-k3s-secrets-encryption.yml"
SCRIPT = ROOT / "scripts/rotate-k3s-secrets-encryption.ps1"
WORKFLOW = ROOT / ".github/workflows/k3s-secrets-encryption-rotation.yml"

def fail(message: str) -> None:
    print(f"K3s secrets rotation contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)

def require(path: Path, fragments: tuple[str, ...]) -> None:
    if not path.is_file():
        fail(f"missing {path.relative_to(ROOT)}")
    raw = path.read_text(encoding="utf-8")
    for fragment in fragments:
        if fragment not in raw:
            fail(f"{path.relative_to(ROOT)} missing {fragment}")

def main() -> int:
    require(PLAYBOOK, ("serial: 1", "k3s_rotation_snapshot_path", "secrets-encrypt rotate-keys", "reencrypt_finished", "systemd_service", "k3s_secrets_rotation_approved"))
    require(SCRIPT, ("-AllowProduction", "[switch]$Apply", "status = if ($Apply)", "ANSIBLE_VAULT_PASSWORD_FILE", "redacted"))
    require(WORKFLOW, ("inputs.apply", "snapshot_path", "environment: ${{ inputs.environment }}", "ANSIBLE_SSH_PRIVATE_KEY", "ANSIBLE_VAULT_PASSWORD"))
    print("K3s secrets rotation contract PASS: snapshot prerequisite, HA serial order, dry-run and protected credentials are wired")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
