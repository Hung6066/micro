# Authorization microservices và shared foundation: review và hướng mở rộng

Ngày review: 2026-07-30  
Phạm vi: IdentityService, shared authorization packages, các service nghiệp vụ, Angular shared foundation và các consumer hiện tại.

## Kết luận điều hành

Hệ thống đã vượt qua RBAC sơ khai: có OpenIddict/OIDC, JWT ký RSA và mã hóa JWE, permission registry, BFF session, CSRF, DPoP hooks, MFA/passkey, SCIM, audit và shared frontend package. Nhưng authorization chưa phải một contract thống nhất xuyên microservices.

Đánh giá: **nền tảng tốt cho production hardening, nhưng chưa nên mở rộng multi-facility/partner/agent trước khi đóng P0/P1**.

## Kiến trúc đang thực sự chạy

1. IdentityService lấy permission từ RolePermission trong DB khi phát token (OpenIddict handler, IdentityService).
2. Các service chính đăng ký AddHisHopeAuthorization() và bảo vệ REST bằng Permission:<code>; shared registry có fallback authenticated-user.
3. Có **hai** implementation authorization song song: `src/Shared/Authorization/His.Hope.Authorization` và `src/Shared/Infrastructure/.../Authorization`. Cả hai cùng định nghĩa policy/handler/fallback.
4. Permission handler ưu tiên claim `permissions`, rồi fallback role-to-permission nếu claim vắng.
5. Facility middleware đọc `facility_id`/`facility_ids`, nhưng `User.FacilityId` hiện `[NotMapped]` và còn TODO migration.
6. Frontend foundation có auth coordinator, permission service, bearer interceptor và public `src/index.ts`; permission snapshot chỉ dùng cho UX, backend mới enforce thật.

## Findings ưu tiên

### P0 — Bulk import chỉ yêu cầu authenticated, chưa yêu cầu permission

`/api/v1/admin` chỉ có `.RequireAuthorization()`, sau đó gọi `MapBulkImportEndpoints()`; bulk endpoints không thêm policy. Bằng chứng: `IdentityServiceEndpointExtensions.cs:634-643`, `BulkImportEndpoints.cs:11-17`.

Đề xuất: preview dùng `admin.users.read`, execute dùng `admin.users.write`. Thêm integration test: Provider/Nurse/BillingClerk = 403, Admin = 2xx.

### P0 — Facility policy chưa thành data isolation của domain service

Facility handler kiểm tra route/query/header, nhưng repository nghiệp vụ hiện chỉ filter trạng thái/ID; PatientRepository không có facility predicate (`PatientRepository.cs:18-61`). Không thấy FacilityId/TenantId/global query filter trong các domain repository chính.

Đề xuất: mỗi aggregate có FacilityId/TenantId và query scope bắt buộc, hoặc service gọi resource authorizer trước khi load. Không dùng `X-Facility-Id` như nguồn tin cậy độc lập.

### P1 — User facility chưa persist

`User.FacilityId` bị `[NotMapped]` (`User.cs:23-28`). OIDC handler chỉ copy `facility_id` từ claims user (`OpenIddictHandlers.cs:94-97`). Facility membership nên là table/aggregate chính thức với primary flag, active interval, source, version và audit; token claim chỉ là projection.

### P1 — Hai shared authorization modules gây drift

Hai package cùng có `AddHisHopeAuthorization`, policy registry và handler. Caller phải biết package nào là source of truth. Giữ một public package `His.Hope.Authorization`; legacy code chuyển thành adapter nội bộ có deadline. Registry, claim constants, policy names, decision reason và version phải có một contract test.

### P1 — Role fallback tạo hai nguồn sự thật

Identity phát permission từ DB, còn resource services có thể suy quyền từ role. Đây là compatibility hữu ích nhưng làm revoke khó và không có policy version. Production nên fail-closed khi thiếu permission snapshot; fallback chỉ bật bằng compatibility flag, telemetry và deadline. Thêm `authz_version`, `policy_version`, `jti`, `amr`, và `cnf` khi sender-constrained.

### P1 — Chưa có resource/row authorization

Permission route chỉ trả lời user được gọi loại hành động; chưa trả lời user được đọc patient/encounter/invoice cụ thể. Microsoft nêu rõ `[Authorize]` không đủ cho resource-based authorization vì resource thường được load sau attribute evaluation ([Microsoft resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0)).

