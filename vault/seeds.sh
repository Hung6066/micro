#!/bin/bash
# Vault Secrets Seeding Script - His.Hope EMR
# Seeds all service secrets into Vault KV stores
# Requires: vault CLI, openssl
# Must be run after Vault is initialized and unsealed with root token

set -euo pipefail

VAULT_ADDR=${VAULT_ADDR:-"https://127.0.0.1:8200"}
VAULT_SKIP_VERIFY=${VAULT_SKIP_VERIFY:-true}
export VAULT_ADDR VAULT_SKIP_VERIFY

if [ -z "${VAULT_TOKEN:-}" ]; then
  if [ -f "./vault-credentials/root_token.txt" ]; then
    export VAULT_TOKEN=$(cat ./vault-credentials/root_token.txt)
  else
    echo "ERROR: VAULT_TOKEN not set and root_token.txt not found."
    echo "Run init.sh first or set VAULT_TOKEN."
    exit 1
  fi
fi

echo "Seeding secrets into Vault..."

# ------------------------------------------------------------------
# Database Secrets
# ------------------------------------------------------------------
echo "  Writing database/patientdb..."
vault kv put secret/his-hope/database/patientdb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=patientdb \
  username=patient_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=50 \
  connection_timeout=10

echo "  Writing database/identitydb..."
vault kv put secret/his-hope/database/identitydb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=identitydb \
  username=identity_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=30 \
  connection_timeout=10

echo "  Writing database/clinicaldb..."
vault kv put secret/his-hope/database/clinicaldb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=clinicaldb \
  username=clinical_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=100 \
  connection_timeout=10

echo "  Writing database/appointmentdb..."
vault kv put secret/his-hope/database/appointmentdb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=appointmentdb \
  username=appointment_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=30 \
  connection_timeout=10

echo "  Writing database/labdb..."
vault kv put secret/his-hope/database/labdb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=labdb \
  username=lab_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=50 \
  connection_timeout=10

echo "  Writing database/billingdb..."
vault kv put secret/his-hope/database/billingdb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=billingdb \
  username=billing_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=50 \
  connection_timeout=10

echo "  Writing database/pharmacydb..."
vault kv put secret/his-hope/database/pharmacydb \
  host=postgres.his-hope.svc.cluster.local \
  port=5432 \
  database=pharmacydb \
  username=pharmacy_user \
  password=$(openssl rand -base64 32) \
  sslmode=require \
  max_connections=50 \
  connection_timeout=10

# ------------------------------------------------------------------
# RabbitMQ Secrets
# ------------------------------------------------------------------
echo "  Writing rabbitmq..."
RABBITMQ_PASSWORD=$(openssl rand -base64 32)
RABBITMQ_ERLANG_COOKIE=$(openssl rand -base64 64)
vault kv put secret/his-hope/rabbitmq \
  host=rabbitmq.his-hope.svc.cluster.local \
  port=5672 \
  management_port=15672 \
  username=his-hope-user \
  password="$RABBITMQ_PASSWORD" \
  erlang_cookie="$RABBITMQ_ERLANG_COOKIE" \
  vhost=his-hope \
  ssl_enabled=true \
  heartbeat=60 \
  connection_timeout=30

# ------------------------------------------------------------------
# Redis Secrets
# ------------------------------------------------------------------
echo "  Writing redis..."
REDIS_PASSWORD=$(openssl rand -base64 48)
vault kv put secret/his-hope/redis \
  host=redis.his-hope.svc.cluster.local \
  port=6379 \
  password="$REDIS_PASSWORD" \
  db=0 \
  ssl_enabled=true \
  pool_size=20 \
  timeout=10 \
  max_retries=3

# ------------------------------------------------------------------
# JWT Signing Keys
# ------------------------------------------------------------------
echo "  Writing identity-service/jwt..."
JWT_PRIVATE_KEY=$(openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 2>/dev/null)
JWT_PUBLIC_KEY=$(echo "$JWT_PRIVATE_KEY" | openssl rsa -pubout 2>/dev/null)
JWT_KEY_ID=$(openssl rand -hex 16)
vault kv put secret/his-hope/identity-service/jwt \
  algorithm=RS256 \
  key_id="$JWT_KEY_ID" \
  private_key="$JWT_PRIVATE_KEY" \
  public_key="$JWT_PUBLIC_KEY" \
  issuer=his-hope-identity \
  audience=his-hope-services \
  token_ttl=15m \
  refresh_ttl=7d \
  clock_skew=30s

# Configure Transit key for JWT signing
vault write -f transit/keys/jwt-signing \
  type=rsa-4096 \
  exportable=true \
  auto_rotate_period=720h

# Export RSA public key for service validation
vault read -field=public_key transit/keys/jwt-signing > /tmp/jwt-public-key.pem 2>/dev/null || true
vault kv put secret/his-hope/jwt/public-key \
  key_pem="$(cat /tmp/jwt-public-key.pem 2>/dev/null || echo '')"

# ------------------------------------------------------------------
# Service-Specific Secrets
# ------------------------------------------------------------------
echo "  Writing patient-service/config..."
vault kv put secret/his-hope/patient-service/config \
  log_level=info \
  max_batch_size=100 \
  sync_interval=30s \
  encryption_enabled=true \
  audit_log_enabled=true

echo "  Writing clinical-service/config..."
vault kv put secret/his-hope/clinical-service/config \
  log_level=info \
  phi_audit_enabled=true \
  hipaa_compliance=true \
  encryption_at_rest=true \
  data_retention_days=2555

echo "  Writing identity-service/config..."
vault kv put secret/his-hope/identity-service/config \
  log_level=info \
  mfa_enabled=true \
  password_policy=hipaa-compliant \
  session_timeout=30m \
  max_login_attempts=5 \
  lockout_duration=15m

