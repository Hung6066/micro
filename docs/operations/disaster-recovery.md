# Kế hoạch Disaster Recovery — His.Hope EMR

> **Tài liệu:** OPS-DR-001
> **Version:** 1.0
> **Audience:** SRE, Platform Engineer, Security Officer
> **Cập nhật:** 2026-07-16

---

## 1. Mục tiêu RPO/RTO

| Metric | Target | Ý nghĩa |
|--------|--------|---------|
| **RPO** (Recovery Point Objective) | 60 giây | Dữ liệu mất mát tối đa 60s, đảm bảo bởi CockroachDB changefeeds và backup incremental hàng giờ |
| **RTO** (Recovery Time Objective) | 30 giây mỗi service | Service khôi phục trong 30s nhờ Linkerd auto-failover, K8s auto-restart, multi-region active-active |
| **RTO Full Cluster** | 15 phút | Toàn bộ cluster khôi phục trong 15 phút từ disaster toàn vùng |
| **MTTR** (Mean Time To Recover) | < 5 phút | Thời gian trung bình khắc phục sự cố nhờ auto-remediation engine |

### 1.1 Kiến trúc High Availability

His.Hope được triển khai **active-active** trên 3 regions:

```
Region: us-east1 (Primary)    Region: europe-west1        Region: asia-east1
┌──────────────────────┐    ┌──────────────────────┐    ┌──────────────────────┐
│ CockroachDB: 2 voters│◄──►│ CockroachDB: 2 voters│◄──►│ CockroachDB: 1 voter │
│ Services: 3-5 pods   │    │ Services: 3 pods      │    │ Services: 2 pods     │
│ Redis: 2 shards      │    │ Redis: 2 shards       │    │ Redis: 2 shards      │
│ RabbitMQ: 3 nodes    │    │ RabbitMQ: 3 nodes     │    │ RabbitMQ: 3 nodes    │
└──────────────────────┘    └──────────────────────┘    └──────────────────────┘
         │                          │                          │
         └──────────────────────────┼──────────────────────────┘
                                    │
                          ┌─────────▼─────────┐
                          │  Global HTTPS LB  │
                          │  + Cloud Armor WAF│
                          │  + CloudFlare CDN │
                          └───────────────────┘
```

---

## 2. Chiến lược Backup

### 2.1 CockroachDB Backups

| Loại Backup | Tần suất | Retention | Storage | Config |
|-------------|----------|-----------|---------|--------|
| **Full Backup** | Daily (00:00 UTC) | 30 ngày | S3 (his-hope-backups bucket) | `cockroach/config/backup-cronjob.yaml` |
| **Incremental Backup** | Hourly | 7 ngày | S3 (his-hope-backups/hourly) | CockroachDB built-in incremental |
| **Changefeeds** | Real-time | 72 giờ | Cold storage (Cloud Storage) | CockroachDB CDC → Pub/Sub → Cold Storage |
| **WAL Archive** | Continuous | 7 ngày | S3 (his-hope-backups/wal) | CockroachDB WAL archival |

#### Cấu hình Backup CronJob (K8s)

```bash
# Áp dụng backup cronjob
kubectl apply -f cockroach/config/backup-cronjob.yaml
kubectl apply -f cockroach/config/backup-config.yaml

# Kiểm tra backup job schedule
kubectl get cronjob -n his-hope cockroachdb-backup

# Kiểm tra backup history
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "SHOW BACKUP;"

# Trigger backup thủ công ngay lập tức
kubectl create job --from=cronjob/cockroachdb-backup manual-backup-$(Get-Date -Format yyyyMMddHHmmss) -n his-hope
```

#### Full Backup Command (Manual)

