# Manufacturing Service — Microservices và Clean Architecture

## Phạm vi

Manufacturing Service là một microservice độc lập, sở hữu dữ liệu sản xuất riêng và giao tiếp với các bounded context khác qua API/event contract. Buyer app không truy cập database hoặc domain nội bộ; API Gateway là ingress duy nhất.

## Ranh giới microservice

| Context | Trách nhiệm | Không sở hữu |
|---|---|---|
| Manufacturing | nguyên liệu, lot, recipe, hao hụt, máy, QC, production batch, costing | user/role, catalog thương mại, thanh toán |
| Procurement | supplier, PO, inbound receipt | giá bán và order buyer |
| Buyer/Commerce | catalog và đặt hàng | trạng thái nội bộ của lot/máy |
| Identity | tenant, user, OIDC, policy | dữ liệu sản xuất |

Mỗi service có database/schema và migration riêng. Liên kết liên-service dùng event versioned (`*.v1`) với `eventId`, `schemaVersion`, `occurredAt`, `correlationId`, `facilityId`; không dùng foreign key xuyên database.

## Clean Architecture mục tiêu

```text
API (HTTP/auth/DTO) -> Application (use cases/ports) -> Domain (entities/rules)
                                  ^
                                  |
                       Infrastructure (EF Core/RabbitMQ)
```

- **Domain**: quy tắc yield, loss, FEFO, reservation, QC disposition, production state machine; không tham chiếu ASP.NET Core, EF Core hoặc RabbitMQ.
- **Application**: command/query handlers, tenant/facility authorization ports, transaction boundaries và event ports.
- **Infrastructure**: `ManufacturingDbContext`, repositories, outbox dispatcher, RabbitMQ consumer, migrations.
- **API**: route binding, authentication/tenant claim, HTTP status mapping; không tính toán nghiệp vụ trực tiếp.

## Trạng thái hiện tại và lộ trình

Các business flow đã được cô lập trong bounded microservice. `ManufacturingService.Api` hiện là composition root và HTTP adapter; `ManufacturingService.Application` giữ policy/use case; `ManufacturingService.Infrastructure` sở hữu EF Core, migrations, stores và messaging.

Đã hoàn tất các seam sau mà không thay đổi HTTP contract:

1. Costing/yield calculator thuần trong Domain.
2. Policy procurement, reservations và production trong Application.
3. EF repositories, migrations, outbox dispatcher và RabbitMQ consumer trong Infrastructure.
4. Đăng ký DI và migrate database qua `AddManufacturingInfrastructure`/`MigrateManufacturingDatabase`.

Lộ trình tiếp theo là audit các service hiện hữu để tái sử dụng cùng shared core (auth, contracts, service defaults, messaging) và bổ sung contract tests ở từng ingress.

Mỗi bước phải giữ các gate: build không lỗi, migration/app startup, tenant boundary, protected routes trả `401`, smoke manufacturing, RabbitMQ `unack=0`, và buyer E2E.

## Quy tắc mở rộng

- Không thêm bảng dùng chung giữa services.
- Không gọi trực tiếp DbContext từ service khác.
- Event consumer phải idempotent theo `(eventType, aggregateId)` và chuyển poison message sang dead-letter/không requeue.
- Mọi projection CEO phải đánh dấu dữ liệu thiếu giá/ước tính; không được trình bày estimate như actual.

## Vertical slice đã tách lớp (2026-08-25)

- `ManufacturingService.Domain` giữ calculator yield/loss/cost thuần.
- `ManufacturingService.Application` chứa `CostProjectionUseCase` và các policy nghiệp vụ.
- `ManufacturingService.Infrastructure` chứa `Persistence`, `Messaging`, `Migrations` và composition extension.
- Shared auth, contracts, infrastructure và service defaults được dùng qua project references chung.
- API không còn biên dịch trực tiếp source persistence/messaging; boundary được kiểm chứng bằng build Docker, migration startup, smoke và buyer E2E.
