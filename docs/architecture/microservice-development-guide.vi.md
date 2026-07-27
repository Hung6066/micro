# His.Hope Microservice Development Guide

Tài liệu này là quy ước bắt buộc khi tạo microservice mới hoặc mở rộng service hiện có. Mục tiêu là giữ domain và dữ liệu độc lập, đồng thời dùng chung platform capability đã được chuẩn hóa.

## 1. Quy tắc phân chia

Đưa code vào shared platform khi code không biết domain cụ thể, được dùng bởi ít nhất ba service, có API ổn định và test độc lập. Giữ code trong service khi nó chứa business rule, entity, aggregate, schema hoặc workflow riêng.

Không để service tham chiếu `Domain`, `Application` hoặc `Infrastructure` của service khác.

```text
Service API
  -> Contracts, AspNetCore, Authorization, Validation, Resilience
  -> Messaging.Abstractions, Observability, ServiceDefaults

Service Infrastructure
  -> DbContext/migrations riêng
  -> Messaging RabbitMq/Redis/Sql adapters
  -> external clients và persistence riêng
```

## 2. Shared package ownership

| Package | Trách nhiệm | Không chứa |
| --- | --- | --- |
| `His.Hope.Core` | primitive/domain abstraction ổn định | REST DTO, EF, entity cụ thể |
| `His.Hope.Contracts` | REST/gRPC DTO, pagination, query, error, event, bulk | business logic |
| `His.Hope.AspNetCore` | auth, correlation, ProblemDetails, OpenAPI, headers | domain/persistence |
| `His.Hope.Authorization` | permission và role policies | permission data riêng service |
| `His.Hope.Validation` | validation registration và MediatR pipeline | business validator cụ thể |
| `His.Hope.Resilience` | retry, timeout, breaker, concurrency | business fallback |
| `His.Hope.Messaging.Abstractions` | outbox, inbox, idempotency, durable job contracts | transport implementation |
| `His.Hope.Messaging.RabbitMq` | RabbitMQ publisher adapter | domain event decision |
| `His.Hope.Messaging.Redis` | Redis idempotency và durable job adapter | business workflow |
| `His.Hope.Messaging.Sql` | SQL outbox, inbox, idempotency adapter | provider/migration scheduling |
| `His.Hope.Observability` | audit, logging, tracing, metrics contracts | exporter vendor code |
| `His.Hope.Observability.OpenTelemetry` | OTLP/Prometheus instrumentation | secret/deployment policy |
| `His.Hope.Persistence` | explicit EF migration runner | migration files của service |
| `His.Hope.ServiceDefaults` | golden-path startup, validation, health, resilience | endpoint/domain logic |

## 3. Tạo microservice mới

### 3.1 Xác định bounded context

Trước khi tạo project, ghi rõ:

- domain và aggregate service sở hữu;
- database/schema service sở hữu;
- public REST/gRPC API;
- event publish/consume;
- permission và security boundary;
- lý do cần deploy/scale độc lập.

Không tạo service mới chỉ để tách một controller nếu chưa có ownership rõ ràng.

### 3.2 Cấu trúc project

```text
src/Services/OrderService/
  OrderService.Domain/
  OrderService.Application/
  OrderService.Infrastructure/
  OrderService.Api/
tests/Services/OrderService/
  OrderService.Domain.Tests/
  OrderService.Application.Tests/
  OrderService.IntegrationTests/
```

- `Domain`: entity, value object, aggregate, domain event.
- `Application`: command/query, handler, DTO nội bộ, port, validator.
- `Infrastructure`: EF Core, external client, messaging, migrations.
- `Api`: endpoint, auth binding, transport mapping, host startup.

### 3.3 Shared references

API project nên tham chiếu các package sau:

```xml
<ProjectReference Include="..\..\..\Shared\Contracts\His.Hope.Contracts\His.Hope.Contracts.csproj" />
<ProjectReference Include="..\..\..\Shared\AspNetCore\His.Hope.AspNetCore\His.Hope.AspNetCore.csproj" />
<ProjectReference Include="..\..\..\Shared\Authorization\His.Hope.Authorization\His.Hope.Authorization.csproj" />
<ProjectReference Include="..\..\..\Shared\Validation\His.Hope.Validation\His.Hope.Validation.csproj" />
<ProjectReference Include="..\..\..\Shared\Resilience\His.Hope.Resilience\His.Hope.Resilience.csproj" />
<ProjectReference Include="..\..\..\Shared\Messaging\His.Hope.Messaging.Abstractions\His.Hope.Messaging.Abstractions.csproj" />
<ProjectReference Include="..\..\..\Shared\Observability\His.Hope.Observability\His.Hope.Observability.csproj" />
<ProjectReference Include="..\..\..\Shared\ServiceDefaults\His.Hope.ServiceDefaults\His.Hope.ServiceDefaults.csproj" />
```

