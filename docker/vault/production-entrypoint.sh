#!/bin/sh
set -eu

test -n "${VAULT_RAFT_NODE_ID:-}"
test -s /run/secrets/vault_cert.pem
test -s /run/secrets/vault_key.pem
test -s /run/secrets/vault_ca.pem
mkdir -p /vault/data
chown -R vault:vault /vault/data

case "${VAULT_SEAL_TYPE:-}" in
  transit)
    test -s /run/secrets/vault_transit_token
    test -n "${VAULT_TRANSIT_SEAL_ADDR:?}"
    test -n "${VAULT_TRANSIT_KEY_NAME:?}"
    VAULT_SEAL_STANZA="seal \"transit\" {\n  address = \"${VAULT_TRANSIT_SEAL_ADDR}\"\n  token = \"$(tr -d '\r\n' < /run/secrets/vault_transit_token)\"\n  key_name = \"${VAULT_TRANSIT_KEY_NAME}\"\n  mount_path = \"${VAULT_TRANSIT_MOUNT_PATH:-transit/}\"\n  tls_ca_cert = \"/run/secrets/vault_ca.pem\"\n}"
    ;;
  azurekeyvault)
    test -n "${AZURE_TENANT_ID:?}"
    test -n "${VAULT_AZUREKEYVAULT_VAULT_NAME:?}"
    test -n "${VAULT_AZUREKEYVAULT_KEY_NAME:?}"
    AZURE_CLIENT_LINES=""
    if [ -n "${AZURE_CLIENT_SECRET_FILE:-}" ]; then
      test -s "$AZURE_CLIENT_SECRET_FILE"
      test -n "${AZURE_CLIENT_ID:?AZURE_CLIENT_ID is required with a local service principal}"
      AZURE_CLIENT_LINES="\n  client_id = \"${AZURE_CLIENT_ID}\"\n  client_secret = \"$(tr -d '\r\n' < "$AZURE_CLIENT_SECRET_FILE")\""
    fi
    VAULT_SEAL_STANZA="seal \"azurekeyvault\" {\n  tenant_id = \"${AZURE_TENANT_ID}\"\n  vault_name = \"${VAULT_AZUREKEYVAULT_VAULT_NAME}\"\n  key_name = \"${VAULT_AZUREKEYVAULT_KEY_NAME}\"${AZURE_CLIENT_LINES}\n}"
    ;;
  *)
    echo "VAULT_SEAL_TYPE must be transit or azurekeyvault; Shamir-only startup is refused" >&2
    exit 64
    ;;
esac
export VAULT_SEAL_STANZA
envsubst '$VAULT_RAFT_NODE_ID' < /run/vault/vault-raft.hcl > /tmp/rendered.hcl
sed -i '/^\$VAULT_SEAL_STANZA$/d' /tmp/rendered.hcl
printf '%b\n' "$VAULT_SEAL_STANZA" >> /tmp/rendered.hcl
chmod 0600 /tmp/rendered.hcl
chown vault:vault /tmp/rendered.hcl
exec su-exec vault vault server -config=/tmp/rendered.hcl
