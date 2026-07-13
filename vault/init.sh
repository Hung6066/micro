#!/bin/bash
# Vault Initialization Script - His.Hope EMR
# Initializes Vault with Shamir seal, configures auth methods, secrets engines, and PKI
# Requires: vault CLI, jq

set -euo pipefail

VAULT_ADDR=${VAULT_ADDR:-"https://127.0.0.1:8200"}
VAULT_SKIP_VERIFY=${VAULT_SKIP_VERIFY:-true}
export VAULT_ADDR VAULT_SKIP_VERIFY

KEY_SHARES=5
KEY_THRESHOLD=3

OUTPUT_DIR="./vault-credentials"
mkdir -p "$OUTPUT_DIR"

echo "=== Step 1: Initializing Vault ==="
if ! vault status -format=json 2>/dev/null | jq -e '.initialized == true' > /dev/null 2>&1; then
  vault operator init \
    -key-shares="$KEY_SHARES" \
    -key-threshold="$KEY_THRESHOLD" \
    -format=json > "$OUTPUT_DIR/init.json"

  echo "Vault initialized successfully."
else
  echo "Vault is already initialized."
fi

jq -r '.root_token' "$OUTPUT_DIR/init.json" > "$OUTPUT_DIR/root_token.txt"
jq -r '.unseal_keys_b64[]' "$OUTPUT_DIR/init.json" > "$OUTPUT_DIR/unseal_keys.txt"

ROOT_TOKEN=$(cat "$OUTPUT_DIR/root_token.txt")
VAULT_TOKEN="$ROOT_TOKEN"
export VAULT_TOKEN

echo "=== Step 2: Unsealing Vault ==="
UNSEAL_KEYS=($(jq -r '.unseal_keys_b64[]' "$OUTPUT_DIR/init.json"))
for i in 0 1 2; do
  vault operator unseal "${UNSEAL_KEYS[$i]}"
done

echo "=== Step 3: Enabling Auth Methods ==="
vault auth enable approle
vault auth enable kubernetes
vault auth enable jwt

echo "=== Step 4: Enabling Secrets Engines ==="
vault secrets enable -version=2 -path=secret kv
vault secrets enable -path=transit transit
vault secrets enable -path=pki pki
vault secrets enable -path=pki_int pki

echo "=== Step 5: Configuring PKI ==="
vault write pki/root/generate/internal \
  common_name="His.Hope EMR Root CA" \
  ttl=87600h \
  key_type=rsa \
  key_bits=4096 \
  exclude_cn_from_sans=true

vault write pki/config/urls \
  issuing_certificates="https://vault-active.his-hope.svc.cluster.local:8200/v1/pki/ca" \
  crl_distribution_points="https://vault-active.his-hope.svc.cluster.local:8200/v1/pki/crl"

vault write pki_int/intermediate/generate/internal \
  common_name="His.Hope EMR Intermediate CA" \
  ttl=43800h \
  key_type=rsa \
  key_bits=4096 \
  exclude_cn_from_sans=true \
  format=pem_bundle > "$OUTPUT_DIR/intermediate.csr"

vault write pki/root/sign-intermediate \
  csr=@"$OUTPUT_DIR/intermediate.csr" \
  format=pem_bundle \
  ttl=43800h > "$OUTPUT_DIR/intermediate.cert.pem"

vault write pki_int/intermediate/set-signed \
  certificate=@"$OUTPUT_DIR/intermediate.cert.pem"

vault write pki_int/roles/internal-service \
  allowed_domains="his-hope.svc.cluster.local" \
  allow_subdomains=true \
  allow_bare_domains=true \
  allow_glob_domains=true \
  allow_any_name=true \
  enforce_hostnames=false \
  max_ttl=720h \
  key_type=rsa \
  key_bits=2048 \
  server_flag=true \
  client_flag=true \
  require_cn=false

echo "=== Step 6: Creating Vault Policies ==="
vault policy write patient-service ./policies/patient-service.hcl
vault policy write identity-service ./policies/identity-service.hcl
vault policy write clinical-service ./policies/clinical-service.hcl
vault policy write admin ./policies/admin.hcl
vault policy write approle ./policies/approle.hcl

echo "=== Step 7: Configuring AppRole Roles ==="
declare -A APP_ROLES
APP_ROLES[patient-service]="patient-service"
APP_ROLES[identity-service]="identity-service"
APP_ROLES[clinical-service]="clinical-service"
APP_ROLES[appointment-service]="appointment-service"

for ROLE in "${!APP_ROLES[@]}"; do
  POLICY="${APP_ROLES[$ROLE]}"
  vault write auth/approle/role/"$ROLE" \
    token_policies="$POLICY" \
    token_ttl=24h \
    token_max_ttl=72h \
    secret_id_num_uses=0 \
    secret_id_ttl=0 \
    bind_secret_id=true \
    role_id=$(openssl rand -hex 16)

  vault read -field=role_id auth/approle/role/"$ROLE"/role-id > "$OUTPUT_DIR/${ROLE}_role_id.txt"
  vault write -f -field=secret_id auth/approle/role/"$ROLE"/secret-id > "$OUTPUT_DIR/${ROLE}_secret_id.txt"
done

echo "=== Step 8: Enabling Audit Devices ==="
vault audit enable syslog \
  facility=AUTH \
  tag=vault \
  log_raw=false \
  hmackey=true

echo "=== Step 9: Seeding Initial Secrets ==="
bash ./seeds.sh

echo "=== Step 10: Configuring Kubernetes Auth ==="
K8S_HOST=$(kubectl config view --minify -o jsonpath='{.clusters[0].cluster.server}')
K8S_CA_CERT=$(kubectl config view --minify -o jsonpath='{.clusters[0].cluster.certificate-authority-data}' | base64 --decode)
K8S_SA_JWT=$(kubectl get secret vault-auth -n his-hope -o jsonpath='{.data.token}' | base64 --decode 2>/dev/null || echo "")

if [ -n "$K8S_SA_JWT" ]; then
  vault write auth/kubernetes/config \
    token_reviewer_jwt="$K8S_SA_JWT" \
    kubernetes_host="$K8S_HOST" \
    kubernetes_ca_cert="$K8S_CA_CERT" \
    issuer="https://kubernetes.default.svc.cluster.local"
fi

echo ""
echo "========================================"
echo "  Vault Initialization Complete"
echo "========================================"
echo ""
echo "Root Token: $(cat "$OUTPUT_DIR/root_token.txt")"
echo "Unseal Keys:"
cat "$OUTPUT_DIR/unseal_keys.txt"
echo ""
echo "Service Credentials ($OUTPUT_DIR/):"
for f in "$OUTPUT_DIR"/*_role_id.txt; do
  SERVICE=$(basename "$f" _role_id.txt)
  echo "  $SERVICE:"
  echo "    Role ID:  $(cat "$f")"
  echo "    Secret ID: $(cat "$OUTPUT_DIR/${SERVICE}_secret_id.txt")"
done
echo ""
echo "========================================"
