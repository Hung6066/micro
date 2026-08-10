#!/bin/sh
set -eu

test -s /run/secrets/vault_test_token
export VAULT_TOKEN="$(tr -d '\r\n' < /run/secrets/vault_test_token)"
export VAULT_CACERT=/run/secrets/vault_ca.pem

first="$(vault read -format=json database/creds/patient-service-db)"
lease="$(printf '%s' "$first" | sed -n 's/.*"lease_id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
username="$(printf '%s' "$first" | sed -n 's/.*"username"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
password="$(printf '%s' "$first" | sed -n 's/.*"password"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
test -n "$lease" -a -n "$username" -a -n "$password"

PGPASSWORD="$password" psql "host=postgres port=5432 dbname=patientdb user=$username sslmode=verify-full sslrootcert=/run/secrets/postgres_ca.pem" -c 'select 1' >/dev/null
vault lease revoke "$lease" >/dev/null

second="$(vault read -format=json database/creds/patient-service-db)"
second_user="$(printf '%s' "$second" | sed -n 's/.*"username"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
test -n "$second_user" -a "$second_user" != "$username"
echo "Vault lease rotation and revoke -> PostgreSQL reconnect: PASS"
