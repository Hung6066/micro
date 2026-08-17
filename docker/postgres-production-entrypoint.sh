#!/bin/sh
set -eu

mkdir -p /var/lib/postgresql/tls
for name in postgres_ca.pem postgres_cert.pem postgres_key.pem; do
  test -s "/run/secrets/$name"
  cp "/run/secrets/$name" "/var/lib/postgresql/tls/$name"
done
chown -R postgres:postgres /var/lib/postgresql/tls
chmod 0444 /var/lib/postgresql/tls/postgres_ca.pem /var/lib/postgresql/tls/postgres_cert.pem
chmod 0400 /var/lib/postgresql/tls/postgres_key.pem
exec /usr/local/bin/docker-entrypoint.sh "$@"
