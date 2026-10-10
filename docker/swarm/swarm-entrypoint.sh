#!/bin/sh
set -eu

read_secret() {
  if [ ! -r "$1" ]; then
    echo "Missing required Docker secret: $1" >&2
    exit 78
  fi
  tr -d '\r\n' < "$1"
}

db_user="$(read_secret /run/secrets/manufacturing_postgres_user)"
db_password="$(read_secret /run/secrets/manufacturing_postgres_password)"
db_host="${DB_HOST:?DB_HOST is required}"
db_port="${DB_PORT:-5432}"
redis_url="${REDIS_URL:?REDIS_URL is required}"

export ConnectionStrings__Redis="$redis_url"
export Redis__ConnectionString="$redis_url"
export ConnectionStrings__IdentityDb="Host=$db_host;Port=$db_port;Database=identitydb;Username=$db_user;Password=$db_password"
export ConnectionStrings__CommerceDb="Host=$db_host;Port=$db_port;Database=commercedb;Username=$db_user;Password=$db_password"
export ConnectionStrings__ContentDb="Host=$db_host;Port=$db_port;Database=contentdb;Username=$db_user;Password=$db_password"
export ConnectionStrings__ManufacturingDb="Host=$db_host;Port=$db_port;Database=manufacturingdb;Username=$db_user;Password=$db_password"

if [ -n "${RABBIT_HOST:-}" ]; then
  rabbit_user="$(read_secret /run/secrets/manufacturing_rabbitmq_user)"
  rabbit_password="$(read_secret /run/secrets/manufacturing_rabbitmq_password)"
  export EventBus__HostName="$RABBIT_HOST"
  export EventBus__Port="${RABBIT_PORT:-5672}"
  export EventBus__UserName="$rabbit_user"
  export EventBus__Password="$rabbit_password"
fi

if [ -r /run/secrets/commerce_data_protection_password ]; then
  export DataProtection__CertificatePassword="$(read_secret /run/secrets/commerce_data_protection_password)"
elif [ -r /run/secrets/content_data_protection_password ]; then
  export DataProtection__CertificatePassword="$(read_secret /run/secrets/content_data_protection_password)"
elif [ -r /run/secrets/manufacturing_data_protection_password ]; then
  export DataProtection__CertificatePassword="$(read_secret /run/secrets/manufacturing_data_protection_password)"
fi

exec dotnet "$@"