```bash
# Backup toàn bộ cluster
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  BACKUP INTO 's3://his-hope-backups/full/$(Get-Date -Format yyyy-MM-dd)?AUTH=implicit'
  AS OF SYSTEM TIME '-1m';
"

# Backup từng database
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  BACKUP DATABASE patientdb INTO 's3://his-hope-backups/patientdb/full?AUTH=implicit';
  BACKUP DATABASE identitydb INTO 's3://his-hope-backups/identitydb/full?AUTH=implicit';
  BACKUP DATABASE clinicaldb INTO 's3://his-hope-backups/clinicaldb/full?AUTH=implicit';
  BACKUP DATABASE appointmentdb INTO 's3://his-hope-backups/appointmentdb/full?AUTH=implicit';
"

# Kiểm tra backup files trong S3
aws s3 ls s3://his-hope-backups/full/ --recursive
```

#### Incremental Backup

```bash
# Tạo incremental backup dựa trên full backup gần nhất
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  BACKUP INTO LATEST IN 's3://his-hope-backups/full?AUTH=implicit'
  AS OF SYSTEM TIME '-1m';
"
```

#### Changefeed to Cold Storage

```bash
# Tạo changefeed replica đến BigQuery cho analytics + cold storage
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  CREATE CHANGEFEED FOR TABLE patientdb.*, clinicaldb.*, identitydb.*
  INTO 'kafka://pubsub.googleapis.com:443/projects/his-hope/topics/cockroach-cdc'
  WITH updated, resolved='15s', min_checkpoint_frequency='30s';
"

# Kiểm tra changefeed status
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "SHOW CHANGEFEED JOBS;"
```

### 2.2 Redis Backup

```bash
# Trigger RDB snapshot trên tất cả Redis nodes
kubectl exec -it redis-0 -n his-hope -- redis-cli BGSAVE
kubectl exec -it redis-1 -n his-hope -- redis-cli BGSAVE
kubectl exec -it redis-2 -n his-hope -- redis-cli BGSAVE

# Kiểm tra snapshot status
kubectl exec -it redis-0 -n his-hope -- redis-cli LASTSAVE

# Backup được lưu trong PVC: /data/dump.rdb và /data/appendonly.aof
# PVC được snapshot bởi CSI driver hàng ngày
```

### 2.3 Vault Backup

```bash
# Vault Raft snapshot backup (chạy trên Vault leader)
kubectl exec -it vault-0 -n vault -- vault operator raft snapshot save /vault/data/vault-raft.snap

# Copy snapshot ra ngoài cluster
kubectl cp vault/vault-0:/vault/data/vault-raft.snap ./backups/vault-raft-$(Get-Date -Format yyyyMMdd).snap

# Lưu vào S3
aws s3 cp ./backups/vault-raft-$(Get-Date -Format yyyyMMdd).snap s3://his-hope-backups/vault/
```

### 2.4 K8s Resource Backup (Velero)

```bash
# Backup toàn bộ namespace his-hope
velero backup create his-hope-daily \
  --include-namespaces his-hope,vault,monitoring \
  --include-resources "*" \
  --storage-location aws \
  --ttl 720h

# Kiểm tra backup status
velero backup describe his-hope-daily

# Lên lịch backup tự động
velero schedule create his-hope-daily-schedule \
  --schedule="0 2 * * *" \
  --include-namespaces his-hope,vault,monitoring \
  --ttl 720h
```

---

## 3. Restore Procedures

### 3.1 Full Cluster Restore (Toàn bộ CockroachDB)

**Trường hợp sử dụng:** Toàn bộ CockroachDB cluster bị corrupt hoặc tất cả regions down.

