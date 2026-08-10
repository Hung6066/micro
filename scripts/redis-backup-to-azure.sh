#!/usr/bin/env bash
set -Eeuo pipefail

: "${REDIS_HOST:?Set REDIS_HOST}"
: "${REDIS_PORT:?Set REDIS_PORT}"
: "${REDISCLI_AUTH:?Set REDISCLI_AUTH in the protected process environment}"
: "${AZURE_STORAGE_ENDPOINT:?Set AZURE_STORAGE_ENDPOINT}"
: "${AZURE_STORAGE_CONTAINER:?Set AZURE_STORAGE_CONTAINER}"
: "${AZURE_STORAGE_SAS_TOKEN:?Set AZURE_STORAGE_SAS_TOKEN}"
: "${AZURE_BACKUP_PREFIX:?Set AZURE_BACKUP_PREFIX}"

command -v redis-cli >/dev/null || { echo 'redis-cli is required' >&2; exit 1; }
command -v azcopy >/dev/null || { echo 'azcopy is required' >&2; exit 1; }
[[ "$REDISCLI_AUTH" != *REPLACE_ME* && "$REDISCLI_AUTH" != *'<'* ]] || { echo 'Placeholder Redis password refused' >&2; exit 1; }

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
work_dir="${BACKUP_WORK_DIR:-/var/backups/his-hope}"
mkdir -p "$work_dir"
dump="${work_dir}/redis-${stamp}.rdb"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" --tls ${REDIS_TLS_ARGS:-} --rdb "$dump" >/dev/null

sas="${AZURE_STORAGE_SAS_TOKEN#\?}"
destination="${AZURE_STORAGE_ENDPOINT%/}/${AZURE_STORAGE_CONTAINER}/${AZURE_BACKUP_PREFIX%/}/redis/$(basename "$dump")?${sas}"
azcopy copy "$dump" "$destination" --overwrite=false >/dev/null
sha256sum "$dump" | awk '{print "Redis RDB upload PASS: sha256=" $1}'
