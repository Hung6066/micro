#!/bin/sh
set -eu

until pg_isready -h postgres -U postgres >/dev/null 2>&1; do sleep 2; done
read_secret() { test -s "$1"; tr -d '\r\n' < "$1"; }
ADMIN_PASSWORD="$(read_secret /run/secrets/postgres_admin_password)"
MIGRATOR_PASSWORD="$(read_secret /run/secrets/postgres_migrator_password)"

PGPASSWORD="$ADMIN_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d postgres \
  --set=migrator_password="$MIGRATOR_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE his_hope_migrator LOGIN PASSWORD %L', :'migrator_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'his_hope_migrator');
\gexec
SELECT format('ALTER ROLE his_hope_migrator WITH LOGIN PASSWORD %L', :'migrator_password');
\gexec
SQL

for database in identitydb patientdb appointmentdb clinicaldb labdb billingdb pharmacydb; do
  PGPASSWORD="$ADMIN_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d postgres \
    -v target_database="$database" <<'SQL' >/dev/null
SELECT format('CREATE DATABASE %I', :'target_database')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'target_database');
\gexec
SQL
  PGPASSWORD="$ADMIN_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d postgres \
    -v target_database="$database" <<'SQL' >/dev/null
SELECT format('ALTER DATABASE %I OWNER TO his_hope_migrator', :'target_database');
\gexec
SQL
  PGPASSWORD="$ADMIN_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d "$database" \
    -c 'GRANT USAGE, CREATE ON SCHEMA public TO his_hope_migrator; ALTER SCHEMA public OWNER TO his_hope_migrator; ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO his_hope_migrator; ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO his_hope_migrator;' >/dev/null
done
echo "PostgreSQL migration/deployer account is ready; runtime roles remain DML-only"
