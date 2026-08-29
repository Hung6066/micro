# Lộ trình nâng cấp operator-mobile theo Manufacturing Service

Ngày cập nhật: 2026-08-29  
Phạm vi: ứng dụng Angular + Capacitor `operator-mobile` và các contract/API Manufacturing Service cần thiết cho thao tác tại hiện trường.

## Mục tiêu và nguyên tắc

Operator-mobile là ứng dụng thực thi công việc tại điểm làm việc, không phải bản thu nhỏ của dashboard quản trị. Mỗi màn hình phải trả lời được bốn câu hỏi: operator được giao việc gì, dữ liệu nào bắt buộc trước khi commit, thao tác đang ở trạng thái nào, và nếu mất mạng thì bản ghi sẽ được đồng bộ ra sao.

Các nguyên tắc bắt buộc:

1. Backend Manufacturing Service là nơi quyết định authorization, tenant/facility boundary, lifecycle và optimistic concurrency.
2. Command có side-effect phải idempotent, có correlation/operation id, audit actor và reason/evidence khi policy yêu cầu.
3. Chỉ Production, Quality và Maintenance được phép tạo queue offline; các màn hình đọc chỉ dùng cache có thời hạn và phải hiển thị trạng thái stale.
4. Feature code chỉ dùng API của operator-mobile và shared frontend/mobile contracts; không import trực tiếp Capacitor hoặc truy cập database.
5. Tất cả text đi qua i18n dictionary; màu, spacing, typography và select dùng token/shared component. Không sửa shared foundation để bù lỗi CSS cục bộ của page.
6. Không coi build xanh là runtime proof. Mỗi phase phải có unit, lint, build, contract và live smoke evidence tương ứng; gate không chạy được phải ghi `environment-blocked`.

## Hiện trạng đã có

| Luồng | Đã có trong operator-mobile | Khoảng trống chính |
|---|---|---|
| Production | Batch Started, ghi operation/sản lượng, QC status, KPI/OEE/exception/cost read model | Lifecycle start/pause/resume/complete, measurement, material/lot consumption, scrap và work queue |
| Quality | Chọn lot/inspection plan, tạo inspection đa chỉ tiêu, tạo sample | Disposition hold/release/reject, deviation tại hiện trường, checklist theo plan |
| Maintenance | Machine health, calibration, telemetry read, PM plan, hoàn tất work order offline | Downtime start/resolve, checklist động, evidence attachment và work assignment |
| Traceability | QR scan, lot genealogy, recall impact, quality/status/inventory history | Offline cache có kiểm soát, lot disposition và thao tác kho được phân quyền |
| Sync | Queue mã hóa, retry, idempotency/conflict status, retry từng bản ghi và stale/cache policy | Dead-letter/retention policy, telemetry đồng bộ và conflict resolution nghiệp vụ có tải lại snapshot |

## Lộ trình triển khai theo phase

### Phase 0 — Contract và runtime trust (P0)

**Mục tiêu:** không còn màn hình rỗng do thiếu context hoặc gọi API sai; xác thực/tenant phải rõ trước khi mở command.

- Chuẩn hóa `401/403/409/422` thành lỗi có mã và thông điệp i18n.
- Hiển thị tenant, operator email/display name, facility và quyền hiện hành; không hiển thị subject UUID thay cho định danh người dùng.
- Work queue read model: batch, inspection/sample và work order được giao cho operator.
- API client bắt buộc truyền tenant context và operation/idempotency key đúng contract.
- Cache read có timestamp, TTL và nhãn stale; không giả vờ dữ liệu realtime khi offline.

**Exit criteria:** authenticated smoke có tenant hợp lệ, request có bearer/session + tenant, endpoint không còn `401` do thiếu context; unauthenticated endpoint vẫn trả `401`; lint không có warning mới.

### Phase 1 — Controlled execution (P0/P1)

**Production**

- Start → InProgress → Pause/Resume → AwaitingQA → Complete.
- Ghi operation measurement theo công đoạn/máy.
- Chọn input lot, actual quantity, output/rework/waste và loss reason.
- Hiển thị mass-balance preview và chặn complete nếu policy thiếu dữ liệu.

**Quality**

- Checklist theo inspection plan version.
- Tạo deviation từ batch/inspection.
- Đưa lot/sample vào Hold, Released hoặc Rejected theo permission.

**Maintenance**

- Nhận work order được giao.
- Checklist bắt buộc theo maintenance plan.
- Bắt đầu/kết thúc downtime; liên kết downtime với batch/operation.

**Exit criteria:** mỗi command có happy path, validation, duplicate retry và optimistic-concurrency test; offline queue đồng bộ đúng một lần về mặt nghiệp vụ.

