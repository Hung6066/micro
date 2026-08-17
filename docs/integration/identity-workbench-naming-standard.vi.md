# Identity Workbench naming standard

## Phạm vi

Identity Workbench là control-plane duy nhất cho quản trị danh tính, ứng dụng,
ủy quyền, governance, session và phân tích quyền. Tên trong database, HTTP
contract, action và admin menu phải dùng cùng một từ vựng. Backend vẫn giữ
`/api/v1/admin/iam` làm base route để không phá các client hiện hữu.

## Từ vựng chuẩn

| Lớp | Quy ước | Ví dụ |
|---|---|---|
| SQL table | `snake_case`, tên miền IAM có tiền tố `iam_`, số nhiều | `iam_permission_sets`, `iam_workload_roles` |
| API resource | danh từ số nhiều, kebab-case | `/api/v1/admin/iam/permission-sets` |
| API action | động từ kebab-case, chỉ dùng khi không thể biểu diễn bằng HTTP verb | `activate`, `deactivate`, `publish`, `revoke`, `rotate-credential` |
| UI menu id | kebab-case, trùng resource key | `permission-sets`, `workload-roles`, `policy-simulator` |
| UI label | i18n key `admin.<resource>`; fallback tiếng Anh ổn định | `admin.permissionSets` |
| DTO/API method | tiền tố `Iam` + resource; không thêm alias kiểu `getThings` | `getIamPermissionSets`, `createIamPermissionSet` |

## Canonical resource map

`overview`, `scopes` (organization/tenant/account/environment), `services`,
`permission-sets`, `assignments`, `workload-roles`, `groups`, `boundaries`,
`resource-policies`, `api-audiences`, `trusted-issuers` và `analyzer`.

Application routes (`clients`, `users`, `roles`) vẫn tồn tại như compatibility
routes; UI mới phải gọi chúng qua các menu Identity Workbench tương ứng và
không tạo thêm route song song.

## Compatibility và migration

Không đổi tên vật lý các bảng đã có migration. `IdentityWorkbenchTableNames`
là catalog duy nhất cho mapping EF; migration đổi tên chỉ được tạo trong một
release riêng sau khi backup, dual-read và kiểm tra rollback. Route cũ được giữ
để tương thích, còn route/method mới phải lấy từ `IdentityWorkbench` (C#) hoặc
`identity-workbench.naming.ts` (Angular).

## Action và audit

CRUD dùng `POST/PUT/DELETE` với resource; lifecycle dùng `activate`,
`deactivate`, `publish`, `revoke`, `rotate-credential`; phân tích dùng
`simulate`, `compile`, `reconcile`, `export`. Mọi action phải được server-side
authorization và audit log; UI chỉ ẩn/hiện affordance, không phải enforcement.

## Kiểm tra bắt buộc

- Manifest 12 phần: `config/identity-workbench-12-parts.v1.json`.
- Validator tổng: `scripts/config/validate-identity-workbench-12-parts.ps1`.
- `scripts/config/validate-identity-workbench-naming.ps1`
- `dotnet build src/Shared/Contracts/His.Hope.Contracts/His.Hope.Contracts.csproj`
- build Angular `admin-app`
- targeted menu/API smoke; full E2E chỉ được gọi pass khi chạy tới completion.
