#!/usr/bin/env python3
"""Static contract for Linkerd Server/ServerAuthorization mTLS policy."""

from pathlib import Path
import re
import sys
import yaml

ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "k8s/linkerd/server.yaml"
AUTH = ROOT / "k8s/linkerd/server-authorization.yaml"
CONTROL = ROOT / "k8s/linkerd/linkerd-control-plane.yaml"
CNI = ROOT / "scripts/configure-linkerd-cni-k3s.ps1"
HELM_TASKS = ROOT / "ansible/enterprise-k3s/roles/linkerd_observability/tasks/main.yml"
OBS_VARS = ROOT / "ansible/enterprise-k3s/group_vars/observability.yml"

def fail(message: str) -> None:
    print(f"Linkerd policy contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)

def docs(path: Path):
    try:
        return [d for d in yaml.safe_load_all(path.read_text(encoding="utf-8")) if d]
    except yaml.YAMLError as exc:
        fail(f"invalid YAML in {path.relative_to(ROOT)}: {exc}")

def main() -> int:
    servers = {d.get("metadata", {}).get("name") for d in docs(SERVER) if d.get("kind") == "Server"}
    auths = {d.get("metadata", {}).get("name") for d in docs(AUTH) if d.get("kind") == "ServerAuthorization"}
    if not servers:
        fail("no Linkerd Server resources found")
    missing = sorted(servers - auths)
    if missing:
        fail(f"ServerAuthorization missing for: {', '.join(missing)}")
    for d in docs(AUTH):
        if d.get("kind") != "ServerAuthorization":
            continue
        client = d.get("spec", {}).get("client", {})
        if client.get("meshTLS", {}).get("unauthenticated") is not False:
            fail(f"{d['metadata']['name']} must fail closed for unauthenticated mesh TLS")
        if d["metadata"]["name"].endswith("-grpc") and "unauthenticated" in client:
            fail(f"gRPC policy {d['metadata']['name']} must not include unauthenticated network access")
    control = CONTROL.read_text(encoding="utf-8")
    for fragment in ("webhookFailurePolicy: Fail", "cpu:", "memory:", "scheme: kubernetes.io/tls"):
        if fragment not in control:
            fail(f"control-plane manifest missing {fragment}")
    cni = CNI.read_text(encoding="utf-8")
    for fragment in ("/var/lib/rancher/k3s/data/cni", "/var/lib/rancher/k3s/agent/etc/cni/net.d", "rollout status daemonset"):
        if fragment not in cni:
            fail(f"K3s CNI ordering/path contract missing {fragment}")
    helm = HELM_TASKS.read_text(encoding="utf-8")
    for fragment in ("--version", "--wait", "--timeout", "linkerd_crds_chart_version", "linkerd_control_plane_chart_version", "linkerd_viz_chart_version"):
        if fragment not in helm:
            fail(f"Linkerd Helm installation missing immutable/versioned control: {fragment}")
    versions = []
    for line in OBS_VARS.read_text(encoding="utf-8").splitlines():
        if "linkerd_" in line and "_chart_version:" in line:
            version = line.split(":", 1)[1].strip().strip("'\"")
            versions.append(version)
            if version == "edge" or not re.fullmatch(r"\d+\.\d+\.\d+", version):
                fail(f"Linkerd chart version is not an approved immutable semantic version: {version}")
    if len(versions) != 3:
        fail("expected pinned CRD/control-plane/viz Linkerd chart versions")
    print(f"Linkerd policy contract PASS: {len(servers)} servers have fail-closed mTLS authorizations; CNI, injector and pinned chart controls wired")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
