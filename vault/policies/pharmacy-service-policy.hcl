# Pharmacy Service - Vault Policy
# Grants read access to medication and prescription data secrets
# SECURITY: Pharmacy service handles medication/prescription PHI - audit logging required

path "secret/data/his-hope/pharmacy-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/pharmacydb" {
  capabilities = ["read"]
}

path "secret/data/his-hope/rabbitmq" {
  capabilities = ["read"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "secret/data/his-hope/eventbus/pharmacy" {
  capabilities = ["read"]
}

path "secret/data/his-hope/pharmacy/medications/*" {
  capabilities = ["read", "list"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}

path "audit/hash" {
  capabilities = ["create", "update"]
}

path "transit/keys/jwt-public" {
  capabilities = ["read"]
}
