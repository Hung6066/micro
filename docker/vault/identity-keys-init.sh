#!/bin/sh
# Materializes identity JWT key files from Vault KV for the identity service.
# Read-only: the PEM set must already exist at KV mount/KEY_PATH (see docs/security/openship-micro-identity-files.hcl).
# Vault is reached over mTLS with an AppRole login; the token is never placed in argv.
set -eu

CERTS=/etc/hishop/certs
KEYS="jwt-private-key.pem jwt-public-key.pem jwt-encryption-private-key.pem"
KV="${VAULT_KV_MOUNT:-openship-runtime}"
KEY_PATH="${VAULT_KEY_PATH:-openship/projects/micro/development/identity-keys}"
APPROLE="${VAULT_APPROLE_MOUNT:-approle}"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
umask 077
: > "$WORK/empty"

die() { echo "identity-keys-init: $*" >&2; exit 1; }

: "${VAULT_ADDR:?VAULT_ADDR is required}" "${VAULT_CACERT:?}" "${VAULT_CLIENT_CERT:?}" "${VAULT_CLIENT_KEY:?}"
: "${VAULT_ROLE_ID_FILE:?}" "${VAULT_SECRET_ID_FILE:?}"

apk add --no-cache curl jq >/dev/null

# Sends one Vault request. Response body goes to stdout; HTTP status goes to $STATUS.
# Headers are read from a file so the token never appears in the process list.
vault_api() {
  method=$1; path=$2; body=$3
  STATUS=$(curl -sS -o "$WORK/resp" -w '%{http_code}' -X "$method" \
    --cacert "$VAULT_CACERT" --cert "$VAULT_CLIENT_CERT" --key "$VAULT_CLIENT_KEY" \
    -H @"$WORK/headers" --data-binary @"$body" "$VAULT_ADDR$path")
  cat "$WORK/resp"
}

printf 'Content-Type: application/json\n' > "$WORK/headers"
jq -n --rawfile role "$VAULT_ROLE_ID_FILE" --rawfile secret "$VAULT_SECRET_ID_FILE" \
  '{role_id: ($role | gsub("\\s+$"; "")), secret_id: ($secret | gsub("\\s+$"; ""))}' > "$WORK/login.json"
vault_api POST "/v1/auth/$APPROLE/login" "$WORK/login.json" > "$WORK/login.resp"
[ "$STATUS" = 200 ] || die "AppRole login failed (HTTP $STATUS)"
TOKEN=$(jq -r '.auth.client_token // empty' "$WORK/login.resp")
[ -n "$TOKEN" ] || die "AppRole login returned no token"
printf 'X-Vault-Token: %s\n' "$TOKEN" >> "$WORK/headers"

vault_api GET "/v1/$KV/data/$KEY_PATH" "$WORK/empty" > "$WORK/kv.json"
[ "$STATUS" = 200 ] || die "KV read of $KV/$KEY_PATH failed (HTTP $STATUS)"
for f in $KEYS; do
  jq -er --arg k "$f" '.data.data[$k]' "$WORK/kv.json" > "$WORK/$f" || die "KV entry $f is missing"
done

for f in $KEYS; do
  install -m 600 -o 1654 -g 1654 "$WORK/$f" "$CERTS/$f"
done

vault_api POST "/v1/auth/token/revoke-self" "$WORK/empty" > /dev/null || true
echo "identity-keys-init: JWT key files ready from Vault $KV/$KEY_PATH"