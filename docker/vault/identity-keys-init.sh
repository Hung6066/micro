#!/bin/sh
# Materializes identity JWT key files from Vault for the identity service.
# Vault is reached over mTLS with an AppRole login. Transit keys are ensured,
# and the PEM material lives in KV so it survives container recreation.
set -eu

CERTS=/etc/hishop/certs
KEYS="jwt-private-key.pem jwt-public-key.pem jwt-encryption-private-key.pem"
KV="${VAULT_KV_MOUNT:-secret}"
KEY_PATH="${VAULT_KEY_PATH:-micro/identity}"
TRANSIT="${VAULT_TRANSIT_MOUNT:-transit}"
APPROLE="${VAULT_APPROLE_MOUNT:-approle}"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
umask 077
: > "$WORK/empty"

die() { echo "identity-keys-init: $*" >&2; exit 1; }

: "${VAULT_ADDR:?VAULT_ADDR is required}" "${VAULT_CACERT:?}" "${VAULT_CLIENT_CERT:?}" "${VAULT_CLIENT_KEY:?}"
: "${VAULT_ROLE_ID_FILE:?}" "${VAULT_SECRET_ID_FILE:?}"

apk add --no-cache curl jq openssl >/dev/null

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

ensure_transit_key() {
  name=$1; type=$2
  vault_api GET "/v1/$TRANSIT/keys/$name" "$WORK/empty" > /dev/null
  case $STATUS in
    200) return 0 ;;
    404) ;;
    *) die "transit key $name read failed (HTTP $STATUS)" ;;
  esac
  printf '{"type":"%s"}' "$type" > "$WORK/key.json"
  vault_api POST "/v1/$TRANSIT/keys/$name" "$WORK/key.json" > /dev/null
  [ "$STATUS" = 204 ] || [ "$STATUS" = 200 ] || die "transit key $name create failed (HTTP $STATUS)"
}
ensure_transit_key jwt-signing rsa-2048
ensure_transit_key mfa-secret aes256-gcm96

vault_api GET "/v1/$KV/data/$KEY_PATH" "$WORK/empty" > "$WORK/kv.json"
case $STATUS in
  200)
    for f in $KEYS; do
      jq -er --arg k "$f" '.data.data[$k]' "$WORK/kv.json" > "$WORK/$f" || die "KV entry $f is missing"
    done
    ;;
  404)
    openssl genrsa -out "$WORK/jwt-private-key.pem" 2048 2>/dev/null
    openssl rsa -in "$WORK/jwt-private-key.pem" -pubout -out "$WORK/jwt-public-key.pem" 2>/dev/null
    openssl genrsa -out "$WORK/jwt-encryption-private-key.pem" 2048 2>/dev/null
    jq -n \
      --rawfile a "$WORK/jwt-private-key.pem" \
      --rawfile b "$WORK/jwt-public-key.pem" \
      --rawfile c "$WORK/jwt-encryption-private-key.pem" \
      '{options: {cas: 0}, data: {"jwt-private-key.pem": $a, "jwt-public-key.pem": $b, "jwt-encryption-private-key.pem": $c}}' \
      > "$WORK/kv-write.json"
    vault_api POST "/v1/$KV/data/$KEY_PATH" "$WORK/kv-write.json" > /dev/null
    [ "$STATUS" = 200 ] || die "KV write failed (HTTP $STATUS); if another init created the key, rerun"
    ;;
  *) die "KV read failed (HTTP $STATUS)" ;;
esac

for f in $KEYS; do
  install -m 600 -o 1654 -g 1654 "$WORK/$f" "$CERTS/$f"
done

vault_api POST "/v1/auth/token/revoke-self" "$WORK/empty" > /dev/null || true
echo "identity-keys-init: JWT key files ready from Vault $KV/$KEY_PATH"