```bash
# === Bước 1: Xác nhận backup gần nhất ===
aws s3 ls s3://his-hope-backups/full/ | Select-Object -Last 5

# === Bước 2: Dừng tất cả services (tránh write conflig) ===
kubectl scale deployment --all --replicas=0 -n his-hope

# === Bước 3: Restore databases từ backup ===
# Xóa databases hiện tại (nếu corrupt)
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  DROP DATABASE IF EXISTS patientdb CASCADE;
  DROP DATABASE IF EXISTS identitydb CASCADE;
  DROP DATABASE IF EXISTS clinicaldb CASCADE;
  DROP DATABASE IF EXISTS appointmentdb CASCADE;
  DROP DATABASE IF EXISTS labdb CASCADE;
  DROP DATABASE IF EXISTS billingdb CASCADE;
  DROP DATABASE IF EXISTS pharmacydb CASCADE;
"

# Restore từng database từ full backup
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  RESTORE DATABASE patientdb FROM LATEST IN 's3://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE identitydb FROM LATEST IN 's3://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE clinicaldb FROM LATEST IN 's3://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE appointmentdb FROM LATEST IN 's3://his-hope-backups/full?AUTH=implicit';
"

# === Bước 4: Chạy lại migrations để đảm bảo schema đúng ===
kubectl apply -f cockroach/config/migration-job.yaml
kubectl wait job/migration-job -n his-hope --for=condition=Complete --timeout=600s

# === Bước 5: Scale services trở lại ===
kubectl apply -k k8s/overlays/prod/

# === Bước 6: Verify ===
kubectl get pods -n his-hope
linkerd viz stat deploy -n his-hope
```

### 3.2 Single Database Restore

**Trường hợp sử dụng:** Một database (vd: patientdb) bị corrupt hoặc xóa nhầm.

```bash
# Restore chỉ patientdb từ backup gần nhất
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  DROP DATABASE IF EXISTS patientdb CASCADE;
  RESTORE DATABASE patientdb FROM LATEST IN 's3://his-hope-backups/patientdb?AUTH=implicit';
  GRANT ALL ON DATABASE patientdb TO patient_user;
  ALTER DATABASE patientdb CONFIGURE ZONE USING
    constraints = '{\"+us-east1=2,+europe-west1=1,+asia-east1=1}';
"

# Restart patient-service để reconnect
kubectl rollout restart deployment/his-hope-patient-service -n his-hope

# Verify
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  USE patientdb; SELECT count(*) FROM \"Patients\";
"
```

### 3.3 Point-in-Time Recovery (PITR)

**Trường hợp sử dụng:** Khôi phục về một thời điểm cụ thể trước khi data corruption xảy ra.

```bash
# Xác định thời điểm corruption (từ audit log, alerts)
# Ví dụ: lỗi phát hiện lúc 2026-07-15T14:30:00Z
# Restore về thời điểm 5 phút trước đó

$PITR_TIME = "2026-07-15T14:25:00Z"

kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  RESTORE DATABASE patientdb
  FROM 's3://his-hope-backups/full/2026-07-15?AUTH=implicit'
  AS OF SYSTEM TIME '${PITR_TIME}';
"

# So sánh dữ liệu sau PITR
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  USE patientdb; SELECT id, first_name, last_name, updated_at FROM \"Patients\" ORDER BY updated_at DESC LIMIT 10;
"
```

### 3.4 Vault Disaster Recovery

**Trường hợp sử dụng:** Vault cluster không thể unseal hoặc tất cả nodes down.

