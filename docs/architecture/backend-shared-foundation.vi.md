# His.Hope — Backend Shared Foundation

## Kết luận

His.Hope đã có backend shared foundation, nhưng trước đây việc ghép module
chưa có một entrypoint thống nhất. Các service dùng đồng thời một số package
nhưng phải tự gọi `AddHisHopeServiceDefaults(...)` và
`AddHisHopeEnterpriseInfrastructure(...)`, dẫn đến khác biệt cấu hình và nguy
cơ đăng ký thiếu hoặc đăng ký trùng.

Entrypoint chuẩn cho HTTP service hiện là:

```csharp
builder.Services.AddHisHopeServicePlatform(
    builder.Configuration,
    "patient-service");
```

Implementation nằm trong
`src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/ServiceDefaultsExtensions.cs`.

## Phạm vi shared foundation

`AddHisHopeServicePlatform` đăng ký các capability dùng chung:

- ASP.NET Core defaults, ProblemDetails, correlation và validation errors.
- Request localization: `vi-VN`, `en-US`, UTC service timestamps và metadata
  regionalization.
- OpenTelemetry, structured logging hooks và Prometheus health endpoints.
- Resilience pipelines và graceful service defaults.
- Redis hybrid cache, cache warmup và distributed cache coordination.
- RabbitMQ/SQL messaging adapters và transactional outbox support.
- DPoP, security headers, rate limiting, brute-force protection và audit hooks.
- Authorization cache partitioning, lock manager và graceful degradation.

Các capability vẫn thuộc service-specific layer:

- DbContext, migration assembly, aggregate, repository và query index.
- JWT/OIDC client audience và policy permission của service.
- Endpoint, gRPC contract và integration event riêng của bounded context.
- Database health check có tên database cụ thể.

## Quy tắc sử dụng

1. HTTP service mới phải dùng `AddHisHopeServicePlatform`.
2. BFF hoặc host đặc biệt chỉ dùng `AddHisHopeServiceDefaults` khi có lý do
   rõ ràng và phải ghi trong ADR.
3. Không gọi lại `AddHisHopeEnterpriseInfrastructure` sau khi đã gọi platform
   entrypoint.
4. Không đặt connection string, Redis policy hoặc retry policy riêng trong
   endpoint code; dùng options của shared foundation.
5. Shared foundation không chứa domain entity, service-specific DbContext hoặc
   cross-service repository.

## Migration từ service cũ

Đã chuyển Identity, Patient, Appointment, Clinical, Lab, Billing, Pharmacy và
FHIR Gateway sang entrypoint thống nhất. Database registration và migration
runner vẫn được giữ ở service-specific layer để không làm mất ownership của
bounded context.

## Verification contract

Mỗi service phải đạt:

- build không lỗi;
- có `/health/live` và `/health/ready`;
- có correlation ID và ProblemDetails nhất quán;
- có OpenTelemetry service name riêng;
- có resilience và cancellation cho outbound call;
- có authorization fail-closed;
- có database migration được chạy bởi deploy job, không chạy cạnh tranh trong
  API replica;
- không truy cập database của bounded context khác.

Shared foundation là một module sâu: service chỉ biết một interface composition
nhỏ, còn registration, policy và adapter cross-cutting được giữ tập trung để
thay đổi một lần và kiểm thử một lần.
