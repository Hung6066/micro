#!/usr/bin/env bash
set -Eeuo pipefail

: "${AZURE_STORAGE_ENDPOINT:?Set AZURE_STORAGE_ENDPOINT}"
: "${AZURE_STORAGE_CONTAINER:?Set AZURE_STORAGE_CONTAINER}"
: "${AZURE_STORAGE_SAS_TOKEN:?Set AZURE_STORAGE_SAS_TOKEN}"
: "${AZURE_BACKUP_PREFIX:?Set AZURE_BACKUP_PREFIX}"

command -v k3s >/dev/null || { echo 'k3s is required' >&2; exit 1; }
command -v azcopy >/dev/null || { echo 'azcopy is required' >&2; exit 1; }
[[ "$AZURE_STORAGE_ENDPOINT" =~ ^https://[a-z0-9-]+\.blob\.core\.windows\.net/?$ ]] || { echo 'Invalid Azure endpoint' >&2; exit 1; }
[[ "$AZURE_STORAGE_SAS_TOKEN" != *REPLACE_ME* && "$AZURE_STORAGE_SAS_TOKEN" != *'<'* ]] || { echo 'Placeholder SAS refused' >&2; exit 1; }

host_name="$(hostname -s)"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
work_dir="${K3S_BACKUP_DIR:-/var/lib/rancher/k3s/server/db/snapshots}"
mkdir -p "$work_dir"

k3s etcd-snapshot save --name "etcd-snapshot-${host_name}-${stamp}"
# K3s 1.35 writes embedded-etcd snapshots without a .db suffix. Select the
# newest regular snapshot by the K3s naming convention, independent of suffix.
latest="$(find "$work_dir" -maxdepth 1 -type f -name 'etcd-snapshot-*' -printf '%T@ %p\n' | sort -nr | head -n1 | cut -d' ' -f2-)"
[[ -n "$latest" && -f "$latest" ]] || { echo 'No etcd snapshot produced' >&2; exit 1; }

sas="${AZURE_STORAGE_SAS_TOKEN#\?}"
[[ "${#sas}" -ge 20 ]] || { echo 'Azure SAS token is too short' >&2; exit 1; }
blob_base="${AZURE_STORAGE_ENDPOINT%/}/${AZURE_STORAGE_CONTAINER}/${AZURE_BACKUP_PREFIX%/}/k3s/${host_name}/$(basename "$latest")"
destination="${blob_base}?${sas}"
checksum="${latest}.sha256"
sha256sum "$latest" > "$checksum"
azcopy copy "$latest" "$destination" --overwrite=false >/dev/null
azcopy copy "$checksum" "${blob_base}.sha256?${sas}" --overwrite=false >/dev/null
echo "K3s etcd snapshot upload PASS: host=${host_name} file=$(basename "$latest")"