```bash
# === Bước 1: Xác minh vault-credentials files đã backup ===
ls -la vault-credentials/
# Required: init.json, root_token.txt, unseal_keys.txt

# === Bước 2: Init lại Vault cluster ===
kubectl delete statefulset vault -n vault --cascade=orphan
kubectl delete pvc -l app=vault -n vault

# Tái tạo Vault cluster
kubectl apply -f k8s/vault/vault-statefulset.yaml
kubectl wait pod -l app=vault -n vault --for=condition=Ready --timeout=300s

# === Bước 3: Init với Shamir shares cũ nếu còn ===
# Cách A: Nếu còn unseal keys
bash vault/init.sh  # Script sẽ skip init nếu vault đã initialized

# Cách B: Nếu mất unseal keys, init mới hoàn toàn
vault operator init -key-shares=5 -key-threshold=3 -format=json > vault-credentials/init-new.json

# === Bước 4: Restore từ Raft snapshot ===
kubectl cp ./backups/vault-raft-20260715.snap vault/vault-0:/tmp/restore.snap

kubectl exec -it vault-0 -n vault -- vault operator raft snapshot restore /tmp/restore.snap \
  -force

# === Bước 5: Unseal + Verify ===
# Unseal với 3/5 keys
kubectl exec -it vault-0 -n vault -- vault operator unseal <UNSEAL_KEY_1>
kubectl exec -it vault-0 -n vault -- vault operator unseal <UNSEAL_KEY_2>
kubectl exec -it vault-0 -n vault -- vault operator unseal <UNSEAL_KEY_3>

# Verify secrets intact
kubectl exec -it vault-0 -n vault -- vault kv list secret/his-hope/

# === Bước 6: Re-seed secrets nếu cần ===
bash vault/seeds.sh
```

#### Vault Unseal Key Management

| Item | Custodian | Storage |
|------|-----------|---------|
| **Unseal Key 1** | CISO | Physical safe #1 |
| **Unseal Key 2** | VP Engineering | Physical safe #2 |
| **Unseal Key 3** | Lead SRE | Physical safe #3 |
| **Unseal Key 4** | Security Architect | Physical safe #4 |
| **Unseal Key 5** | CTO | Physical safe #5 |
| **Root Token** | CISO (escrow) | Hardware HSM |
| **Recovery Keys** | N/A (Shamir seal, not auto-unseal) | — |

Shamir configuration: **5 shares, threshold 3**. Cần ít nhất 3/5 key custodians để unseal Vault.

---

## 4. Multi-Region Failover

### 4.1 Active-Active Architecture

His.Hope vận hành active-active trên 3 regions. Traffic được route qua Global Load Balancer:

```
       User Request
            │
   ┌────────▼────────┐
   │  Global HTTPS LB │
   │  (Cloud DNS +    │
   │   Cloud Armor)   │
   └──┬─────────┬─────┘
      │         │           │
   ┌──▼──┐  ┌──▼──┐   ┌───▼───┐
   │us-  │  │eu-  │   │asia-  │
   │east1│  │west1│   │east1  │
   │ 40% │  │ 35% │   │ 25%   │
   └─────┘  └─────┘   └───────┘
```

### 4.2 Automatic Failover (Region Down)

Khi một region down, Global Load Balancer tự động reroute traffic:

```bash
# Kiểm tra health của mỗi region endpoint
gcloud compute health-checks list --filter="name~his-hope"

# Mô phỏng failover test
# 1. Kiểm tra current traffic distribution
linkerd multicluster gateways -n linkerd-multicluster

# 2. Scale down 1 region (mô phỏng outage)
kubectl scale deployment --all --replicas=0 -n his-hope --context=his-hope-asia-east1

# 3. Verify traffic tự động chuyển sang 2 regions còn lại
# (Không có downtime — active-active, LB tự detect unhealthy)

# 4. Verify CockroachDB vẫn operational
kubectl exec -it cockroachdb-0 -n his-hope --context=his-hope-us-east1 -- \
  cockroach node status --insecure
# Expected: 3-4 nodes (region asia-east1 đã biến mất khỏi topology)

# 5. Restore region
kubectl apply -k k8s/overlays/prod/ --context=his-hope-asia-east1
```

### 4.3 Manual Cross-Region Failover