Đề xuất: application seam `IResourceAuthorizer`/AccessScope với subject, action, resource type/id, facility, purpose, step-up, policy version và reason code. Repository nhận AccessScope để filter ngay tại DB.

### P1 — Frontend permission snapshot thiếu freshness/revocation contract

`HisHopePermissionService` chỉ giữ `Set<string>`, wildcard matching, không có version/TTL/source/loading state (`shared/frontend-foundation/src/auth/his-hope-permission.service.ts:10-31`). Guard cũng xác nhận backend mới là enforcement (`permission.guard.ts:20-21,64-65`).

Đề xuất public contract: `AuthorizationSnapshot { subjectId, tenantId, permissions, roles, version, issuedAt, expiresAt, source }`; state gồm unknown/loading/ready/stale/denied. Authz snapshot chỉ memory/session-bound, clear khi logout.

### P2 — Table views cần tách khỏi admin authorization surface

`admin.MapTableViewEndpoints()` kế thừa authenticated-only; view filter theo UserId là đúng ownership, nhưng resource name vẫn là client string (`TableViewEndpoints.cs:10-30`). Nếu là preference cá nhân, đưa về user scope; nếu chứa export/query intent, thêm permission và allow-list.

### P2 — Anonymous mobile telemetry cần abuse controls

Crash/RUM anonymous và log payload (`MobilePlatformEndpoints.cs:61-85`). Cần rate limit, size/field allow-list, redaction, abuse budget và cấm PHI trong telemetry.

## Target authorization model

Không nên nhảy ngay sang Zanzibar riêng. Lộ trình phù hợp là **RBAC + contextual ABAC + quan hệ resource**:

```text
Authentication
  -> subject, client, amr, token binding, session
Coarse authorization
  -> permission/action + service scope
Resource authorization
  -> facility/tenant, care relationship, purpose, state transition, step-up
Data enforcement
  -> query scope / row filter / aggregate policy
Audit and invalidation
  -> decision event, policy version, revocation evidence
```

