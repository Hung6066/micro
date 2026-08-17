#!/usr/bin/env python3
"""Static fail-closed contract for K3s host and control-plane hardening."""

from pathlib import Path
import sys
import yaml

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "ansible/enterprise-k3s/roles/k3s_server/templates/config.yaml.j2"
AGENT = ROOT / "ansible/enterprise-k3s/roles/k3s_agent/templates/config.yaml.j2"
PSA = ROOT / "ansible/enterprise-k3s/roles/k3s_server/templates/psa.yaml.j2"
AUDIT = ROOT / "ansible/enterprise-k3s/roles/k3s_server/templates/audit-policy.yaml.j2"
OS_TASKS = ROOT / "ansible/enterprise-k3s/roles/os_hardening/tasks/main.yml"
INVENTORY = ROOT / "ansible/enterprise-k3s/inventory/production.yml"
GROUP_VARS = ROOT / "ansible/enterprise-k3s/group_vars/all.yml"

def fail(message: str) -> None:
    print(f"K3s host security contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)

def require(path: Path, fragments: tuple[str, ...]) -> None:
    if not path.is_file():
        fail(f"missing {path.relative_to(ROOT)}")
    raw = path.read_text(encoding="utf-8")
    for fragment in fragments:
        if fragment not in raw:
            fail(f"{path.relative_to(ROOT)} missing {fragment}")

def main() -> int:
    require(SERVER, ("secrets-encryption: true", "protect-kernel-defaults: true", "enable-admission-plugins=NodeRestriction,EventRateLimit", "admission-control-config-file=", "audit-log-path=", "audit-policy-file=", "streaming-connection-idle-timeout=5m", "terminated-pod-gc-threshold=10", "node-label:"))
    require(AGENT, ("protect-kernel-defaults: true", "streaming-connection-idle-timeout=5m", "terminated-pod-gc-threshold=10", "tls-cipher-suites=", "node-label:"))
    require(PSA, ("apiVersion: apiserver.config.k8s.io/v1", "restricted", "exemptions:"))
    require(AUDIT, ("apiVersion: audit.k8s.io/v1", "resources: [\"secrets\"]", "RequestResponse"))
    require(OS_TASKS, ("swapoff -a", "/etc/modules-load.d/k3s.conf", "br_netfilter", "/etc/sysctl.d/90-kubelet.conf"))
    require(INVENTORY, ("k3s_servers:", "k3s_workers:", "k3s_worker_labels:", "workload.his-hope.io/app=true", "workload.his-hope.io/data=true"))
    require(GROUP_VARS, ("k3s_server_labels:", "workload.his-hope.io/system=true"))
    try:
        psa = yaml.safe_load(PSA.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as exc:
        fail(f"invalid PSA template YAML: {exc}")
    if psa.get("kind") != "AdmissionConfiguration":
        fail("PSA template must be an AdmissionConfiguration")
    print("K3s host security contract PASS: encryption, audit, PSA, kubelet GC/timeouts, swap/sysctl and node governance are wired")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
