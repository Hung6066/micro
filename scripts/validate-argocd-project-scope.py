"""Ensure every rendered Argo Application resource is covered by AppProject scope."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[1]
BOOTSTRAP = ROOT / "k8s" / "gitops" / "bootstrap"

# These are the cluster-scoped kinds used by the repository's GitOps sources.
# Unknown kinds are treated as namespaced so a new cluster-scoped resource must
# be added here and to AppProject.clusterResourceWhitelist deliberately.
CLUSTER_SCOPED = {
    ("", "Namespace"),
    ("apiextensions.k8s.io", "CustomResourceDefinition"),
    ("scheduling.k8s.io", "PriorityClass"),
    ("constraints.gatekeeper.sh", "K8sApprovedImageRegistry"),
    ("constraints.gatekeeper.sh", "K8sRequiredResources"),
    ("constraints.gatekeeper.sh", "K8sRestrictedWorkload"),
    ("templates.gatekeeper.sh", "ConstraintTemplate"),
    ("policy.sigstore.dev", "ClusterImagePolicy"),
    ("rbac.authorization.k8s.io", "ClusterRole"),
    ("rbac.authorization.k8s.io", "ClusterRoleBinding"),
    ("snapshot.storage.k8s.io", "VolumeSnapshotClass"),
}


def run(*args: str) -> str:
    # kubectl/kustomize emits UTF-8 even when PowerShell's active code page is
    # cp1252; decode explicitly so a non-ASCII manifest cannot crash this
    # fail-closed validator before it evaluates scope.
    result = subprocess.run(
        args,
        cwd=ROOT,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
        check=False,
    )
    if result.returncode:
        raise RuntimeError(f"command failed ({result.returncode}): {' '.join(args)}\n{result.stderr.strip()}")
    return result.stdout


def group_for(api_version: str) -> str:
    return "" if "/" not in api_version else api_version.split("/", 1)[0]


def render(path: str) -> list[dict]:
    text = run("kubectl", "kustomize", path, "--load-restrictor", "LoadRestrictionsNone")
    return [doc for doc in yaml.safe_load_all(text) if doc]


def main() -> int:
    bootstrap_docs = render("k8s/gitops/bootstrap")
    project = next((d for d in bootstrap_docs if d.get("kind") == "AppProject"), None)
    if not project or project.get("metadata", {}).get("name") != "his-hope":
        raise RuntimeError("His.Hope AppProject is missing from the bootstrap render.")

    project_spec = project.get("spec", {})
    namespaced = {
        (str(item.get("group", "")), str(item.get("kind")))
        for item in project_spec.get("namespaceResourceWhitelist", [])
    }
    cluster = {
        (str(item.get("group", "")), str(item.get("kind")))
        for item in project_spec.get("clusterResourceWhitelist", [])
    }
    if ("*", "*") in namespaced or ("*", "*") in cluster:
        raise RuntimeError("AppProject wildcard resource scope is forbidden.")

    applications = [d for d in bootstrap_docs if d.get("kind") == "Application"]
    if not applications:
        raise RuntimeError("No Argo Applications were rendered.")

    missing: set[tuple[str, str, str]] = set()
    seen: set[tuple[str, str, str]] = set()
    for application in applications:
        name = str(application.get("metadata", {}).get("name", "unknown"))
        source_path = application.get("spec", {}).get("source", {}).get("path")
        if not source_path:
            raise RuntimeError(f"Application {name} has no local source path.")
        for document in render(str(source_path)):
            kind = document.get("kind")
            if not kind:
                continue
            group = group_for(str(document.get("apiVersion", "v1")))
            key = (group, str(kind))
            seen.add((name, group, str(kind)))
            allowed = key in cluster if key in CLUSTER_SCOPED else key in namespaced
            if not allowed:
                missing.add((name, group, str(kind)))

    if missing:
        for application, group, kind in sorted(missing):
            print(f"MISSING_SCOPE|{application}|{group or 'core'}|{kind}")
        print(f"ARGocd_PROJECT_SCOPE_FAIL missing={len(missing)} resources={len(seen)}")
        return 1

    print(f"Argo Project scope PASS: applications={len(applications)} resourceKinds={len(seen)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # pragma: no cover - CLI error boundary
        print(f"Argo Project scope ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
