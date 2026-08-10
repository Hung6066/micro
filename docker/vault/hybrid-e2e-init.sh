#!/bin/sh
set -eu

until wget -qO- http://vault:8200/v1/sys/health >/dev/null 2>&1; do sleep 2; done
until [ -s /run/spire/bootstrap/join-token ]; do sleep 2; done

export VAULT_ADDR=http://vault:8200
export VAULT_TOKEN="${VAULT_DEV_ROOT_TOKEN_ID:-root}"

vault auth enable -path=jwt-spiffe jwt 2>/dev/null || true
vault delete auth/jwt-spiffe/config >/dev/null 2>&1 || true
vault write auth/jwt-spiffe/config \
  oidc_discovery_url=http://spire-oidc:8082

vault secrets enable -path=database database 2>/dev/null || true
while IFS='|' read -r service database; do
  connection="${service}-postgres"
  role="${service}-db"

  vault write "database/config/${connection}" \
    plugin_name=postgresql-database-plugin \
    allowed_roles="${role}" \
    connection_url="postgresql://{{username}}:{{password}}@postgres:5432/${database}?sslmode=disable" \
    username=vault_manager \
    password="${VAULT_DB_ADMIN_PASSWORD}" \
    password_authentication=password

  vault policy write "${role}" - <<POLICY
path "database/creds/${role}" {
  capabilities = ["read"]
}
POLICY

  vault write "auth/jwt-spiffe/role/${service}" \
    role_type=jwt \
    user_claim=sub \
    bound_audiences=vault \
    bound_subject="spiffe://his-hope.local/ns/his-hope/sa/${service}" \
    policies="${role}" \
    ttl=15m \
    max_ttl=1h

  vault write "database/roles/${role}" \
    db_name="${connection}" \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}'; GRANT CONNECT ON DATABASE ${database} TO \"{{name}}\"; GRANT USAGE ON SCHEMA public TO \"{{name}}\"; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO \"{{name}}\"; GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO \"{{name}}\";" \
    revocation_statements="DROP ROLE IF EXISTS \"{{name}}\";" \
    default_ttl=15m \
    max_ttl=1h
done <<'SERVICES'
identity-service|identitydb
patient-service|patientdb
appointment-service|appointmentdb
clinical-service|clinicaldb
lab-service|labdb
billing-service|billingdb
pharmacy-service|pharmacydb
SERVICES

# Remove the single-connection name used by the first Compose prototype so
# operators cannot accidentally bind a service to the wrong database.
vault delete database/config/his-hope-postgres >/dev/null 2>&1 || true

echo "Vault hybrid E2E bootstrap completed"
