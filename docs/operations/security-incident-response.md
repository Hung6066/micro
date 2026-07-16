# Ứng phó Sự cố Bảo mật — His.Hope EMR

> **Tài liệu:** OPS-SECIR-001
> **Version:** 1.0
> **Audience:** Security Team, SRE On-Call, Compliance Officer, CISO
> **Phân loại:** Confidential — Internal Use Only
> **Cập nhật:** 2026-07-16

---

## 1. Incident Classification

### 1.1 Phân loại Sự cố Bảo mật

| Loại | Code | Định nghĩa | Ví dụ |
|------|------|-----------|-------|
| **Data Breach** | SEC-DB | Truy cập trái phép vào ePHI hoặc PII | Database dump bị exfiltrate, row-level security bypass |
| **Unauthorized Access** | SEC-UA | Người dùng hoặc service truy cập vượt quyền | JWT giả mạo, RBAC bypass, stolen credentials |
| **Denial of Service** | SEC-DoS | Tấn công làm hệ thống không khả dụng | DDoS vào API Gateway, resource exhaustion |
| **Malware / Ransomware** | SEC-MAL | Mã độc trên infrastructure hoặc application | Container escape, ransomware trên PVC |
| **Insider Threat** | SEC-INS | Nhân viên nội bộ misuse quyền truy cập | Truy cập ePHI không có lý do y tế |
| **Infrastructure Compromise** | SEC-INF | K8s cluster, node, hoặc service bị chiếm quyền | Compromised service account, kubeconfig leak |
| **Supply Chain Attack** | SEC-SC | Package, image, hoặc dependency bị compromise | Malicious NuGet package, poisoned base image |
| **Certificate / Key Compromise** | SEC-CERT | Private key hoặc certificate bị lộ | Vault unseal key leak, JWT signing key exposed |

### 1.2 Severity Levels

| Mức độ | Tiêu chí | Response SLA |
|--------|----------|-------------|
| **SEV-S1 (Critical)** | ePHI của >100 patients bị compromised; hệ thống down do attack | Phản ứng ngay lập tức, escalate CISO trong 5 phút |
| **SEV-S2 (High)** | ePHI của <100 patients bị compromised; unauthorized access confirmed | 15 phút acknowledge, escalate trong 30 phút |
| **SEV-S3 (Medium)** | Attempted attack detected và blocked; suspicious activity | 2 giờ acknowledge |
| **SEV-S4 (Low)** | Vulnerability scan findings; policy violation | Next business day |

---

## 2. Immediate Containment Steps

### 2.1 Quy trình Chung (First 15 Minutes)

**Khi phát hiện security incident, lập tức thực hiện các bước dưới đây. Ưu tiên CONTAINMENT trước, investigation sau.**

```
BƯỚC 0: KÍCH HOẠT INCIDENT RESPONSE
  → PagerDuty: Trigger @security-team (his-hope-security-p1)
  → Slack: Post vào #his-hope-security-incidents
  → SCIM: Tạo incident ticket với tag SEC-YYYYMMDD-NNN

BƯỚC 1: CONTAINMENT (ISOLATION) — 5 phút đầu
  → Network isolation service bị compromise
  → Revoke JWT tokens của user/service bị compromise
  → Rotate bất kỳ credential nào bị ảnh hưởng

BƯỚC 2: EVIDENCE PRESERVATION — 10 phút tiếp theo
  → Snapshot pod logs, audit logs
  → Capture Hubble flows, Jaeger traces
  → Dump K8s events, Cilium endpoint state

BƯỚC 3: ASSESSMENT — 15 phút
  → Xác định scope: service nào, data nào, bao nhiêu records
  → Đánh giá HIPAA notification requirement
  → Quyết định tiếp tục containment hay escalate
```

### 2.2 Network Isolation (Cilium Policy)

```bash
# === CÁCH 1: Isolate một pod cụ thể ===
# Tạo CiliumNetworkPolicy deny-all cho pod bị compromise
# → File: cilium/network-policies.yaml

kubectl apply -f - <<EOF
apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
metadata:
  name: emergency-isolate-<SERVICE-NAME>
  namespace: his-hope
spec:
  endpointSelector:
    matchLabels:
      app: <SERVICE-NAME>
  ingress:
    - {}  # Deny all ingress (empty rule = deny)
  egress:
    - {}  # Deny all egress (empty rule = deny)
EOF

# === CÁCH 2: Isolate toàn bộ namespace ===
# Patch namespace với network isolation
kubectl annotate namespace his-hope \
  policy.cilium.io/isolate-ingress=true \
  policy.cilium.io/isolate-egress=true --overwrite

# === CÁCH 3: Disconnect pod khỏi network dùng Cilium endpoint disconnect ===
POD_NAME="<pod-name>"
ENDPOINT_ID=$(kubectl get ciliumendpoint -n his-hope -l app=<SERVICE-NAME> -o jsonpath='{.items[0].status.id}')
kubectl exec -it -n kube-system ds/cilium -- cilium endpoint disconnect $ENDPOINT_ID
```

