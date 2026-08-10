#!/bin/bash
set -e
set -u

function create_database() {
    local database=$1
    echo "  Creating database '$database'"
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
        CREATE DATABASE "$database";
        GRANT ALL PRIVILEGES ON DATABASE "$database" TO "$POSTGRES_USER";
EOSQL
}

if [ -n "$POSTGRES_MULTIPLE_DATABASES" ]; then
    echo "Creating multiple databases: $POSTGRES_MULTIPLE_DATABASES"
    IFS=',' read -ra DB_ARRAY <<< "$POSTGRES_MULTIPLE_DATABASES"
    for db in "${DB_ARRAY[@]}"; do
        create_database "$db"
    done
    echo "Multiple databases created"
fi

# Keep query telemetry available in every service database. This is idempotent
# and is also safe to run from a one-off migration/operations job on existing volumes.
IFS=',' read -ra DB_ARRAY <<< "${POSTGRES_MULTIPLE_DATABASES:-}"
for db in "${DB_ARRAY[@]}"; do
    if [ -n "$db" ]; then
        psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" \
            -c 'CREATE EXTENSION IF NOT EXISTS pg_stat_statements;'
    fi
done
