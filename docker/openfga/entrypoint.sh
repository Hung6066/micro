#!/bin/sh
set -eu
umask 077
: "${OPENFGA_DATASTORE_URI:?Managed PostgreSQL binding is required}"
: "${OPENSHIP_DATABASE_CA_PEM:?Managed PostgreSQL CA is required}"
: "${OPENFGA_AUTHN_PRESHARED_KEYS:?OpenFGA API authentication is required}"
case "$OPENFGA_DATASTORE_URI" in
  postgres://*sslmode=verify-full*|postgresql://*sslmode=verify-full*) ;;
  *) echo "OpenFGA requires a PostgreSQL URI with sslmode=verify-full" >&2; exit 1 ;;
esac
printf '%s\n' "$OPENSHIP_DATABASE_CA_PEM" > /tmp/openfga-postgres-ca.pem
export OPENFGA_DATASTORE_URI="${OPENFGA_DATASTORE_URI}&sslrootcert=/tmp/openfga-postgres-ca.pem"
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
