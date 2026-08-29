# Database và Storage Security Hardening Runbook

Cập nhật: 2026-08-29  
Phạm vi: Identity, PostgreSQL/CockroachDB, Redis, object storage, backup và
audit storage trong His.Hope.

Tài liệu này là kế hoạch implementation, deployment và validation. Nó không
tuyên bố “bảo mật tuyệt đối”; mục tiêu là defense-in-depth, fail-closed và có
bằng chứng phục hồi được kiểm chứng.

Tham chiếu:

- [Database lifecycle standard](../architecture/database-lifecycle-standard.vi.md)
- [Database platform roadmap](../architecture/database-platform-roadmap.vi.md)
- [Full production backup matrix](full-production-backup-matrix.vi.md)
- [CNPG/Azure/MinIO backup strategy](cnpg-azure-minio-backup-strategy.vi.md)
- [Production security operations checklist](../security/production-security-ops-checklist.md)
- [Identity control-plane status](../integration/identity-control-plane-implementation-status.vi.md)
- [Manufacturing implementation and validation report](manufacturing-platform-implementation-validation.vi.md)
- [Production storage attestation template](production-storage-attestation-template.vi.md)

## 1. Mục tiêu và nguyên tắc không thương lượng

1. Database và storage không public; chỉ workload identity được allow-list mới
   được kết nối.
2. Runtime application không dùng `postgres`, không có `SUPERUSER`,
   `CREATEROLE`, `CREATEDB` hoặc quyền migration.
3. Mọi secret, encryption key, certificate và backup credential lấy từ Vault/KMS
   hoặc secret manager; không truyền qua command line và không ghi log.
4. Encryption in transit bắt buộc; encryption at rest và backup encryption dùng
   key riêng theo purpose.
5. Tenant/facility boundary phải được enforce ở backend/database; UI hoặc query
   parameter không phải security boundary.
6. Backup chỉ được coi là đạt khi restore được vào môi trường cô lập và verify
   checksum, schema, row count và application smoke.
7. Audit phải append-only, có retention, tamper evidence và cảnh báo khi delivery
   bị trễ hoặc mất dữ liệu.

## 2. Mô hình quyền database

Mỗi database/service cần tối thiểu các role sau:

| Role | Mục đích | Quyền production |
|---|---|---|
| `<service>_runtime` | Application runtime | Chỉ schema/table cần thiết; không DDL, không role management |
| `<service>_migrator` | Migration job | Chỉ tồn tại trong migration job; không cấp cho deployment thường |
| `<service>_backup` | Backup/PITR | Read/replication/WAL scope cần thiết; không sửa dữ liệu nghiệp vụ |
| `audit_writer` | Audit sink | Chỉ append vào audit schema/table hoặc queue |
| `security_readonly` | Incident investigation | Read-only, JIT, ticket và audit bắt buộc |

Không dùng một credential cho runtime, migration và backup. Identity database,
domain database và audit database phải có boundary riêng; external tenant
database phải có placement/credential riêng.

### 2.1 PostgreSQL/Cockroach implementation

Production deployment phải thực hiện theo thứ tự:

1. Tạo admin credential trong Vault/KMS.
2. Tạo runtime/migrator/backup roles bằng migration init job.
3. Cấp database/schema grants tối thiểu.
4. Bật TLS với CA/certificate riêng; yêu cầu `sslmode=verify-full` hoặc cơ chế
   tương đương.
5. Cấu hình `pg_hba`/network policy chỉ cho service CIDR và replication CIDR.
6. Tắt hoặc rotate bootstrap/admin credential sau provisioning.
7. Kiểm tra quyền bằng negative test: runtime không được `CREATE TABLE`, đọc
   database khác, tạo role, đọc secret hoặc xóa backup.

Các artefact repository liên quan:

- `docker/docker-compose.identity-production.yml`
- `docker/postgres-migrator-init.sh`
- `docker/postgres-workload-identity-init.sh`
- `docker/pg_hba-pitr.conf`
- `k8s/base/postgres.yaml`
- `k8s/production-ha/`
- `scripts/configure-vault-postgres.ps1`
- `scripts/verify-database-platform-contract.ps1`

