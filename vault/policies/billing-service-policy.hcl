# Billing Service - Vault Policy
# Grants read access to billing data secrets and infrastructure credentials
# SECURITY: Billing service handles payment/insurance PHI - audit logging required

path "secret/data/his-hope/billing-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/billingdb" {
  capabilities = ["read"]
}

path "secret/data/his-hope/rabbitmq" {
  capabilities = ["read"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "secret/data/his-hope/eventbus/billing" {
  capabilities = ["read"]
}

path "secret/data/his-hope/payments/*" {
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