### Phase 2 — Traceability và field evidence (P1)

- QR/barcode mở lot, batch, machine hoặc work order.
- Lot disposition và inventory transaction theo permission Warehouse/QC.
- Evidence chuẩn: ảnh, ghi chú, instrument/method, timestamp và optional location.
- Forward/backward genealogy có giới hạn depth, pagination và stale indicator.
- Shift handover: unresolved batch, hold lot, downtime và work order quá hạn.

**Exit criteria:** một lot thành phẩm truy ngược được raw lots và truy xuôi được impacted lots; evidence/audit có actor, thời gian, tenant/facility; không lộ dữ liệu tenant khác.

### Phase 3 — Operational intelligence (P2)

- Cảnh báo calibration/expiry/downtime và notification theo ca.
- FEFO/availability read model tại điểm dùng nguyên liệu.
- OEE/yield/cost chỉ hiển thị khi đủ input, nếu thiếu phải ghi `insufficient-data`.
- SOP/versioned instructions theo process step.
- E-signature/second-person approval cho release, deviation close và override equipment restriction.

**Exit criteria:** mọi KPI truy được về fact/event nguồn; UI không cho operator thực hiện approve/release nếu thiếu permission; telemetry không làm chậm command nghiệp vụ.

### Phase 4 — Production hardening (P2)

- Capacitor native camera, secure storage và certificate pinning được kiểm thử trên thiết bị thật.
- Offline encryption key rotation, queue retention/dead-letter và wipe khi logout/device revoke.
- Accessibility, Vietnamese/English parity, light/dark theme và visual regression ở viewport mobile.
- Release gate: dependency audit, lint, build, unit, live Playwright, API contract, security và rollback evidence.

**Exit criteria:** native smoke trên Android/iOS, recovery sau expired session, offline replay/conflict drill, và release artifact có evidence hash.

## Phân quyền command

| Permission | Operator | QC Inspector | QC Supervisor | Maintenance Technician | Production Supervisor | Warehouse |
|---|---:|---:|---:|---:|---:|---:|
| production.operation.record | ✓ |  |  |  | ✓ |  |
| production.batch.lifecycle |  |  |  |  | ✓ |  |
| quality.inspection.record |  | ✓ | ✓ |  |  |  |
| quality.sample.disposition |  |  | ✓ |  |  |  |
| quality.deviation.create | ✓ | ✓ | ✓ |  | ✓ |  |
| maintenance.work-order.complete |  |  |  | ✓ | ✓ |  |
| maintenance.downtime.record | ✓ |  |  | ✓ | ✓ |  |
| inventory.lot.disposition |  |  | ✓ |  |  | ✓ |
| inventory.lot.move |  |  |  |  |  | ✓ |

Permission ở bảng chỉ là UI capability hint; API vẫn là authority cuối cùng.

## Mapping API cần dùng

- Production: `GET /production-orders`, `GET /production-batches`, `POST /production-batches/{id}/{start|pause|resume|complete|cancel}`, `POST /production-batches/{id}/operations`, `POST /production-batches/{id}/measurements`.
- Quality: `GET /quality-samples`, `POST /quality-inspections`, `POST /quality-samples/{id}/disposition`, `POST /production-batches/{id}/deviations`, `POST /lots/{id}/disposition`.
- Maintenance: `GET /maintenance-work-orders`, `POST /machines/{id}/maintenance-work-orders/{workOrderId}/complete`, `POST /machines/{id}/downtimes`, `POST /machines/{id}/downtimes/{downtimeId}/resolve`.
- Traceability/inventory: `GET /lots/{id}/genealogy`, `/recall-impact`, `/status-history`, `/inventory-transactions`, `/traceability/epcis`.

## Verification matrix

| Gate | Phase 0 | Phase 1 | Phase 2 | Phase 3/4 |
|---|---:|---:|---:|---:|
| i18n key/default parity EN/VI | required | required | required | required |
| theme token/light-dark | required | required | required | required |
| typography/font weight | required | required | required | required |
| hh-select/shared contracts | required | required | required | required |
| operator lint/build | required | required | required | required |
| unit/component tests | required | required | required | required |
| API contract/auth/tenant | required | required | required | required |
| offline/idempotency/conflict | baseline | required | required | required |
| live Playwright/native smoke | baseline | required | required | required |

## Quy tắc nghiệm thu

- `PASS`: có command output/test artifact và phạm vi kiểm tra bao phủ requirement.
- `FAIL`: có lỗi tái hiện được hoặc assertion không đạt.
- `SKIPPED`: test không nằm trong phase hiện tại, phải nêu lý do.
- `ENVIRONMENT-BLOCKED`: thiếu service/token/device/cluster; không được báo xanh thay thế.