Development Compose có thể dùng credential local để test, nhưng không được
promotion. Các fallback như `POSTGRES_PASSWORD=postgres` và user `postgres`
phải bị chặn bởi production validator/deployment policy.

### 2.2 Data-level protection

- Dùng RLS hoặc view/security-definer function cho tenant/facility-sensitive
  data; test cross-tenant đọc/ghi đều phải nhận deny.
- Dữ liệu PHI/PII/credential recovery nên có application envelope encryption:
  DEK theo object/record, KEK trong KMS/Vault.
- Không đưa PHI vào URL, log, exception, metric label, Redis key hoặc backup
  filename.
- Migration additive/staged; không drop/rename trực tiếp khi chưa có backup,
  rollback và compatibility window.

## 3. Redis/session storage

Redis là storage nhạy cảm, không phải cache vô hại. Production phải:

- private network và TLS;
- ACL user riêng theo workload;
- TTL bắt buộc cho session, nonce, challenge, revocation và rate-limit key;
- không lưu raw access/refresh token nếu protected session/encryption đủ dùng;
- tách prefix/ACL cho session, revocation, queue và cache;
- không dùng Redis làm nguồn sự thật cho permission hoặc tenant boundary;
- revoke session/token ngay khi disable user, đổi quyền, MFA reset hoặc phát hiện
  refresh-token reuse.

Validation:

```powershell
rtk docker compose -f docker/docker-compose.yml ps redis
rtk powershell -NoProfile -Command "docker exec his-hope-redis redis-cli ACL LIST"
rtk powershell -NoProfile -Command "docker exec his-hope-redis redis-cli CONFIG GET requirepass"
```

> Chỉ chạy các lệnh kiểm tra trực tiếp với production context đã được phê duyệt;
> không in password hoặc token ra terminal/log.

## 4. Object storage và file storage

### 4.1 Bucket policy

Tách bucket/prefix theo sensitivity:

- `public-derived-assets`: chỉ chứa bản đã sanitize/resize;
- `private-business-documents`: tài liệu nội bộ;
- `phi-pii`: dữ liệu nhạy cảm, access JIT/audited;
- `audit-worm`: append-only/WORM;
- `backup`: immutable, cross-account/cross-region.

Mọi bucket private-by-default, block public ACL/policy, versioning bật, object
lock/retention bật cho audit và backup. Frontend không bao giờ nhận access key;
download dùng short-lived signed URL do backend cấp.

### 4.2 Upload/download controls

- Giới hạn byte size, MIME, extension và magic bytes.
- Đổi tên bằng UUID; không dùng path/name từ user.
- Scan malware trước khi publish.
- Sanitize HTML/SVG; không render user content bằng bypass sanitizer.
- Kiểm soát `Content-Type`, `Content-Disposition` và cache header ở backend.
- Audit actor, tenant, object id, action, result và correlation id; không log
  signed URL đầy đủ hoặc secret.
- Encryption server-side bằng KMS key riêng; dữ liệu nhạy cảm thêm envelope
  encryption ở application layer.

Repository phải chọn đúng provider production (Azure Blob/S3-compatible/CNPG
object store theo platform contract); local filesystem fallback chỉ dành cho
development hoặc test cô lập và không được xem là HA/DR evidence.

Manifest production hiện đã bắt buộc Secret `backup/minio-tls`, mount certificate
read-only cho MinIO và `mc`, dùng HTTPS cho traffic nội bộ, tạo bucket bằng
`mc mb --with-lock`, bật versioning và đặt retention `COMPLIANCE 30d`. Secret
certificate phải do CA nội bộ/KMS quản lý; không commit material vào repository.

## 5. Backup, PITR và restore

### 5.1 Thiết kế bắt buộc

- Full/incremental backup mã hóa trước khi rời cluster.
- WAL/PITR archive liên tục và có cảnh báo khi lag.
- Backup account/storage tách khỏi production account.
- Immutable retention/object lock; không cho application xóa backup.
- Ít nhất một bản copy khác region hoặc failure domain.
- Manifest, checksum, encryption key version và retention metadata được lưu cùng
  evidence, không chứa plaintext secret.
