# Nacoms — capability blueprint cho food manufacturing enterprise

Ngày: 2026-08-26  
Mục đích: chuyển các nghiệp vụ Nacoms từ CRUD/transaction rời rạc thành năng lực vận hành có thể truy vết, kiểm soát và mở rộng. Đây là blueprint kiến trúc và backlog triển khai; **không phải** kết luận Nacoms đã được chứng nhận ISO hay đã tuân thủ một thị trường pháp lý cụ thể.

## Kết luận điều hành

Một hệ thống sản xuất thực phẩm enterprise không được đo bằng số lượng màn hình hay số cột trên một entity. Nó được đo bằng khả năng trả lời đáng tin cậy các câu hỏi: lô nguyên liệu nào đã đi vào thành phẩm nào, ai cho phép sử dụng/release, thông số nào lệch giới hạn, hao hụt đến từ đâu, tồn khả dụng nào có thể hứa cho khách và phạm vi recall là gì.

Vì vậy Nacoms cần xây dựng quanh ba nguồn sự thật liên kết được:

1. **Master data có kiểm soát phiên bản** — item, UoM, nhà cung cấp, facility/location, specification, recipe/routing, equipment và reason code.
2. **Facts bất biến theo thời gian** — receipt, QC, hold/release, move, issue, transform, pack, ship, maintenance và deviation; mọi correction là fact mới có lý do và người phê duyệt.
3. **Read model điều hành có thể tính lại** — ATP, yield, loss, OEE, expiry risk, recall impact, cost variance. Dashboard không là nguồn để ghi dữ liệu.

