#!/usr/bin/env bash
set -Eeuo pipefail

: "${AZURE_STORAGE_ENDPOINT:?Set AZURE_STORAGE_ENDPOINT}"
: "${AZURE_STORAGE_CONTAINER:?Set AZURE_STORAGE_CONTAINER}"
: "${AZURE_STORAGE_SAS_TOKEN:?Set AZURE_STORAGE_SAS_TOKEN}"
: "${AZURE_BACKUP_PREFIX:?Set AZURE_BACKUP_PREFIX}"
: "${KUBE_CONTEXT:?Set KUBE_CONTEXT explicitly}"

command -v kubectl >/dev/null || { echo 'kubectl is required' >&2; exit 1; }
command -v azcopy >/dev/null || { echo 'azcopy is required' >&2; exit 1; }
kubectl --context "$KUBE_CONTEXT" get --raw=/readyz >/dev/null

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
work_dir="${BACKUP_WORK_DIR:-/var/backups/his-hope}"
mkdir -p "$work_dir"
archive="${work_dir}/kubernetes-config-${stamp}.yaml"

{
  echo '# Generated without Secret resources. Secret material must remain in Vault.'
  for resource in namespaces configmaps deployments statefulsets daemonsets services ingresses networkpolicies serviceaccounts roles rolebindings clusterroles clusterrolebindings pvc; do
    kubectl --context "$KUBE_CONTEXT" get "$resource" --all-namespaces -o yaml 2>/dev/null || true
  done
} > "$archive"

sas="${AZURE_STORAGE_SAS_TOKEN#\?}"
destination="${AZURE_STORAGE_ENDPOINT%/}/${AZURE_STORAGE_CONTAINER}/${AZURE_BACKUP_PREFIX%/}/kubernetes/$(basename "$archive")?${sas}"
azcopy copy "$archive" "$destination" --overwrite=false >/dev/null
sha256sum "$archive" | awk '{print "Kubernetes config upload PASS: sha256=" $1}'