- Restore vào namespace/database cô lập; không restore thử ghi đè production.

Mục tiêu tham khảo:

| Workload | RPO | RTO | Drill |
|---|---:|---:|---|
| Identity/audit | ≤ 5 phút | ≤ 30 phút | Hàng tháng |
| Commerce/Manufacturing | ≤ 15 phút | ≤ 60 phút | Hàng tháng |
| Media/object storage | Theo versioning | ≤ 4 giờ | Hàng quý |

### 5.2 Lệnh validation repository

```powershell
rtk powershell -File scripts/validate-storage-backup-contract.ps1 -StaticOnly
rtk powershell -File scripts/validate-database-storage-security-contract.ps1
rtk powershell -File scripts/tests/database-storage-security-contract.Tests.ps1
rtk python scripts/validate-azure-blob-retention.py --env-file <protected-azure-env> --minimum-days 30
rtk python -m unittest scripts/tests/test_azure_blob_retention.py
rtk powershell -File scripts/configure-azure-blob-immutability.ps1 -AccountName <account> -ContainerName <container>
rtk kubectl kustomize --load-restrictor LoadRestrictionsNone k8s/overlays/prod-shared-storage
rtk powershell -File scripts/verify-production-backup-restore.ps1
rtk powershell -File scripts/verify-full-production-backup.ps1
rtk powershell -File scripts/validate-cnpg-backup-platform.ps1
rtk powershell -File scripts/validate-dr-evidence.ps1
rtk powershell -File scripts/validate-shared-storage-contract.ps1 -Kubeconfig <production-kubeconfig>
rtk python scripts/validate-storage-host-audit-contract.py
```

Một lệnh validator pass chỉ chứng minh contract/static invariant. Release gate
chỉ đạt khi có thêm restore artifact, thời gian restore, checksum verification,
row/schema verification và người phê duyệt.

`validate-azure-blob-retention.py` là runtime gate bổ sung cho Azure: nó dùng
protected environment file, không in SAS, và chỉ pass khi container immutability
policy ở mode `Locked` với retention tối thiểu 30 ngày. Script đã được nối vào
CNPG bootstrap và production go-live workflow.

## 6. Network, workload identity và Kubernetes

- Database/storage/Vault chỉ expose private endpoint.
- K8s NetworkPolicy default-deny ingress/egress, chỉ mở flow cần thiết.
- Dùng SPIFFE/SPIRE hoặc workload identity; Vault dynamic DB credentials với
  lease ngắn và tự revoke.
- Không dùng static Vault token trong production.
- Pod security restricted, read-only root filesystem nếu tương thích, drop Linux
  capabilities, seccomp mặc định, non-root.
- Không mount cả secret directory nếu workload chỉ cần một key.
- StorageClass/volume encryption phải được provider xác nhận, không chỉ dựa vào
  tên class.
- Database exporter chỉ được lấy metric tối thiểu; không expose query result,
  credential hoặc sensitive labels.

Artefact kiểm tra:

```powershell
rtk powershell -File scripts/validate-k8s-production-secrets.ps1
rtk powershell -File scripts/validate-linkerd-spire-mtls-k3s.ps1
rtk powershell -File scripts/validate-k3s-host-security-contract.py
rtk powershell -File scripts/validate-shared-platform-boundaries.ps1
rtk powershell -File scripts/validate-production-vault-secrets.ps1
```

## 7. Audit, detection và response

Audit tối thiểu cho database/storage:

- login/logout và failed login;
- grant/revoke/role/schema/migration;
- read/export/delete dữ liệu nhạy cảm;
- object upload/download/delete/share;
- backup/restore/PITR;
- Vault/KMS key access/rotation/failure;
- RLS/cross-tenant denial;
- session/token revoke và refresh-token reuse.

Log không được chứa password, token, recovery code, secret, private key, signed
URL nguyên vẹn hoặc PHI không cần thiết.

Alert/P1 auto-response:

- khóa hoặc step-up khi credential abuse;
- revoke token family khi reuse;
- cảnh báo backup/WAL lag;
- quarantine object malware;
- block public bucket/policy drift;
- tạo incident khi audit outbox/DLQ vượt SLA;
- evidence gap phải làm release fail-closed.

