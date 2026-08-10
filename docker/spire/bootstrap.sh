#!/bin/sh
set -eu

SOCKET=/run/spire/sockets/server.sock
TOKEN_FILE=/run/spire/bootstrap/join-token
mkdir -p /run/spire/bootstrap
until /opt/spire/bin/spire-server healthcheck -socketPath "$SOCKET" >/dev/null 2>&1; do sleep 2; done

/opt/spire/bin/spire-server token generate -socketPath "$SOCKET" \
  -spiffeID spiffe://his-hope.local/agent/docker > "$TOKEN_FILE"

for service in identity-service patient-service clinical-service appointment-service lab-service billing-service pharmacy-service; do
  /opt/spire/bin/spire-server entry create -socketPath "$SOCKET" \
    -spiffeID "spiffe://his-hope.local/ns/his-hope/sa/$service" \
    -parentID "spiffe://his-hope.local/agent/docker" \
    -selector "docker:label:com.docker.compose.service:$service" >/dev/null 2>&1 || true
done

# Each Vault-authenticated workload gets its own fetcher identity and token
# volume. This prevents one service from presenting another service's SVID.
for service in identity patient appointment clinical lab billing pharmacy; do
  service_id="${service}-service"
  /opt/spire/bin/spire-server entry create -socketPath "$SOCKET" \
    -spiffeID "spiffe://his-hope.local/ns/his-hope/sa/${service_id}" \
    -parentID "spiffe://his-hope.local/agent/docker" \
    -selector "docker:label:com.docker.compose.service:spire-jwt-fetcher-${service}" >/dev/null 2>&1 || true
done

/opt/spire/bin/spire-server entry create -socketPath "$SOCKET" \
  -spiffeID "spiffe://his-hope.local/ns/his-hope/sa/spire-oidc" \
  -parentID "spiffe://his-hope.local/agent/docker" \
  -selector "docker:label:com.docker.compose.service:spire-oidc" >/dev/null 2>&1 || true

# The OIDC provider shares the agent PID namespace so it can use the agent
# Workload API socket; Docker attestation therefore resolves the caller to the
# agent container in this Compose profile.
/opt/spire/bin/spire-server entry create -socketPath "$SOCKET" \
  -spiffeID "spiffe://his-hope.local/ns/his-hope/sa/spire-oidc" \
  -parentID "spiffe://his-hope.local/agent/docker" \
  -selector "docker:label:com.docker.compose.service:spire-agent" >/dev/null 2>&1 || true
echo "SPIRE join token is ready"
