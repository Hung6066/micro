#!/usr/bin/env bash
set -Eeuo pipefail

: "${VAULT_ADDR:?Set VAULT_ADDR}"
: "${VAULT_TOKEN:?Set VAULT_TOKEN (read/snapshot-only token)}"
: "${AZURE_STORAGE_ENDPOINT:?Set AZURE_STORAGE_ENDPOINT}"
: "${AZURE_STORAGE_CONTAINER:?Set AZURE_STORAGE_CONTAINER}"
: "${AZURE_STORAGE_SAS_TOKEN:?Set AZURE_STORAGE_SAS_TOKEN}"
: "${AZURE_BACKUP_PREFIX:?Set AZURE_BACKUP_PREFIX}"

command -v vault >/dev/null || { echo 'vault CLI is required' >&2; exit 1; }
command -v azcopy >/dev/null || { echo 'azcopy is required' >&2; exit 1; }
[[ "$VAULT_TOKEN" != *REPLACE_ME* && "$VAULT_TOKEN" != *'<'* ]] || { echo 'Placeholder Vault token refused' >&2; exit 1; }

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
work_dir="${BACKUP_WORK_DIR:-/var/backups/his-hope}"
mkdir -p "$work_dir"
snapshot="${work_dir}/vault-raft-${stamp}.snap"
VAULT_TOKEN="$VAULT_TOKEN" vault operator raft snapshot save "$snapshot"

sas="${AZURE_STORAGE_SAS_TOKEN#\?}"
destination="${AZURE_STORAGE_ENDPOINT%/}/${AZURE_STORAGE_CONTAINER}/${AZURE_BACKUP_PREFIX%/}/vault/$(basename "$snapshot")?${sas}"
azcopy copy "$snapshot" "$destination" --overwrite=false >/dev/null
sha256sum "$snapshot" | awk '{print "Vault snapshot upload PASS: sha256=" $1}'