### 2.3 Pod Termination

```bash
# === Terminate pod bị compromise (SAU KHI đã capture evidence) ===

# 1. Drain pod khỏi service (remove khỏi endpoint)
kubectl label pod <pod-name> -n his-hope app.kubernetes.io/exclude-from-service=true --overwrite

# 2. Scale deployment xuống 0 nếu cần isolate hoàn toàn
kubectl scale deploy/<service-name> -n his-hope --replicas=0

# 3. Delete pod (StatefulSet/Deployment sẽ không recreate do replicas=0)
kubectl delete pod <pod-name> -n his-hope --force --grace-period=0

# 4. Xóa PVC nếu có malware persistence (cẩn thận: mất data!)
# kubectl delete pvc <pvc-name> -n his-hope
```

### 2.4 Token Revocation (POST /api/v1/auth/revoke)

```bash
# === Revoke JWT token của user/service bị compromise ===

# Cách 1: Revoke tất cả tokens của một user
# Sử dụng API Gateway endpoint
$API_URL = "http://api-gateway.his-hope.svc.cluster.local:5000"
$ADMIN_TOKEN = "<admin-jwt-token>"

# Revoke theo user ID
curl -X POST "$API_URL/api/v1/auth/revoke" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"userId": "<USER-ID>", "revokeAll": true}'

# Cách 2: Revoke một token cụ thể (theo jti — JWT ID)
curl -X POST "$API_URL/api/v1/auth/revoke" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"token": "<JWT-TOKEN-SUSPECTED>"}'

# Cách 3: Thêm thẳng token vào Redis blacklist (manual override)
# Lấy jti từ JWT payload (decode base64 phần payload)
$JTI = "abc123-def456"  # Lấy từ JWT payload .jti
kubectl exec -it redis-0 -n his-hope -- redis-cli SETEX "blacklist:jti:$JTI" 604800 "revoked"

# Verify token bị revoked
kubectl exec -it redis-0 -n his-hope -- redis-cli EXISTS "blacklist:jti:$JTI"
# Expected: (integer) 1

# === Rotate service credentials bị ảnh hưởng ===

# 1. Vault AppRole rotation
kubectl exec -it vault-0 -n vault -- vault write -f auth/approle/role/patient-service/secret-id

# 2. Database password rotation
kubectl exec -it vault-0 -n vault -- vault kv patch secret/his-hope/database/patientdb \
  password=$(openssl rand -base64 32)

# 3. JWT signing key rotation (khẩn cấp)
kubectl exec -it vault-0 -n vault -- vault write -f transit/keys/jwt-signing/rotate

# 4. mTLS certificate revocation
kubectl exec -it vault-0 -n vault -- vault write pki_int/revoke \
  serial_number=$(echo "<CERT-SN>")
```

---

## 3. Evidence Collection

### 3.1 Audit Logs

```bash
# === 1. CockroachDB Audit Logs ===
# Tất cả thay đổi PHI được ghi trong audit_log table + triggers (012-audit-triggers.sql)

kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT * FROM patientdb.audit_log
  WHERE action_timestamp > '2026-07-16 10:00:00'
  ORDER BY action_timestamp DESC
  LIMIT 100;
" --format csv > .\evidence\audit_log_patientdb.csv

# === 2. Vault Audit Logs ===
kubectl logs -n vault deploy/vault -l app=vault --tail=1000 > .\evidence\vault_audit.log

# === 3. K8s Audit Logs (nếu enabled) ===
kubectl get events -n his-hope --sort-by=.lastTimestamp > .\evidence\k8s_events.log

# === 4. Application Logs (Kibana export) ===
# Kibana query: service: * AND @timestamp > now-2h
# → Export CSV từ Kibana UI
```

### 3.2 Hubble Flows (Network Traffic)

