ui = true
disable_mlock = true

storage "raft" {
  path    = "/vault/data"
  node_id = "$VAULT_RAFT_NODE_ID"
  retry_join {
    leader_api_addr = "https://vault-1:8200"
    leader_ca_cert_file = "/run/secrets/vault_ca.pem"
  }
  retry_join {
    leader_api_addr = "https://vault-2:8200"
    leader_ca_cert_file = "/run/secrets/vault_ca.pem"
  }
}

listener "tcp" {
  address         = "0.0.0.0:8200"
  cluster_address = "0.0.0.0:8201"
  tls_disable     = false
  tls_cert_file   = "/run/secrets/vault_cert.pem"
  tls_key_file    = "/run/secrets/vault_key.pem"
  tls_min_version = "tls13"
}

api_addr     = "https://$VAULT_RAFT_NODE_ID:8200"
cluster_addr = "https://$VAULT_RAFT_NODE_ID:8201"

$VAULT_SEAL_STANZA