Thiết kế này phù hợp với ISO 22000 (FSMS kết hợp communication, management, PRP và HACCP), ISO 22005 (nguyên tắc/yêu cầu nền tảng cho traceability trong food chain), FDA Food Traceability Rule (CTE/KDE/traceability lot) và GS1 EPCIS 2.0 (visibility events, transformation input-output, sensor/context data). [ISO 22000](https://www.iso.org/standard/65464.html) · [ISO 22005](https://www.iso.org/standard/36297.html) · [FDA Food Traceability Rule](https://www.fda.gov/food/food-safety-modernization-act-fsma/fsma-final-rule-requirements-additional-traceability-records-certain-foods) · [GS1 EPCIS 2.0](https://ref.gs1.org/standards/epcis/2.0.0/)

## Ranh giới vận hành

```text
Supplier/Farm → Purchase order → Receive + QC → Raw-material lot
                                      │                 │
Specification / recipe / routing ────┼── Production order → Batch / operation
Equipment + calibration + telemetry ─┘                         │
                                                           Transform
                                              input lots → WIP/FG lots → release → reserve/ship
                                                             │
                                            deviation / CAPA / waste / rework
```

Theo ISA-95, business planning/logistics (ERP, Level 4) và manufacturing operations management/MES (Level 3) là ranh giới tích hợp rõ ràng; mô hình này không yêu cầu đưa PLC/SCADA vào cùng transaction database với nghiệp vụ ERP. Nacoms nên phát command tới MOM và nhận event/projection, còn integration máy ban đầu chỉ read-only qua gateway. [ISA-95 official overview](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard)

## Capability checklist và dữ liệu tối thiểu

| Năng lực | Dữ liệu enterprise phải có | Quy tắc không được bỏ | Ưu tiên |
|---|---|---|---|
| Thu mua & supplier assurance | Supplier code/legal name, site/farm, contacts, approval status, certificate, approved-material list, payment/incoterm, lead time, risk score, effective dates | PO chỉ chọn supplier/material đang approved; certificate hết hạn tạo block/exception | P0 |
| Receiving & nguyên liệu | PO line, supplier lot, internal traceability lot, received/accepted/rejected quantity, canonical UoM, gross/tare/net weight, received time, carrier, facility/location, CoA/attachment, expiry/harvest date | Một receipt có thể tạo nhiều lot; không tự `Released` trước disposition QC | P0 |
| Item/UoM/specification | SKU, item type, allergen, storage condition, shelf-life policy, QA specification version, sampling plan, test limits, UoM conversion precision | Lệnh/batch snapshot version hiệu lực; cấm sửa version lịch sử | P0 |
| Recipe & process | Master recipe, immutable version, yield target/range, routing/operation, parameter set, approved substitutions, CCP/OPRP, effective-from/to, approver | Batch lưu recipe/routing version; release recipe có four-eyes approval | P0 |
| Production & mass balance | Production order, batch, operation start/end, line/equipment/operator, input lot + actual weighed qty, output good/rework/by-product/waste qty, reason, measurements | Không completed khi mass balance thiếu; correction là adjustment có approval | P0 |
| Lot lifecycle & kho | Lot code, parent/child genealogy, status (Quarantine/Released/Hold/Blocked/Expired), stock ledger, facility/warehouse/bin, owner, reservation, expiry, FEFO priority | Không dùng stock aggregate làm source of truth; mọi move/issue/ship ghi ledger event | P0 |
| Quality & food safety | Inspection plan/version, sample identity, result/value/unit/method/instrument, pass/fail, disposition, hold/release decision, e-sign actor/time, evidence | Failed/unknown critical result tự chặn release theo policy; deviation/CAPA liên kết lot/batch | P0 |
| Traceability & recall | CTE, KDE, traceability lot code, source/destination, read point, business location, event/recorded time, business document, transformation input/output | Có truy ngược/truy xuôi theo lot; event append-only và idempotent | P0 |
| Equipment & maintenance | Equipment hierarchy, capability, status, calibration due date, meter, PM plan, work order, downtime reason, maintenance evidence | Equipment/status không overwrite history; late calibration block operation theo policy | P1 |
| Cost, loss & performance | Standard/actual material/labour/overhead, loss classification, variance reason, planned/run/downtime time, good/reject qty | Không gộp waste, rework, shrinkage và variance thành một "loss" | P1 |
| Sales/CEO control tower | ATP by released lot, expiry risk, service level, yield variance, OEE only when input complete, recall impact, margin variance | Projection có event source/correlation; CEO cannot mutate plant transactions | P2 |

FDA dùng CTE (critical tracking event) và KDE (key data element) làm cấu trúc lưu traceability; các CTE bao gồm receiving, shipping và transformation. Với food thuộc phạm vi quy định, KDE phải liên kết traceability lot tương ứng. Đây là reference design hữu ích cho Nacoms, nhưng phạm vi pháp lý cần được xác nhận theo thị trường bán hàng trước khi coi là nghĩa vụ áp dụng. [FDA CTE/KDE](https://www.fda.gov/food/food-safety-modernization-act-fsma/fsma-final-rule-requirements-additional-traceability-records-certain-foods)

Với luồng FDA, internal lot không thay thế external traceability lot: receipt cần giữ supplier lot/TLC gốc, quantity/UoM, nguồn trước đó, nơi-ngày nhận và reference document; transformation cần giữ input/output TLC cùng quantities. FDA công bố khả năng cung cấp dữ liệu truy xuất trong 24 giờ cho đối tượng thuộc phạm vi rule; đây là acceptance target tốt cho recall drill, không phải mặc định một nghĩa vụ pháp lý của Nacoms. [FDA CTE and KDE resource](https://www.fda.gov/media/163132/download)

## Entity model: làm giàu đúng cách, không tạo “god entity”

Mỗi aggregate dưới đây giữ dữ liệu thuộc vòng đời của nó; dữ liệu liên aggregate được liên kết bằng ID bất biến và event, không sao chép tuỳ tiện.

| Aggregate | Entity/child nên bổ sung | Ví dụ trường trọng yếu |
|---|---|---|
| Supplier | `SupplierSite`, `SupplierApproval`, `SupplierCertificate`, `SupplierMaterialApproval` | `approvalStatus`, `riskLevel`, `certificateType`, `validFrom/To`, `materialId`, `approvedBy` |
| Material/Product | `ItemSpecificationVersion`, `UomConversion`, `AllergenProfile`, `ShelfLifePolicy` | `specVersion`, `testMethod`, `min/max/target`, `baseUom`, `roundingScale`, `storageTemperature` |
| Procurement/receipt | `PurchaseOrderLine`, `GoodsReceipt`, `ReceiptLine`, `ReceiptEvidence` | `ordered/received/accepted/rejectedQuantity`, `supplierLotCode`, `coaUri`, `receivedAt`, `netWeight` |
| Lot/inventory | `InventoryLot`, `StockLedgerEntry`, `LotStatusHistory`, `LotReservation` | `traceabilityLotCode`, `status`, `expiryAt`, `facility/location`, `quantityDelta`, `eventId` |
| Recipe/production | `RecipeVersion`, `RoutingVersion`, `Batch`, `OperationExecution`, `BatchMaterialConsumption`, `BatchOutput` | `effectiveAt`, `approvedAt/by`, `targetYield`, `actualQuantity`, `lossReason`, `equipmentId` |
| Quality | `InspectionPlanVersion`, `QualitySample`, `TestResult`, `Disposition`, `Deviation`, `CapaAction` | `sampledAt`, `method`, `resultValue/unit`, `limit`, `decision`, `evidenceUri`, `closedAt` |
| Equipment | `Equipment`, `CalibrationRecord`, `MaintenancePlan`, `MaintenanceWorkOrder`, `DowntimeEvent` | `equipmentClass`, `meterReading`, `dueAt`, `downtimeReason`, `workPerformed`, `verifiedBy` |
| Traceability | `TraceabilityEvent`, `TransformationLink`, `RecallCase` | `eventType`, `occurredAt`, `recordedAt`, `bizLocation`, `readPoint`, `inputLotId`, `outputLotId`, `correlationId` |

GS1 EPCIS 2.0 có `TransformationEvent` cho ngữ cảnh input consumed/output produced, trường event time/location/context và sensor data. Nacoms không cần công bố EPCIS API ngay, nhưng nên giữ semantic tương thích: `what/when/where/why/how`, source/destination, transformation ID, input/output lot and quantity. [GS1 EPCIS 2.0 — event types and dimensions](https://ref.gs1.org/standards/epcis/2.0.0/)

### Trường kỹ thuật áp dụng chung

Không phải mọi cột dưới đây đều thuộc tất cả entity, nhưng mọi entity có state thay đổi phải có tối thiểu:

```text
Id, TenantId, FacilityId, BusinessKey,
Status, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy,
RowVersion, CorrelationId
```

Các fact/event cần thêm `OccurredAtUtc`, `RecordedAtUtc`, `SourceSystem`, `IdempotencyKey`, `SchemaVersion`, `ReasonCode`, `EvidenceReference`. `RowVersion` giải quyết concurrent command; `IdempotencyKey` bảo vệ retry integration; audit record nên chỉ ghi diff/actor/reason, không chứa secret hoặc PII thừa.

## State machine bắt buộc

| Đối tượng | Luồng chuẩn |
|---|---|
| Supplier approval | `Draft → UnderReview → Approved/Suspended/Expired → Retired` |
| Recipe/specification | `Draft → InReview → Approved → Effective → Superseded/Retired` |
| Receipt lot | `Received/Quarantine → Released | Rejected | Hold` |
| Production batch | `Planned → Released → InProgress → AwaitingQA → Completed | Cancelled` |
| Finished-good lot | `Quarantine → Released → Reserved → Shipped | Hold | Recall | Expired` |
| Deviation/CAPA | `Open → Containment → Investigation → ActionInProgress → EffectivenessReview → Closed` |
| Maintenance work order | `Planned → Released → InProgress → Completed → Verified/Closed` |

Transition phải nằm trong application use case/domain method, được permission-check ở API boundary, ghi audit event/outbox cùng transaction và test với transition bất hợp lệ. Đừng expose endpoint generic `PATCH status`.

## Automation có kiểm soát

| Trigger | Automation được phép | Phải giữ người phê duyệt? |
|---|---|---|
| QC vượt giới hạn | Tạo deviation, đặt lot `Hold`, gửi cảnh báo | Có, để release/disposition |
| Receipt hoàn tất | Sinh internal lot, stock-ledger receipt, tạo inspection pending | Có, QC release |
| Operation hoàn tất | Tính mass-balance preview và cảnh báo lệch | Có, đóng batch/approve loss |
| Calibration due | Tạo work order/cảnh báo và gắn equipment restriction | Có, override use |
| Expiry risk/FEFO | Đề xuất allocation/transfer, không auto-ship | Có, fulfillment owner |
| Recall case | Tính impact graph, freeze candidate lots, tạo task | Có, QA/recall lead quyết định scope |

## Clean Architecture và integration contract

1. **Domain**: aggregate, value object (quantity + UoM, lot state, result limit), invariant và lifecycle transition; không tham chiếu HTTP/EF/queue.
2. **Application**: command/query use case, authorization policy, optimistic concurrency, transaction boundary, outbox event.
3. **Infrastructure**: EF mapping/migration, object storage evidence, broker, telemetry adapter, idempotent inbox consumer.
4. **API**: versioned request/response, problem details, tenant/facility scoping, idempotency header cho command rủi ro.
5. **Operator UI**: form chỉ gửi command rõ ngữ nghĩa; show status/effective version/audit timeline; selectors phải hiển thị human name + business code; dashboard chỉ đọc projection.

Sử dụng outbox/inbox và event schema version từ P0 để tránh việc UI/API retry làm double receipt, double stock hoặc double loss. Với tích hợp Level 3–4, ISA-95 Part 2/4/5 phân biệt object/attribute, MOM integration và transaction business-to-manufacturing; điều đó phù hợp với contract event hơn là gọi thẳng database/service nội bộ. [ISA-95 series](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard)

Để quản trị roadmap thay vì gom tất cả vào một ManufacturingService, dùng các domain lifecycle của MESA (Production, Production Asset, Product, Supply Chain, Workforce và Order-to-Cash) làm lens phân ranh ownership. Chúng là khung năng lực; không thay thế Clean Architecture hoặc bounded context của Nacoms. [MESA Smart Manufacturing Model](https://mesa.org/topics-resources/mesa-model/)

## Lộ trình nâng cấp có nghiệm thu

| Phase | Scope giới hạn | Evidence pass/fail |
|---|---|---|
| P0 — trustworthy lot | Supplier approval, receipt, quality disposition, lot lifecycle, stock ledger, transformation link, audit/outbox | Từ một FG lot truy ngược tất cả raw lots và forward toàn bộ impacted FG/customer reservation; retry command không nhân đôi ledger |
| P1 — controlled production | Recipe/spec version, batch operation, actual weigh-in/out, mass balance, deviation/CAPA, equipment/calibration/maintenance | 5 batch case gồm rework/waste/hold; yield/loss tính lại từ facts và invalid transition bị reject |
| P2 — operational intelligence | Expiry/FEFO, ATP, cost/loss variance, OEE input contract, controlled telemetry pilot | KPI truy được về event nguồn; dashboard đánh dấu `insufficient-data` khi thiếu input thay vì hiển thị số giả |
| P3 — ecosystem scale | EPCIS exporter/importer, supplier portal, sales/commerce integration, recall drill, retention/backup/DR | Recall drill có tenant/facility boundary, log, RTO/RPO evidence và contract compatibility tests |

## Checkpoint triển khai 2026-08-26

Đã triển khai và chạy local Docker hai lát cắt có thể nghiệm thu:

1. **Lot và receipt có hồ sơ truy xuất**: `LotCode`, loại lot, quốc gia nguồn gốc, ngày sản xuất/nhận, facility/location, COA, nguồn lot, quality status và lịch sử disposition. Receipt có delivery note, carrier/vehicle, nhiệt độ nhận, COA, người nhận và tách accepted/rejected quantity. Migration backfill dữ liệu cũ trước khi áp unique/check constraint.
2. **Supplier governance**: hồ sơ pháp lý/liên hệ/quốc gia/rủi ro, lifecycle `Draft → PendingApproval → Approved`, audit của quyết định và API từ chối tạo PO với `422 supplier_not_approved` trước khi supplier được phê duyệt. Migration tương thích đánh dấu supplier có sẵn là `Approved/Standard` với actor `migration`; dữ liệu này vẫn phải được QA rà soát nghiệp vụ sau rollout.
3. **QC đa chỉ tiêu (P2 bước đầu)**: inspection nhận nhiều `QualityTestResult` (mã/tên, giá trị, UoM, giới hạn dưới/trên, phương pháp, evidence), bắt buộc kết quả Fail đi cùng trạng thái Fail, lưu child rows có unique `(inspection, testCode)`, phát outbox summary và hiển thị test results trong Operator.
4. **Reliability command (P2)**: các command side-effect hỗ trợ chuẩn `Idempotency-Key` (tương thích `X-HisHope-Operation-Id`) với uniqueness theo tenant/subject/route; retry trùng trả `409 operation_replayed`. Lot disposition nhận `ExpectedUpdatedAt` và trả `409 concurrency_conflict` khi bản ghi đã thay đổi.
5. **CCP/OPRP evidence gate (P2 bước đầu)**: các chỉ tiêu có mã `CCP-*` hoặc `OPRP-*` bắt buộc có `Method` và `EvidenceReference`; thiếu bằng chứng bị từ chối trước khi ghi inspection.
6. **Approver segregation (P3 bước đầu)**: endpoint approve đặc tả sản phẩm và approve công thức yêu cầu permission riêng (`manufacturing.specification.approve`, `manufacturing.recipe.approve`), tách khỏi quyền tạo/ghi nhận vận hành.
7. **Recall/EPCIS drill (P3 bước đầu)**: `recall-impact` truy xuôi genealogy có giới hạn depth/lot; `traceability/epcis` xuất document EPCIS 2.0-like theo tenant, time window và event provenance từ outbox.
8. **Backup readiness (P3 bước đầu)**: thêm `scripts/manufacturing/Test-BackupRestoreReadiness.ps1` để xác thực SHA-256 và khả năng đọc archive `pg_restore --list` mà không mutate database; full restore phải chạy trên recovery database theo change ticket.
9. **SLO probe (P3 bước đầu)**: thêm `scripts/manufacturing/Test-ManufacturingSlo.ps1` đo availability và p95 latency cho health/authenticated endpoints, trả exit code 2 khi vượt ngưỡng.
10. **Restore drill local (P3)**: đã dump `manufacturingdb` custom-format, xác minh checksum/archive và restore thành công vào database cô lập `manufacturing_restore_drill`; dữ liệu kiểm chứng gồm 13 lots và 57 outbox messages. Không ghi đè database vận hành.
11. **Restore drill automation (P3)**: thêm `scripts/manufacturing/Invoke-RestoreDrill.ps1`; mỗi lần chạy tạo database cô lập theo timestamp, restore backup và yêu cầu tối thiểu có lots/outbox trước khi pass.
12. **Replay retention (P3)**: operation replay key được dọn tự động sau 7 ngày trước khi reserve key mới, giữ bounded storage cho tenant chạy dài hạn.

Đây chưa phải bằng chứng enterprise production hoàn chỉnh. Các gate còn lại (CCP/OPRP theo product family, authorization permission riêng cho approver, recall drill, EPCIS, retention/backup/DR và SLO) vẫn phải được triển khai, kiểm thử và có bằng chứng runtime riêng.

## Acceptance suite enterprise

- **Data integrity**: quantity luôn được quy đổi từ canonical UoM; âm stock, double-post và update cross-tenant bị chặn.
- **Traceability**: truy ngược/truy xuôi nhiều-nhiều, including transformation/rework/pack; export evidence theo khoảng thời gian/facility.
- **Quality**: hold/release và specification version hoạt động ở API lẫn UI; QC fail không thể allocate/ship.
- **Audit & approval**: actor, timestamp, before/after, reason, evidence và approver có thể đọc nhưng không sửa.
- **Reliability**: idempotency/outbox/inbox, dead-letter monitoring, replay test, optimistic concurrency conflict response.
- **Security**: tenant + facility scope, least-privilege permissions, attachments malware/content policy, secrets ngoài DB, audit access to sensitive records.
- **Operations**: migration rehearsal với rollback plan, backup/restore test, metrics/tracing/correlation ID, SLO cho receipt/trace query.
- **UX**: form hiển thị required data/risk before commit; entities không bị làm nghèo để vừa UI; action nguy hiểm có confirmation + reason/evidence.

## Quyết định còn cần Nacoms xác nhận

1. Thị trường/chuẩn pháp lý mục tiêu (Việt Nam, EU, US hoặc khách hàng private label) và thời hạn lưu hồ sơ.
2. Danh mục allergen, CCP/OPRP, giới hạn moisture/microbiology và policy hold/release cho từng product family.
3. Quy tắc lot code, batch size, cân tích hợp, barcode/QR label và mức tự động hóa dây chuyền hiện hữu.
4. RTO/RPO, retention, partitioning theo tenant/facility và volume event/telemetry dự kiến.

## Nguồn chính thức

- [GS1 EPCIS 2.0, ratified June 2022](https://ref.gs1.org/standards/epcis/2.0.0/)
- [FDA — Food Traceability Rule, CTE and KDE](https://www.fda.gov/food/food-safety-modernization-act-fsma/fsma-final-rule-requirements-additional-traceability-records-certain-foods)
- [ISO 22000:2018 — Food safety management systems](https://www.iso.org/standard/65464.html)
- [ISO 22005:2007 — Traceability in the feed and food chain](https://www.iso.org/standard/36297.html)
- [ISA-95 — Enterprise-control system integration](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard)
- [MESA Smart Manufacturing Model](https://mesa.org/topics-resources/mesa-model/)

## Validation delta 2026-08-26 (current repository evidence)

- All state-changing Manufacturing routes now inherit one `MobileOperationReplayFilter` at the route-group boundary; safe read methods bypass it. The supported client contract is `Idempotency-Key` (with the legacy header retained for compatibility).
- Full `ManufacturingService.Integration.Tests` passed: **36 passed, 0 failed, 0 skipped**. The authenticated production workflow including recall impact and EPCIS export passed **1/1** after the replay-retention EF translation fix.
- API, shared foundation, and operator builds passed; the Manufacturing API container and operator container were rebuilt and report healthy readiness. Frontend production dependency audit reports **0 production vulnerabilities**; Docker's dev/build dependency advisories are not shipped in the Nginx runtime image.
- The SLO script now requires an explicit access token (`-AccessToken` or `HIS_HOPE_MANUFACTURING_ACCESS_TOKEN`) for authenticated probes and reports a separate `AuthenticatedProbePass` field. `-SkipAuthenticated` is explicitly infrastructure-only.
- The SLO script also supports the web BFF session (`-WebSession`) and session-cookie environment input. A local login through the gateway was used without printing credentials: 6/6 probes passed, availability 100%, p95 38.74 ms, authenticated probe pass.
- Remaining production gates are environmental evidence, not unimplemented local code: authenticated SLO run with a real token, independent DR/failover rehearsal with measured RTO/RPO, and an external security assessment.
- Restore-drill automation was executed against the isolated recovery PostgreSQL container: 13 lots and 57 outbox messages verified, RTO 2.25 seconds and backup-age RPO proxy 0.04 minutes, both within 300-second/15-minute local thresholds. This is local recovery evidence, not a production failover claim.
- Restore-drill database prefixes are now validated against a strict PostgreSQL identifier allow-list before interpolation into `CREATE DATABASE`; an injection-shaped prefix is rejected before any Docker/SQL operation.
- Authenticated operator UI completeness smoke passed **7/7** with the local BFF login session: protected-route redirect, route navigation, master-data write, lot reservation, supplier creation, production-order creation, and quality-inspection recording. Tab assertions use semantic `role=tab` selectors so hidden sections are not mistaken for active content.
- Buyer app public UI smoke passed **2/2** (locale/theme/contrast and invalid callback without empty-token errors). Authenticated buyer route contract passed **1/1** across catalog, cart, orders, profile, notifications, and RFQ with no console errors or HTTP 5xx. Buyer nginx now uses runtime Docker DNS resolution and enlarged proxy buffers for chunked Identity session cookies.
- Cross-app nginx validation passed for buyer, operator và admin (`nginx -t` trên cả 3 container); gateway, Manufacturing API, operator và buyer đều healthy. Endpoint manufacturing-summary không có session trả đúng **401**, xác nhận authorization boundary không bị nới lỏng.
- Enterprise production validator đã pass sau khi sửa migration Identity bị trùng lease columns: DPoP coverage, RFC9700/OIDC conformance **9/9**, Identity infrastructure/application gates và independent OIDC penetration evidence đều xanh. Migration follow-up hiện tạo column/index có điều kiện, an toàn khi replay trên database đã có schema.
- Storage backup contract static gate đã chạy pass sau khi sửa default `RepositoryRoot` của validator (không còn phụ thuộc `$PSScriptRoot` trong parameter binding). CNPG manifest/schedule, object-store reference, digest pinning, PVC migration tooling và restore safety đều được xác nhận; các gate CSI/Azure runtime tiếp tục được đánh dấu skipped đúng phạm vi.
- NuGet vulnerability audit không phát hiện package runtime có lỗ hổng cho `ManufacturingService.Api` và `IdentityService.Api` (transitive, nguồn nuget.org hiện tại). Đây là snapshot audit; CI cần chạy lại theo advisory database tại thời điểm release.
- Database restore evidence contract đã được hoàn thiện: `Invoke-RestoreDrill.ps1` ghi JSON chuẩn (`status`, `rtoMinutes`, `rpoMinutes`, `executedAtUtc`, `restoreVerified`, `target`) và drill local mới nhất xác minh **13 lots / 68 outbox**, RTO **2.02s**, RPO proxy **0.23 phút**; `validate-dr-evidence.ps1 -OnlyFile database-restore-drill.json` pass.
- Production Azure restore wrapper `verify-production-backup-restore.ps1` cũng đã ghi đủ `rtoMinutes` và `target`, tránh làm mất evidence khi wrapper gọi CNPG drill; script đã parse-validate thành công.
- Enterprise production phases validator đã chạy lại sau các sửa đổi DR/migration: DPoP, RFC9700 **9/9**, Identity unit/infrastructure/integration gates và independent OIDC evidence pass; các gate runtime ngoài local vẫn hiển thị `skipped`/blocked đúng phạm vi.
- Phase 2 và Phase 3 validator chạy độc lập đều pass: assurance policy/device posture, authorization/persistence và các integration gates liên quan đều xanh.
- Production cutover input validator hiện blocked duy nhất bởi thiếu production kubeconfig; secure root, Azure env key-set (giá trị redacted) và hai CA chain đều pass. Không đọc hoặc in private key/secret.
- Kiểm tra an toàn ngày 2026-08-26 chỉ tìm thấy `C:\Users\Admin\.kube\config` với context `kubernetes-admin@kubernetes`; đây không được coi là production kubeconfig. Không tự động dùng kubeconfig mặc định để vượt gate production.
- Procurement integrity hardening: PO create/update giờ chỉ chấp nhận material đang `Active` trong đúng tenant và từ chối SKU trùng trong cùng PO; contract suite xác nhận lỗi `material_not_found` trước khi ghi transaction.
- Manufacturing integration suite sau hardening procurement đạt **37/37 pass**; dữ liệu test dùng UoM riêng `kg-http` để tránh đụng unique key với các test master-data chạy song song.
- Production builds ngày 2026-08-26 đều pass cho operator, buyer và admin; runtime health pass tại gateway `:5000/health`, operator `:4300/health/ready` và buyer `:4205/health`. Cảnh báo admin duy nhất là dependency `qrcode` CommonJS optimization bailout, không phải build failure.
- Supplier certificate profile đã được đưa vào persistence/API: certificate type/number, issuer, issued/expiry dates, status, evidence reference, actor và tenant-scoped GET/POST; migration `AddSupplierCertificates` có unique supplier-number và date check. Manufacturing integration suite sau thay đổi đạt **38/38 pass**.
- Manufacturing image `docker-manufacturingservice` đã được rebuild/restart sau migration; gateway `/health` trả **200**, endpoint suppliers không có session trả đúng **401**, xác nhận auth boundary vẫn giữ nguyên.
- Supplier material approval profile đã được thêm độc lập với certificate: `MaterialSku`, `ApprovedUom`, effective window, status, notes, actor; API GET/POST tenant-scoped và migration có unique supplier/material cùng date check. Full integration suite tiếp tục đạt **38/38 pass**; image đã rebuild/restart và health gateway **200**.
- Supplier material approval enforcement đã bật trong PO create/update: chỉ cho phép material active đúng tenant, có approval hiện hành theo supplier, status `Approved` và effective window; supplier chuyển sang `Approved` tự động tạo approval cho các material active hiện hữu. Full integration suite sau enforcement đạt **38/38 pass**; Manufacturing API đã rebuild/restart và gateway health **200**.
- Machine calibration record đã được bổ sung theo tenant/machine với loại hiệu chuẩn, số chứng chỉ duy nhất, provider, evidence, kết quả, thời điểm hiệu chuẩn và hạn kế tiếp; database có FK, unique index và check constraint ngày. API GET/POST tenant-scoped, tự cập nhật mốc maintenance sớm hơn khi cần; test calibration và demo seed pass, full integration suite hiện đạt **39/39 pass**. Image Manufacturing đã rebuild/restart và gateway health **200**.
- Inspection plan version đã được bổ sung với plan/product version, sampling method/frequency, acceptance criteria, effective window và lifecycle Draft → Submitted → Approved → Retired; database có unique version theo tenant/plan và date constraint, API GET/POST/status tenant-scoped, có permission QualityInspect. Seed demo và lifecycle contract pass; full integration suite hiện đạt **40/40 pass**. Image Manufacturing đã rebuild/restart và gateway health **200**.
- Quality inspection hiện hỗ trợ liên kết nullable tới `InspectionPlanVersionId`; khi client chỉ định plan, service bắt buộc plan cùng tenant/product, trạng thái `Approved` và còn trong effective window trước khi ghi inspection. Migration liên kết có FK/index, contract test xác nhận truy vết inspection → plan; full integration suite vẫn đạt **40/40 pass**, gateway health **200**.
- Quality sample/disposition đã được bổ sung: sample gắn inspection + lot, mã mẫu duy nhất theo inspection, người/vị trí/thời điểm lấy mẫu, evidence notes và disposition metadata. API GET/POST/disposition tenant-scoped, transition chỉ cho phép `Pending → Accepted|Rejected|Hold`, có FK/index và audit actor/time. Seed demo và contract workflow pass; full integration suite đạt **40/40 pass**, image đã rebuild/restart và gateway health **200**.
- Maintenance plan đã được bổ sung theo machine: plan code, loại bảo trì, chu kỳ ngày, checklist, assignee, active flag, next due và last generated; database có unique/index/check constraint. API GET/POST tenant-scoped; bộ sinh work order ưu tiên plan đến hạn, ghi checklist/assignee, cập nhật next due theo chu kỳ và vẫn giữ fallback mốc machine cũ. Seed demo và contract workflow pass; full integration suite đạt **41/41 pass**, image đã rebuild/restart và gateway health **200**.
- Batch costing ledger: tenant-scoped batch cost snapshot with material, labor, overhead and loss attribution, output-unit cost, recalculation API, migration and demo seed.
