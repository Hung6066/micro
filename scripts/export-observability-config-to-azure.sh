#!/usr/bin/env bash
set -Eeuo pipefail

: "${KUBE_CONTEXT:?Set KUBE_CONTEXT explicitly}"
: "${AZURE_STORAGE_ENDPOINT:?Set AZURE_STORAGE_ENDPOINT}"
: "${AZURE_STORAGE_CONTAINER:?Set AZURE_STORAGE_CONTAINER}"
: "${AZURE_STORAGE_SAS_TOKEN:?Set AZURE_STORAGE_SAS_TOKEN}"
: "${AZURE_BACKUP_PREFIX:?Set AZURE_BACKUP_PREFIX}"

command -v kubectl >/dev/null || { echo 'kubectl is required' >&2; exit 1; }
command -v azcopy >/dev/null || { echo 'azcopy is required' >&2; exit 1; }
kubectl --context "$KUBE_CONTEXT" get --raw=/readyz >/dev/null

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
work_dir="${BACKUP_WORK_DIR:-/var/backups/his-hope}"
mkdir -p "$work_dir"
archive="${work_dir}/observability-config-${stamp}.yaml"

{
  echo '# Observability configuration export. Secret resources are intentionally excluded.'
  for namespace in monitoring linkerd-viz; do
    for resource in configmaps deployments statefulsets daemonsets services serviceaccounts roles rolebindings networkpolicies poddisruptionbudgets servicemonitors prometheusrules; do
      kubectl --context "$KUBE_CONTEXT" get "$resource" -n "$namespace" -o yaml 2>/dev/null || true
    done
  done
} > "$archive"

sas="${AZURE_STORAGE_SAS_TOKEN#\?}"
destination="${AZURE_STORAGE_ENDPOINT%/}/${AZURE_STORAGE_CONTAINER}/${AZURE_BACKUP_PREFIX%/}/observability/$(basename "$archive")?${sas}"
azcopy copy "$archive" "$destination" --overwrite=false >/dev/null
sha256sum "$archive" | awk '{print "Observability config upload PASS: sha256=" $1}'