Tài liệu này là backlog thực thi; không tuyên bố Manufacturing Service hoặc operator-mobile đã hoàn tất các phase chưa có evidence tương ứng.

## Validation delta 2026-08-29

- Phase 0 tài liệu/contract matrix: **PASS** ở mức repository audit; endpoint mapping và quyền đã được đối chiếu với source hiện tại.
- Phase 1 operator batch lifecycle: đã triển khai UI/API client cho `pause`, `resume`, `complete` qua offline queue, giữ `expectedVersion` và operation id. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 1 QC sample disposition: đã triển khai tải sample theo inspection và command `Pending → Accepted|Rejected|Hold` qua offline queue; actor/reason đi cùng payload. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 1 Maintenance downtime: đã triển khai ghi nhận và kết thúc downtime theo machine, tải open downtime và liên kết timestamp/reason. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 1 Maintenance checklist: checklist được dựng từ ghi chú work order (phân tách theo dòng/dấu chấm phẩy), bắt buộc hoàn tất từng mục trước khi đóng lệnh; có fallback checklist cô lập. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 1 operation measurement: đã triển khai ghi nhận measurement theo production batch với `measurementType`, `value`, `uom`, `measuredAt` và source; command chạy qua queue với `expectedVersion`. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 1 loss review: đã triển khai chọn operation có hao hụt và gửi quyết định `Approved|Rejected`, reviewer và notes tới endpoint loss-review qua queue; phù hợp policy supervisor review trước khi complete. **Lint PASS, unit 45/45 PASS, production build PASS; cần live permission/API smoke**.
- Phase 1 deviation: đã triển khai tạo deviation từ Quality page theo batch đang chạy, bắt buộc type/description/impact và gán `requestedBy` từ subject hiện hành. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 2 traceability lot disposition: đã triển khai chọn lot đã mở và ghi `Released|Hold|Rejected|Consumed` kèm lý do/bằng chứng, actor và `expectedUpdatedAt` qua offline queue để tránh ghi đè thay đổi mới hơn. **Lint PASS, unit 45/45 PASS, production build PASS**.
- Phase 2 shift handover: đã thêm màn hình tổng hợp batch đang chạy, lot Hold/Quarantined, downtime mở và work order quá hạn từ các read endpoint có cache/stale policy. **Lint PASS, unit 47/47 PASS, production build PASS; cần API smoke với dữ liệu ca thật**.
- Phase 0 read resilience: đã thêm cache đọc ngắn hạn theo tenant với timestamp/TTL; khi refresh lỗi, màn hình giữ bản đọc gần nhất và hiển thị nhãn stale, không lưu token hoặc PHI.
- Phase 0 error contract: đã chuẩn hóa thông điệp phía client cho `401/403/409/422` theo `status/errorCode`, có bản dịch EN/VI và test utility; lỗi quyền không còn bị hiển thị như lỗi tải dữ liệu chung.
- Phase 2 sync recovery: đã thêm nút đồng bộ thật theo endpoint, retry có chủ đích cho bản ghi `failed/conflict`, và dispatch transport theo command; logout xóa queue cục bộ theo yêu cầu hardening. **Lint PASS, unit 47/47 PASS, production build PASS**.
- Validator lặp lại `scripts/validate-operator-mobile-phases.ps1` đã chạy đầy đủ: source gates P0/P1/P2, queue recovery, shift handover, lint, unit `47/47` và production build đều **PASS**. Evidence: `artifacts/evidence/operator-mobile-phases.json`.
- Audit i18n độc lập `scripts/audit-i18n-keys.ps1`: **112 referenced keys, missing 0**; validator phase hiện ghi nhận **173 mobile keys EN/VI, missing 0**.
- Sau bổ sung cache/error contract, checklist động và sync recovery, unit suite hiện là **47/47 PASS**; production build tiếp tục **PASS**.
- Live authenticated smoke cho các command lifecycle chưa được tuyên bố pass trong delta này; cần session/token có quyền Production Supervisor và batch dữ liệu thật. Nếu thiếu môi trường, ghi `environment-blocked`, không thay bằng build result.
- Live Playwright tại `tests/e2e` với dev server `localhost:4310`: **2/4 PASS** (unauthenticated redirect, login entry); **2/4 ENVIRONMENT-BLOCKED/FAIL** vì tài khoản `admin@hishop.com` bị route guard trả `forbidden` do thiếu permission Manufacturing, nên các request dashboard không được phát sinh. Không hạ guard để làm xanh test; cần seed/assign đúng permission và backend healthy rồi chạy lại.
- Shared foundation chỉ nhận bổ sung key i18n EN/VI cần thiết; không thay đổi styling/behavior của `hh-select` hoặc mobile layout để sửa riêng page operator.
