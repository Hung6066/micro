# PHI Exfiltration (Suspicious Bulk Read / Audit Trail Query) Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0 (HIPAA breach — mandatory reporting) |
| **Service** | All services (patient, clinical, lab, billing) |
| **Owner** | Security Team (@security), DBA, Compliance Officer |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `SuspiciousBulkRead` — > 1,000 patient records queried in < 1m by a single user
- `AnomalousQueryPattern` — Query pattern differs from user's baseline (> 3σ deviation)
- `DataVolumeExport` — API response size > 10MB for non-export endpoint
- `UnusualAccessTime` — PHI accessed outside normal hours (e.g., 02:00–05:00)
- `NewUserDataAccess` — User accessed a patient record with no clinical relationship
- `AuditTrailTampering` — Audit log deletion or gap detected

## Symptoms

- **Audit log**: A single user ID appears in `patient_record_accessed` events at > 100/min
- **Kibana**: Multiple `GET /api/v1/patients/{id}` calls in rapid succession, with different patient IDs
- **Grafana**: `grpc_server_handled_total` shows a spike on `PatientService.GetPatient` RPC
- **Identity service**: User who never accessed > 10 patients/day suddenly accesses 500+ patients
- **CockroachDB**: High `sql_select` count, many queries without `WHERE` clause filters
- **Network**: Linkerd metrics show data egress spike from a single pod
- **SIEM**: Alert from external monitoring (if integrated)

## Diagnosis

### Phase 1 — Confirm the Exfiltration

```bash
# 1. Identify the anomalous user
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT actor_id, action, count(*) AS count FROM patient.audit_log WHERE action = 'PATIENT_RECORD_ACCESSED' AND timestamp > now() - interval '15 minutes' GROUP BY actor_id, action ORDER BY count DESC LIMIT 10;"

# 2. Get the user's details
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT id, username, email, role FROM identity.users WHERE id = '{suspicious-user-id}';"

# 3. Check their recent sessions and IPs
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT client_ip, user_agent, timestamp FROM identity.audit_logs WHERE actor_id = '{suspicious-user-id}' AND timestamp > now() - interval '1 hour' ORDER BY timestamp;"

# 4. Count distinct patients accessed
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(DISTINCT details->>'patient_id') AS distinct_patients FROM patient.audit_log WHERE actor_id = '{suspicious-user-id}' AND action = 'PATIENT_RECORD_ACCESSED' AND timestamp > now() - interval '1 hour';"

# 5. Check if the user has a legitimate clinical relationship with those patients
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) AS patients_with_relationship FROM patient.audit_log al INNER JOIN clinical.patient_providers pp ON al.details->>'patient_id' = pp.patient_id::text WHERE al.actor_id = '{suspicious-user-id}' AND al.action = 'PATIENT_RECORD_ACCESSED' AND al.timestamp > now() - interval '1 hour' AND pp.provider_id = '{suspicious-user-id}';"

# 6. Check for bulk export patterns
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT details->>'endpoint' AS endpoint, count(*) FROM patient.audit_log WHERE actor_id = '{suspicious-user-id}' AND timestamp > now() - interval '1 hour' GROUP BY endpoint ORDER BY count(*) DESC;"

# 7. Check if data left the network (Linkerd egress metrics)
linkerd viz stat deploy/his-hope-{service} -n his-hope -t http --to svc/his-hope-{patient-service}
```

### Phase 2 — Scope the Breach

```bash
# 1. List all PHI fields accessed
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT details->>'fields_accessed' AS fields FROM patient.audit_log WHERE actor_id = '{suspicious-user-id}' AND action = 'PATIENT_RECORD_ACCESSED' AND timestamp > now() - interval '1 hour' AND details->>'fields_accessed' IS NOT NULL;"

# 2. Check if the data was downloaded (API endpoint that returns files)
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM patient.audit_log WHERE actor_id = '{suspicious-user-id}' AND (action LIKE '%EXPORT%' OR action LIKE '%DOWNLOAD%' OR action LIKE '%LAB_RESULT_FILE%') AND timestamp > now() - interval '1 hour';"

# 3. Check billing data access
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) FROM billing.audit_log WHERE actor_id = '{suspicious-user-id}' AND action LIKE '%CLAIM%' AND timestamp > now() - interval '1 hour';"
```

## Mitigation

### Step 1 — IMMEDIATE: Stop the Exfiltration

