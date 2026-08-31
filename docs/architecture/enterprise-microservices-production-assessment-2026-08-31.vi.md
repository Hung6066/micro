# Enterprise Microservices Production Assessment

Ngày đánh giá: 2026-08-31

## Kết luận ngắn

Hệ thống hiện tại **đã có nền tảng kiến trúc enterprise khá tốt ở mức source/repository** nhưng **chưa thể xem là đạt enterprise production hoàn chỉnh** cho quy mô rất lớn và mức bảo mật cao nhất.

Điểm mạnh hiện tại:

- Có shared platform packages với boundary tương đối rõ cho host bootstrap, messaging, auth, observability và health.
- Có tenant context chuẩn hóa bằng header + endpoint filter, đồng thời vẫn giữ contract cũ ở chế độ tương thích có telemetry.
- Có outbox, idempotency, saga persistence/recovery, Redis/health/security middleware dùng lại được giữa nhiều service.
- Có direction gate, persistence gate, communication gate, tenant-context gate và health contract gate đang pass trên source hiện tại.

Điểm chưa đủ để gọi là production enterprise hoàn chỉnh:

- Chưa có bằng chứng đầy đủ cho load, failover, chaos, multi-region, cross-service DR ở toàn bộ hệ thống.
- Frontend foundation đã là workspace package, nhưng các app vẫn đang consume qua `file:../shared/frontend-foundation`, nên boundary phát hành chưa thật sự tách rời như một platform package độc lập.
- Một số service vẫn còn truyền `tenantKey` khá sâu ở application/persistence seam; HTTP contract đã chuẩn hóa hơn nhưng seam nội bộ chưa hoàn toàn “context-only”.
- Tài liệu kiến trúc có mô tả mục tiêu rất cao như multi-region, auto-failover, HA database platform; các claim này hiện chưa được chứng minh end-to-end trong lượt audit này.

## So sánh với chuẩn microservices lớn

### 1. Shared nên giữ ở đâu

Theo current checkout, boundary package chung đang được mô tả khá đúng:

- `His.Hope.ServiceDefaults`: host composition, validation, OpenAPI, live/ready health.
- `His.Hope.Messaging.Abstractions`: envelope, outbox/inbox, idempotency, durable-job interfaces.
- `His.Hope.Messaging.RabbitMq|Redis|Sql`: adapter triển khai hạ tầng.

Evidence:

- `docs/architecture/shared-platform-packages.md:15`
- `docs/architecture/shared-platform-packages.md:16`
- `docs/architecture/shared-platform-packages.md:21`
- `docs/architecture/shared-platform-packages.md:23`
- `docs/architecture/shared-platform-packages.md:72`
- `docs/architecture/shared-platform-packages.md:121`

Đây là hướng đúng với guidance chính thống:

- Microsoft nhấn mạnh mỗi microservice phải sở hữu data + logic của chính nó, dưới vòng đời tự trị.
- AWS nhấn mạnh database-per-service giúp loose coupling; cross-service query phải qua API composition hoặc CQRS/read model.

Nguồn:

- Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice
- AWS Prescriptive Guidance: https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/database-per-service.html
- AWS Prescriptive Guidance: https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/api-composition.html
- AWS Prescriptive Guidance: https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/cqrs-pattern.html

### 2. Cái gì không nên shared

Repo cũng đã ghi khá đúng là shared package không được nuốt business autonomy:

- `docs/architecture/microservice-development-guide.vi.md:28`
- `docs/architecture/microservice-development-guide.vi.md:32`
- `docs/architecture/microservice-development-guide.vi.md:39`
- `docs/architecture/microservice-development-guide.vi.md:54`
- `docs/architecture/microservice-development-guide.vi.md:182`
- `docs/architecture/microservice-development-guide.vi.md:294`

Đánh giá:

- Shared transport, auth plumbing, observability, health, idempotency, outbox, saga, correlation là đúng.
- Shared domain model giữa bounded contexts là không đúng.
- Shared query/join trực tiếp DB giữa services là không đúng cho production scale.

### 3. Bounded context và service seam

Hướng chuẩn theo Azure là một microservice không nên span quá một bounded context. Repo tuyên bố nguyên tắc này trong kiến trúc tổng thể và cũng có gate layer direction riêng.

