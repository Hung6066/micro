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

## Đánh giá bổ sung theo Manufacturing Service

Không nên đưa toàn bộ endpoint của Manufacturing Service vào mobile. Mobile chỉ nhận các capability cần quyết định hoặc ghi nhận ngay tại hiện trường; CRUD master data, costing, forecasting, ML và phê duyệt nhiều bước vẫn thuộc operator-app/web.

| Ưu tiên | Tính năng nên thêm vào operator-mobile | Contract/service đã có | Lý do vận hành và giới hạn |
|---|---|---|---|
| P0 | Work queue theo operator/facility, production order và batch detail | `GET /production-orders`, `/production-batches`, `/maintenance-work-orders`, `/quality-samples` | Không mở màn hình rỗng; phải lọc tenant/facility và assignment trước khi cho commit. |
| P0 | Auth/session diagnostics và tenant context | OIDC/BFF session, tenant-scoped Manufacturing endpoints | Hiển thị email/display name, tenant và quyền; 401/403 phải hướng dẫn sửa session/quyền, không retry mù. |
| P1 | Material issue/consumption và FEFO availability | `/products/{sku}/availability`, `/products/{sku}/fefo`, lot reservations/inventory transactions | Ghi nhận đúng input lot, tránh dùng nhầm lot hết hạn; chỉ offline khi contract idempotent và có permission Warehouse. |
| P1 | Mass-balance, good/rework/waste và loss approval | batch operations, measurements, loss-review, transformations | Chặn hoàn tất khi thiếu output/loss reason hoặc chưa được supervisor review. |
| P1 | QC plan/spec theo version và deviation/CAPA follow-up | `/inspection-plan-versions`, `/product-specifications`, `/deviations`, `/capas` | Checklist phải lấy từ version Approved; operator tạo deviation nhưng không tự approve/close. |
| P1 | Equipment readiness trước khi start | `/machines`, `/machines/{id}/calibrations`, `/machines/{id}/telemetry`, machine health | Cảnh báo calibration quá hạn, machine unavailable và downtime đang mở trước khi ghi operation. |
| P2 | Lot genealogy, recall impact và disposition evidence | `/lots/{id}/genealogy`, `/recall-impact`, `/status-history`, `/lots/{id}/disposition` | Hỗ trợ truy vết tại hiện trường; giới hạn depth/pagination và không tải dữ liệu tenant khác. |
| P2 | Shift handover và notification inbox | dashboard exceptions, notification contract hiện có | Tổng hợp việc chưa giải quyết, hold lot, downtime, overdue WO; push chỉ deep-link tới route hợp lệ. |
| P2 | SOP/instruction artifact có version và acknowledgment | Manufacturing Service đã có `sop-artifacts` với version, checksum, effective window, lifecycle approve/retire và acknowledgment theo operator | Mobile chỉ hiển thị artifact Approved/effective; xác nhận là online-only và idempotent theo operator. |
| P3 | Native evidence, e-signature và second-person approval | `NativeCapabilityService`; `business-signatures` yêu cầu permission và lưu hash bất biến; approval endpoints của QC/recipe/spec | Camera/reference và server-side business signature đã có source/contract; certificate pinning và device smoke vẫn là release gate. |

### Không ưu tiên đưa lên mobile

Các endpoint dashboard/cost projection, sales forecast/actual, ML dataset, supplier/RFQ/PO CRUD, recipe lifecycle approval và master-data CRUD nên giữ ở operator-app/web. Mobile chỉ nên nhận read-only summary hoặc deep-link khi có yêu cầu ca sản xuất; tránh biến thiết bị hiện trường thành màn hình quản trị.

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

## Completion audit hiện tại

