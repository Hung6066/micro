# Database Platform Runbook

Runbook này áp dụng cho PostgreSQL service-owned databases, Redis và outbox.
Không coi Docker Compose local là production HA.

## Trước mỗi release

1. Chạy contract gate:

   ```powershell
   ./scripts/verify-database-platform-contract.ps1
   ```

2. Tạo và kiểm tra idempotent migration script cho từng `DbContext` trong
   pipeline. Chỉ migration job được phép có quyền DDL; API production dùng
   `Persistence__RunMigrationsOnStartup=false`.
3. Kiểm tra tổng connection budget:

   ```text
   replicas × Database__MaxPoolSize
     + migration/admin/monitoring reserve
     < PostgreSQL max_connections
   ```

5. Render production manifests và chạy secret gate. Không deploy nếu manifest
   còn password mặc định hoặc placeholder:

   ```powershell
   ./scripts/validate-k8s-production-secrets.ps1
   ```

4. Kiểm tra query plan của endpoint top-N trước/sau migration. Không thêm index
   chỉ vì compile pass; phải có workload hoặc `EXPLAIN (ANALYZE, BUFFERS)`.

## Migration an toàn

- Migration runner khóa bằng PostgreSQL advisory lock theo DbContext; nhiều
  replica có thể khởi động nhưng chỉ một runner thực thi DDL.
- DDL production vẫn nên chạy bằng job riêng, có backup/checkpoint trước đó.
- Nếu migration fail: dừng rollout, giữ bản release trước, chụp log/correlation
  id, kiểm tra `__EFMigrationsHistory`, rồi rollback theo migration đã được
  review. Không dùng `EnsureCreated` trên database production.

## Slow query và connection exhaustion

- Compose bật `pg_stat_statements`, `track_io_timing` và log câu lệnh chậm từ
  500ms. Với volume cũ, tạo extension bằng operations job idempotent.
- Ưu tiên kiểm tra: pool saturation, lock wait, sequential scan, query không
  có facility predicate, pagination không có sort ổn định, và N+1 collection.
- Giảm `Database__MaxPoolSize` trước khi tăng replica nếu tổng budget vượt giới
  hạn DB. Không chữa connection exhaustion bằng cách tăng pool vô hạn.

## Outbox và cache

- Theo dõi tuổi bản ghi outbox lâu nhất, retry count, dead-letter và publish
  latency. Không xóa outbox chưa có chính sách retention/audit.
- Redis hiện là single-node local baseline. Production cần Redis managed/HA,
  TLS, ACL, persistence policy và tách cache không quan trọng khỏi state dùng
  cho session/token/revocation.

## Backup, restore và failover

Admin Manager có màn hình **Database Platform** để xem continuity posture và
gửi yêu cầu backup/restore drill. Đây là control plane; browser không được
nhận credential, KMS key hoặc tự chạy `pg_restore`.

- `GET /api/v1/admin/database-continuity/status`: PITR, encryption, retention và target
  RPO/RTO; storage URI được redact.
- `POST /api/v1/admin/database-continuity/backups`: tạo job backup qua Redis
  worker.
- `POST /api/v1/admin/database-continuity/restore-drills`: chỉ chấp nhận target
  `staging` hoặc `isolated`, bắt buộc xác nhận và restore point ISO-8601.
- `GET /api/v1/admin/database-continuity/audit?page=1&pageSize=20`: lịch sử
  backup/restore để Admin hiển thị theo job, trạng thái, môi trường, actor,
  correlation ID, thời gian, lỗi và kết quả đã redact.
- `DatabaseContinuityService` là service độc lập; executor chỉ chạy khi `DatabaseContinuity:ExecutorPath` là absolute path đã
  được cấp qua deployment/Vault. Khi chưa cấu hình, job phải fail với
  `continuity_executor_not_configured`; không fallback sang shell tùy ý.

### Nơi lưu dữ liệu continuity và audit

Không ghi nội dung dump hoặc dữ liệu bệnh nhân vào bảng audit. Mỗi lớp có một
mục đích và retention riêng:

| Dữ liệu | Nơi lưu | Nội dung |
|---|---|---|
| Job queue/state | Redis (`his-hope:database-continuity:*`) | Job ID, operation, status và tiến độ ngắn hạn |
| Audit bền vững | PostgreSQL `postgres.his_hope_database_continuity_audit` | Operation, target, actor, correlation ID, timestamps, error code và result JSON đã redact |
| Backup payload | `DATABASE_CONTINUITY_STORAGE_URI` hoặc volume `database_continuity_backups` local | PostgreSQL custom dump đã mã hóa từng chunk bằng Vault Transit; kèm manifest/checksum |
| Health/metrics | Prometheus + Alertmanager | Availability Vault/service, backup age, restore failure và RTO |