Evidence:

- `docs/architecture.md:109`
- `docs/architecture.md:110`
- `scripts/validate-service-architecture.ps1`
- Output gate hiện tại: `Service architecture gate passed: 45 service projects preserve Domain -> Application -> Infrastructure -> Api direction.`

Nguồn:

- Azure Architecture Center: https://learn.microsoft.com/en-us/azure/architecture/microservices/model/microservice-boundaries

## Trạng thái hiện tại trong codebase

### 0. Migration ownership và rolling upgrade

Trong lượt audit này đã đóng một rủi ro vận hành: Commerce, Content và Manufacturing
không còn tự ý chạy EF migration ở mọi lần khởi động. Các service chỉ chạy migration
khi bật rõ `Persistence:RunMigrationsOnStartup` hoặc `Persistence:MigrationOnly`; ở
environment không phải Development/Testing nếu thiếu cờ, process fail-closed. Chế độ
`MigrationOnly` thoát trước khi mở HTTP workload, phù hợp để chạy bằng migration Job.

Evidence:

- `src/Services/CommerceService/CommerceService.Api/Program.cs:50`
- `src/Services/ContentService/ContentService.Api/Program.cs:66`
- `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs:65`
- `scripts/validate-database-migration-contract.ps1:113`

Compose development vẫn bật cờ một cách tường minh để giữ trải nghiệm local/demo;
production không được suy luận migration từ startup của replica.

### 1. Shared backend foundation

`ServiceDefaults` đang chuẩn hóa request localization và live/ready health endpoints:

- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/ServiceDefaultsExtensions.cs:25`
- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/ServiceDefaultsExtensions.cs:68`
- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/ServiceDefaultsExtensions.cs:92`
- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/ServiceDefaultsExtensions.cs:97`
- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/ServiceDefaultsExtensions.cs:102`
- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/HisHopeHealthRoutes.cs:6`
- `src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/HisHopeHealthRoutes.cs:7`

`His.Hope.Infrastructure` đang gom đúng loại concern hạ tầng dùng chung:

