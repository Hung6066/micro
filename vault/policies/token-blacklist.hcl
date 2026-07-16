# Token Blacklist Service - Vault Policy
# Grants access to Redis credentials for the token blacklist service
# and transit key for audit hashing.

# Read Redis connection credentials for the token blacklist Redis keyspace
path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

# Read Redis credentials specific to the token blacklist keyspace
path "secret/data/his-hope/redis/token-blacklist" {
  capabilities = ["read", "list"]
}

# Audit hash capability for logging token revocation events
path "audit/hash" {
  capabilities = ["create", "update"]
}

# Read the CA certificate for mTLS connections
path "pki/cert/ca" {
  capabilities = ["read"]
}

# Read service-specific configuration
path "secret/data/his-hope/token-blacklist/*" {
  capabilities = ["read", "list"]
}

# Read JWT public key for token validation (if needed)
path "transit/keys/jwt-public" {
  capabilities = ["read"]
}
