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

if [ -n "${OPENSHIP_OIDC_CLIENT_ID:-}" ]; then
  : "${OPENSHIP_OIDC_ISSUER:=https://identity-api.nacoms.com.vn/}"
  : "${OPENSHIP_OIDC_SCOPES:=openid profile email roles hishop:permissions hishop:admin}"
  case "$OPENSHIP_OIDC_ISSUER" in https://*) ;; *) echo "OpenShip OIDC issuer must use HTTPS." >&2; exit 1 ;; esac
  for value in "$OPENSHIP_OIDC_ISSUER" "$OPENSHIP_OIDC_CLIENT_ID" "$OPENSHIP_OIDC_SCOPES"; do
    if ! printf '%s' "$value" | grep -Eq '^[A-Za-z0-9._:/ -]*$'; then
      echo "OpenShip OIDC runtime config contains unsupported characters." >&2
      exit 1
    fi
  done
  export OPENSHIP_OIDC_ISSUER OPENSHIP_OIDC_CLIENT_ID OPENSHIP_OIDC_SCOPES
  envsubst '${OPENSHIP_OIDC_ISSUER} ${OPENSHIP_OIDC_CLIENT_ID} ${OPENSHIP_OIDC_SCOPES}' \
    < /etc/nginx/templates/runtime-config.openship.js.template \
    > /tmp/runtime-config.openship.js
else
  printf '%s\n' 'window.__HISHOPE_CONFIG__ = window.__HISHOPE_CONFIG__ || {};' \
    > /tmp/runtime-config.openship.js
fi

envsubst '${NGINX_DNS_RESOLVER} ${OPENSHIP_API_GATEWAY_HOST}' \
  < /etc/nginx/templates/admin.conf.template \
  > /tmp/default.conf

exec nginx -g 'daemon off;'