```bash
# === Force failover từ us-east1 sang europe-west1 ===

# 1. Drain traffic từ us-east1
# Cập nhật Global LB để chuyển 100% traffic đến europe-west1
kubectl apply -f k8s/multi-region/global-loadbalancer.yaml --context=his-hope-europe-west1

# 2. Verify service health ở region mới
kubectl get pods -n his-hope --context=his-hope-europe-west1
linkerd viz stat deploy -n his-hope --context=his-hope-europe-west1

# 3. Linkerd multicluster service mirroring (để cross-region gọi được)
kubectl get svc -n his-hope -l multicluster.linkerd.io/exported=true

# 4. CockroachDB reparent nếu cần
# (Thường tự động — Raft consensus tự chọn leader mới khi old leader down)
kubectl exec -it cockroachdb-0 -n his-hope --context=his-hope-europe-west1 -- \
  cockroach node status --insecure
# Expected: europe-west1 là gateway node với 5 nodes visible
```

### 4.4 Linkerd Multicluster Failover

```bash
# Kiểm tra multicluster connectivity
linkerd multicluster check

# List exported services từ mỗi cluster
kubectl get svc -n his-hope -l multicluster.linkerd.io/exported=true

# Test cross-cluster gRPC call
kubectl exec -it deploy/patient-service -n his-hope --context=his-hope-us-east1 -- \
  grpcurl -authority patient-service.his-hope.svc.cluster.local:5006 \
  patient-service-cluster-europe-west1:5006 patient.PatientGrpcService/CheckPatientExists
```

---

## 5. Data Corruption Recovery

### 5.1 Phát hiện Corruption

```bash
# 1. Kiểm tra checksum — CockroachDB built-in
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT * FROM crdb_internal.invalid_objects;
"

# 2. Kiểm tra consistency — SCRUB command
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  EXPERIMENTAL SCRUB DATABASE patientdb;
"

# 3. Audit log — tìm anomalous patterns
# Kibana query: @timestamp > now-1h AND service: patient-service AND action: (DELETE OR UPDATE)
# Và cross-reference với user đang login
```

### 5.2 Recovery Steps cho Data Corruption

```bash
# === Scenario: Bảng Patients bị corrupt do application bug ===

# 1. Xác định phạm vi corruption (thời gian, bảng, rows affected)
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT count(*) FROM patientdb.\"Patients\" WHERE updated_at > '2026-07-15 14:00:00';
"

# 2. Nếu chỉ một vài rows → manual fix từ audit log
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT * FROM patientdb.\"OutboxMessages\" WHERE \"Type\" LIKE '%Patient%' ORDER BY \"OccurredOn\" DESC LIMIT 20;
"

# 3. Nếu corruption lan rộng → PITR (xem Section 3.3)

# 4. Nếu toàn bộ database → Full Restore (xem Section 3.2)

# 5. Sau khi restore, verify với checksum
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  EXPERIMENTAL SCRUB TABLE patientdb.\"Patients\";
"
```

---

## 6. Communication Plan & Escalation Matrix

### 6.1 Mức độ Incident

| Mức độ | Định nghĩa | Ví dụ |
|--------|-----------|-------|
| **SEV 1** | Toàn bộ hệ thống down, data loss, ảnh hưởng bệnh nhân | CockroachDB cluster crash, toàn bộ regions down |
| **SEV 2** | Một service/region down, ảnh hưởng một phần | Patient service down, Redis cluster partition |
| **SEV 3** | Degraded performance, non-critical failure | p99 latency tăng, circuit breaker mở |

### 6.2 Escalation Path

```
SEV 1: SRE On-call (5 min) → SRE Lead (10 min) → CTO (15 min) → CEO (30 min)
SEV 2: SRE On-call (10 min) → SRE Lead (30 min) → Engineering Manager (60 min)
SEV 3: SRE On-call (30 min) → SRE Lead (2 hours, next business day)
```

### 6.3 Notification Channels

| Kênh | Purpose |
|------|---------|
| **PagerDuty** | Automated alert → On-call SRE phone call |
| **Slack #his-hope-incidents** | Real-time incident coordination |
| **Slack #his-hope-alerts** | Automated alert mirror |
| **Slack #his-hope-announcements** | Stakeholder communication |
| **Email his-hope-sre@** | Incident report, postmortem distribution |
| **Status Page (status.hishop.com)** | External customer communication |

