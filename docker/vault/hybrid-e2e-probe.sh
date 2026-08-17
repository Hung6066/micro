#!/bin/sh
set -eu

export VAULT_ADDR=http://vault:8200
JWT=$(cat /run/spire/jwt/vault.jwt)
VAULT_TOKEN=$(vault write -field=token auth/jwt-spiffe/login role=patient-service jwt="$JWT")
test -n "$VAULT_TOKEN"
export VAULT_TOKEN
mkdir -p /run/e2e
LEASE=$(vault read -format=json database/creds/patient-service-db)
PGUSER=$(printf '%s' "$LEASE" | sed -n 's/.*"username"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
PGPASSWORD=$(printf '%s' "$LEASE" | sed -n 's/.*"password"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
test -n "$PGUSER"
test -n "$PGPASSWORD"
{
  printf 'PGUSER=%s\n' "$PGUSER"
  printf 'PGPASSWORD=%s\n' "$PGPASSWORD"
} > /run/e2e/credentials.env
chmod 0600 /run/e2e/credentials.env
echo "SPIFFE JWT -> Vault JWT -> one leased PostgreSQL credential: PASS"
