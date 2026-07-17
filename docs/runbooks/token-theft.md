# Token Theft (JWT Compromise / Token Revocation) Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0 (HIPAA breach risk) |
| **Service** | Identity Service, Redis (JWT blacklist), API Gateway |
| **Owner** | Security Team (@security), SRE Team |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `SuspiciousTokenUsage` — Same JWT used from 2 distinct IPs in < 1m
- `BruteForceTokenReplay` — Same token presented > 100 times in 5m
- `GeographicallyImpossibleLogin` — Token used from two cities > 500km apart in < 10m
- `JwtRevocationQueueOverflow` — Manual revocation requests accumulating
- Manual report from user: "I lost my phone with the app logged in" or "unauthorized activity"

## Symptoms

- **Audit log**: Same `sub` (user ID) appears with requests from two different IPs/cities
- **Kibana**: Multiple `sub` values seen from unexpected geographies
- **Identity service logs**: Token refreshed while still valid (indicates stolen token)
- **User report**: Patient or doctor reports seeing activity they did not perform
- **Security team notification**: Partners report suspicious access patterns
- **Redis**: `jwt:blacklist:*` keys growing rapidly as revocation requests come in

## Diagnosis

```bash
# 1. Search audit logs for the affected user
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM identity.audit_logs WHERE actor_id = '{user-id}' AND timestamp > now() - interval '1 hour' ORDER BY timestamp;"

# 2. Check all IPs associated with the compromised token
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT DISTINCT client_ip, count(*) AS request_count FROM identity.audit_logs WHERE actor_id = '{user-id}' AND timestamp > now() - interval '1 hour' GROUP BY client_ip ORDER BY request_count DESC;"

# 3. Check if the token is still in Redis blacklist (should NOT be there if active)
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  EXISTS "jwt:blacklist:{jti}"

# 4. Check active sessions for the user
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  KEYS "session:{user-id}:*"

# 5. Get session metadata
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  HGETALL "session:{user-id}:{session-id}"

# 6. Check if the token was recently issued
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM identity.refresh_tokens WHERE user_id = '{user-id}' ORDER BY created_at DESC LIMIT 10;"

# 7. Search Kibana for all requests from the suspicious IPs
# Query: client_ip: "{suspicious-ip}" AND @timestamp > now-1h

# 8. Check API Gateway access logs 
kubectl logs deploy/his-hope-yarp -n his-hope --tail=200 | findstr "{suspicious-ip}"
```

## Mitigation

### Step 1 — Immediate Token Revocation (CRITICAL)

```bash
# Option A: Revoke all sessions for the user via identity API
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/admin/revoke-user-sessions \
    -H 'Content-Type: application/json' \
    -d '{"userId":"{user-id}","reason":"token_theft","revokedBy":"{sre-name}"}'

# Option B: Manually blacklist the specific JWT in Redis
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  SET "jwt:blacklist:{jti}" "{user-id}:revoked:$(date -u +%s)" EX 86400

# Option C: Block all tokens for the user (if system supports JTI families)
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  SET "jwt:user-block:{user-id}" "blocked:$(date -u +%s)" EX 3600
```

### Step 2 — Force User Password Reset

```bash
# Initiate password reset
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/admin/force-password-reset \
    -H 'Content-Type: application/json' \
    -d '{"userId":"{user-id}","notifyUser":true}'

# Verify the user's refresh tokens are invalidated
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "UPDATE identity.refresh_tokens SET revoked_at = now() WHERE user_id = '{user-id}' AND revoked_at IS NULL;"
```

### Step 3 — Block Suspicious IPs (if applicable)

```bash
# Option A: Block at Cilium network policy level
cat <<EOF | kubectl apply -f -
apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
metadata:
  name: block-suspicious-ip-${suspicious_ip_sanitized}
  namespace: his-hope
spec:
  endpointSelector:
    matchLabels: {}
  ingressDeny:
    - fromCIDR:
        - ${suspicious_ip}/32
EOF

# Option B: Block at YARP Gateway level
kubectl exec deploy/his-hope-yarp -n his-hope -- \
  curl -sf -X POST http://localhost:5000/admin/block-ip \
    -H 'Content-Type: application/json' \
    -d '{"ip":"{suspicious-ip}","reason":"token_theft","ttlMinutes":1440}'
```

### Step 4 — Rotate Secrets (if signing key may be compromised)

```bash
# Check when the JWT signing key was last rotated
kubectl get secret jwt-signing-key -n his-hope -o jsonpath='{.metadata.annotations.last-rotation}'

# Rotate the signing key
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/admin/rotate-signing-key

# ⚠ This invalidates ALL tokens — all users must re-login (use only if key compromise suspected)
```

## Resolution

### Root Cause Investigation

```bash
# 1. Determine how the token was stolen:
#    - Was it transmitted over HTTP? (check API Gateway TLS config)
#    - Was it in browser storage? (XSS vector?)
#    - Was the device compromised? (user's phone/computer)
#    - Was the JWT signing key leaked? (check Vault audit log)

# 2. Check Vault audit log for signing key access
vault audit list -detailed
vault audit read syslog

# 3. Check if the token was logged anywhere (Kibana)
# Search: properties.token OR properties.jwt OR authorization
# ⚠ Redact after finding — tokens must never be logged

# 4. Review system access for the time window
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM identity.audit_logs WHERE timestamp BETWEEN '{incident-start}' AND '{incident-end}' ORDER BY timestamp;"
```

### Verification

```bash
# 1. Compromised token is blacklisted
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  TTL "jwt:blacklist:{jti}"
# → Should return a positive value (key still exists with TTL)

# 2. User cannot authenticate with old password
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -w "%{http_code}" -X POST http://localhost:5000/api/v1/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"username":"{user-id}","password":"{old-password}"}'
# → Should return 401

# 3. Suspicious IP is blocked
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -w "%{http_code}" http://localhost:5000/api/v1/health \
    -H 'X-Forwarded-For: {suspicious-ip}'
# → Should return 403 if IP blocking is active

# 4. No further unauthorized access from that IP
# Kibana: Check audit log for the suspicious IP in the last hour
```

## Postmortem

Use the security incident postmortem template at `docs/security/security-incident-response.md`.

### Immediate Actions Checklist

```
□ 1. All active tokens for the user revoked
□ 2. User password reset forced
□ 3. Suspicious IPs blocked at network level
□ 4. User notified of incident
□ 5. HIPAA breach assessment initiated (if PHI accessed)
□ 6. JWT signing key rotated (if compromise suspected)
□ 7. Incident documented in security log
```

### Evidence Collection

```bash
# Export all relevant logs for the investigation
kubectl logs deploy/his-hope-identity-service -n his-hope \
  --since=2h > identity-service-logs-${incident-timestamp}.txt

kubectl logs deploy/his-hope-yarp -n his-hope \
  --since=2h > yarp-logs-${incident-timestamp}.txt

# Export audit log from CockroachDB
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM identity.audit_logs WHERE actor_id = '{user-id}' AND timestamp > now() - interval '2 hours';" \
  > audit-log-export-${incident-timestamp}.csv
```

---

> **Last updated**: 2026-07-17 | **Maintainer**: @security | **Next review**: 2026-09-17
