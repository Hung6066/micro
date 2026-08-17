#!/bin/sh
set -eu

until pg_isready -h postgres -U postgres >/dev/null 2>&1; do sleep 2; done

read_secret() {
  test -s "$1"
  tr -d '\r\n' < "$1"
}

POSTGRES_ADMIN_PASSWORD="$(read_secret /run/secrets/postgres_admin_password)"
SPIRE_DB_PASSWORD="$(read_secret /run/secrets/spire_database_password)"

PGPASSWORD="$POSTGRES_ADMIN_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d postgres \
  --set=spire_password="$SPIRE_DB_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE spire_server LOGIN PASSWORD %L', :'spire_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'spire_server');
\gexec
SELECT format('ALTER ROLE spire_server WITH LOGIN PASSWORD %L', :'spire_password');
\gexec
SELECT format('CREATE DATABASE spiredb OWNER spire_server')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'spiredb');
\gexec
SQL

PGPASSWORD="$POSTGRES_ADMIN_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d spiredb \
  -c 'GRANT USAGE, CREATE ON SCHEMA public TO spire_server; ALTER SCHEMA public OWNER TO spire_server;' >/dev/null

echo "SPIRE PostgreSQL datastore is ready"
