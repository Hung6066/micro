#!/bin/sh
set -eu

until pg_isready -h postgres -U postgres >/dev/null 2>&1; do sleep 2; done

# Keep the secret out of the SQL text. psql's \gexec executes the generated
# ALTER/CREATE statement after the password has been safely quoted as a SQL
# literal by quote_literal().
PGPASSWORD="$POSTGRES_PASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U postgres -d postgres \
  --set=manager_password="$VAULT_DB_ADMIN_PASSWORD" <<'SQL'
SELECT CASE
  WHEN EXISTS (SELECT FROM pg_roles WHERE rolname = 'vault_manager')
  THEN format('ALTER ROLE vault_manager WITH LOGIN PASSWORD %L SUPERUSER', :'manager_password')
  ELSE format('CREATE ROLE vault_manager LOGIN PASSWORD %L SUPERUSER', :'manager_password')
END;
\gexec
SQL
echo "PostgreSQL Vault management account is ready"
