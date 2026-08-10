# Brute Force (Failed Login Spike / Account Lockout) Runbook

| Field | Value |
|-------|-------|
| **Severity** | P1 (P0 if credentials compromised or DoS) |
| **Service** | Identity Service, API Gateway, Redis (rate limiter) |
| **Owner** | Security Team (@security), SRE Team |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `FailedLoginSpike` — Failed login rate > 50/min sustained for 5m
- `AccountLockoutWave` — > 10 accounts locked in 5m
- `SuspiciousLoginSource` — > 100 failed logins from a single IP in 5m
- `ConcurrentLoginAttempts` — Same username attempted from > 5 distinct IPs in 1m
- `RateLimiterBypass` — Rate limit counter not decrementing (possible distributed attack)

## Symptoms

- **Kibana**: `Authentication failed` log entries spike (> 50/min)
- **Grafana**: `identity_failed_logins_total` counter climbs rapidly
- **Redis**: Rate limit keys (`ratelimit:login:{ip}:*`) growing rapidly
- **Identity service logs**: Repeated `InvalidCredentialsException` or `UserLockedOutException`
- **User reports**: Legitimate users unable to log in (account locked or rate-limited)
- **PagerDuty**: `FailedLoginSpike` alert firing
- **Support tickets**: "I can't log in" from multiple users (collateral damage from lockout)

## Diagnosis

```bash
# 1. Check failed login rate
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf http://localhost:9464/metrics | findstr "identity_failed_logins"

# 2. Identify source IPs of the attack
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT client_ip, count(*) AS attempts FROM identity.audit_logs WHERE action = 'LOGIN_FAILED' AND timestamp > now() - interval '15 minutes' GROUP BY client_ip ORDER BY attempts DESC LIMIT 20;"

# 3. Identify target usernames
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT actor_id, count(*) AS attempts, count(DISTINCT client_ip) AS distinct_ips FROM identity.audit_logs WHERE action = 'LOGIN_FAILED' AND timestamp > now() - interval '15 minutes' GROUP BY actor_id ORDER BY attempts DESC LIMIT 20;"

# 4. Check currently locked accounts
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT id, username, lockout_end FROM identity.users WHERE lockout_end IS NOT NULL AND lockout_end > now() ORDER BY lockout_end DESC LIMIT 20;"

# 5. Check Redis rate limiter keys
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  KEYS "ratelimit:login:*" | wc -l

# 6. Check API Gateway logs for request pattern
kubectl logs deploy/his-hope-yarp -n his-hope --tail=500 | findstr "POST /auth/login"

# 7. Check if this is a credential stuffing attack (same password tried across accounts)
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT details->>'reason' AS reason, count(*) FROM identity.audit_logs WHERE action = 'LOGIN_FAILED' AND timestamp > now() - interval '15 minutes' AND details IS NOT NULL GROUP BY reason ORDER BY count(*) DESC;"
```

## Mitigation

### Step 1 — Block Attack Source

```bash
# Option A: Block the top attacking IPs immediately
kubectl exec deploy/his-hope-yarp -n his-hope -- \
  for ip in $(kubectl exec deploy/his-hope-identity-service -n his-hope -- \
    curl -sf http://localhost:5000/api/v1/admin/top-attack-ips?minutes=15); do \
    curl -sf -X POST http://localhost:5000/admin/block-ip \
      -H 'Content-Type: application/json' \
      -d "{\"ip\":\"$ip\",\"reason\":\"brute_force\",\"ttlMinutes\":60}"; \
  done

# Option B: Block at Cilium network policy level
cat <<EOF | kubectl apply -f -
apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
metadata:
  name: block-brute-force-ips
  namespace: his-hope
spec:
  endpointSelector:
    matchLabels:
      app: his-hope-identity-service
  ingressDeny:
    - fromCIDR:
$(kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT client_ip FROM identity.audit_logs WHERE action = 'LOGIN_FAILED' AND timestamp > now() - interval '15 minutes' GROUP BY client_ip HAVING count(*) > 50;" \
  --format=csv | tail -n +2 | head -20 | sed 's/^/      - /')
EOF
```

### Step 2 — Tighten Rate Limiting

