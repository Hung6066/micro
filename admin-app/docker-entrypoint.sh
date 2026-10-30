#!/bin/sh
set -eu

resolver="$(awk '$1 == "nameserver" { print $2; exit }' /etc/resolv.conf)"
: "${NGINX_DNS_RESOLVER:=${resolver}}"
if [ -z "${OPENSHIP_API_GATEWAY_HOST:-}" ]; then
  if [ -n "${KUBERNETES_SERVICE_HOST:-}" ]; then
    OPENSHIP_API_GATEWAY_HOST="micro-api-gateway"
  else
    OPENSHIP_API_GATEWAY_HOST="api-gateway"
  fi
fi
export NGINX_DNS_RESOLVER OPENSHIP_API_GATEWAY_HOST

envsubst '${NGINX_DNS_RESOLVER} ${OPENSHIP_API_GATEWAY_HOST}' \
  < /etc/nginx/templates/admin.conf.template \
  > /tmp/default.conf

exec nginx -g 'daemon off;'
