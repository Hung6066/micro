#!/bin/sh
set -eu

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
    < /etc/nginx/runtime-config.openship.js.template \
    > /tmp/runtime-config.openship.js
else
  printf '%s\n' 'window.__HISHOPE_CONFIG__ = window.__HISHOPE_CONFIG__ || {};' \
    > /tmp/runtime-config.openship.js
fi

exec nginx -g 'daemon off;'