| Phase | Phạm vi | Bằng chứng hiện có | Trạng thái | Việc còn thiếu để đóng phase |
|---|---|---|---|---|
| P0 | tenant/session, API error, read cache, i18n/theme/font/select boundary | source audit, validator, i18n audit, backend tenant-boundary tests | **PASS ở repository; runtime auth chưa đủ** | authenticated smoke với bearer/session và tenant thật trên `localhost:4310` |
| P1 | production/QC/maintenance commands, checklist, idempotency | validator, unit 49/49, Manufacturing Application 20/20, Integration 56/58 | **PASS ở source/contract** | replay/conflict drill với permission và dữ liệu ca thật |
| P2 | traceability, disposition, evidence, handover, queue recovery | validator, unit/build/lint, integration tenant-boundary | **PASS ở source/contract** | native camera smoke và authenticated API smoke |
| P3 | notification, recipe context, second-person deviation review | validator, `ComplianceEndpoints`, `ManufacturingCompliance` và shared contracts | **PARTIAL** | certificate pinning/key rotation; authenticated/device smoke |
| P4 | retention/dead-letter, queue wipe, device/release hardening | validator và unit queue; Android `:app:assembleDebug` tạo APK debug thành công | **PARTIAL** | certificate pin release hash, key rotation drill, cài/chạy Android/iOS smoke, rollback evidence |

