#!/bin/sh
set -eu
test -s /run/secrets/spire_database_password
export SPIRE_DB_PASSWORD="$(tr -d '\r\n' < /run/secrets/spire_database_password)"
exec /opt/spire/bin/spire-server run -config /run/spire/config/server-prod.conf -expandEnv
