"""Report rendered workload containers that are not ready for Pod Security restricted."""

from pathlib import Path
import sys

import yaml


def pod_template(document):
    kind = document.get("kind")
    spec = document.get("spec", {})
    if kind == "CronJob":
        return spec.get("jobTemplate", {}).get("spec", {}).get("template", {})
    return spec.get("template", {})


def workload_documents(document):
    """Yield workload documents from a single manifest or kubectl List output."""
    if document and document.get("kind", "").endswith("List"):
        yield from document.get("items", [])
    elif document:
        yield document


def main() -> int:
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/k8s/prod.yaml")
    missing = []
    for root_document in yaml.safe_load_all(path.read_text(encoding="utf-8")):
        for document in workload_documents(root_document):
            if document.get("kind") not in {"Deployment", "StatefulSet", "DaemonSet", "Job", "CronJob"}:
                continue
            template = pod_template(document)
            pod_spec = template.get("spec", {})
            pod_security = pod_spec.get("securityContext", {})
            containers = pod_spec.get("containers", []) + pod_spec.get("initContainers", [])
            for container in containers:
                security = container.get("securityContext", {})
                resources = container.get("resources", {})
                seccomp = security.get("seccompProfile", {}).get("type") or pod_security.get("seccompProfile", {}).get("type")
                issues = []
                if security.get("runAsNonRoot") is not True and pod_security.get("runAsNonRoot") is not True:
                    issues.append("runAsNonRoot")
                if security.get("allowPrivilegeEscalation") is not False:
                    issues.append("allowPrivilegeEscalation")
                if seccomp != "RuntimeDefault":
                    issues.append("seccompProfile")
                if not resources.get("requests", {}).get("cpu") or not resources.get("requests", {}).get("memory"):
                    issues.append("requests")
                if not resources.get("limits", {}).get("cpu") or not resources.get("limits", {}).get("memory"):
                    issues.append("limits")
                if issues:
                    missing.append((document["kind"], document.get("metadata", {}).get("name"), container.get("name"), ",".join(issues)))
    for kind, name, container, issues in missing:
        print(f"{kind}|{name}|{container}|{issues}")
    print(f"TOTAL_NONCOMPLIANT_CONTAINERS={len(missing)}")
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