Infrastructure tham chiếu thêm RabbitMQ/Redis/SQL adapter và `His.Hope.Persistence` khi cần. Khi internal NuGet feed sẵn sàng, thay ProjectReference bằng package version; không trộn hai kiểu trong cùng deployment profile.

## 4. Host startup chuẩn

```csharp
using His.Hope.Authorization;
using His.Hope.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisHopeServiceDefaults(
    builder.Configuration, "order-service");
builder.Services.AddHisHopeAuthorization();
builder.Services.AddHisHopeValidation(typeof(Program).Assembly);

builder.Services.AddOrderApplication();
builder.Services.AddOrderInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseHisHopeServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();
app.MapHisHopeHealthEndpoints();
app.MapOrderEndpoints();
app.Run();
```

Không tự tạo lại correlation middleware, ProblemDetails, validation middleware, health endpoint hoặc resilience registration nếu capability đã có trong shared package.

## 5. Thêm API endpoint

### 5.1 Contract và query

DTO public đặt trong `His.Hope.Contracts`; không expose EF entity/domain entity trực tiếp. Danh sách dùng pagination contract chung, không tạo `PagedResult<T>` riêng.

```csharp
public sealed record GetOrdersRequest(
    string? Search,
    string? Sort,
    string? Cursor,
    int PageSize = 20);
```

Quy tắc:

- whitelist field được sort/filter;
- giới hạn page size;
- server-side sorting/filtering cho dataset lớn;
- cursor dựa trên stable ordering key;
- query state phải có thể đồng bộ với URL ở frontend.

### 5.2 Error contract

Mọi lỗi phải đi qua ProblemDetails chung và có `status`, `code`, `correlationId`, `errors` cho validation. Không trả stack trace, SQL detail, password, token hoặc secret.

### 5.3 Authorization

Permission code theo resource/action, ví dụ:

```text
orders.view
orders.create
orders.update
orders.cancel
```

Backend luôn kiểm tra permission; frontend chỉ dùng permission để ẩn/disable action và không được coi đó là security boundary.

## 6. Database và migration

Mỗi service có `DbContext` và migration riêng:

```csharp
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));
builder.Services.AddHisHopeMigrationRunner<OrderDbContext>();
```

Production migration chạy qua deployment job/worker:

```csharp
using var scope = host.Services.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
await runner.MigrateAsync(cancellationToken);
```

Không dùng `EnsureCreatedAsync` production. Không để nhiều web replica chạy migration đồng thời. Schema change lớn phải theo expand -> migrate -> contract để hỗ trợ rolling deployment.

## 7. Messaging và durable jobs

Event dùng `EventEnvelope` với event ID, type, schema version, occurred time, correlation ID và causation ID. Payload thuộc service phát hành; service consumer không tham chiếu domain assembly của producer.

Transaction nghiệp vụ và outbox phải cùng transaction database. Consumer gọi `IInboxStore.TryBeginAsync` trước xử lý. Mutation HTTP dùng `IIdempotencyStore` với request fingerprint.

Chọn adapter:

- RabbitMQ cho transport publish/consume.
- SQL cho transactional outbox/inbox/idempotency.
- Redis cho distributed idempotency và durable jobs.

Bulk import, export lớn, report và workflow dài phải chạy theo flow:

```text
API -> enqueue -> worker claim -> progress -> complete/dead-letter
```

Không chạy job dài trong HTTP request và không dùng in-memory state production.

## 8. Resilience HTTP/gRPC

```csharp
builder.Services.AddHisHopeResilience(builder.Configuration);

builder.Services.AddHttpClient("identity", client =>
{
    client.BaseAddress = new Uri(configuration["Services:Identity:BaseUrl"]!);
})
.AddHttpMessageHandler(sp => new HisHopeResilienceHandler(
    sp.GetRequiredService<HisHopeResiliencePipelines>()
        .CreateHttp("identity")));
```

Chỉ retry lỗi transient. Không retry validation, 401/403, business conflict hoặc mutation không idempotent. Timeout, retry và breaker phải cấu hình theo downstream.

## 9. Audit và observability