### 6.4 Communication Template (Initial Alert)

```
🚨 SEV-{N}: {Short description}
Time: {UTC timestamp}
Impact: {X}% of users affected, {Y} region(s) down
Services affected: {patient-service, identity-service, ...}
Incident Commander: {Name} (Slack: @{handle}, Phone: {number})
Status: Investigating | Mitigating | Resolved
Link: {PagerDuty incident URL}
```

### 6.5 Stakeholder Update Cadence

- **SEV 1:** Cập nhật mỗi 15 phút
- **SEV 2:** Cập nhật mỗi 30 phút
- **SEV 3:** Cập nhật mỗi 2 giờ

---

## 7. DR Test Schedule

### 7.1 Lịch Test Định kỳ

| Loại Test | Tần suất | Mô tả | Thời gian dự kiến |
|-----------|----------|-------|-------------------|
| **Backup Verification** | Weekly | Restore backup vào cluster staging, verify data integrity | 2 giờ |
| **Single Service DR** | Monthly | Failover 1 service giữa các regions | 30 phút |
| **Region Failover** | Quarterly | Full region failover (active → standby) | 4 giờ |
| **Full DR Simulation** | Quarterly | Toàn bộ cluster restore từ backup trong môi trường isolated | 8 giờ |
| **Chaos GameDay** | Weekly | 14 chaos experiments chạy thường xuyên, full chaos monthly | 1-10 giờ |

### 7.2 Full DR Test Procedure (Quarterly)

```bash
# === Chuẩn bị (Day -1) ===
# 1. Notify tất cả teams qua Slack #his-hope-chaos
# 2. Verify backup gần nhất success
# 3. Tạo isolated staging environment
kubectl create namespace his-hope-dr-test

# === Test Day ===
# 4. Simulate full cluster outage
kubectl scale deployment --all --replicas=0 -n his-hope

# 5. Start stopwatch → Measure RTO
$startTime = Get-Date

# 6. Restore CockroachDB từ backup mới nhất
kubectl apply -f cockroach/config/migration-job.yaml -n his-hope-dr-test
# ... (follow Section 3.1)

# 7. Restore services
kubectl apply -k k8s/overlays/prod/ -n his-hope-dr-test
kubectl wait deployment --all --for=condition=Available -n his-hope-dr-test --timeout=600s

# 8. Verify
linkerd viz stat deploy -n his-hope-dr-test
# Run smoke test suite

# 9. Stop stopwatch
$endTime = Get-Date
$rto = $endTime - $startTime
Write-Host "RTO achieved: $($rto.TotalSeconds) seconds"

# === Post-Test ===
# 10. Cleanup DR environment
kubectl delete namespace his-hope-dr-test

# 11. Document kết quả
# RTO target: 900s (15 min)
# RTO actual: ___s
# RPO actual: ___s (check data freshness)
# Issues found: ___
# Action items: ___

# 12. Postmortem trong 48h
```

### 7.3 Backup Verification (Weekly)

```bash
# Tạo cluster staging để verify backup
kubectl apply -f cockroach/config/cockroachdb-statefulset.yaml -n his-hope-staging

# Restore backup gần nhất
kubectl exec -it cockroachdb-0 -n his-hope-staging -- cockroach sql --insecure -e "
  RESTORE DATABASE patientdb FROM LATEST IN 's3://his-hope-backups/full?AUTH=implicit';
"

# Verify: count rows, check recent data, run integrity checks
kubectl exec -it cockroachdb-0 -n his-hope-staging -- cockroach sql --insecure -e "
  USE patientdb;
  SELECT count(*) AS patient_count FROM \"Patients\";
  SELECT max(updated_at) AS latest_update FROM \"Patients\";
  EXPERIMENTAL SCRUB DATABASE patientdb;
"

# Cleanup
kubectl delete namespace his-hope-staging
```

