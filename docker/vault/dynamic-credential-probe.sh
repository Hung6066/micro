#!/bin/sh
set -eu

until [ -s /run/e2e/credentials.env ]; do sleep 1; done
. /run/e2e/credentials.env
PGPASSWORD="$PGPASSWORD" psql -v ON_ERROR_STOP=1 -h postgres -U "$PGUSER" -d postgres \
  -c 'select current_user, 1 as vault_dynamic_credential_ok;'
echo "Vault dynamic credential -> PostgreSQL connection: PASS"