```bash
# Capture toàn bộ flows từ service bị compromise (1 giờ trước)
$ENV:HUBBLE_SERVER = "localhost:4245"

hubble observe -n his-hope \
  --from-label app=<SERVICE-NAME> \
  --since 1h \
  -o json > .\evidence\hubble_flows_<service>_$(Get-Date -Format yyyyMMddHHmm).json

# Capture flows bị DROPPED (evidence cho security policy hoạt động)
hubble observe -n his-hope \
  --verdict DROPPED \
  --since 1h \
  -o json > .\evidence\hubble_dropped_$(Get-Date -Format yyyyMMddHHmm).json

# Capture flows với full metadata
hubble observe -n his-hope \
  --from-label app=<SERVICE-NAME> \
  --since 1h \
  -o jsonpb > .\evidence\hubble_full_$(Get-Date -Format yyyyMMddHHmm).jsonpb
```

### 3.3 Jaeger Traces

```bash
# Query Jaeger để lấy tất cả traces từ service bị compromise trong time window
# Port-forward Jaeger
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 &

# Query API (thay vì UI)
$JAEGER_URL = "http://localhost:16686/api/traces"
$startTime = [DateTimeOffset]::Parse("2026-07-16T10:00:00Z").ToUnixTimeMilliseconds() * 1000
$endTime = [DateTimeOffset]::Parse("2026-07-16T12:00:00Z").ToUnixTimeMilliseconds() * 1000

Invoke-RestMethod "$JAEGER_URL?service=patient-service&start=$startTime&end=$endTime&limit=1000&lookback=2h" `
  | ConvertTo-Json -Depth 20 > .\evidence\jaeger_traces_patient.json
```

### 3.4 K8s Resource State

```bash
# Dump toàn bộ state của namespace (evidence preservation)
kubectl get all -n his-hope -o yaml > .\evidence\k8s_all_resources.yaml

# Dump ConfigMaps và Secrets (không decode)
kubectl get configmaps -n his-hope -o yaml > .\evidence\k8s_configmaps.yaml
kubectl get secrets -n his-hope -o yaml > .\evidence\k8s_secrets.yaml

# Dump NetworkPolicy state
kubectl get networkpolicies -n his-hope -o yaml > .\evidence\network_policies.yaml

# Dump CiliumNetworkPolicy state
kubectl get ciliumnetworkpolicies -n his-hope -o yaml > .\evidence\cilium_policies.yaml

# Dump Vault state (nếu accessible)
kubectl exec -it vault-0 -n vault -- vault read sys/health -format=json > .\evidence\vault_health.json
```

### 3.5 Evidence Chain of Custody

```bash
# Tạo SHA256 checksums cho tất cả evidence files
Get-ChildItem -Path .\evidence\ -File | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "$hash  $($_.Name)" | Out-File -Append .\evidence\CHAIN_OF_CUSTODY.txt
}

# Timestamp evidence
Get-Date -Format o | Out-File -Append .\evidence\CHAIN_OF_CUSTODY.txt

# Đóng gói evidence
Compress-Archive -Path .\evidence\* -DestinationPath .\evidence\incident-$(Get-Date -Format yyyyMMdd-HHmm)-evidence.zip

# Upload vào secure storage (không để trên máy local)
# aws s3 cp .\evidence\incident-*.zip s3://his-hope-security/incidents/ --sse AES256
```

---

## 4. HIPAA Breach Notification Timeline

### 4.1 HIPAA Breach Notification Rule (45 CFR 164.400-414)

| Mốc thời gian | Hành động | Người chịu trách nhiệm |
|---------------|----------|------------------------|
| **Phát hiện** (T+0) | Bắt đầu điều tra, document thời điểm phát hiện | Security Team |
| **Trong vòng 24h** (T+24h) | Notify Compliance Officer; activate incident response team | Security Team Lead |
| **Trong vòng 60 ngày** (T+60d) | Gửi notification đến affected individuals, HHS Secretary | Compliance Officer + Legal |
| **Nếu >500 patients** (T+60d) | Đồng thời notify prominent media outlets | Legal + PR |
| **Immediately** (T+60d) | Nếu >500 patients → notify HHS đồng thời với patient notification | Compliance Officer |

### 4.2 Breach Risk Assessment

Đánh giá 4 yếu tố để xác định xem có cần notification không:

1. **Nature and extent of PHI involved** — Loại dữ liệu bị lộ (diagnosis, treatment, payment...)
2. **The unauthorized person who used the PHI or to whom the disclosure was made** — Ai đã truy cập
3. **Whether the PHI was actually acquired or viewed** — Có thực sự bị xem/sao chép không
4. **The extent to which the risk to the PHI has been mitigated** — Đã có biện pháp mitigation chưa

Nếu đánh giá cho thấy **low probability PHI has been compromised**, có thể không cần notification. Tuy nhiên, mặc định luôn err on side of notification cho patient safety.

### 4.3 Notification Template (Patients)

```
Subject: Important Notice Regarding Your Health Information

