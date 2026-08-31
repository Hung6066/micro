# Audit và thiết kế pattern cho Identity, Manufacturing, Commerce, Content

Ngày audit: 2026-08-31  
Phạm vi: source hiện tại dưới `src/Services`, shared infrastructure, integration tests và các validation script có sẵn.  
Nguyên tắc: giữ database ownership theo service; không thêm generic repository hoặc framework mới nếu chưa có use case chứng minh.

## 1. Kết luận điều hành

Nền tảng đã có các seam đúng hướng:

- Clean Architecture theo hướng `Domain -> Application -> Infrastructure -> Api` được validation script xác nhận cho 45 service projects.
- Shared contracts, tenant placement, authorization, resilience, health, Outbox, idempotency và Saga đã tồn tại ở shared modules.
- Identity đã có OIDC/OpenIddict, PKCE, DPoP, Redis session, Vault, permission/facility authorization và nhiều test security.
- Manufacturing đã có các policy thuần, các store theo workflow, Outbox, consumer RabbitMQ, event receipts và persistent fulfillment Saga.
- Commerce đã có transaction cho order + Outbox và các persistence/security tests.
- Content có bounded data model rõ, public/manage route tách biệt và test API cơ bản.

Điểm cần sửa không phải là “đổi toàn bộ sang một pattern”, mà là làm sâu interface của từng module và bỏ các điểm orchestration/persistence đang quá nông hoặc quá lớn:

1. **P0 — Identity:** tách endpoint composition và direct EF queries thành vertical slices/use-case handlers; bảo toàn authorization ở server-side.
2. **P0 — Manufacturing:** tiếp tục chia `ManufacturingPersistence.cs` thành các workflow module đã có port; chuẩn hóa transaction + Outbox + inbox deduplication cho mọi mutation/event consumer.
3. **P1 — Commerce:** đưa order/cart/RFQ orchestration ra khỏi `Program.cs` và `CommerceStore`; giữ Commerce là source of truth cho order state, dùng event-driven integration với Manufacturing.
4. **P1 — Content:** áp dụng CQRS-lite + explicit publication state machine; bổ sung audit/outbox chỉ cho các thay đổi cần downstream propagation, không biến Content thành hệ thống event-sourcing.
5. **P1 — Cross-service:** dùng một integration contract: envelope, correlation, tenant, audience, idempotency key, event version và retry/dead-letter semantics.

## 2. Bằng chứng hiện tại

| Khu vực | Bằng chứng source | Trạng thái kiểm chứng |
|---|---|---|
| Layer direction | `scripts/validate-service-architecture.ps1` | Pass, 45 projects |
| Communication seam | `scripts/validate-communication-boundaries.ps1` | Pass |
| Identity composition | `IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs` dài 2.616 dòng; `IdentityServiceRegistrationExtensions.cs` đăng ký auth, rate limit, health, Vault, hosted workers | Source có, cần làm sâu seam |
| Identity application | `IdentityService.Application/UseCases/**` có MediatR command/query handlers | Pattern có, chưa áp dụng đồng đều cho endpoint/admin surface |
| Manufacturing persistence | `ManufacturingService.Infrastructure/Persistence/ManufacturingPersistence.cs` dài 2.477 dòng; đồng thời có các store theo workflow trong các file riêng | Có seam tốt nhưng còn legacy monolith |
| Manufacturing integration | `CommerceOrderConsumer`, `ManufacturingOutboxDispatcher`, `EventReceipts`, `CommerceOrderFulfillmentSaga` | Có source và integration tests; cần kiểm tra đầy đủ runtime matrix khi release |
| Commerce transaction | `CommercePersistence.SaveOrderAndOutboxAsync` dùng transaction; `CommerceOrderPersistenceTests` kiểm tra duplicate event | Pattern có |
| Commerce API | `CommerceService.Api/Program.cs` còn endpoint composition; host DI/pipeline đã tách vào `Composition/CommerceServiceHostExtensions.cs` | Cần tiếp tục tách endpoint/use-case seam |
| Content | `PostgresContentStore` dài 613 dòng; `ContentService.Api/Program.cs` dài 413 dòng; public/manage groups đã tách | Đủ cho CRUD/CMS cơ bản; thiếu publication/event seam sâu |
| Shared reliability | `His.Hope.Infrastructure.Outbox`, `Idempotency`, `Saga`, `DataLifecycle`, `Resilience` | Có thể tái sử dụng, không nên tạo bản thứ hai |
| Tests | Identity integration/security rộng; Manufacturing policy/traceability/RabbitMQ/tenant placement; Commerce persistence/security; Content public/manage API | Không coi test source là runtime production proof |

## 3. Thiết kế pattern đích

### 3.1 Pattern chung: vertical slice + deep module

Mỗi use case là một module có interface nhỏ và implementation sâu:

