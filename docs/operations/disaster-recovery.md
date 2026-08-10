# Kế hoạch Disaster Recovery — His.Hope EMR

> **Tài liệu:** OPS-DR-001
> **Version:** 2.0
> **Audience:** SRE, Platform Engineer, Security Officer
> **Cập nhật:** 2026-07-23
>
> **Trạng thái:** Các lệnh CockroachDB/GCS/GKE bên dưới là tài liệu legacy và
> không được dùng cho production K3s hiện tại. Profile authoritative hiện hành
> nằm trong phần 0 và các runbook/workflow được liên kết ở đó.

---

## 0. Profile DR production hiện hành (authoritative)

| Thành phần | Cơ chế hiện hành | Bằng chứng bắt buộc |
|------------|------------------|---------------------|
| K3s control-plane | 3 server độc lập, etcd snapshot qua Azure | `control-plane-rebuild-drill.json` |
| PostgreSQL | CloudNativePG + Barman plugin tới Azure Blob | `database-restore-drill.json` |
| Secrets | Vault recovery/unseal và rotation không lộ giá trị | `vault-recovery-drill.json` |
| Harbor | Pull image digest từ clean node | `harbor-clean-node-test.json` |
| PVC/stateful data | CSI replicated storage + VolumeSnapshot restore | `longhorn-snapshot-restore.json` |
| Application | Restore → migration → auth/API smoke test | `application-restore-smoke.json` |

Các giá trị Azure chỉ được đọc từ `azure-production.env` trong protected
workflow; không commit hoặc ghi vào evidence. Mỗi evidence phải có
`status=pass`, `restoreVerified=true`, `target`, `rpoMinutes`, `rtoMinutes` và
`executedAtUtc`. Validator fail-closed:

```powershell
pwsh scripts/validate-dr-evidence.ps1 `
  -EvidenceDirectory artifacts/evidence `
  -OutputPath artifacts/evidence/production-dr-evidence.json
pwsh scripts/validate-storage-backup-contract.ps1 `
  -SecureEnvFile D:\secure\his-hope\azure-production.env `
  -RequireSecureEnv `
  -OutputPath artifacts/evidence/production-storage-backup.json
```

Bootstrap/restore không chạy trực tiếp từ build CI. Dùng protected workflows:

- `.github/workflows/cnpg-azure-backup-bootstrap.yml`
- `.github/workflows/cnpg-restore-drill.yml`
- `.github/workflows/vault-recovery-drill.yml`
- `.github/workflows/longhorn-storage-bootstrap.yml`
- `.github/workflows/k3s-production-go-live-gate.yml`

Mọi thao tác `apply` hoặc restore production cần change approval và target cô
lập; không tạo evidence giả để làm xanh gate.

### CNPG restore drill hiện hành

Dùng `cnpg-restore-drill.yml` với một manifest CNPG đã review, namespace mới
không trùng `spire`/`his-hope`, và `apply=true` trong protected environment.

`scripts/test-cnpg-restore-drill.ps1` chỉ apply manifest vào namespace cô lập,
chờ cluster healthy, chạy `SELECT 1` trên primary, ghi RPO/RTO redacted rồi
xóa namespace restore. Không dùng workflow này để restore in-place hoặc ghi
secret vào evidence. Dry-run là mặc định.

### Vault recovery drill hiện hành

`vault-recovery-drill.yml` chỉ restart một member của Vault StatefulSet trong
protected environment rồi kiểm tra `initialized=true` và `sealed=false` bằng
Vault CLI qua TLS nội bộ. Drill xác minh Raft member recovery và Azure Key
Vault auto-unseal, không in recovery key/token, và không thay đổi secret data.
Production yêu cầu `apply=true` cùng change approval.

---

## 1. Mục tiêu RPO / RTO

| Metric | Target | Cơ chế đảm bảo |
|--------|--------|-----------------|
| **RPO** (Recovery Point Objective) | 5 phút | CockroachDB CDC real-time → GCS, incremental backup mỗi 6 giờ |
| **RTO** (Recovery Time Objective) | 30 phút | Full stack: DB restore + K8s deploy + health verify |
| **RTO Single Service** | 5 phút | K8s auto-healing (probes), Linkerd circuit breaker |
| **RTO Single Zone** | 0 giây | CockroachDB multi-zone voting, Linkerd cross-zone failover |

---

## 2. Chiến lược Backup

| Thành phần | Cơ chế | Tần suất | Retention |
|------------|--------|----------|-----------|
| **CockroachDB Full** | `BACKUP INTO` → GCS `his-hope-backups/full/` | Hàng ngày, 02:00 UTC+7 | 30 ngày |
| **CockroachDB Incremental** | CockroachDB built-in incremental | Mỗi 6 giờ | 7 ngày |
| **CockroachDB CDC** | Changefeed → Pub/Sub → GCS cold storage | Real-time | 72 giờ |
| **K8s Manifests** | Git (GitHub repo) | Mỗi commit | Vĩnh viễn |
| **Secrets** | Vault Raft snapshot (sealed, auto-replicate) | Hàng ngày | 30 ngày |
| **Container Images** | GHCR (immutable, image digest pinning) | Mỗi build | Vĩnh viễn |

**Verify backup:**
```bash
gsutil ls -l gs://his-hope-backups/full/ | Select-Object -Last 5
```

---

## 3. Kịch bản DR & Quy Trình

### a. Single Service Failure
**Auto-remediate:** K8s probes → restart, Linkerd circuit breaker → ngăn retry storm.
**Manual (nếu cần):**
```bash
kubectl rollout restart deployment/<svc> -n his-hope
linkerd viz stat deploy/<svc> -n his-hope
```

### b. Single Node Failure
**Auto-remediate:** GKE node auto-repair (detect unhealthy → drain → recreate VM).
**Manual:**
```bash
kubectl drain <node> --ignore-daemonsets --delete-emptydir-data
kubectl get nodes
```

### c. Single Zone Failure
**Auto-remediate:** CockroachDB Raft tự động bầu leader mới; K8s reschedule pods sang zone khác.
**Không cần can thiệp thủ công.** Theo dõi:
```bash
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach node status --insecure
```

### d. Full Region Failure (us-east1)
**DNS failover sang europe-west1:**
```bash
gcloud dns record-sets update hishope.com --type=A --ttl=60 \
  --rrdatas=<europe-west1-lb-ip> --zone=his-hope-production