- `src/Shared/Infrastructure/His.Hope.Infrastructure/Outbox/OutboxServiceExtensions.cs:10`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Outbox/OutboxServiceExtensions.cs:14`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Outbox/OutboxProcessor.cs:17`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Idempotency/InboxDeduplicator.cs:9`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/RedisConnectionFactory.cs:7`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/SecurityHeadersMiddleware.cs:15`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/DpopAccessTokenMiddleware.cs:112`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Messaging/EventBusSecurity.cs:9`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/PersistentSagaOrchestrator.cs:24`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Saga/SagaRecoveryService.cs:24`

Đánh giá:

- Shared foundation backend đang đi đúng hướng.
- Rủi ro chính là package này có thể thành “shared mega-kernel” nếu tiếp tục hút business-specific policy vào trong.

### 2. Tenant context và tenancy boundary

Repo đã có TenantContext thật ở edge:

- `src/Shared/AspNetCore/His.Hope.AspNetCore/Tenancy/TenantContextEndpointFilter.cs:11`
- `src/Shared/AspNetCore/His.Hope.AspNetCore/Tenancy/TenantContextEndpointFilter.cs:19`
- `src/Shared/AspNetCore/His.Hope.AspNetCore/Tenancy/TenantContextEndpointFilter.cs:38`
- `src/Shared/AspNetCore/His.Hope.AspNetCore/Tenancy/TenantContextEndpointFilter.cs:57`
- `src/Shared/AspNetCore/His.Hope.AspNetCore/Tenancy/TenantContextEndpointFilter.cs:62`
- `src/Shared/AspNetCore/His.Hope.AspNetCore/Tenancy/TenantContextEndpointFilter.cs:80`

Manufacturing, Commerce và Content đã được nối vào seam này:

- `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs:14`
- `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs:18`
- `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs:64`
- `src/Services/ContentService/ContentService.Api/Program.cs:18`
- `src/Services/ContentService/ContentService.Api/Program.cs:20`
- `src/Services/ContentService/ContentService.Api/Program.cs:64`
- `src/Services/ContentService/ContentService.Api/Program.cs:161`
- `src/Services/CommerceService/CommerceService.Api/Composition/CommerceServiceHostExtensions.cs:24`
- `src/Services/CommerceService/CommerceService.Api/Composition/CommerceServiceHostExtensions.cs:25`

Gate đi kèm đang pass:

- `scripts/verify-tenant-context-contract.ps1:83`
- Output gate hiện tại: `Checked 13 service API projects and 5 frontend roots. Tenant context contract passed.`

Đánh giá:

- Đây là một bước enterprise đúng hướng: tenant được đưa về context/header thay vì để trôi nổi trong DTO/query.
- Nhưng ở seam sâu hơn, Commerce/Content vẫn còn nhiều method persistence nhận `tenantKey` trực tiếp. Điều đó chưa sai, nhưng cho thấy context-only mới hoàn thành tốt ở HTTP boundary, chưa hoàn tất ở toàn bộ module seam.

### 3. Service-owned data

Commerce, Content, Identity đang có DbContext và physical table naming riêng:

- `src/Services/CommerceService/CommerceService.Infrastructure/Persistence/CommercePersistence.cs:33`
- `src/Services/CommerceService/CommerceService.Infrastructure/Persistence/CommercePersistence.cs:133`
- `src/Services/CommerceService/CommerceService.Infrastructure/Persistence/CommercePersistence.cs:143`
- `src/Services/ContentService/ContentService.Infrastructure/Persistence/ContentPersistence.cs:29`
- `src/Services/ContentService/ContentService.Infrastructure/Persistence/ContentPersistence.cs:109`
- `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:153`
- `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:154`

Đánh giá:

- Service-owned schema/table naming đang khá nhất quán.
- Điều này phù hợp với database-per-service và domain ownership.
- Tuy nhiên, một số service vẫn còn bridge/in-memory adapter hoặc legacy event bus path; với production scale cần tiếp tục thu hẹp đường chạy “local dev only” để tránh drift giữa dev và prod.

### 4. BFF security boundary

BFF layer hiện đang dùng cookie session + CSRF thay vì đẩy token vào browser storage:

- `docs/api/bff-endpoints.md:115`
- `docs/api/bff-endpoints.md:116`
- `src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs:35`
- `src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs:36`
- `src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs:37`
- `src/Bff/His.Hope.Bff.Core/Authentication/CsrfValidatorMiddleware.cs:44`
- `src/Bff/His.Hope.Bff.Core/Authentication/CsrfValidatorMiddleware.cs:63`

Đánh giá:

- Đây là pattern tốt cho web enterprise.
- Nhưng production-grade còn cần chứng minh bằng browser E2E và negative tests xuyên đủ các BFF/module quan trọng.

### 5. Identity security posture

Identity đang có nhiều guardrail tốt:

- Vault bắt buộc trong production cho các path nhạy cảm:
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceSupportTypes.cs:83`
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceSupportTypes.cs:95`
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceSupportTypes.cs:97`
- MFA secret encryption đòi Vault transit trong production:
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:638`
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:645`
- Readiness có Vault check:
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:614`
- Persistence standardization:
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:153`
  - `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs:154`

Đánh giá:

- Identity là service trưởng thành nhất về security posture trong codebase hiện tại.
- Dù vậy, enterprise production ở mức rất cao vẫn cần external validation như load, red-team/pentest, key rotation drill, outage/failover drill, federation chaos test.

## Frontend/app boundary

Frontend foundation đã tiến xa hơn trước: có package riêng, API surface test và tài liệu tích hợp.

Evidence:

- `shared/frontend-foundation/package.json`
- `shared/frontend-foundation/src/api-surface.spec.ts:2`
- `shared/frontend-foundation/src/api-surface.spec.ts:205`
- `shared/frontend-foundation/README.md:73`
- `shared/frontend-foundation/README.md:80`

Các app hiện đã chuyển sang consume theo SemVer release contract của foundation:

- `admin-app/package.json:26`
- `manufacturing-buyer-app/package.json:22`
- `operator-mobile/package.json:39`

Đánh giá:

