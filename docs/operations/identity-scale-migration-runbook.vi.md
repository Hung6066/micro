# Runbook scale Identity theo từng nấc

Tài liệu này là quy trình bắt buộc khi số user, token và audit tăng mạnh. Mục
tiêu là mở rộng mà không đổi contract đột ngột, không chạy migration tranh chấp
và không biến việc partition thành một lần nâng cấp phá vỡ dữ liệu.

## 0. Invariant không được phá

- `asp_net_users`, role, claim, assignment và OpenIddict giữ nguyên khóa và
  contract trong suốt một release; không đổi tên/drop trực tiếp.
- API runtime không chạy migration. Migration job dùng advisory lock và quyền
  database riêng.
- Mỗi service sở hữu database của mình; Identity không đọc database nghiệp vụ.
- Thay đổi lớn dùng expand/contract: thêm cấu trúc, dual-read/dual-write có
  kiểm soát, backfill theo batch có checkpoint, chuyển đọc, rồi mới thu hồi cũ
  sau ít nhất một chu kỳ phát hành.

## 1. Trước khi chạm schema

1. Chạy `scripts/inspect-identity-scale-readiness.ps1` với
   `IDENTITY_DATABASE_CONNECTION_STRING` hoặc `-ConnectionStringFile`; lưu JSON
   vào evidence của release.
   Mặc định validator tính 3 replica × pool 20 + 20 kết nối reserved; thay đổi
   bằng `-ServiceReplicaCount`, `-PoolMaxPerReplica` và `-ReservedConnections`
   theo capacity review. Gate fail nếu tổng budget không còn thấp hơn
   `max_connections`.
2. Chụp row count, kích thước bảng/index, dead tuple, lock/deadlock, p95 query,
   pool wait, replication lag và backup age.
   Snapshot scale-readiness cũng ghi nhận bảng ứng viên đã là partitioned hay
   chưa; trạng thái `warning` ở đây chỉ mở capacity review, không tự động chạy
   DDL trên production. Snapshot phải bao gồm cả các bảng quan hệ tăng theo user
   (`asp_net_user_claims`, `asp_net_user_roles`, `asp_net_user_logins`, MFA,
   password history) và OpenIddict authorization/token; thiếu bất kỳ bảng nào là
   schema-drift và phải dừng rollout.
3. Chạy `npm run validate:identity-migrations`,
   `scripts/validate-database-migration-contract.ps1` và drift check trên bản
   sao độc lập. Không triển khai nếu migration safety hoặc drift fail.
   Migration dry-run phải chạy artifact trên database rỗng và chạy lại lần hai
   trên cùng schema; lần chạy thứ hai bắt buộc thành công để chứng minh
   idempotency.
4. Xác định capacity budget: tổng kết nối = tổng `(replica × maxPool)` cộng
   migration, monitoring và failover. Giữ headroom tối thiểu theo SLO đã duyệt;
   không tự ý tăng pool ở từng service.

## 2. Nấc mở rộng theo dữ liệu

### Dưới ngưỡng vận hành

Giữ bảng giao dịch hiện tại, tối ưu query bằng projection/AsNoTracking/keyset,
đặt retention cho token/outbox/audit và dùng index theo query shape. BRIN cho
bảng log theo thời gian là additive; vẫn giữ B-tree cho lookup chính xác.
Retention worker phải có `MaxRowsPerRun` và `BatchSize` bounded; backlog được
chia qua nhiều chu kỳ để không giữ lock hoặc chiếm writer liên tục sau outage.

Các endpoint danh sách phải giới hạn `PageSize` ở mức platform và giới hạn số
trang offset sâu; truy vấn phải có tie-breaker ổn định bằng khóa chính sau cột
sort. Khi cần duyệt sâu hơn, bổ sung cursor/keyset contract thay vì nới vô hạn
`OFFSET`, vì offset lớn vẫn phải quét và bỏ qua nhiều dòng dù đã có index.

### Bảng log đạt ngưỡng lớn

Tạo bảng archive/partition mới bằng migration additive. Dùng `CREATE TABLE ...
LIKE`, tạo partition theo tháng hoặc quý, copy theo khoảng khóa/thời gian có
checkpoint, đối soát count/checksum, sau đó chuyển writer trong một cửa sổ ngắn.
Không partition trực tiếp `asp_net_users` chỉ vì row count: khóa/unique/FK và
đường đăng nhập phải được benchmark và thiết kế lại trước.

Mọi migration có `PARTITION BY` hoặc `PARTITION OF` phải có comment kiểm soát
`-- partition-approved: <table>` cho từng bảng append-only được capacity review
phê duyệt. Guard `validate-identity-migration-safety.ps1` sẽ từ chối partition
user/relationship tables hoặc partition DDL không có marker này.

### Vượt khả năng một PostgreSQL writer

Đầu tiên thêm read replica/read model cho truy vấn báo cáo và dashboard có thể
chấp nhận lag. Sau đó dùng PgBouncer/PgCat, giới hạn pool và kiểm thử failover.
Chỉ khi một writer vẫn không đạt SLO mới thiết kế shard theo tenant/hash với
directory định tuyến; shard key phải xuất hiện trong mọi command, migration và
audit trail. Không chia shard bằng cách để service tự đoán database.

## 3. Expand/contract và rollback

- Expand: thêm cột/index/table nullable, deploy code tương thích cả schema cũ và
  mới. Index lớn cần chạy `CONCURRENTLY` bằng migration job SQL riêng ngoài
  EF idempotent artifact (EF tạo `DO` block, PostgreSQL không cho concurrent
  index trong block); không chèn SQL concurrent trực tiếp vào artifact.
- Backfill: batch nhỏ, checkpoint bền vững, giới hạn lock/throttle, metric tiến độ
  và dead-letter cho bản ghi lỗi.
- Contract: chỉ sau đối soát và một release ổn định mới bỏ code/schema cũ; tạo
  rollback migration đã thử trên bản restore, không dùng `DROP TABLE` để rollback.
- Nếu cutover lỗi: dừng contract, quay lại read cũ, giữ dữ liệu mới để đối soát;
  phục hồi bằng PITR/backup khi cần.

## 4. Gates trước production

Phải có đủ: migration dry-run + checksum, drift sạch, load 50/200/500 VU,
query-plan baseline, pool saturation, failover reconnect, PITR restore độc lập,
backup encryption/retention và dashboard cảnh báo. Thiếu runtime evidence phải
ghi `environment-blocked`, không gọi là production-ready.

## 5. Lịch review

Mỗi release lưu JSON capacity snapshot và quyết định nấc tiếp theo. Khi một
trong các chỉ báo vượt ngưỡng đã duyệt (row count, table size, p95, pool wait,
replication lag, disk hoặc outbox age), mở capacity review trước khi tăng
replica hoặc đổi schema.