Audit bắt buộc cho create/update/delete, permission/role change, secret rotation, bulk action, login failure, MFA change và session revoke. Audit cần actor, subject, action, resource, result, correlation ID và timestamp; không ghi token/password/PHI dư thừa.

```csharp
builder.Services.AddHisHopeOpenTelemetryExporters(
    builder.Configuration, "order-service");
```

OTLP endpoint lấy từ `OpenTelemetry:OtlpEndpoint` hoặc key compatibility `Otlp:Endpoint`. Liveness chỉ kiểm tra process; readiness kiểm tra dependency bắt buộc.

## 10. Frontend feature mới

Feature phải dùng `@his-hope/frontend-foundation` cho button, form, dialog, toast, badge, tabs, DataTable, tokens và theme.

Mỗi page cần:

- i18n service, không hard-code text;
- permission-aware action;
- DataTable server query, URL state, sort/filter/pagination;
- loading giữ layout, empty/error/retry/offline state;
- mobile chuyển table thành item/card khi không đủ chiều rộng;
- light/dark/high-contrast theme;
- keyboard navigation và focus contract.

Chỉ tạo component riêng khi nó chứa workflow/domain UX riêng và không có component tương đương trong foundation.

## 11. Quy trình khi thêm mới

### Thêm field/API

1. Xác định field thuộc domain service nào.
2. Cập nhật entity, migration và application handler.
3. Cập nhật `His.Hope.Contracts` nếu field public.
4. Cập nhật validator, error code, audit/event schema.
5. Cập nhật frontend model, table column, i18n và permission.
6. Thêm contract test và backward-compatibility test.

### Thêm shared component

1. Tìm component tương tự trước khi tạo mới.
2. Viết public API và accessibility contract.
3. Định nghĩa loading, error, empty, keyboard, focus và responsive states.
4. Hỗ trợ light/dark/high-contrast.
5. Thêm story, interaction test, visual regression.
6. Version package, changelog và migration guide khi public API đổi.

### Thêm shared backend capability

1. Chứng minh capability dùng bởi ít nhất ba service hoặc là security/platform requirement.
2. Đặt contract vào package phù hợp.
3. Đặt implementation adapter ở package riêng.
4. Không tham chiếu domain service.
5. Thêm `AddHisHope...` DI extension.
6. Thêm health, metrics, logging và tests.
7. Cập nhật package version, docs và release notes.

## 12. Versioning và breaking change

Không breaking thường là thêm endpoint, thêm optional response field hoặc thêm event version song song. Breaking change là xóa/đổi kiểu field, đổi semantic error, xóa permission hoặc đổi thứ tự pagination.

Breaking change bắt buộc có API diff review, compatibility test, migration guide, deprecation window và rollback plan.

## 13. Bảng quyết định shared hay service-specific

| Câu hỏi | Nếu có | Quyết định |
| --- | --- | --- |
| Code có chứa business rule hoặc thuật ngữ domain không? | Có | Giữ riêng trong service |
| Code có truy cập entity/DbContext/schema của service không? | Có | Giữ riêng trong service |
| Code được dùng bởi từ ba service trở lên với cùng semantics không? | Có | Cân nhắc promote lên shared |
| Code là auth, correlation, ProblemDetails, validation pipeline hoặc observability nền tảng không? | Có | Dùng shared package |
| Code chỉ giống nhau về syntax nhưng khác business meaning không? | Có | Không share; giữ duplicate có chủ đích |
| Code cần thay đổi theo một service nhanh hơn các service khác không? | Có | Giữ riêng |
| Code có public API ổn định và test độc lập được không? | Có | Có thể đưa vào shared package |
| Code chỉ để tránh vài dòng lặp lại không? | Có | Không tạo abstraction |
| Code cần cùng lifecycle, version và release với tất cả service không? | Có | Shared package phù hợp |
| Code cần provider/configuration riêng của service không? | Có | Đặt adapter trong Infrastructure service |

### 13.1 Có chấp nhận duplicate code không?

**Có. Duplicate code được chấp nhận khi nó bảo vệ boundary hoặc business autonomy.** Không phải mọi duplication đều cần loại bỏ.

#### Chấp nhận duplicate

- Mapping DTO riêng cho từng bounded context.
- Validator có rule giống nhau nhưng message/semantic khác nhau.
- Adapter gọi cùng loại external API nhưng khác retry/fallback policy.
- Migration, seed data và DbContext của từng service.
- Use case tương tự nhưng có authorization, transaction hoặc audit khác nhau.
- Một đoạn code nhỏ, ổn định, chỉ xuất hiện ở một hoặc hai service.