- Đây là mô hình tốt cho platform team + product team scale độc lập hơn: app pin
  SemVer (`^1.1.0`) trong manifest, còn npm workspace chỉ cung cấp package local
  khi phát triển. CI có thể thay bằng registry artifact mà không đổi source import.
- `validate-shared-package-governance.ps1` hiện fail nếu consumer quay lại `file:`
  dependency hoặc khai báo không phải SemVer.

## Những gì đã đạt

Trong lượt tiếp theo đã bổ sung profile k6 tenant-scale cho Manufacturing tại
`tests/Load/k6/manufacturing-scale.js`. Profile mặc định ramp lên 500 VU, bắt buộc
`AUTH_TOKEN` và `TENANT_KEY`, gửi `X-HisHope-Tenant`, và đo p95/p99 cho dashboard,
production-order và event-receipt reads. `k6 inspect` pass; kết quả SLO thực tế vẫn
cần chạy trên môi trường có dữ liệu/outbox backlog đại diện.

Quy trình chạy load, failover và DR được chuẩn hóa tại
`docs/operations/scale-failover-dr-runbook.vi.md`.

Ngày 2026-08-31 đã chạy Compose dependency restart drill thành công:
`artifacts/runtime/compose-dependency-failover.json` ghi nhận cả Redis,
RabbitMQ và PostgreSQL phục hồi healthy; các endpoint health của gateway,
Manufacturing, Commerce và Content trả HTTP 200 sau drill. Kết quả này chỉ là
single-node recovery evidence, không thay thế HA/region failover.

Artifact frontend foundation đã được đóng gói tại
`artifacts/packages/his-hope-frontend-foundation-1.1.0.tgz`; validator
`scripts/validate-frontend-artifact.ps1` xác nhận metadata, entry points và
không có dependency `file:`. Tất cả consumer manifests và lockfiles của
frontend foundation (bao gồm clinical app) hiện dùng SemVer; `file:` còn lại
chỉ thuộc mobile-foundation riêng. Production-phase gate chạy lại pass cho DPoP,
RFC 9700 (9/9), Identity tests, SIEM/WORM, tenant-context và JWKS rotation;
gate dừng ở load SLO vì baseline chưa có HTTP requests thực đo.

Scale-readiness live snapshot ngày 2026-08-31 đã chạy với PostgreSQL local
port 5433 và pass: `max_connections=100`, current connections=21 (21%),
connection budget 80 còn 20 headroom, 9 index scale bắt buộc, active/created
listing query plans dùng composite index. Identity hiện mới có 51 user và các
bảng audit/outbox đều chưa lớn, nên chưa có cơ sở để bật partition vật lý.

Audit seam tại `artifacts/evidence/tenant-context-seams.json` xác nhận
HTTP/frontend edge không còn selector `tenantKey` (0 occurrence), nhưng vẫn
ghi nhận seam nội bộ cần migration có kiểm soát: Manufacturing persistence
3032 dòng, Commerce 198, Content 168. Không được xóa cơ học vì các giá trị này
đồng thời là partition predicate, event compatibility và cross-tenant safety;
cần chuyển từng port sang scoped context rồi mới loại bỏ tham số.

Production Kustomize render cũng đã được chạy với `--load-restrictor
LoadRestrictionsNone`: 166 documents render thành công, overlay không có
Deployment Commerce/Content/Manufacturing nên migration contract hiện gồm 7
service. Artifact render nằm tại `artifacts/k8s/prod.yaml`.

### P0 đã đạt ở mức source/repo

- Layer direction giữa Domain -> Application -> Infrastructure -> Api có gate pass.
- Persistence boundary có gate pass.
- Communication boundary có gate pass.
- Tenant context contract có gate pass.
- Health contract có gate pass.

### P1 đã đạt một phần

- Shared backend packages đã khá đúng seam.
- BFF security boundary đang đúng hướng.
- Identity control plane có permission/resource policy/boundary model đủ sâu.
- Manufacturing có outbox + saga + recovery seam cho workflow liên service.

### P2 chưa chứng minh đủ

- Load/perf ở tenant lớn, user lớn, outbox backlog lớn.
- Redis/DB/RabbitMQ failover với business SLO.
- Multi-region hoặc DR drill cấp hệ thống.
- Frontend artifact versioning độc lập giữa shared foundation và app consumers.
- Chuẩn hóa context-only xuyên toàn bộ service seam, không chỉ edge contract.