Dear [Patient Name],

We are writing to inform you of a security incident involving [Healthcare Provider Name]
that may have involved your protected health information.

What Happened: [Brief description of incident]
What Information Was Involved: [Types of PHI]
What We Are Doing: [Mitigation steps]
What You Can Do: [Recommended patient actions]
For More Information: [Contact details, credit monitoring if applicable]

We take the privacy and security of your health information very seriously and
deeply regret any concern this may cause you.

Sincerely,
[Privacy Officer / CISO]
His.Hope EMR Security Team
```

---

## 5. Post-Incident Review

### 5.1 Security Postmortem Structure

```markdown
# Security Incident Postmortem: INC-SEC-YYYY-MMDD-NNN

| Field | Value |
|-------|-------|
| **Incident ID** | INC-SEC-YYYY-MMDD-NNN |
| **Classification** | Data Breach / Unauthorized Access / DoS / Malware |
| **Severity** | SEV-S1 / SEV-S2 / SEV-S3 / SEV-S4 |
| **Detection Time** | YYYY-MM-DD HH:MM UTC |
| **Detection Method** | Prometheus Alert / Hubble / Audit Log / Manual Report |
| **Containment Time** | YYYY-MM-DD HH:MM UTC |
| **Resolved Time** | YYYY-MM-DD HH:MM UTC |
| **Services Affected** | [List] |
| **Data Affected** | X ePHI records |
| **Attacker Vector** | [Mô tả] |
| **Incident Commander** | [Name] |

---

## Attack Timeline

| Time (UTC) | Event | Evidence Source |
|------------|-------|----------------|
| 10:15:00 | Unauthorized access attempt detected | Vault audit log |
| 10:15:30 | Failed login (5 attempts) | identity-service log |
| 10:16:00 | Successful login with compromised credentials | identity-service log |
| 10:16:45 | ePHI exfiltration via GET /patients?limit=10000 | Hubble flow + API log |
| 10:18:00 | Alert triggered: UnusualDataAccess | Prometheus → PagerDuty |
| 10:19:00 | On-call acknowledged | PagerDuty |
| 10:20:00 | Network isolation applied | Cilium policy |
| 10:21:00 | Token revoked, user account disabled | identity-service |
| 10:22:00 | All patient-service pods restarted | kubectl |

---

## Root Cause Analysis

**What allowed this to happen?**
[Detailed analysis]

**5 Whys:**
1. Why were credentials compromised? → [Answer]
2. Why wasn't MFA enforced? → [Answer]
3. Why did the rate limit not block brute force? → [Answer]
4. Why wasn't the anomalous data access pattern caught earlier? → [Answer]
5. Why was there no egress filtering on patient data volume? → [Answer]

---

## Containment Effectiveness

- [ ] Network isolation (CiliumNetworkPolicy)
- [ ] Token revocation (Redis blacklist)
- [ ] Credential rotation (Vault)
- [ ] Service restart / redeploy
- [ ] Evidence preserved (audit logs, flows, traces)

---

## HIPAA Impact Assessment

- Patients affected: X
- PHI data types exposed: [List]
- Notification required: Yes / No
- If yes, notification deadline: YYYY-MM-DD (60 days from discovery)
- Breach reported to HHS: Yes / No (date)

---

## Action Items

| # | Action | Owner | Priority | Due | Status |
|---|--------|-------|----------|-----|--------|
| 1 | Enforce MFA for all provider accounts | Identity Team | P0 | 2026-07-20 | [ ] |
| 2 | Implement egress rate limiting on ePHI endpoints | API Team | P0 | 2026-07-18 | [ ] |
| 3 | Add anomaly detection for data exfiltration patterns | Security Team | P1 | 2026-08-01 | [ ] |
| 4 | Review and tighten Cilium egress policies | Platform Team | P1 | 2026-07-25 | [ ] |
| 5 | Conduct security awareness training | All Teams | P2 | 2026-08-15 | [ ] |

---

## Lessons Learned
- [Bài học]

---