```bash
# 1. Reduce login rate limit window
kubectl exec deploy/his-hope-yarp -n his-hope -- \
  curl -sf -X PUT http://localhost:5000/admin/rate-limit-policy \
    -H 'Content-Type: application/json' \
    -d '{"endpoint":"/auth/login","permitLimit":5,"windowSeconds":60}'

# 2. Stagger lockout duration
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X PUT http://localhost:5000/api/v1/admin/lockout-policy \
    -H 'Content-Type: application/json' \
    -d '{"maxFailedAttempts":5,"lockoutDurationMinutes":15,"lockoutEscalationMinutes":60}'
```

### Step 3 — Unlock Collateral Damage (Legitimate Users)

```bash
# Unlock specific legitimate user accounts
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/admin/unlock-account \
    -H 'Content-Type: application/json' \
    -d '{"userId":"{user-id}","reason":"brute_force_collateral"}'

# Or bulk unlock all accounts locked in the last 15m (if legitimate lockout wave)
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "UPDATE identity.users SET lockout_end = NULL, access_failed_count = 0 WHERE lockout_end > now() - interval '15 minutes' AND lockout_end IS NOT NULL;"
```

### Step 4 — Enable CAPTCHA or MFA Challenge (if available)

```bash
# Force MFA challenge for suspicious logins
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -X POST http://localhost:5000/api/v1/admin/mfa-policy \
    -H 'Content-Type: application/json' \
    -d '{"requireMfaForAllLogins":true,"gracePeriodMinutes":15}'
```

## Resolution

### Root Cause Investigation

```bash
# 1. Determine attack type
#    - Credential stuffing: Same password tried for many users
#    - Dictionary attack: Many passwords for one user
#    - Distributed attack: Many IPs, low attempts each
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT
    CASE
      WHEN COUNT(DISTINCT actor_id) > 50 AND COUNT(DISTINCT details->>'credential') < 10 THEN 'credential_stuffing'
      WHEN COUNT(DISTINCT actor_id) < 5 AND COUNT(DISTINCT details->>'credential') > 50 THEN 'dictionary'
      WHEN COUNT(DISTINCT client_ip) > 20 AND COUNT(*) / COUNT(DISTINCT client_ip) < 5 THEN 'distributed'
      ELSE 'unknown'
    END AS attack_type
  FROM identity.audit_logs
  WHERE action = 'LOGIN_FAILED' AND timestamp > now() - interval '15 minutes';"

# 2. Check if any credentials were compromised
# Look for logged-in sessions with unusual geo
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT DISTINCT client_ip FROM identity.audit_logs WHERE action = 'LOGIN_SUCCESS' AND actor_id IN (SELECT DISTINCT actor_id FROM identity.audit_logs WHERE action = 'LOGIN_FAILED' AND timestamp > now() - interval '30 minutes');"
```

### Verification

```bash
# 1. Failed login rate back to normal
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf http://localhost:9464/metrics | findstr "identity_failed_logins_total"

# 2. No new locked accounts
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) FROM identity.users WHERE lockout_end IS NOT NULL AND lockout_end > now();"

# 3. Legitimate users can log in
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -w "\n%{http_code}" -X POST http://localhost:5000/api/v1/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"username":"test-doctor","password":"{test-password}"}'
# → Should return 200 with token

# 4. Blocked IPs receive 403
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf -w "\n%{http_code}" -X POST http://localhost:5000/api/v1/auth/login \
    -H 'Content-Type: application/json' \
    -H 'X-Forwarded-For: {blocked-ip}' \
    -d '{"username":"test-doctor","password":"wrong-password"}'
# → Should return 403

# 5. No collateral damage — all legit users unlocked
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) FROM identity.users WHERE lockout_end IS NOT NULL AND lockout_end > now();"
```

## Postmortem

Use the security incident postmortem template at `docs/security/security-incident-response.md`.

### Key Metrics

- Total failed login attempts during incident
- Number of distinct attacking IPs
- Number of accounts locked (malicious vs legitimate)
- Time from alert to mitigation
- Whether WAF or external DDoS protection (Cloudflare, AWS Shield) should be engaged

### Recommendations

```
□ Rate limit login endpoint at the edge (YARP/CDN), not just the app
□ Implement CAPTCHA after 3 failed attempts from same IP
□ Add account lockout notification email to user
□ Consider geo-blocking for non-operational regions
□ Review if password complexity policy needs strengthening
□ Enable MFA for all accounts (priority: remote-access roles)
```

---

> **Last updated**: 2026-07-17 | **Maintainer**: @security | **Next review**: 2026-09-17
