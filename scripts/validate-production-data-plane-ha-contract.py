#!/usr/bin/env python3
"""Fail-closed HA contract for the production SPIRE/data-plane overlay."""

from __future__ import annotations

import pathlib
import re
import subprocess
import sys

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[1]


def fail(message: str) -> None:
    print(f"Production data-plane HA contract FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def workload(documents: list[dict], kind: str, name: str, namespace: str) -> dict:
    for document in documents:
        metadata = document.get("metadata", {})
        if (
            document.get("kind") == kind
            and metadata.get("name") == name
            and metadata.get("namespace") == namespace
        ):
            return document
    fail(f"missing {kind} {namespace}/{name}")


def require_spread(resource: dict, label: str) -> None:
    constraints = resource.get("spec", {}).get("template", {}).get("spec", {}).get(
        "topologySpreadConstraints", []
    )
    if not any(
        constraint.get("topologyKey") == "kubernetes.io/hostname"
        and constraint.get("maxSkew") == 1
        and constraint.get("whenUnsatisfiable") == "DoNotSchedule"
        for constraint in constraints
    ):
        fail(f"{label} has no required hostname topology spread constraint")
    if not any(
        constraint.get("topologyKey") == "topology.kubernetes.io/zone"
        and constraint.get("maxSkew") == 1
        and constraint.get("whenUnsatisfiable") == "ScheduleAnyway"
        for constraint in constraints
    ):
        fail(f"{label} has no zone failure-domain topology spread constraint")


def require_pdb(documents: list[dict], name: str, namespace: str, minimum: int) -> None:
    for document in documents:
        metadata = document.get("metadata", {})
        spec = document.get("spec", {})
        selector = spec.get("selector", {}).get("matchLabels", {})
        if (
            document.get("kind") == "PodDisruptionBudget"
            and metadata.get("name") == name
            and metadata.get("namespace") == namespace
            and selector.get("app.kubernetes.io/name") == name
            and int(spec.get("minAvailable", 0)) >= minimum
        ):
            return
    fail(f"{namespace}/{name} has no PDB preserving {minimum} available pod(s)")


def require_namespace_security(documents: list[dict], name: str, enforce: str) -> None:
    namespace = next(
        (document for document in documents if document.get("kind") == "Namespace" and document.get("metadata", {}).get("name") == name),
        None,
    )
    if namespace is None:
        fail(f"missing namespace {name}")
    labels = namespace.get("metadata", {}).get("labels", {})
    if labels.get("pod-security.kubernetes.io/enforce") != enforce:
        fail(f"namespace {name} must enforce Pod Security {enforce}")
    if labels.get("pod-security.kubernetes.io/warn") != "restricted" or labels.get("pod-security.kubernetes.io/audit") != "restricted":
        fail(f"namespace {name} must warn and audit restricted workloads")
    if labels.get("policy.sigstore.dev/include") != "true":
        fail(f"namespace {name} must opt into signature admission")


def main() -> int:
    try:
        rendered = subprocess.run(
            [
                "kubectl",
                "kustomize",
                str(ROOT / "k8s/overlays/prod-spire-azure-shared-storage"),
                "--load-restrictor",
                "LoadRestrictionsNone",
            ],
            check=True,
            capture_output=True,
            text=True,
        ).stdout
    except (OSError, subprocess.CalledProcessError) as exc:
        fail(f"unable to render production data-plane overlay: {exc}")

    documents = [document for document in yaml.safe_load_all(rendered) if document]
    if re.search(r"(?im)^\s*storageClass(?:Name)?:\s*local-path\s*$", rendered):
        fail("production data-plane still renders local-path storage")
    longhorn_snapshot_resources = [
        f"{document.get('kind')}/{document.get('metadata', {}).get('name')}"
        for document in documents
        if document.get("kind") == "VolumeSnapshotClass"
        and (
            document.get("metadata", {}).get("name") == "longhorn"
            or document.get("driver") == "driver.longhorn.io"
        )
    ]
    if longhorn_snapshot_resources:
        fail(
            "production shared-storage overlay must not render Longhorn snapshot resources: "
            + ", ".join(sorted(longhorn_snapshot_resources))
        )
    image_references = re.findall(r"(?m)^\s*image:\s*(\S+)", rendered)
    unpinned_images = [image for image in image_references if not re.search(r"@sha256:[0-9a-f]{64}$", image)]
    if unpinned_images:
        fail("production data-plane contains unpinned images: " + ", ".join(unpinned_images))
    mutable_versions = [
        f"{document.get('kind')}/{document.get('metadata', {}).get('name')}"
        for document in documents
        if document.get("metadata", {}).get("labels", {}).get("app.kubernetes.io/version") in {"latest", "dev", "main"}
    ]
    if mutable_versions:
        fail("mutable app.kubernetes.io/version label(s): " + ", ".join(sorted(mutable_versions)))
    oidc = workload(documents, "Deployment", "spire-oidc", "spire")
    minio = workload(documents, "StatefulSet", "minio", "backup")
    spire = workload(documents, "StatefulSet", "spire-server", "spire")
    postgres = workload(documents, "Cluster", "spire-postgres", "spire")
    agent = workload(documents, "DaemonSet", "spire-agent", "spire")
    agent_template = agent.get("spec", {}).get("template", {})
    agent_spec = agent_template.get("spec", {})
    agent_container = next(
        (container for container in agent_spec.get("containers", []) if container.get("name") == "spire-agent"),
        None,
    )
    if agent_template.get("metadata", {}).get("annotations", {}).get("his-hope.io/security-exception") != "spire-node-agent-integration":
        fail("spire/agent must declare the reviewed node-integration security exception")
    if not agent_spec.get("hostPID") or not agent_spec.get("hostNetwork") or not agent_container or not agent_container.get("securityContext", {}).get("privileged"):
        fail("spire/agent node-integration exception is not explicitly configured")
    if not agent_container.get("resources", {}).get("requests") or not agent_container.get("resources", {}).get("limits"):
        fail("spire/agent must retain resource requests and limits")
    require_namespace_security(documents, "backup", "restricted")
    require_namespace_security(documents, "spire", "privileged")

    if int(oidc.get("spec", {}).get("replicas", 1)) < 2:
        fail("spire/oidc replicas must be at least 2")
    if int(minio.get("spec", {}).get("replicas", 1)) < 4:
        fail("backup/minio replicas must be at least 4")
    if int(spire.get("spec", {}).get("replicas", 1)) < 3:
        fail("spire/spire-server replicas must be at least 3")
    if int(postgres.get("spec", {}).get("instances", 1)) < 3:
        fail("spire/spire-postgres instances must be at least 3")
    if postgres.get("spec", {}).get("storage", {}).get("storageClass") != "viettel-shared":
        fail("spire/spire-postgres must use the approved replicated CSI class viettel-shared")
    for stateful_set, label in ((minio, "backup/minio"), (spire, "spire/spire-server")):
        classes = {
            claim.get("spec", {}).get("storageClassName")
            for claim in stateful_set.get("spec", {}).get("volumeClaimTemplates", [])
        }
        if classes != {"viettel-shared"}:
            fail(f"{label} must use the approved replicated CSI class viettel-shared")

    require_spread(oidc, "spire/oidc")
    require_spread(minio, "backup/minio")
    require_spread(spire, "spire/spire-server")
    require_pdb(documents, "spire-oidc", "spire", 1)
    require_pdb(documents, "minio", "backup", 3)
    require_pdb(documents, "spire-server", "spire", 2)
    print("Production data-plane HA contract PASS: OIDC 2+, MinIO 4+, SPIRE 3+, CNPG 3+, PDB and topology coverage")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