Audit table được tạo idempotent khi `DatabaseContinuityService` khởi động và
được cập nhật khi job được enqueue, bắt đầu chạy, hoàn tất hoặc thất bại. Quyền
đọc endpoint audit phải giữ ở role Admin; backup payload và Vault key không bao
giờ trả về browser. Local test dùng volume Docker; production phải dùng object
storage bền vững, versioning/retention và Vault HA.

### Provider và fallback

`StorageProvider=auto` chọn adapter theo scheme của `StorageUri`:

```text
# Local mặc định
DATABASE_CONTINUITY_STORAGE_URI=file:///var/lib/his-hope/backups
DATABASE_CONTINUITY_STORAGE_PROVIDER=auto
DATABASE_CONTINUITY_STORAGE_FALLBACK_ENABLED=true

# AWS S3, MinIO, Wasabi, Backblaze hoặc S3-compatible khác
DATABASE_CONTINUITY_STORAGE_URI=s3://his-hope-backups/production
DATABASE_CONTINUITY_STORAGE_PROVIDER=auto
AWS_REGION=ap-southeast-1
AWS_ACCESS_KEY_ID=<inject-at-runtime>
AWS_SECRET_ACCESS_KEY=<inject-at-runtime>
# Chỉ dùng cho S3-compatible endpoint riêng như MinIO
AWS_ENDPOINT_URL=https://minio.example.internal
```

Backup luôn tạo payload tại local trước. Service sau đó đồng bộ `.dump.vault`
và manifest lên provider chính. Restore sẽ tải bản mới nhất về local trước khi
chạy executor. Nếu provider chính lỗi và `StorageFallbackEnabled=true`, job tiếp
tục dùng local, audit ghi `storage_fallback_local`; nếu tắt fallback, job fail để
không che giấu mất kết nối storage. Không đặt credential cloud trong image hoặc
repository; dùng Vault Agent, workload identity hoặc secret injection.

Sau mỗi backup thành công, retention cleanup tự động xóa cả `.dump.vault` và
manifest cũ hơn `RetentionDays` ở provider chính rồi mới xóa bản local tương
ứng. Nếu provider ngoài không truy cập được, local backup được giữ lại và audit
ghi `retention_provider_unavailable_local_retained`; không xóa mù để tránh mất
bản backup duy nhất. PostgreSQL custom dump đã có compression; không nén lại
payload Vault ciphertext vì thường làm tăng chi phí CPU mà không giảm dung lượng.

Mặc định mỗi database luôn giữ lại bản mới nhất (`KeepLastBackupsPerDatabase=1`)
dù bản đó cũ hơn retention window. Có thể tăng lên 2 hoặc 3 khi cần rollback
ngắn hạn:

```text
DATABASE_CONTINUITY_RETENTION_DAYS=30
DATABASE_CONTINUITY_KEEP_LAST_BACKUPS_PER_DATABASE=1
```

Production chỉ đạt khi có evidence thực tế cho:

- backup encryption, retention và PITR;
- restore vào môi trường cô lập với checksum và thời gian thực tế;
- promote/failover PostgreSQL và reconnect toàn bộ service;
- Redis recovery và behavior khi cache mất;
- RPO/RTO của Identity, clinical và billing.

Local Compose hiện đã bật PITR thật:

- PostgreSQL `wal_level=replica`, `archive_mode=on`, `archive_timeout=60s`;
- WAL được archive vào volume `postgres_wal_archive` bằng `archive_command`;
- `DatabaseContinuityService` tạo physical base backup bằng `pg_basebackup`,
  kiểm tra bằng `pg_verifybackup` và giữ WAL/base backup theo retention;
- `DATABASE_CONTINUITY_PITR_ENABLED=true` trong Compose.

Lịch tự động mặc định trong Compose:

- WAL: chạy liên tục qua PostgreSQL `archive_command`; `archive_timeout=60s` chỉ là
  giới hạn tối đa khi chưa đủ WAL segment. Khi segment đầy, PostgreSQL archive ngay.
- Backup thường: mỗi 24 giờ (`DATABASE_CONTINUITY_BACKUP_INTERVAL_HOURS=24`). Job tạo
  logical custom dump đã mã hóa cho toàn bộ database và đồng thời tạo/reuse physical
  base backup; backup chạy ngay sau khi chưa có trạng thái lịch trong Redis.