```text
HTTP/gRPC adapter
    -> request/response contract
    -> application use-case interface
    -> domain policy / aggregate invariant
    -> persistence + outbox adapter
```

Quy tắc:

- Endpoint chỉ bind/authorize/validate và gọi một use case.
- Application interface chứa invariant, error mode, cancellation và idempotency semantics; không để caller biết EF schema.
- Infrastructure adapter thực hiện EF, broker, Redis, Vault hoặc provider HTTP.
- Chỉ tạo seam khi có ít nhất hai implementation/adapter hoặc test cần thay thế dependency.
- Không tạo `IGenericRepository<T>`, base handler hoặc “service manager” gom mọi use case.

### 3.2 Identity — Authorization Server + IAM Control Plane

**Pattern:** Ports-and-adapters + CQRS vertical slices + policy/strategy pipeline + transactional outbox.

Các module đích:

- `AuthenticateUser`, `CompleteMfa`, `CompletePasskey`, `RefreshSession`, `RevokeSession`.
- `ResolveEffectivePermissions` và `EvaluateFacilityAccess` là policy modules thuần, không phụ thuộc HTTP.
- `ManageUsers`, `ManageRoles`, `ManageClients`, `ManagePermissionSets` là admin slices, mỗi slice sở hữu query projection, validator, authorization requirement và audit event.
- `DirectoryProvisioning`, `SecuritySignal`, `PushDelivery` là outbound adapters sau commit qua outbox/lease worker.

Pattern cần giữ:

- OpenIddict là protocol adapter; không tự viết authorization server khác.
- JWT permission claim chỉ là input cacheable; quyết định resource/facility/tenant phải kiểm tra server-side.
- Vault là secret/key adapter; provider failure phải fail-closed.
- Audit, protocol records, outbox và append-only history có lifecycle khác nhau; không áp soft-delete chung.

Gap chính: `IdentityServiceEndpointExtensions.cs` còn vừa route composition vừa query/mutation EF trực tiếp. Tách dần theo slice, bắt đầu access governance và admin table; giữ route contract và negative authorization tests.

### 3.3 Manufacturing — workflow modules + policy + fulfillment process manager

**Pattern:** Domain policy/aggregate + workflow-specific ports + transactional Outbox/Inbox + persistent Saga cho cross-step fulfillment.

Workflow modules:

- Master data: SKU/material/UOM/warehouse/storage.
- Planning/recipe: versioned recipe/specification.
- Production: production batch, operation execution, yield/loss.
- Quality/compliance: inspection, CAPA, deviations, signatures.
- Inventory/procurement: ledger, reservation, RFQ/quotation, supplier governance.
- Maintenance: machine, calibration, work order, downtime.

Mỗi workflow có interface sâu kiểu `IManufacturingProductionOrderStore` hoặc `IManufacturingReservationStore`; implementation không nên quay lại một `PostgresManufacturingStore` chung.

Saga rule:

- Manufacturing Saga chỉ điều phối fulfillment/production steps do Manufacturing sở hữu.
- Commerce là authority của order lifecycle; trạng thái order cập nhật bằng versioned event/command, không ghi chéo database.
- Mọi consumer deduplicate theo event id hoặc `(event type, aggregate id, version)` và lưu receipt cùng transaction với side effect.
- Retry chỉ cho transport/transient; business 4xx chuyển failed/dead-letter và cần operator action.

Gap chính: persistence đã có nhiều module tốt nhưng legacy file vẫn rất lớn và nhiều `SaveChanges()` synchronous. Tách theo workflow, ép cancellation token và đưa các mutation quan trọng về một transaction boundary có Outbox.

### 3.4 Commerce — order aggregate + transactional integration

**Pattern:** Aggregate/order state machine + CQRS-lite + idempotent command + transactional Outbox + read projections.

Commands lõi:

- `CreateCart`, `ChangeCartLine`, `SubmitOrder`, `CancelOrder`.
- `CreateRfq`, `UpdateRfq`, `RespondRfq`.
- `MarkFulfillmentAccepted`, `MarkFulfillmentRejected`, `MarkFulfillmentCompleted` — chỉ nhận event/command hợp lệ từ integration contract.

Invariants:

- `(tenant, buyer, order)` luôn được scope từ authenticated context, không nhận tùy ý từ body.
- Submit order chốt snapshot giá/UOM/minimum quantity; không đọc lại catalog tùy tiện sau khi đã commit.
- Idempotency key gắn với authenticated subject + tenant + operation; cùng key khác request hash phải conflict.
- Order state transition là explicit state machine, không gán status tự do từ endpoint.

Gap chính: `Program.cs` và `CommerceStore` còn là orchestration seam nông; đưa logic vào command handlers/domain policy và để persistence adapter thực hiện `SaveOrderAndOutboxAsync`.

### 3.5 Content — publication state machine + CQRS-lite + cache-aside

