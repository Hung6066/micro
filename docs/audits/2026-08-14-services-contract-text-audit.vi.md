# Audit type/text xuyên services

## Kết luận

Không nên gom mọi type và text vào một package duy nhất. Chỉ các vocabulary
xuyên boundary mới có một nguồn chuẩn; domain type nội bộ vẫn thuộc service sở hữu.

## Đã chuẩn hóa

- `AuthorizationConstants.Claims.PrincipalType`: `principal_type`.
- `AuthorizationConstants.PrincipalTypes`: `human`, `workload`.
- `AuthorizationConstants.Policies.HumanAdmin`: policy server-side; không phải principal type.
- IdentityService, DatabaseContinuityService và FhirGateway đã dùng constants cho principal type.
- Các endpoint IdentityService dùng policy constant thay cho literal `HumanAdmin`.
- Permission catalog tiếp tục là `HisHopePermissions`; route policy dùng các tên compile-time trong `AuthorizationPolicyNames.Permissions`.

## Quy tắc ownership

| Loại | Nguồn chuẩn | Service/client được làm gì |
|---|---|---|
| Claim/policy name | `SharedKernel.Authorization` | Đọc và kiểm tra; không tự định nghĩa lại |
| Permission code/descriptor | `HisHopePermissions` + seed DB | Service đăng ký code; admin chỉ gán code hợp lệ |
| API/event DTO | `Shared/Contracts` hoặc generated OpenAPI/gRPC | Versioning, không dùng entity nội bộ |
| Error/problem code | Shared contract | Service trả `code`, `args`, correlation id |
| UI label/help/error text | Shared Foundation i18n | Service không trả text hiển thị cố định |
| Domain enum/value object | Từng service | Không đưa vào shared package nếu không qua boundary |

## Kết quả migration policy

Đã loại bỏ literal dạng `Permission:<code>` khỏi `src/Services`. Minimal API,
gRPC attributes và các authorization checks động đều dùng
`AuthorizationPolicyNames.Permissions.*`, nên typo policy bị phát hiện ở compile time.

Các chuỗi tiếng Việt trong `LocalizationSeedData`/migration là dữ liệu bản địa hóa
có chủ đích, không phải business logic; không di chuyển sang service khác.

## Gate bắt buộc khi tiếp tục migration

1. Không còn literal `principal_type`, `human`, `workload`, `HumanAdmin` ngoài shared constants.
2. Không còn literal `Permission:<code>` trong services; policy names nằm ở shared constants.
3. API trả error code ổn định; frontend dịch qua shared foundation/i18n.
4. Chạy build IdentityService, authorization endpoint coverage và compose internal smoke.

Gate tự động tương ứng:
`scripts/config/validate-shared-authorization-vocabulary.ps1`.