- Restore-drill: mỗi 168 giờ/7 ngày (`DATABASE_CONTINUITY_RESTORE_DRILL_INTERVAL_HOURS=168`),
  chỉ restore vào môi trường `isolated`, không chạm production.
- Retention mặc định: 30 ngày cho logical backup, base backup và WAL archive; giữ tối
  thiểu bản logical mới nhất của mỗi database (`KeepLastBackupsPerDatabase=1`).

Nếu chạy nhiều replica continuity service, Redis scheduler lock bảo đảm mỗi kỳ chỉ
enqueue một backup và một restore-drill. Có thể thay đổi lịch bằng biến môi trường:

```text
DATABASE_CONTINUITY_SCHEDULER_ENABLED=true
DATABASE_CONTINUITY_BACKUP_INTERVAL_HOURS=24
DATABASE_CONTINUITY_RESTORE_DRILL_INTERVAL_HOURS=168
DATABASE_CONTINUITY_RETENTION_DAYS=30
DATABASE_CONTINUITY_MAX_ATTEMPTS=3
```

Worker dùng Redis Streams consumer group với visibility timeout 5 phút: pending
message được auto-claim khi worker chết; lỗi được retry tối đa
`DATABASE_CONTINUITY_MAX_ATTEMPTS`, sau đó ghi vào stream dead-letter
`his-hope:database-continuity:dead-letter` và audit vẫn giữ trạng thái Failed.
Metrics `last_success_timestamp_seconds` tách riêng backup và restore-drill để
Alertmanager không nhầm một backup thành bằng chứng restore.

Thứ tự khôi phục chuẩn là: chọn thời điểm cần khôi phục → lấy physical base backup
gần nhất trước thời điểm đó → replay WAL liên tục đến `recovery_target_time` → kiểm tra
checksum/schema → khởi động các service phụ thuộc → chạy smoke test và đối soát.
Compose hiện chạy physical WAL replay tới named restore point trong PostgreSQL tạm
của service `postgres-restore-drill`, sau đó mới logical restore từng database vào
target cô lập. Đây là evidence local/staging; production vẫn phải chạy cùng quy
trình trên cluster restore độc lập và object-store WAL/base backup.

Đây là PITR local cho kiểm thử. Production phải thay `archive-wal.sh` bằng
pgBackRest hoặc WAL-G tới object storage mã hóa, có TLS, versioning và quyền
replication riêng; không dùng rule `pg_hba-pitr.conf` mở rộng của Compose.

Container `postgres-replica` trong Compose hiện là database độc lập theo profile,
không có WAL shipping/streaming replication và không được dùng cho failover.
HA production phải chọn managed PostgreSQL hoặc vận hành Patroni/etcd (kèm
fencing, backup và DNS/connection endpoint), sau khi có owner và RPO/RTO được
phê duyệt.

## Evidence bắt buộc

Mỗi release lưu lại: migration script hash, query-plan diff, p95/p99 và error
rate, DB CPU/IOPS/connections/locks, backup age, restore duration, replication
lag, outbox oldest age và quyết định rollback. Build xanh không thay thế các
runtime evidence này.
## Vault Transit trong Docker Compose

Compose có Vault dev mode tại `http://localhost:8200` và continuity service kiểm tra `/v1/sys/health` cùng key `transit/his-hope-backup-encryption`. Đây chỉ là môi trường kiểm thử; không dùng token `root` hoặc dev mode ở production. Production phải dùng Vault HA, TLS và AppRole/Kubernetes auth, policy chỉ cho phép `transit/encrypt`/`transit/decrypt` trên key backup.

Khởi tạo local: `docker compose -f docker/docker-compose.yml up -d vault vault-init database-continuity-service`. Các service khác dùng chung abstraction `His.Hope.Secrets` qua `AddHisHopeVault`; service chỉ được cấp policy/path cần thiết, không đọc root token.

## Bật backup/restore có điều kiện

Chỉ đặt `DATABASE_CONTINUITY_ENABLED=true` sau khi đã cấu hình đủ:

```text
DATABASE_CONTINUITY_ENABLED=true
DATABASE_CONTINUITY_STORAGE_URI=s3://his-hope-backups/production
DATABASE_CONTINUITY_EXECUTOR_PATH=/opt/his-hope/bin/database-continuity-executor.sh
DATABASE_CONTINUITY_EXECUTOR_WORKING_DIRECTORY=/opt/his-hope/bin
DATABASE_CONTINUITY_ENCRYPTION_PROVIDER=vault-transit
VAULT_ADDR=https://vault.example.internal
VAULT_TOKEN=<inject-at-runtime>
```

