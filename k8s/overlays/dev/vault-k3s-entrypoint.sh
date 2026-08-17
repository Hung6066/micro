#!/bin/sh
set -eu

test -n "${POD_NAME:?POD_NAME is required}"
test -s /run/tls/tls.crt
test -s /run/tls/tls.key
test -s /run/tls/ca.crt
test -n "${AZURE_TENANT_ID:?AZURE_TENANT_ID is required}"
test -n "${AZURE_CLIENT_ID:?AZURE_CLIENT_ID is required}"
test -n "${AZURE_CLIENT_SECRET:?AZURE_CLIENT_SECRET is required}"
test -n "${VAULT_AZUREKEYVAULT_VAULT_NAME:?VAULT_AZUREKEYVAULT_VAULT_NAME is required}"
test -n "${VAULT_AZUREKEYVAULT_KEY_NAME:?VAULT_AZUREKEYVAULT_KEY_NAME is required}"

cat > /tmp/vault.hcl <<EOF
ui = true
disable_mlock = true

storage "raft" {
  path = "/vault/data"
  node_id = "${POD_NAME}"
  retry_join {
    leader_api_addr = "https://vault-0.vault-internal.his-hope-dev.svc.cluster.local:8200"
    leader_ca_cert_file = "/run/tls/ca.crt"
  }
  retry_join {
    leader_api_addr = "https://vault-1.vault-internal.his-hope-dev.svc.cluster.local:8200"
    leader_ca_cert_file = "/run/tls/ca.crt"
  }
  retry_join {
    leader_api_addr = "https://vault-2.vault-internal.his-hope-dev.svc.cluster.local:8200"
    leader_ca_cert_file = "/run/tls/ca.crt"
  }
}

listener "tcp" {
  address = "0.0.0.0:8200"
  cluster_address = "0.0.0.0:8201"
  tls_disable = false
  tls_cert_file = "/run/tls/tls.crt"
  tls_key_file = "/run/tls/tls.key"
  tls_client_ca_file = "/run/tls/ca.crt"
  tls_min_version = "tls13"
}

api_addr = "https://${POD_NAME}.vault-internal.his-hope-dev.svc.cluster.local:8200"
cluster_addr = "https://${POD_NAME}.vault-internal.his-hope-dev.svc.cluster.local:8201"

seal "azurekeyvault" {
  tenant_id = "${AZURE_TENANT_ID}"
  client_id = "${AZURE_CLIENT_ID}"
  client_secret = "${AZURE_CLIENT_SECRET}"
  vault_name = "${VAULT_AZUREKEYVAULT_VAULT_NAME}"
  key_name = "${VAULT_AZUREKEYVAULT_KEY_NAME}"
}
EOF

exec vault server -config=/tmp/vault.hcl
