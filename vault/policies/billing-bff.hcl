# Billing BFF - Vault Policy
# Grants read access to billing-bff secrets and Redis session credentials
# SECURITY: Billing BFF handles session tokens - minimal privileges only

path "secret/data/his-hope/billing-bff/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