```bash
# A: Kill the user's active sessions
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  KEYS "session:{suspicious-user-id}:*" | ForEach-Object { \
    kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD DEL $_; \
  }

# B: Revoke all JWTs for the user
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  SET "jwt:user-block:{suspicious-user-id}" "security_incident:$(date -u +%s)-phi_exfiltration" EX 86400

# C: Disable the user account immediately
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "UPDATE identity.users SET is_active = false, lockout_end = now() + interval '24 hours' WHERE id = '{suspicious-user-id}';"

# D: Block the user's IP(s) at the network level
for ip in $(kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT DISTINCT client_ip FROM identity.audit_logs WHERE actor_id = '{suspicious-user-id}' AND timestamp > now() - interval '1 hour';" --format=csv | tail -n +2); do \
  cat <<EOF | kubectl apply -f -
apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
metadata:
  name: block-phi-exfil-$(echo $ip | tr . -)
  namespace: his-hope
spec:
  endpointSelector:
    matchLabels: {}
  ingressDeny:
    - fromCIDR:
        - ${ip}/32
EOF
done
```

### Step 2 — Preserve Evidence

```bash
# A: Snapshot the affected database at this point in time
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "BACKUP INTO 's3://his-hope-forensic-backups/phi-incident-$(date -u +%Y%m%dT%H%M%S)/' AS OF SYSTEM TIME '-5s';"

# B: Export relevant audit logs to secure storage
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM patient.audit_log WHERE actor_id = '{suspicious-user-id}' AND timestamp > now() - interval '2 hours' ORDER BY timestamp;" \
  --format=csv > phi-incident-$(date -u +%Y%m%dT%H%M%S)-audit-log.csv

# C: Collect network flow logs from Linkerd
linkerd viz -n his-hope edges deploy > phi-incident-$(date -u +%Y%m%dT%H%M%S)-edges.txt
linkerd viz -n his-hope top deploy/his-hope-{patient-service} > phi-incident-$(date -u +%Y%m%dT%H%M%S)-top.txt

# D: Capture API Gateway logs
kubectl logs deploy/his-hope-yarp -n his-hope --since=2h > phi-incident-$(date -u +%Y%m%dT%H%M%S)-yarp-logs.txt
```

### Step 3 — Initiate HIPAA Breach Protocol

```bash
# A: Log the incident in the security incident tracker
cat > /tmp/phi-incident-$(date -u +%Y%m%dT%H%M%S).md << INC_EOF
# PHI Exfiltration Incident

**Date**: $(date -u)
**Severity**: P0 — HIPAA Breach
**User**: {suspicious-user-id}
**Patients Affected**: {count_distinct_patients}
**Data Accessed**: {PHI fields}
**Duration**: {start_time} → {detection_time}
**Contained**: {yes/no}
**Reported to**: {HIPAA officer name}
INC_EOF

# B: The HIPAA breach notification clock starts NOW
#  - 60 days to notify affected individuals
#  - Must notify HHS Secretary
#  - Risk assessment must begin immediately
```

## Resolution

### Root Cause Investigation

| Vector | Investigation | Prevention |
|---|---|---|
| **Compromised credentials** | Check if user's password was phished; check Vault/SSO logs | Enforce MFA; monitor credential stuffing |
| **Insider threat** | Review user's access patterns over last 30 days; interview manager | Background checks; least-privilege reviews |
| **API without auth** | Check if any PHI endpoint lacks auth/rate limiting | API security audit; enforce OAuth on all PHI endpoints |
| **Misconfigured RBAC** | Check if the user's role has excessive permissions | Role-based access control audit |
| **Data export feature** | Check if bulk export was used without logging | Add approval workflow for bulk exports; alert on all exports |
| **Partner API key leak** | Check if an API key was exposed in logs or GitHub | API key rotation; secret scanning in CI/CD |

### Verification

```bash
# 1. User account is disabled
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT is_active FROM identity.users WHERE id = '{suspicious-user-id}';"
# → false

# 2. All sessions revoked
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD \
  KEYS "session:{suspicious-user-id}:*" | Measure-Object | Select-Object -ExpandProperty Count
# → 0

# 3. No further PHI access from the user
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) FROM patient.audit_log WHERE actor_id = '{suspicious-user-id}' AND timestamp > now() - interval '5 minutes';"
# → 0

# 4. Affected patients identified and notified (check compliance tracker)
```

## Postmortem

Use the HIPAA breach response template at `docs/security/hipaa-breach-response.md`.

### Mandatory Reporting

| To Whom | Deadline | Completed |
|---|---|---|
| Affected individuals | Within 60 days | □ |
| HHS Secretary | Depends on scale (>500 = immediately) | □ |
| State attorney general | Per state law | □ |
| Law enforcement (if criminal) | Within 24h | □ |
| Cyber insurance | Per policy | □ |

### Recommendations

```
□ Implement anomaly detection ML model for PHI access patterns
□ Add explicit "View PHI reason" dialog requiring clinical justification
□ Enforce MFA for all PHI-accessing roles (enabled, but verify coverage)
□ Add data loss prevention (DLP) agent on all workstations
□ Restrict bulk PHI exports to approved devices only
□ Implement query parameter allowlisting for PHI endpoints
□ Add automatic screen recording for high-risk roles
```

---

> **Last updated**: 2026-07-17 | **Maintainer**: @security | **Next review**: 2026-09-17
