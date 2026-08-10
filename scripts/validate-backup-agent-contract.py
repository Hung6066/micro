"""Validate the K3s etcd-to-Azure backup agent without reading secret values."""

from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    template = (ROOT / "ansible/enterprise-k3s/roles/backup_agents/templates/backup.env.j2").read_text(encoding="utf-8")
    unit = (ROOT / "ansible/enterprise-k3s/roles/backup_agents/templates/k3s-etcd-snapshot.service.j2").read_text(encoding="utf-8")
    script = (ROOT / "scripts/k3s-etcd-snapshot-to-azure.sh").read_text(encoding="utf-8")
    workflow = (ROOT / ".github/workflows/k3s-backup-agent-rollout.yml").read_text(encoding="utf-8")
    blob_validator = (ROOT / "scripts/validate-azure-blob-access.py").read_text(encoding="utf-8")

    failures: list[str] = []
    for key in (
        "AZURE_STORAGE_ENDPOINT",
        "AZURE_STORAGE_CONTAINER",
        "AZURE_STORAGE_SAS_TOKEN",
        "AZURE_BACKUP_PREFIX",
    ):
        if f"{key}={{{{ {('vault_backup_sas_token' if key == 'AZURE_STORAGE_SAS_TOKEN' else key.lower())} | to_json }}}}" not in template:
            # Keep the check explicit for the Jinja variable names; this avoids
            # accepting an unquoted SAS value hidden behind a broad regex.
            expected = {
                "AZURE_STORAGE_ENDPOINT": "azure_storage_endpoint",
                "AZURE_STORAGE_CONTAINER": "azure_storage_container",
                "AZURE_STORAGE_SAS_TOKEN": "vault_backup_sas_token",
                "AZURE_BACKUP_PREFIX": "azure_backup_prefix",
            }[key]
            if f"{key}={{{{ {expected} | to_json }}}}" not in template:
                failures.append(f"{key} is not JSON-quoted in backup.env.j2")

    for required in ("EnvironmentFile=/etc/his-hope/backup.env", "NoNewPrivileges=true", "ProtectSystem=strict", "ReadWritePaths="):
        if required not in unit:
            failures.append(f"systemd unit missing {required}")
    if '[[ "${#sas}" -ge 20 ]]' not in script:
        failures.append("snapshot script lacks minimum SAS length guard")
    if "--check" not in workflow or "APPROVE-BACKUP-AGENTS" not in workflow or "--diff=false" in workflow:
        failures.append("backup workflow must be check-first and use the explicit approval code")
    if "k3s-etcd-snapshot-freshness.json" not in workflow or "freshnessWindowMinutes" not in workflow:
        failures.append("backup workflow must collect fresh snapshot evidence from all control-plane servers")
    if "systemctl start his-hope-k3s-etcd-snapshot.service" not in workflow:
        failures.append("backup workflow must run one controlled snapshot smoke after apply")
    if "systemctl show his-hope-k3s-etcd-snapshot.service --property=Result --value" not in workflow:
        failures.append("backup workflow must verify oneshot Result=success instead of is-active")
    remaining = (ROOT / "scripts/validate-k3s-remaining-gates.ps1").read_text(encoding="utf-8")
    for required in (
        "his-hope-k3s-etcd-snapshot.timer",
        "his-hope-k3s-etcd-snapshot.service",
        "systemctl show $BackupServiceUnit --property=Result --value",
        "ExecMainStatus",
        "InventoryPath",
        "SshKeyPath",
        "remoteRecords",
    ):
        if required not in remaining:
            failures.append(f"remaining-gates backup check is missing {required}")
    for required in (
        "AZURE_PRODUCTION_ENV_B64",
        "ansible-vault view ansible/enterprise-k3s/group_vars/vault.yml",
        "vault_backup_sas_token",
        "Azure backup source consistency: PASS",
        "steps.runtime.outputs.azure",
    ):
        if required not in workflow:
            failures.append(f"backup workflow must enforce Azure/Vault SAS source consistency ({required})")
    for required in ("--env-file", "Azure Blob access: PASS", "urlopen", "SAS value redacted", "racwl", "se", "expired"):
        if required not in blob_validator:
            failures.append(f"Azure Blob access validator is missing {required}")
    if "validate-azure-blob-access.py" not in workflow:
        failures.append("backup workflow must validate Azure Blob access before Ansible")
    go_live = (ROOT / ".github/workflows/k3s-production-go-live-gate.yml").read_text(encoding="utf-8")
    if "validate-azure-blob-access.py" not in go_live:
        failures.append("production go-live workflow must validate Azure Blob access before backup gates")
    for required in ("ANSIBLE_SSH_PRIVATE_KEY", "-InventoryPath ansible/enterprise-k3s/inventory/production.yml", "-SshKeyPath", "read-only backup host audit"):
        if required not in go_live:
            failures.append(f"production go-live workflow must provide remote backup audit input ({required})")

    if failures:
        print("Backup agent contract FAIL", file=sys.stderr)
        print("\n".join(failures), file=sys.stderr)
        return 1
    print("Backup agent contract PASS: quoted systemd env, hardened unit, SAS guard, smoke and protected workflow")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
