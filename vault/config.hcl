# Vault Server Configuration - His.Hope EMR
# Raft-based HA cluster with Shamir seal and Prometheus telemetry

ui = true

api_addr = "https://vault-active.his-hope.svc.cluster.local:8200"
cluster_addr = "https://vault-active.his-hope.svc.cluster.local:8201"

storage "raft" {
  path = "/vault/data"
  node_id = "vault-node-1"

  retry_join {
    leader_api_addr = "https://vault-0.vault-internal:8200"
  }
  retry_join {
    leader_api_addr = "https://vault-1.vault-internal:8200"
  }
  retry_join {
    leader_api_addr = "https://vault-2.vault-internal:8200"
  }

  max_entry_size = 1048576
  autopilot {
    cleanup_dead_servers = true
    last_contact_threshold = "200ms"
    max_trailing_logs = 100000
    server_stabilization_time = "10s"
    dead_server_last_contact_threshold = "24h"
    min_quorum = 3
  }
}

listener "tcp" {
  address       = "0.0.0.0:8200"
  cluster_address = "0.0.0.0:8201"
  tls_disable   = false
  tls_cert_file = "/vault/tls/server.crt"
  tls_key_file  = "/vault/tls/server.key"
  tls_client_ca_file = "/vault/tls/ca.crt"

  telemetry {
    unauthenticated_metrics_access = false
  }
}

seal "shamir" {
  secret_shreshold = 3
  secret_shares    = 5
}

telemetry {
  prometheus_retention_time = "24h"
  disable_hostname = false
}

service_registration "kubernetes" {}

raw_state {
  enable = false
}

disable_mlock = true

log_level = "info"

audit {
  type = "syslog"
  facility = "AUTH"
  tag = "vault"
  log_raw = false
  hmackey = true
}