IdentityService sở hữu subject, role, permission, facility membership, client/scope và policy publication. Service nghiệp vụ sở hữu domain rule và dữ liệu. Decision adapter chỉ nên thêm khi có local và remote adapter thật. Zanzibar là tham chiếu cho relationship/consistency ở quy mô lớn, không phải yêu cầu triển khai ngay ([Zanzibar paper](https://research.google/pubs/zanzibar-googles-consistent-global-authorization-system/)).

## Shared foundation cần nâng thành platform contract

- Auth package: AuthCoordinator, AuthorizationSnapshotStore có version/TTL, PermissionEvaluator chỉ cho UX, ApiSecurityContext, AccessDeniedState chuẩn hóa 401/403/step-up/stale/offline.
- UI package: capability metadata cho navigation/table/bulk action; field `requiredPermission`, `resourceType`, `selectionScope`, `reauthorizeBeforeCommit`.
- Backend package: một NuGet duy nhất cho permission constants, policy names, claim constants, decision DTO, middleware và test harness.
- Contract package: generate permission metadata/TypeScript capabilities từ một source; cấm raw shared imports; CI kiểm tra registry = DB seed = OpenAPI = frontend.
- Không share EF entities giữa service; share contract và adapter, để domain service enforce row/resource rules.

## Lộ trình

### Phase 0 — release blocker, 1 sprint

1. Chặn bulk import bằng `admin.users.write`.
2. Tắt role fallback production sau compatibility telemetry.
3. Hợp nhất hai authorization package.
4. Tự động inventory route metadata và fail khi mutation chỉ có authenticated-only ngoài allow-list.

### Phase 1 — data isolation, 2–3 sprints

1. Persist facility memberships và migrate claim generation.
2. Thêm AccessScope cho Patient, Clinical, Lab, Billing, Pharmacy.
3. Cross-facility read/update/export/bulk/gRPC negative tests.
4. Immutable authorization audit: subject, action, resource, facility, purpose, reason, policy version.

### Phase 2 — contract platform, 2 sprints

1. Publish versioned NuGet và `@his-hope/frontend-foundation`.
2. Generate capabilities từ một source contract.
3. Shared foundation thêm freshness, denied states, bulk reauthorization.
4. OpenAPI/Protobuf annotations thành artifact kiểm tra được.

### Phase 3 — contextual/ReBAC

Thêm care_team, assigned_provider, department, facility, delegated_actor và purpose_of_use khi domain cần. Remote decision service phải fail-closed, timeout bounded, cache theo policy version. Agent chỉ nhận delegated/attenuated capability, không nhận Admin tổng quát.

## Verification gates

- Route: mọi mutation có explicit permission hoặc allow-list anonymous/system.
- Negative matrix: unauthenticated 401; thiếu permission 403; sai facility 403; đúng facility 2xx.
- Data: facility/tenant scope được áp dụng trước ToList/First, không kiểm tra sau load.
- Token: issuer/audience/algorithm, expiry, refresh rotation/replay, revocation, amr, jti, DPoP/mTLS khi bật.
- Contract: permission registry = DB seed = OpenAPI = frontend capabilities.
- Audit: mọi PHI read/write và role/permission/facility mutation có evidence, redaction, correlation ID.
- Release: build/unit không đủ; cần DB/Redis, gRPC, BFF, browser và Docker/Testcontainers gates.

## Nguồn nghiên cứu

- [ASP.NET Core policy-based authorization](https://learn.microsoft.com/da-dk/ASPNET/Core/security/authorization/policies?view=aspnetcore-6.0)
- [ASP.NET Core resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0)
- [ASP.NET Core fallback policy](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0)
- [OAuth 2.0 Security Best Current Practice RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700)
- [Zanzibar: Google's Consistent, Global Authorization System](https://research.google/pubs/zanzibar-googles-consistent-global-authorization-system/)

## Trạng thái triển khai ngày 2026-07-30

Đã triển khai và kiểm chứng:

- Bulk import users/csv/file yêu cầu `admin.users.write`; preview yêu cầu `admin.users.read`.
- Identity Service persist `user_facilities`, có migration `AddUserFacilityMembership`, upsert membership khi bulk import, và phát hành `facility_id`/`facility_ids` từ membership active.
- Permission handler canonical và legacy đều fail-closed khi token không có permission claim; Identity endpoint mapping đã chuyển sang canonical handler namespace.
- Đăng ký permission `facility.cross`; Facility middleware dùng đúng mã permission; Facility handler không còn allow khi thiếu HTTP context hoặc thiếu target facility.
- Shared foundation permission snapshot có roles, facility IDs, version, issued/expiry metadata; snapshot hết hạn sẽ deny. Có 9 test riêng đạt.
- Có static contract gate tại `scripts/verify-authorization-contract.ps1`.
- Facility/resource scope đã được gắn vào aggregate roots và EF query boundary của Patient, Appointment, Clinical, Lab, Billing và Pharmacy; mutation được stamp và reject khi facility nằm ngoài scope.
- Patient read projection cũng đã mang `FacilityId`, có query filter và additive projection migration; integration event contract hỗ trợ truyền facility scope.
- Đã tạo additive migrations `20260730035207_AddFacilityScope` cho cả sáu service; Patient read projection có migration facility riêng; cache memory/distributed/hybrid được partition theo subject, token, security version và facility scope.
- Verification hiện tại: solution build 58 projects/0 errors; solution tests 699/699; authorization scope tests 3/3; cache partition tests 2/2; frontend foundation build đạt và Karma 43/43; Patient integration 5/5; Lab repository integration 5/5; Billing integration 5/5.

Các gate còn lại trước release:

- Full Playwright tree chưa pass: repo đang trộn nhiều runner/phiên bản, có test không resolve `@playwright/test`, lỗi `test.describe()` ngoài runner và một blog-index failure; đây là lỗi test-harness/repo baseline, chưa phải bằng chứng lỗi authorization.
- Lab endpoint/alert integration có 3 test pass nhưng realtime SignalR test còn treo trong TestServer teardown/startup; repository path 5/5 vẫn pass. Cần tách test host/timeout trước khi gọi full integration gate xanh.
- Docker/Testcontainers đã được xác nhận hoạt động; full service integration gate chưa xanh do các test trên, nên chưa thể tuyên bố release production-ready.
