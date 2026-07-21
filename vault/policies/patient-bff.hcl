# Patient BFF - Vault Policy
# Grants read access to patient-bff secrets and Redis session credentials
# SECURITY: Patient BFF handles session tokens - minimal privileges only

path "secret/data/his-hope/patient-bff/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
