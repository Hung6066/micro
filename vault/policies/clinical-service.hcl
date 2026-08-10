# Clinical Service - Vault Policy
# Grants PHI data access with mandatory audit logging

path "secret/data/his-hope/clinical-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/clinicaldb" {
  capabilities = ["read"]
}

path "audit/hash" {
  capabilities = ["create", "update"]
}