`PASS ở repository/contract` không đồng nghĩa production-ready. Chỉ chuyển sang `PASS` đầy đủ khi cột việc còn thiếu có artifact/runtime output tương ứng. Native MFA/passkey hiện có là cơ chế xác thực thiết bị, không được dùng thay cho chữ ký nghiệp vụ của deviation hoặc release.

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
- Phase 2 field evidence: maintenance, QC inspection và lot disposition có nút chụp ảnh qua `NativeCapabilityService`, lưu URI/reference vào payload queue; web fallback vẫn cho phép nhập reference thủ công. **Lint PASS, unit 49/49 PASS, production build PASS; native camera cần device smoke**.
- Phase 4 queue hardening: queue có giới hạn 100 bản ghi terminal, đếm lần thử và chuyển lỗi mạng kéo dài sang `failed` sau 5 lần; retry vẫn yêu cầu thao tác rõ ràng của operator. **Lint PASS, unit 49/49 PASS, production build PASS**.
- Phase 3 operational notifications: đã thêm inbox cảnh báo dùng notification contract hiện có, hỗ trợ tải, đánh dấu từng bản ghi và đánh dấu tất cả đã đọc; push deep-link `/notifications` giờ có route hợp lệ. **Lint PASS, unit 49/49 PASS, production build PASS; cần smoke với notification service thật**.
- Phase 3 SOP context: batch đã chọn hiển thị recipe Approved, version, công đoạn, yield mục tiêu, nguyên liệu và instruction artifact Approved có version/checksum; operator phải xác nhận online trước khi tiếp tục. **Lint PASS, unit 49/49 PASS, production build PASS**.
- Phase 3 second-person deviation review: Quality page đã tải deviation Open và cho phép approve/reject/close **online-only** với actor thứ hai và ghi chú; không đưa thao tác phê duyệt vào offline queue. Đây là kiểm soát phân tách người thực hiện, chưa phải chữ ký số/e-signature. **Cần authenticated permission smoke**.
- Manufacturing compliance bổ sung: `GET/POST /api/v1/manufacturing/sop-artifacts`, `POST /sop-artifacts/{id}/approve|retire` và `GET/POST /business-signatures`; tenant luôn resolve từ claim/context, không nhận `tenantKey` trong các API compliance mới. Một số API manufacturing cũ còn nhận `tenantKey` tùy chọn chỉ để kiểm tra tương thích bằng `TryResolveTenant`, không cho phép request tự chọn tenant khác. Artifact có checksum SHA-256 và signature có actor server-side, method, timestamp, reason, evidence reference và integrity hash.
- SOP acknowledgment bổ sung: `POST /sop-artifacts/{id}/acknowledge` chỉ chấp nhận artifact Approved, ghi actor từ claim, notes/timestamp và chống xác nhận lặp bằng unique tenant/artifact/actor; Production page hiển thị nội dung Approved và nút xác nhận online.
- Phase 0 read resilience: đã thêm cache đọc ngắn hạn theo tenant với timestamp/TTL; khi refresh lỗi, màn hình giữ bản đọc gần nhất và hiển thị nhãn stale, không lưu token hoặc PHI.
- Phase 0 error contract: đã chuẩn hóa thông điệp phía client cho `401/403/409/422` theo `status/errorCode`, có bản dịch EN/VI và test utility; lỗi quyền không còn bị hiển thị như lỗi tải dữ liệu chung.
- Phase 2 sync recovery: đã thêm nút đồng bộ thật theo endpoint, retry có chủ đích cho bản ghi `failed/conflict`, và dispatch transport theo command; logout xóa queue cục bộ theo yêu cầu hardening. **Lint PASS, unit 47/47 PASS, production build PASS**.
- Validator lặp lại `scripts/validate-operator-mobile-phases.ps1` đã chạy đầy đủ: source gates P0/P1/P2/P3, queue recovery, queue retention/dead-letter, certificate pin boundary, shift handover, notifications, SOP context/acknowledgment, field evidence, second-person deviation review, lint, unit `49/49` và production build đều **PASS**. Evidence: `artifacts/evidence/operator-mobile-phases.json`.
- Manufacturing Service backend contract được chạy độc lập: `ManufacturingService.Application.Tests` **20/20 PASS**; `ManufacturingService.Integration.Tests` **56/58 PASS**, **2 SKIPPED** (external database routing chưa bật trong profile test). Testcontainers/Docker đã khởi động thành công và các case tenant boundary, lifecycle, QC, maintenance, traceability, FEFO/availability và authorization đều có trong suite.
- Compliance endpoint regression: `Compliance_routes_persist_versioned_sop_and_authenticated_business_signature` **1/1 PASS** với PostgreSQL Testcontainer; kiểm chứng draft không được approve, Submitted được approve với checksum SHA-256, chữ ký trùng bị từ chối `409`, và tenant được resolve từ claim.
- Audit i18n độc lập `scripts/audit-i18n-keys.ps1`: **112 referenced keys, missing 0**; validator phase hiện ghi nhận **196 mobile keys EN/VI, missing 0**.
- Sau bổ sung cache/error contract, checklist động, sync recovery và queue hardening, unit suite hiện là **49/49 PASS**; production build tiếp tục **PASS**.
- Live authenticated smoke cho các command lifecycle chưa được tuyên bố pass trong delta này; cần session/token có quyền Production Supervisor và batch dữ liệu thật. Nếu thiếu môi trường, ghi `environment-blocked`, không thay bằng build result.
- Live Playwright tại `tests/e2e` với dev server `localhost:4310`: **2/4 PASS** (unauthenticated redirect, login entry); **2/4 ENVIRONMENT-BLOCKED/FAIL** vì tài khoản `admin@hishop.com` bị route guard trả `forbidden` do thiếu permission Manufacturing, nên các request dashboard không được phát sinh. Không hạ guard để làm xanh test; cần seed/assign đúng permission và backend healthy rồi chạy lại.
- Shared foundation chỉ nhận bổ sung key i18n EN/VI cần thiết; không thay đổi styling/behavior của `hh-select` hoặc mobile layout để sửa riêng page operator.
- Native validation: `operator-mobile/android/:app:assembleDebug` **BUILD SUCCESSFUL**, tạo `android/app/build/outputs/apk/debug/app-debug.apk` (~39 MB). `adb` không có trên host và không có emulator/device nên install, UI-tree, camera/push và logcat smoke là **ENVIRONMENT-BLOCKED**; không coi APK build là native runtime pass.
- Certificate pin release boundary: placeholder `REPLACE_IN_RELEASE` đã được loại khỏi environment source; native pin được đọc từ runtime release configuration, còn release hash thực tế và rotation drill vẫn cần artifact bảo mật do môi trường triển khai cung cấp.
- Manufacturing Service runtime: đã build lại image `docker-manufacturingservice@sha256:6916bbb233306982741c3035b262bb43d7ae9f62b12318afc7a863acb167e8e5` và recreate container `his-hope-manufacturing` bằng `docker/docker-compose.yml`; health `http://localhost:5050/health` trả `200`, container `healthy`. Request SOP không có bearer trả `401` trực tiếp và qua gateway, xác nhận auth boundary đang hoạt động. Evidence: `artifacts/evidence/manufacturing-service-rebuild-20260829.json`.
- Auth runtime fix: mobile permission hydration đã chuyển từ admin-only `/api/v1/admin/me/permissions` sang `/api/v1/auth/me/permissions` (vẫn yêu cầu authenticated session); Identity Service đã build/recreate và healthy. Endpoint mới không bearer trả `401`; authenticated smoke cần chạy lại với session có tenant/permission thật.
