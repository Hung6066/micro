#!/bin/sh
set -eu

read_secret() {
  test -s "$1"
  tr -d '\r\n' < "$1"
}

until wget -qO- "${VAULT_ADDR}/v1/sys/health?standbyok=true&sealedcode=200" >/dev/null 2>&1; do sleep 2; done
export VAULT_TOKEN="$(read_secret /run/secrets/vault_bootstrap_token)"

vault auth enable -path=jwt-spiffe jwt 2>/dev/null || true
vault write auth/jwt-spiffe/config \
  oidc_discovery_url="${SPIRE_OIDC_DISCOVERY_URL}" \
  oidc_discovery_ca_pem=@/run/secrets/spire_oidc_ca.pem
vault secrets enable -path=database database 2>/dev/null || true

while IFS='|' read -r service database; do
  connection="${service}-postgres"
  role="${service}-db"
  vault write "database/config/${connection}" \
    plugin_name=postgresql-database-plugin \
    allowed_roles="${role}" \
    connection_url="postgresql://{{username}}:{{password}}@postgres:5432/${database}?sslmode=verify-full&sslrootcert=/run/secrets/postgres_ca.pem" \
    username="${VAULT_DB_ADMIN_USER}" \
    password="$(read_secret /run/secrets/vault_db_admin_password)" \
    password_authentication=password
  vault policy write "${role}" - <<POLICY
path "database/creds/${role}" {
  capabilities = ["read"]
}
POLICY
  vault write "auth/jwt-spiffe/role/${service}" role_type=jwt user_claim=sub \
    bound_audiences=vault \
    bound_subject="spiffe://his-hope.local/ns/his-hope/sa/${service}" \
    policies="${role}" ttl=15m max_ttl=1h
  vault write "database/roles/${role}" \
    db_name="${connection}" \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}'; GRANT CONNECT ON DATABASE ${database} TO \"{{name}}\"; GRANT USAGE ON SCHEMA public TO \"{{name}}\"; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO \"{{name}}\"; GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO \"{{name}}\";" \
    revocation_statements="DROP ROLE IF EXISTS \"{{name}}\";" default_ttl=15m max_ttl=1h
done <<'SERVICES'
identity-service|identitydb
patient-service|patientdb
appointment-service|appointmentdb
clinical-service|clinicaldb
lab-service|labdb
billing-service|billingdb
pharmacy-service|pharmacydb
SERVICES

echo "Vault production bootstrap completed; revoke the one-shot token now"