**Postmortem Author:** [Name]
**Approved by:** CISO
**Review Date:** YYYY-MM-DD
```

---

## 6. Contact Information Template

### 6.1 Internal Incident Contacts

| Role | Name | Slack | Phone | Email |
|------|------|-------|-------|-------|
| **Incident Commander (IC)** | [Name] | @ic | [Phone] | ic@hishop.com |
| **Security Lead (CISO)** | [Name] | @ciso | [Phone] | ciso@hishop.com |
| **SRE Lead** | [Name] | @sre-lead | [Phone] | sre-lead@hishop.com |
| **Platform Lead** | [Name] | @platform-lead | [Phone] | platform@hishop.com |
| **Backend Lead** | [Name] | @backend-lead | [Phone] | backend@hishop.com |
| **Compliance Officer** | [Name] | @compliance | [Phone] | compliance@hishop.com |
| **Legal Counsel** | [Name] | @legal | [Phone] | legal@hishop.com |
| **PR / Communications** | [Name] | @pr | [Phone] | pr@hishop.com |

### 6.2 External Contacts

| Organization | Purpose | Contact |
|-------------|---------|---------|
| **HHS Office for Civil Rights** | HIPAA breach reporting | https://ocrportal.hhs.gov |
| **Cloud Provider Security (GCP)** | Infrastructure compromise | GCP Support P1 |
| **CockroachDB Enterprise Support** | DB security incident | Enterprise support SLA |
| **HashiCorp Vault Support** | Vault compromise | Enterprise support SLA |
| **Cyber Insurance Provider** | Insurance claim | Policy number: [NUMBER] |
| **Incident Response Retainer** | Forensic investigation | [Firm name + phone] |
| **Local FBI Field Office** | Criminal cyber incident | https://www.fbi.gov/contact-us/field-offices |

### 6.3 Incident Activation

```bash
# Gửi message mẫu đến Slack #his-hope-security-incidents
# (Copy-paste template này, fill in các field)

/announce #his-hope-security-incidents

🚨 SECURITY INCIDENT ACTIVATED 🚨

Incident ID: INC-SEC-YYYYMMDD-NNN
Severity: SEV-[S1/S2/S3/S4]
Classification: [Data Breach / Unauthorized Access / DoS / Malware]
Time Detected: YYYY-MM-DD HH:MM UTC

Brief Description: [What is happening]

Affected Services: [List of services]
Estimated Impact: [X patients, Y services]

Incident Commander: @[IC Name]
PagerDuty: [Incident URL]

ALL TEAMS:
- Do NOT discuss this incident on public channels
- Do NOT modify any affected systems until IC approval
- Preserve all logs and evidence
- Wait for IC instructions

Next update: [Time, typically +30min]
```

---

## 7. Các Biện pháp Phòng ngừa & Hardening References

### 7.1 Security Architecture Files

| File | Purpose |
|------|---------|
| `docs/security/hipaa-compliance.md` | HIPAA compliance mapping đầy đủ |
| `docs/security/hardening-summary.md` | Security hardening checklist |
| `docs/security/cosign-image-signing.md` | Container image signing |
| `docs/adr/006-permission-based-rbac.md` | RBAC authorization design |
| `docs/adr/008-defense-in-depth-network-policies.md` | Cilium + K8s network policies |
| `docs/adr/010-redis-backed-token-management.md` | JWT blacklist architecture |
| `docs/adr/011-image-digest-pinning.md` | Image supply chain security |
| `docs/adr/012-jwt-asymmetric-rsa-signing.md` | JWT RSA key management |
| `k8s/base/network-policies.yaml` | 20+ K8s NetworkPolicies |
| `k8s/linkerd/server-authorization.yaml` | mTLS authorization policies |
| `vault/policies/*.hcl` | Service-specific Vault policies (least privilege) |

### 7.2 Security Monitoring Coverage

| Layer | Tool | What It Detects |
|-------|------|----------------|
| **Network** | Cilium Hubble | Anomalous flows, DROPPED packets, port scans |
| **API** | YARP Rate Limiting | Brute force, excessive requests |
| **Auth** | identity-service logs | Failed login attempts, token anomalies |
| **Data Access** | CockroachDB audit triggers | PHI access logging, unusual query patterns |
| **Infrastructure** | Prometheus Alertmanager | Pod restarts, resource spikes, node anomalies |
| **Secrets** | Vault audit log | Secret access, rotation events, policy violations |
| **Images** | Trivy + Cosign | Vulnerability scan, unsigned images |
| **Dependencies** | SCA (SonarQube) | OSS vulnerability detection |
