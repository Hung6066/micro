# Read-Only Monitoring Policy - Prometheus/Grafana Metrics Scraping
# Grants the absolute minimum permissions needed for metrics collection.
# SECURITY: READ-only on vault health/metrics endpoints, no access to secret data.

path "sys/health" {
  capabilities = ["read", "list"]
}

path "sys/seal-status" {
  capabilities = ["read"]
}

path "sys/ha-status" {
  capabilities = ["read", "list"]
}

path "sys/leader" {
  capabilities = ["read"]
}

path "sys/metrics" {
  capabilities = ["read", "list"]
}

path "sys/auth" {
  capabilities = ["list", "read"]
}

path "sys/audit" {
  capabilities = ["read", "list"]
}

path "sys/policies/acl" {
  capabilities = ["list"]
}

path "sys/policy" {
  capabilities = ["list"]
}

path "auth/token/lookup-self" {
  capabilities = ["read"]
}

path "auth/token/renew-self" {
  capabilities = ["update"]
}

path "sys/capabilities-self" {
  capabilities = ["update"]
}

path "secret/*" {
  capabilities = ["deny"]
}

path "transit/*" {
  capabilities = ["deny"]
}

path "pki/*" {
  capabilities = ["deny"]
}

path "identity/*" {
  capabilities = ["deny"]
}