Validation:

```powershell
rtk powershell -File scripts/run-audit-siem-tamper-drill.ps1
rtk powershell -File scripts/validate-observability-production.ps1
rtk powershell -File scripts/verify-independent-security-evidence.ps1
```

External SIEM/WORM receiver, retention và tamper drill phải có artifact từ hệ
thống thật; local outbox test không thay thế được evidence này.

## 8. Deployment sequence

### Phase 0 — Inventory và freeze

1. Phân loại từng database/table/bucket/object prefix: public, internal,
   confidential, PHI/PII, credential hoặc audit.
2. Liệt kê service account, grant, secret reference, backup target và owner.
3. Freeze destructive migration, permission broadening và bucket public policy.
4. Snapshot/backup có checksum trước mọi thay đổi.

### Phase 1 — Secrets và identity

1. Provision Vault/KMS/HSM key hierarchy.
2. Bật dynamic DB credentials và mTLS.
3. Tách runtime/migrator/backup roles.
4. Xóa production fallback/static token; rotate credential cũ.
5. Bật production fail-closed validators.

Gate này đã được nối vào `.github/workflows/k3s-devsecops-gate.yml` và
`.github/workflows/k3s-production-go-live-gate.yml`. Vì vậy PR hoặc go-live sẽ
không thể âm thầm promote khi provider storage, KMS, TLS hoặc WORM mới chỉ là
placeholder; step phải trả về `PASS` sau khi evidence production được cung cấp.

### Phase 2 — Data/storage boundary

1. Apply grants/RLS/security views.
2. Private hóa bucket, bật versioning/object lock/KMS.
3. Bật Redis TLS/ACL/TTL.
4. Apply NetworkPolicy, private endpoint và workload identity.

### Phase 3 — Backup/observability

1. Enable encrypted WAL/PITR và immutable backup.
2. Configure cross-region/cross-account copy.
3. Connect durable audit tới SIEM/WORM.
4. Configure alert, DLQ/replay và auto-response.

### Phase 4 — Verify rồi promote

1. Static validators.
2. Build/test/security scan.
3. Negative authorization and isolation tests.
4. Restore drill và application smoke trên bản restore.
5. Canary deployment.
6. Review evidence và approval dual-control.
7. Promote digest đã ký; monitor rollback window.

## 9. Validation matrix và release decision

| Gate | Bằng chứng bắt buộc | Quyết định |
|---|---|---|
| Secret hygiene | Secret scan working tree + history; Vault references; không static production token | Fail nếu còn secret hoặc fallback production |
| DB least privilege | Export grants + negative SQL tests + role separation | Fail nếu runtime có admin/DDL |
| TLS/network | TLS verification, NetworkPolicy, private endpoint evidence | Fail nếu DB/storage public |
| Tenant isolation | Cross-tenant/facility read-write deny tests | Fail nếu leakage hoặc chỉ dựa UI |
| At-rest encryption | Provider/KMS evidence và key version | Unverified nếu chỉ có manifest |
| Object storage | Private bucket, signed URL, malware/content tests | Fail nếu public hoặc unrestricted upload |
| Backup/PITR | Encrypted immutable backup + restore artifact + checksum | Fail nếu chỉ kiểm tra file tồn tại |
| Audit/SIEM | Durable delivery, tamper drill, alert/contain evidence | Unverified nếu external receiver chưa có |
| Rotation/revoke | DB/key/credential rotation và revoke test | Fail nếu token/session còn sống sau revoke |
| DR | RPO/RTO measurement và approved restore drill | Unverified nếu chưa chạy thật |

Release chỉ được **PASS** khi không có gate `FAIL` và các gate `UNVERIFIED`
được chấp thuận rõ bởi owner/risk acceptance có expiry. “Container healthy”,
“build pass” hoặc “backup file tồn tại” không đủ để promote.

## 10. Trạng thái baseline hiện tại

