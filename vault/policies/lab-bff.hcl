# Lab BFF - Vault Policy
# Grants read access to lab-bff secrets and Redis session credentials
# SECURITY: Lab BFF handles session tokens - minimal privileges only

path "secret/data/his-hope/lab-bff/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
