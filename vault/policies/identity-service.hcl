# Identity Service - Vault Policy
# Grants JWT signing key access and identity database credentials

path "transit/keys/jwt-signing" {
  capabilities = ["read", "encrypt"]
}

path "secret/data/his-hope/identity-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/identitydb" {
  capabilities = ["read"]
}

path "auth/jwt/login" {
  capabilities = ["create"]
}