`ExecutorPath` bắt buộc là absolute path và `StorageUri` không được rỗng. Nếu thiếu executor, storage hoặc Vault Transit key, Admin API không enqueue job và trả `503`; scheduler cũng không tự chạy. Các biến trên phải được inject bằng secret manager/Vault Agent, không commit vào `.env` hoặc image.

Prometheus scrape `database-continuity-service:5800/metrics`; Alertmanager cảnh báo service down, Vault Transit unavailable, restore drill failed và RTO vượt 30 phút. Trạng thái và audit hiển thị trên Admin > Database Platform.

Kiểm tra local sau khi cấu hình:

```text
docker compose -f docker/docker-compose.yml up -d vault vault-init database-continuity-service
docker exec his-hope-database-continuity /opt/his-hope/bin/database-continuity-executor.sh --operation backup --target-environment production
docker exec his-hope-database-continuity /opt/his-hope/bin/database-continuity-executor.sh --operation restore-drill --target-environment isolated
```

Restore drill chỉ tạo database tạm có prefix `his_hope_restore_drill_`, chạy
checksum/validation rồi tự xóa. Nếu còn database prefix này sau khi job đã
`Completed`, đó là điều kiện fail cần cảnh báo và điều tra; tuyệt đối không
restore trực tiếp lên database production trong job kiểm thử.

## Migration một lần trước rollout

Migration production không chạy từ API replica. Pipeline tạo script EF idempotent
cho tám context và ghi `migration-manifest.json` chứa SHA-256 của từng script:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.10
pwsh scripts/generate-database-migration-scripts.ps1 `
  -OutputDirectory artifacts/database-migrations-current
pwsh scripts/validate-database-migration-contract.ps1 `
  -MigrationDirectory artifacts/database-migrations-current `
  -RenderedProductionManifest artifacts/k8s/prod.yaml `
  -OutputPath artifacts/evidence/database-migration-contract.json
```

Artifact chỉ được đưa vào change đã review và chạy bởi migration/deployer
identity có quyền DDL. Trước khi sync API, xác nhận job đã `Completed` đúng một
lần, lưu exit code/manifest hash/RTO và giữ
`Persistence__RunMigrationsOnStartup=false` trên mọi API deployment. Nếu script
phát hiện SQL destructive hoặc thiếu context/hash, gate trả non-zero và rollout
phải dừng để review expand/contract.

Các service EF hỗ trợ cờ one-shot `Persistence:MigrationOnly=true`. Migration
Job GitOps dùng cùng image digest đã review, đặt đồng thời
`Persistence:RunMigrationsOnStartup=true` và `Persistence:MigrationOnly=true`;
process chạy migration qua Vault database lease rồi thoát với exit code thành
công/thất bại. API Deployment production luôn giữ
`Persistence:RunMigrationsOnStartup=false` và không dùng cờ migration-only.
Không truyền password qua command line hoặc ghi credential vào log.

### Production migration hook

Production overlay có bảy Argo `Sync` Job tại wave `20` trong
`k8s/jobs/production-migration-job.yaml` (mỗi service một ServiceAccount và
Vault database role). Mỗi Job có `backoffLimit: 0`, `activeDeadlineSeconds: 900`,
EF advisory lock và chỉ kết thúc sau khi `Persistence:MigrationOnly=true` đã
hoàn tất. Image của Job được Kustomize transform sang digest giống release;
không được sửa Job bằng `kubectl edit` hoặc truyền password trên command line.
Job dùng Sync wave `20`; các Deployment production được gắn wave `30` và
Ingress wave `40`, nên ConfigMap/Secret/StatefulSet hạ tầng được tạo trước Job.

Trước khi cho phép sync production, chạy:

```powershell
kubectl kustomize k8s/overlays/prod --load-restrictor LoadRestrictionsNone `
  > artifacts/k8s/prod.yaml
pwsh scripts/validate-database-migration-contract.ps1 `
  -MigrationDirectory artifacts/database-migrations-current `
  -RenderedProductionManifest artifacts/k8s/prod.yaml `
  -OutputPath artifacts/evidence/database-migration-contract.json
```

Kết quả bắt buộc là `status=pass`, gồm đủ bảy Job digest-pinned và tám
DbContext migration artifact. Nếu Job fail, dừng sync/rollout, giữ nguyên
schema (không rollback destructive SQL), đọc log Job đã được redacted và xử lý
Vault/SPIRE/PostgreSQL trước khi chạy lại qua một commit đã review.
