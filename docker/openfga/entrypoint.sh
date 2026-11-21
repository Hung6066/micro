#!/bin/sh
set -eu
umask 077
: "${OPENFGA_DATASTORE_URI:?Managed PostgreSQL binding is required}"
: "${OPENSHIP_DATABASE_CA_PEM:?Managed PostgreSQL CA is required}"
: "${OPENFGA_AUTHN_PRESHARED_KEYS:?OpenFGA API authentication is required}"
: "${OPENFGA_POSTGRES_CLIENT_CERT_FILE:=/run/secrets/openfga-postgres/tls.crt}"
: "${OPENFGA_POSTGRES_CLIENT_KEY_FILE:=/run/secrets/openfga-postgres/tls.key}"
case "$OPENFGA_DATASTORE_URI" in
  postgres://*sslmode=verify-full*|postgresql://*sslmode=verify-full*) ;;
  *) echo "OpenFGA requires a PostgreSQL URI with sslmode=verify-full" >&2; exit 1 ;;
esac
test -s "$OPENFGA_POSTGRES_CLIENT_CERT_FILE" && test -s "$OPENFGA_POSTGRES_CLIENT_KEY_FILE" || {
  echo "OpenFGA requires its dedicated PostgreSQL client certificate and key" >&2
  exit 1
}
printf '%s\n' "$OPENSHIP_DATABASE_CA_PEM" > /tmp/openfga-postgres-ca.pem
# libpq rejects a world-readable projected key; keep an app-owned 0600 copy.
cat "$OPENFGA_POSTGRES_CLIENT_CERT_FILE" > /tmp/openfga-postgres-client.crt
cat "$OPENFGA_POSTGRES_CLIENT_KEY_FILE" > /tmp/openfga-postgres-client.key
chmod 0600 /tmp/openfga-postgres-client.key
export OPENFGA_DATASTORE_URI="${OPENFGA_DATASTORE_URI}&sslrootcert=/tmp/openfga-postgres-ca.pem&sslcert=/tmp/openfga-postgres-client.crt&sslkey=/tmp/openfga-postgres-client.key"
export OPENFGA_DATASTORE_ENGINE=postgres
export OPENFGA_AUTHN_METHOD=preshared
export OPENFGA_PLAYGROUND_ENABLED=false
# Hold a database-session lock while the migration subprocess runs on HA starts.
psql --dbname="$OPENFGA_DATASTORE_URI" --no-psqlrc --set=ON_ERROR_STOP=1 <<'SQL'
SELECT pg_advisory_lock(198083011, 2);
\! openfga migrate
\if :SHELL_ERROR
DO $$ BEGIN RAISE EXCEPTION 'OpenFGA migration subprocess failed'; END $$;
\endif
SQL
exec openfga run
