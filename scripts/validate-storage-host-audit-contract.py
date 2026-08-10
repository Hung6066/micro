"""Validate the Longhorn host prerequisite audit remains read-only and fail-closed."""

from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
PLAYBOOK = ROOT / "ansible/enterprise-k3s/playbooks/25-validate-storage-prerequisites.yml"


def main() -> int:
    if not PLAYBOOK.is_file():
        print(f"Missing storage audit playbook: {PLAYBOOK}", file=sys.stderr)
        return 1
    text = PLAYBOOK.read_text(encoding="utf-8")
    required = (
        "hosts: k3s_servers:k3s_workers",
        "serial: 5",
        "ansible_devices",
        "iscsid_active",
        "root_propagation",
        "ansible.builtin.assert:",
        "dedicated_data_disks | length >= 1",
    )
    missing = [item for item in required if item not in text]
    if missing:
        print("Storage audit contract missing: " + ", ".join(missing), file=sys.stderr)
        return 1
    forbidden = ("mkfs", "wipefs", "ansible.builtin.mount:", "apt:", "package:")
    found_forbidden = [item for item in forbidden if item in text]
    if found_forbidden:
        print("Storage audit must remain read-only; found: " + ", ".join(found_forbidden), file=sys.stderr)
        return 1
    print("Storage host audit contract PASS: read-only disk/iSCSI/mount checks are fail-closed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
