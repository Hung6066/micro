# Clinical BFF - Vault Policy
# Grants read access to clinical-bff secrets and Redis session credentials
# SECURITY: Clinical BFF handles session tokens - minimal privileges only

path "secret/data/his-hope/clinical-bff/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