**Pattern:** Content aggregate/state machine + projection/read model + object/media adapter + selective Outbox.

Aggregate/state:

- `Draft -> InReview -> Published -> Archived`.
- Chỉ workflow policy được publish; publish phải có actor, version và audit record.
- Slug/tenant uniqueness, media metadata và upload constraints nằm trong domain/application validation.

Adapter:

- Database là source of truth cho metadata.
- Object storage/CDN là adapter cho binary media; không lưu credential hoặc provider detail trong domain.
- Public reads dùng projection/cache-aside với invalidation theo `ContentPublished.v1`.
- Partnership inquiry/newsletter dùng command validation, abuse/rate-limit và audit; không cần event-sourcing.

Gap chính: `PostgresContentStore` đang chứa nhiều CRUD và seed logic; tách read projection khỏi mutation. Bổ sung Outbox chỉ khi có consumer thực tế như cache invalidation, search indexing hoặc notification.

## 4. Contract liên service bắt buộc

```json
{
  "messageId": "uuid",
  "eventType": "Commerce.OrderSubmitted.v1",
  "occurredAt": "utc",
  "tenantKey": "tenant",
  "aggregateId": "uuid",
  "aggregateVersion": 4,
  "correlationId": "uuid",
  "causationId": "uuid",
  "subject": "service-or-user",
  "audience": "manufacturing-service",
  "idempotencyKey": "string",
  "payload": {}
}
```

Bắt buộc:

- DTO/event contract liên service nằm trong `His.Hope.Contracts` hoặc messaging abstraction; status/error nội bộ giữ trong bounded context.
- Consumer phải validate schema/version, tenant, audience và authorization context.
- Có timeout, bounded retry, dead-letter, replay/receipt và metric theo `correlationId`.
- Không truyền connection string, token, PII không cần thiết hoặc domain entity trực tiếp qua message.

## 5. Lộ trình triển khai

### P0 — an toàn và correctness

1. Chốt event envelope/idempotency/tenant/audience và test contract.
2. Identity: tách access-governance/admin mutation khỏi endpoint composition; thêm handler-level authorization và audit invariant tests.
3. Manufacturing: chuẩn hóa mọi consumer receipt + retry/DLQ; loại dần synchronous `SaveChanges()` trong mutation paths.
4. Commerce: tách `SubmitOrder` thành aggregate/state machine + transactional Outbox; không cho endpoint tự đổi order status.

### P1 — locality và vận hành

1. Chia `ManufacturingPersistence.cs` theo workflow, xóa dần compatibility path sau khi integration tests chuyển sang port mới.
2. Chia Commerce `Program.cs` theo endpoint module và application command/query.
3. Content publication workflow + projection/cache invalidation.
4. Chuẩn hóa metrics: command latency, outbox age, consumer lag, DLQ count, saga stuck/recovery, authorization deny reason.

### P2 — scale có bằng chứng

Chỉ cân nhắc read replica, compiled query, CQRS read database hoặc partition/archive sau khi có p95/p99, query trace, data-retention requirement và load baseline. Không suy ra production readiness từ build hoặc container healthy.

## 6. Acceptance gates

- Build bốn API projects: không error; warning phải có owner (lần audit này Identity/Manufacturing/Content 0 warning, Commerce 1 warning `System.Diagnostics.DiagnosticSource` conflict).
- Architecture/communication boundary scripts pass.
- Mỗi mutation có input validation, authorization, tenant scope, cancellation và stable ProblemDetails/error code.
- Commerce order submit: duplicate key không tạo duplicate order/event; state transition bất hợp lệ bị reject.
- Manufacturing consumer: duplicate delivery không tạo duplicate side effect; RabbitMQ readiness phải là AMQP handshake, không chỉ port open.
- Identity: negative tests cho unauthenticated, wrong tenant/facility, missing permission, expired session/step-up.
- Content: publish authorization, version conflict, tenant isolation, public projection không lộ draft/private data.
- Full relevant integration matrix và authenticated Playwright phải chạy riêng; nếu thiếu Docker/credentials/runtime thì báo `environment-blocked`, không báo pass.
- Trước implementation tiếp theo: `git status`/diff phải được kiểm tra để bảo toàn dirty worktree; không dùng generated artifact hoặc image cũ làm bằng chứng source hiện tại.

## 7. Kết luận

Kiến trúc nên tiến hóa theo hướng **deep vertical slices**, không phải thêm abstraction ngang. Identity cần locality cho policy và admin use case; Manufacturing cần locality cho workflow và reliable messaging; Commerce cần aggregate/state machine làm trung tâm; Content cần publication workflow và projection nhẹ. Shared infrastructure chỉ giữ các cross-cutting concern thật sự chung: protocol, authorization primitives, resilience, outbox/idempotency/saga runtime, health và observability.