## Khuyến nghị nâng cấp để đạt enterprise production thực sự

### 1. Giữ shared nhỏ nhưng sâu

Tiếp tục chỉ shared những thứ sau:

- tenant/context resolution
- auth plumbing
- observability
- health/readiness contract
- outbox/inbox/idempotency
- retry/timeouts/resilience
- saga runtime
- secrets/key access adapters

Không shared:

- aggregate/domain model giữa bounded contexts
- query model liên service
- service-specific policy/rule engine
- DTO nghiệp vụ đặc thù chỉ có một consumer

### 2. Chuẩn hóa “context-only” đến seam nội bộ

Mục tiêu tiếp theo không phải xóa sạch mọi `tenantKey` ngay lập tức, mà là:

- edge dùng `X-HisHope-Tenant` + active context duy nhất
- application/use case lấy tenant từ context abstraction
- persistence chỉ nhận selector rõ ràng ở những seam thật sự cần query partition
- telemetry ghi rõ legacy selector usage cho đến khi tắt hoàn toàn

### 3. Tách release boundary của frontend foundation

Nên nâng cấp từ `file:../shared/frontend-foundation` sang release discipline thật:

- build package versioned mỗi release
- app pin version thay vì source path ở staging/prod
- contract test giữa foundation và app shells
- visual regression cho core shell primitives

### 4. Chỉ partition khi có đúng kiểu dữ liệu

Theo PostgreSQL, partition phải thiết kế cẩn thận; thiết kế kém có thể làm planning/execution tệ đi. Với codebase này:

- phù hợp cho audit/event/outbox/telemetry/time-series lớn
- không nên partition sớm các bảng identity lõi, user-role-claim, current-state transactional tables

### 5. Enterprise security level cao

Áp dụng chặt hơn các nguyên tắc:

- least privilege + deny by default + authorization on every request
- DPoP/PKCE/FAPI-aligned hardening cho client phù hợp
- Vault/KMS rotation drill định kỳ
- tenant isolation theo tier: shared, dedicated, rồi mới tới crypto/data isolation mạnh hơn

## Phán quyết cuối

Nếu chấm theo góc nhìn “kiến trúc cho scale, reuse, bảo mật, tốc độ phát triển”:

- **Shared/backend platform:** tốt, đang đi đúng hướng.
- **Service autonomy/data ownership:** khá tốt, nhưng còn vài seam legacy và đường đi compatibility.
- **Frontend platformization:** tiến bộ rõ, nhưng release boundary chưa đủ cứng.
- **Security architecture:** mạnh ở Identity và web boundary; cần thêm evidence vận hành và negative testing xuyên hệ.
- **Enterprise production readiness tổng thể:** **chưa đạt hoàn chỉnh**, nhưng đã có khung tốt để nâng lên mà không phá vỡ kiến trúc.

## Nguồn chính thống tham chiếu

- Microsoft Learn, Data sovereignty per microservice:
  https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice
- Azure Architecture Center, Identify microservice boundaries:
  https://learn.microsoft.com/en-us/azure/architecture/microservices/model/microservice-boundaries
- AWS Prescriptive Guidance, Database-per-service:
  https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/database-per-service.html
- AWS Prescriptive Guidance, Shared-database-per-service:
  https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/shared-database.html
- AWS Prescriptive Guidance, API composition:
  https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/api-composition.html
- AWS Prescriptive Guidance, CQRS:
  https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/cqrs-pattern.html
- AWS Prescriptive Guidance, Saga:
  https://docs.aws.amazon.com/prescriptive-guidance/latest/modernization-data-persistence/saga-pattern.html
- OpenTelemetry:
  https://opentelemetry.io/docs/what-is-opentelemetry/
- Kubernetes readiness/liveness/startup probes:
  https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/
- RFC 9700, OAuth 2.0 Security BCP:
  https://datatracker.ietf.org/doc/html/rfc9700
- PostgreSQL declarative partitioning:
  https://www.postgresql.org/docs/current/ddl-partitioning.html
- OWASP Authorization Cheat Sheet:
  https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html
