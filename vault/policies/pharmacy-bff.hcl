# Pharmacy BFF - Vault Policy
# Grants read access to pharmacy-bff secrets and Redis session credentials
# SECURITY: Pharmacy BFF handles session tokens - minimal privileges only

path "secret/data/his-hope/pharmacy-bff/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
