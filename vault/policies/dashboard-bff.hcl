# Dashboard BFF - Vault Policy
# Grants read access to dashboard-bff secrets and Redis session credentials
# SECURITY: Dashboard BFF handles session tokens - minimal privileges only

path "secret/data/his-hope/dashboard-bff/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
