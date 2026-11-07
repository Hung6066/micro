# Audit chuẩn hóa service

## Kết luận kiến trúc

Repository đã có các seam dùng chung cần tái sử dụng:

- `His.Hope.Contracts`: API DTO, pagination, query contract, `ApiErrorCodes`, `ApiProblemResponse` và route contract.
- `His.Hope.Configuration`: runtime endpoint binding và `ServicePluginRegistry` để bật/tắt module ở gateway/BFF.
- `His.Hope.ServiceDefaults`: ProblemDetails, correlation, health, localization, resilience, observability và security middleware.
- `His.Hope.Messaging.*`: event envelope, outbox/inbox và giao tiếp bất đồng bộ.
- `His.Hope.Infrastructure.Messaging`: compatibility registration cho consumer legacy còn cần `SubscribeAsync`.

Vì vậy không tạo thêm một lớp repository/specification/communication framework toàn cục nếu chưa có use case chứng minh. Service module được coi là vertical slice có composition root riêng; gateway chỉ publish route khi module được bật và endpoint runtime tồn tại. Tắt module phải là fail-closed, không phải xóa authorization.

## Phạm vi audit

Chạy:

```powershell
./scripts/audit-service-standardization.ps1
```

Script quét toàn bộ C# production code dưới `src` (service, BFF, gateway và shared module), loại `bin/obj/Migrations` và test theo các nhóm: inline HTTP result/error, configuration indexer, connection string, loopback/default endpoint, secret-shaped value, literal pagination, route mapping và repeated hardcoded string. Kết quả là inventory để xử lý theo ownership; số match không tự động có nghĩa là lỗi vì protocol fallback và security-hiding 404 là các ngoại lệ hợp lệ.

## Chuẩn bắt buộc

### API và lỗi

- Endpoint công khai dùng DTO trong `His.Hope.Contracts` và `ApiProblemResponse`/ProblemDetails chuẩn.
- Error code là stable machine-readable value; không trả stack trace, SQL, token, password hoặc PII.
- Dùng `Guard`/exception mapping cho not-found, conflict, validation và authorization; giữ lại 404 ẩn danh có chủ đích trong Identity/SCIM.
- Route dùng contract constant khi route là giao thức liên service; route nội bộ của một service không đưa vào global constants chỉ để tránh một string đơn lẻ.

### Database và performance

- Query đọc có projection, `AsNoTracking` khi phù hợp, batch thay cho N+1 và query tag theo use case.
- Pagination đi qua `QueryRequest`/`PaginationDefaults`, giới hạn page size và whitelist sort/filter.
- Mọi I/O async nhận và truyền `CancellationToken`.
- Slow query được đo theo operation/use case và correlation, không chỉ một ngưỡng tổng quát.
- Chỉ cân nhắc compiled query/read replica sau khi có trace, p95/p99 và tải thực tế; Identity giữ read-after-write consistency.

### Bảo mật và giao tiếp service

- Secret chỉ từ Vault/environment secret store; không đưa secret vào constants, appsettings commit, log hoặc error response.
- URL service/database dùng `RuntimeConfigurationExtensions`; production chặn loopback.
- HTTP/gRPC dùng `IHttpClientFactory`/shared resilience; retry chỉ transient và operation idempotent.
- Mutation liên service cần authorization/audience, correlation, timeout, idempotency và outbox/inbox khi đi qua message broker.
- RabbitMQ consumer legacy phải đăng ký qua `AddHisHopeLegacyRabbitMqEventBus`; producer mới dùng pipeline Base Service.
- Health endpoint chỉ lộ trạng thái cần thiết; readiness không được dùng để bypass authorization của API nghiệp vụ.

### Binding provider bên ngoài

- Service dùng `AddHisHopeServicePlatform`, tự nhận shared binding cho email, SMS,
  Firebase OAuth/FCM và APNs; service không tự tạo `HttpClient` hoặc tự viết retry.
- Cấu hình provider nằm dưới `ExternalProviders:Email`, `ExternalProviders:Sms` và
  `ExternalProviders:Firebase`. API key và Firebase credentials production phải
  tham chiếu Vault qua `ApiKeySecretPath`/`CredentialsSecretPath`.
- Provider mặc định là `noop`; khi bật HTTP provider mà thiếu endpoint hoặc secret,
  request fail-closed. Không log body, token, API key hoặc credential JSON.
- Identity giữ tương thích với `PushProviders` cũ trong lúc chuyển dần sang
  `ExternalProviders`; email và Android push đã đi qua shared capability thật.

### Runtime module

- `ServicePluginRegistry` là control-plane registry hiện tại cho gateway/BFF: `Enabled` + endpoint runtime quyết định module có được publish hay không.
- Metadata plugin được normalize trước khi dùng; key lookup không phân biệt hoa thường và route/permission được trim/deduplicate.
- Tắt module fail-closed: route được disable, downstream cluster không được tạo, và không được tự động cấp quyền.
- Nếu cần module thật trong cùng một host, thêm composition seam chỉ khi có ít nhất hai module độc lập; không tạo plugin loader riêng cho từng service.

## Trạng thái áp dụng

- Đã chuẩn hóa configuration key dùng chung cho Redis, Data Protection, CORS, Unleash, environment và Plugins.
- Đã chuẩn hóa health route constants (`/health`, `/health/live`, `/health/ready`).
- Đã normalize metadata runtime module và bổ sung test registry.
- Các hardcode service-owned còn lại phải xử lý theo inventory của script, không bulk-replace mù vì có ngoại lệ security/protocol.

### Chuẩn hóa hardcoded string

Audit literal có thể chạy lại bằng:

```powershell
./scripts/audit-service-standardization.ps1 -MinimumOccurrences 3 -MinimumFiles 2
```

Script chỉ đưa ra ứng viên lặp ở nhiều file; đây là danh sách review, không phải
lệnh thay thế tự động. Các giá trị wire-level đã có seam chung tại
`His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants`, gồm claims, HTTP
headers, browser session cookie, media types và health routes. Các service dùng
chung seam này thay vì tự khai báo lại cùng một tên.

Quy tắc phân loại bắt buộc:

- Claim/header/cookie/media type/route ổn định giữa service: SharedKernel protocol.
- DTO, event name và message contract liên service: `His.Hope.Contracts`.
- Status, reason code, workflow state và mã lỗi riêng bounded context: Domain của service.
- Text UI, secret, URL môi trường, SQL schema, test fixture và token parser một lần: không đưa vào constants dùng chung.

Không coi các literal lặp trong migration SQL, generated code, dữ liệu demo hoặc
localization key là lỗi chuẩn hóa; chúng phải được review theo lifecycle/owner
riêng.

## Acceptance gates

1. Audit script không phát hiện secret thật hoặc endpoint loopback trong production configuration.
2. Shared configuration tests và service builds pass.
3. Endpoint tests xác nhận ProblemDetails/error code, authz, cancellation và pagination.
4. Integration/runtime tests chứng minh service communication timeout, correlation, retry policy, idempotency và module enable/disable.
5. `git diff --check`, security scan và full relevant test partition pass; gate không chạy được phải báo environment-blocked.