# Verify CockroachDB: europe-west1 + asia-east1 đủ voters
kubectl exec -it cockroachdb-0 -n his-hope --context=his-hope-europe-west1 -- \
  cockroach node status --insecure
# Expected: 3+ nodes live

# Verify services
linkerd viz stat deploy -n his-hope --context=his-hope-europe-west1
```

### e. Database Corruption
**Full database restore:**
```bash
# Xác định phạm vi corruption
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  EXPERIMENTAL SCRUB DATABASE <db-name>;
"

# Dừng services → restore → scale lại
kubectl scale deployment --all --replicas=0 -n his-hope
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  DROP DATABASE IF EXISTS <db-name> CASCADE;
  RESTORE DATABASE <db-name> FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
"
kubectl apply -k k8s/overlays/prod/
```

**Point-in-Time Recovery (PITR — corruption một phần):**
```bash
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  RESTORE DATABASE <db-name> FROM 'gs://his-hope-backups/full/YYYY-MM-DD?AUTH=implicit'
  AS OF SYSTEM TIME 'YYYY-MM-DD HH:MM:SS+07';
"
```

### f. Complete Cluster Loss (Zero-State Recovery)
```bash
# 1. Provision infrastructure
cd terraform/prod && terraform apply -var-file=production.tfvars

# 2. Bootstrap ArgoCD & sync applications
kubectl apply -k k8s/bootstrap/argocd/
argocd app sync his-hope-infra && argocd app sync his-hope-services

# 3. Restore tất cả 7 databases
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  RESTORE DATABASE patientdb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE identitydb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE clinicaldb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE appointmentdb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE labdb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE billingdb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
  RESTORE DATABASE pharmacydb FROM LATEST IN 'gs://his-hope-backups/full?AUTH=implicit';
"

# 4. Chạy migrations, verify
kubectl apply -f cockroach/config/migration-job.yaml
linkerd viz stat deploy -n his-hope
```

---

## 4. Lịch Test DR

| Loại Test | Tần suất | Mô tả |
|-----------|----------|-------|
| **Service Restore Test** | Hàng tháng | Restore 1 service + DB từ backup vào staging, verify data integrity |
| **Database Restore Drill** | Hàng quý | Restore toàn bộ 7 databases, verify checksum + row counts |
| **Region Failover Exercise** | 6 tháng/lần | DNS failover us-east1 → europe-west1, verify full stack |
| **Zero-State Rebuild** | Hàng năm | Terraform apply + ArgoCD sync + DB restore, từ đầu |

**Sau mỗi test — điền báo cáo:**
```
Ngày: YYYY-MM-DD | RPO thực tế: ___ phút | RTO thực tế: ___ phút
Thành công: ... | Thất bại/vấn đề: ... | Action items: ...
```

---

## 5. Liên Hệ Khẩn Cấp

| Nhà cung cấp / Vai trò | Kênh | SLA |
|------------------------|------|-----|
| **GCP Support** | P1 ticket qua Console + hotline | 15 phút response |
| **CockroachDB Enterprise** | `support@cockroachlabs.com` + Slack shared channel | 1 giờ response |
| **SRE On-Call** | PagerDuty | 5 phút acknowledge |
| **SRE Lead** | Slack `@sre-lead` | 30 phút (SEV1 escalation) |
| **CTO** | Slack `@cto` | 1 giờ (war room declared) |

### Escalation Path
```
SEV 1: On-call (5 phút) → SRE Lead (10 phút) → CTO (15 phút) → CEO (30 phút)
SEV 2: On-call (10 phút) → SRE Lead (30 phút) → EM (60 phút)
SEV 3: On-call (30 phút) → SRE Lead (2 giờ, giờ hành chính)
```