#### Không chấp nhận duplicate

- Cùng một cách parse pagination ở nhiều service.
- Mỗi service tự viết ProblemDetails hoặc correlation middleware.
- Mỗi service tự định nghĩa event envelope khác nhau.
- Cùng một auth/security check nhưng implementation khác nhau.
- Copy-paste DataTable, dialog, form field hoặc theme token giữa ba app.
- Cùng một lỗi bảo mật đã được sửa ở một service nhưng còn bản copy lỗi ở service khác.

### 13.2 Ngưỡng promote code lên shared

Không promote chỉ vì code xuất hiện hai lần. Dùng các ngưỡng sau:

| Mức | Điều kiện | Hành động |
| --- | --- | --- |
| 0 | Một service, business-specific | Giữ riêng |
| 1 | Hai service, code giống nhau nhưng semantics chưa chắc chắn | Duplicate có kiểm soát, ghi issue theo dõi |
| 2 | Ba service, semantics và lifecycle giống nhau | Thiết kế shared contract |
| 3 | Security/platform requirement hoặc lỗi phải fix đồng loạt | Promote sớm, bắt buộc có version và test |
| 4 | Shared package có consumer production | Có changelog, compatibility policy và release gate |

### 13.3 Quy trình review duplicate

Khi tạo code trùng lặp, PR cần ghi ngắn gọn:

```text
Duplicate decision:
- Vì sao chưa đưa vào shared:
- Khác biệt business/ownership:
- Số consumer hiện tại:
- Điều kiện promote sau này:
```

Khi promote lên shared, phải kiểm tra:

1. API có ổn định và không kéo domain dependency vào package không.
2. Consumer có cùng behavior, không chỉ cùng tên method không.
3. Có test cho success, error, concurrency, accessibility hoặc retry state phù hợp không.
4. Có migration path cho code cũ không.
5. Có version, changelog và compatibility check không.

### 13.4 Nguyên tắc ngắn gọn

```text
Duplicate business code để giữ service độc lập: chấp nhận.
Duplicate platform/security code: không chấp nhận.
Abstraction chỉ để giảm vài dòng: không tạo.
Shared package phải có contract, owner, version và test.
```

## 14. Checklist trước merge

### Architecture

- [ ] Bounded context và data ownership rõ ràng.
- [ ] Không tham chiếu service khác.
- [ ] Shared code không chứa business rule riêng service.

### API/security

- [ ] Dùng `His.Hope.Contracts` và pagination chung.
- [ ] Có ProblemDetails, error code, correlation ID.
- [ ] Có server-side authorization và audit cho action nhạy cảm.
- [ ] Có concurrency/idempotency khi cần.

### Persistence/messaging

- [ ] DbContext/migration riêng service.
- [ ] Không dùng `EnsureCreatedAsync` production.
- [ ] Outbox/inbox/idempotency dùng durable adapter.
- [ ] Job dài dùng durable worker.

### Frontend

- [ ] Dùng shared foundation và token.
- [ ] Có i18n, theme, responsive và accessibility.
- [ ] Có loading, empty, error, retry và offline state.

### Verification

```powershell
dotnet restore His.Hope.sln
dotnet build His.Hope.sln --no-restore
dotnet test His.Hope.sln --no-restore
pwsh -NoProfile -File scripts/validate-shared-platform-boundaries.ps1
pwsh -NoProfile -File scripts/validate-api-platform-conventions.ps1
pwsh -NoProfile -File scripts/pack-shared-platform.ps1
```

Với frontend, chạy thêm lint, typecheck, axe/accessibility, keyboard test và visual regression trên desktop/tablet/mobile.

## 15. Ví dụ ownership

```text
His.Hope.Contracts
  - PagedResult<T>, ProblemDetails, EventEnvelope, BulkJob

OrderService
  - Order entity/rules
  - OrderDbContext/migrations
  - orders.* permissions
  - orders.order-created.v1 payload

His.Hope.Messaging.Sql
  - outbox/inbox/idempotency storage
His.Hope.Messaging.RabbitMq
  - transport
@his-hope/frontend-foundation
  - DataTable/form/dialog/theme/i18n
Order frontend feature
  - column definitions, route mapping, labels, workflow
```

Khi chưa chắc, để code trong service trước. Chỉ promote lên shared sau khi có consumer thực tế, public contract rõ ràng và test độc lập.