echo "  Writing lab-service/config..."
vault kv put secret/his-hope/lab-service/config \
  log_level=info \
  phi_audit_enabled=true \
  result_retention_days=2555 \
  auto_publish_results=true

echo "  Writing billing-service/config..."
vault kv put secret/his-hope/billing-service/config \
  log_level=info \
  phi_audit_enabled=true \
  invoice_retention_days=2555 \
  payment_gateway=stripe \
  auto_invoice=true

echo "  Writing pharmacy-service/config..."
vault kv put secret/his-hope/pharmacy-service/config \
  log_level=info \
  phi_audit_enabled=true \
  prescription_retention_days=2555 \
  refill_reminder_enabled=true

# ------------------------------------------------------------------
# EventBus Secrets per service
# ------------------------------------------------------------------
echo "  Writing eventbus/patient..."
vault kv put secret/his-hope/eventbus/patient \
  exchange_name=his_hope_patient \
  queue_prefix=patient

echo "  Writing eventbus/clinical..."
vault kv put secret/his-hope/eventbus/clinical \
  exchange_name=his_hope_clinical \
  queue_prefix=clinical

echo "  Writing eventbus/billing..."
vault kv put secret/his-hope/eventbus/billing \
  exchange_name=his_hope_billing \
  queue_prefix=billing

echo "  Writing eventbus/lab..."
vault kv put secret/his-hope/eventbus/lab \
  exchange_name=his_hope_lab \
  queue_prefix=lab

echo "  Writing eventbus/pharmacy..."
vault kv put secret/his-hope/eventbus/pharmacy \
  exchange_name=his_hope_pharmacy \
  queue_prefix=pharmacy

# ------------------------------------------------------------------
# mTLS Certificate Secrets
# ------------------------------------------------------------------
echo "  Generating mTLS certificates..."

generate_mtls_cert() {
  local service_name=$1
  local cn="${service_name}.his-hope.svc.cluster.local"

  openssl req -newkey rsa:2048 -nodes -keyout "/tmp/${service_name}.key" \
    -subj "/CN=${cn}/O=His.Hope EMR/OU=${service_name}" \
    -addext "subjectAltName=DNS:${cn},DNS:${service_name},DNS:*.his-hope.svc.cluster.local" \
    -addext "extendedKeyUsage=serverAuth,clientAuth" \
    -out "/tmp/${service_name}.csr" 2>/dev/null

  # Use vault signed cert approach - store CSR for reference
  vault write pki_int/sign/internal-service \
    csr=@"${service_name}/${service_name}.csr" \
    format=pem_bundle \
    ttl=720h > "/tmp/${service_name}_signed.cert" 2>/dev/null || \
  {
    # If Vault PKI not ready, generate self-signed as fallback
    openssl x509 -req -days 365 -in "/tmp/${service_name}.csr" \
      -signkey "/tmp/${service_name}.key" \
      -out "/tmp/${service_name}.crt" 2>/dev/null
  }

  local cert
  local key
  cert=$(cat "/tmp/${service_name}.crt" 2>/dev/null || cat "/tmp/${service_name}_signed.cert" 2>/dev/null)
  key=$(cat "/tmp/${service_name}.key")

  vault kv put "secret/his-hope/${service_name}/mtls" \
    cert="$cert" \
    key="$key" \
    ca_cert="$(vault read -field=certificate pki/cert/ca 2>/dev/null || echo 'PLACEHOLDER_CA_CERT')" \
    common_name="$cn" \
    expiry="$(date -d '+365 days' -u +'%Y-%m-%dT%H:%M:%SZ' 2>/dev/null || echo 'ANNUAL_ROTATION')"

  rm -f "/tmp/${service_name}.key" "/tmp/${service_name}.csr" "/tmp/${service_name}.crt" "/tmp/${service_name}_signed.cert"
}

# Generate mTLS certs for all services
for svc in patient-service identity-service clinical-service appointment-service lab-service billing-service pharmacy-service; do
  mkdir -p "/tmp/${svc}"
  echo "  Generating mTLS for ${svc}..."
  generate_mtls_cert "$svc"
  rmdir "/tmp/${svc}"
done

# ------------------------------------------------------------------
# Output Summary
# ------------------------------------------------------------------
echo ""
echo "  Writing pharmacy-bff/config..."
vault kv put secret/his-hope/pharmacy-bff/config \
  log_level=info \
  redis_connection_timeout=10 \
  max_request_size=4096

echo "Secrets seeded successfully!"
echo ""
echo "Seeded paths:"
echo "  secret/his-hope/database/patientdb"
echo "  secret/his-hope/database/identitydb"
echo "  secret/his-hope/database/clinicaldb"
echo "  secret/his-hope/database/appointmentdb"
echo "  secret/his-hope/database/labdb"
echo "  secret/his-hope/database/billingdb"
echo "  secret/his-hope/database/pharmacydb"
echo "  secret/his-hope/rabbitmq"
echo "  secret/his-hope/redis"
echo "  secret/his-hope/identity-service/jwt"
echo "  secret/his-hope/jwt/public-key"
echo "  secret/his-hope/patient-service/config"
echo "  secret/his-hope/clinical-service/config"
echo "  secret/his-hope/identity-service/config"
echo "  secret/his-hope/lab-service/config"
echo "  secret/his-hope/billing-service/config"
echo "  secret/his-hope/pharmacy-service/config"
echo "  secret/his-hope/pharmacy-bff/config"
echo "  secret/his-hope/eventbus/{service}"
echo "  secret/his-hope/{service}/mtls (for each service)"
echo ""
echo "Transit key configured: transit/keys/jwt-signing"
