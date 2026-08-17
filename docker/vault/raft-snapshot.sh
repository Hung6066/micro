#!/bin/sh
set -eu

test -s /run/secrets/vault_snapshot_token
export VAULT_TOKEN="$(tr -d '\r\n' < /run/secrets/vault_snapshot_token)"
test -n "${VAULT_CACERT:-}"
mkdir -p /vault/backups
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
vault operator raft snapshot save "/vault/backups/vault-${stamp}.snap"
find /vault/backups -type f -name 'vault-*.snap' -mtime +7 -delete
echo "Vault Raft snapshot created: ${stamp}"