Đã có trong repository: Vault integration và production secret references,
PostgreSQL TLS production compose, WAL/PITR/restore scripts, CNPG/object-store
manifests, Longhorn/Velero contracts, database lifecycle/persistence validators,
durable audit/Security Signal Outbox và WORM/SIEM configuration contracts.

Cần xác nhận bằng runtime evidence trước production: mọi service đã chuyển khỏi
`postgres`/admin grants; RLS phủ đủ bảng nhạy cảm; object storage đang
private-by-default; KMS encryption và key rotation thật; cross-region immutable
backup; restore RPO/RTO; external SIEM/WORM delivery; và signed production
attestation.

Local development giữ fallback credential để phục vụ test hiện tại. Các fallback
này không phải production baseline và phải bị chặn bởi deployment policy trước
khi dùng `production` overlay.

Promotion gate mới `scripts/validate-database-storage-security-contract.ps1` đã
được chạy trên source hiện tại. Kết quả là **BLOCKED**: Vault secret provider,
static-token prohibition, runtime NetworkPolicy và MinIO TLS/Object Lock contract
đạt; PostgreSQL/Redis/RabbitMQ dùng external CSI nhưng chưa có provider evidence,
Azure backup destination còn placeholder. Đây là kết quả đúng theo nguyên tắc
fail-closed, không phải lỗi cần che giấu bằng cách hạ mức kiểm tra. Chỉ chuyển
sang **PASS** sau khi thay provider thật, cấu hình encryption/KMS, private TLS
endpoint, immutable retention và chạy lại validator cùng runtime evidence.

Azure CLI read-only evidence cũng xác nhận các container `his-hope` và `epi`
hiện chưa có immutability policy hoặc immutable versioning; encryption scope
đang cho phép override. Đây là evidence production storage chưa đạt WORM/KMS
hardening và cần storage owner áp dụng protected change.

Không bật immutable versioning ngược trên container đang chứa backup bằng một
thay đổi nóng. Quy trình an toàn là provision container đích mới với immutable
versioning và private access, migrate/copy có checksum, chạy restore/PITR drill,
đổi CNPG destination qua approval, rồi giữ container cũ đến hết retention window.

Runtime evidence mới nhất trên máy phát triển (2026-08-29): Azure Blob access
**FAIL** vì SAS trong protected env đã hết hạn; immutable-retention check trả
HTTP 400. Production kubeconfig không có tại các đường dẫn local đã kiểm tra,
nên chưa thể chạy CSI/restore/DR gates. Các container local PostgreSQL, Redis,
Database Continuity và Vault đều healthy; tuy nhiên PostgreSQL local báo `ssl=off`
và chỉ là development evidence, không được dùng để kết luận production TLS.

Application validation cùng phiên: Manufacturing/Commerce/Content backend build
đều **PASS** (0 errors/0 warnings); Manufacturing application 20/20 tests,
Commerce integration 13/13, Manufacturing integration 56/56 và Content
integration 8/8 **PASS**. Content integration cần chạy với
`DATABASE_CONTENT_URL` lấy từ dev runtime contract, trỏ `localhost:5433`;
không ghi giá trị credential vào tài liệu và không dùng fallback này cho production.

Kubernetes live evidence: context hiện tại `kubernetes-admin@kubernetes` bị
timeout khi gọi API server và không có k3d production container đang chạy.
Do đó CSI, VolumeSnapshot, restore, mTLS và DR chưa được kiểm chứng runtime.

## 11. Rollback và sự cố

- Không rollback bằng cách cấp lại global admin hoặc bật public bucket.
- Với migration: dùng backward-compatible migration/feature flag, restore vào
  cô lập và promote dữ liệu có kiểm soát.
- Với credential leak: revoke/rotate secret, revoke session/token family, block
  workload, preserve audit và mở incident.
- Với backup corruption: dừng promotion, chuyển sang bản immutable gần nhất,
  verify checksum và thông báo RPO impact.
- Với key compromise: disable key version, activate replacement key, re-encrypt
  theo batch, giữ old key chỉ để decrypt dữ liệu hợp lệ trong recovery window.

Mọi rollback phải có ticket, requester/approver khác nhau, timestamp, scope,
evidence trước/sau và review sau sự cố.
