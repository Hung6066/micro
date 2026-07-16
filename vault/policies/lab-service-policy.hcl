# Lab Service - Vault Policy
# Grants read access to lab order data secrets and infrastructure credentials
# SECURITY: Lab service handles test results which are PHI - audit logging required

path "secret/data/his-hope/lab-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/labdb" {
  capabilities = ["read"]
}

path "secret/data/his-hope/rabbitmq" {
  capabilities = ["read"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
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
