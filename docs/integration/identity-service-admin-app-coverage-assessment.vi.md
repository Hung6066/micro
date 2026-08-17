# Đánh giá tích hợp Identity Service với Admin-app

Runbook chuẩn hóa test và quality gates: `docs/integration/identity-test-standardization.vi.md`.

Ngày đánh giá: 2026-08-15  
Phạm vi: `IdentityService.Api`, domain/persistence, background workers và `admin-app`.

## Kết luận điều hành

Identity Service đã có một bề mặt quản trị Admin-app đủ dùng cho vận hành thường ngày và các capability P0–P2 đã triển khai. Không nên đưa secret, private key, raw SAML assertion, raw device attestation hoặc vendor credential vào trình duyệt. Admin-app chỉ nên gọi các endpoint quản trị đã được `HumanAdmin` + permission bảo vệ và hiển thị trạng thái đã chuẩn hóa.

Tuy nhiên, vẫn còn một số khoảng trống có giá trị vận hành cao. Ưu tiên tiếp theo là bổ sung **incident controls** (thu hồi session/token, reset MFA/passkey có audit), **bulk user lifecycle**, và **outbox operations** (SSF retry, provisioning reconcile). Các chức năng này chưa nên giải quyết bằng cách gọi trực tiếp database hoặc endpoint self-service của người dùng.

## Ma trận coverage hiện tại

| Capability Identity Service | API/boundary hiện có | Admin-app hiện tại | Đánh giá |
|---|---|---|---|
| Users, activate/deactivate, role assignment | `/api/v1/admin/users`, `/roles`, `/users/{id}/roles` | Users, Roles, access-management | Đã tích hợp |
| OIDC clients và secret rotation | `/api/v1/admin/clients` | Clients + onboarding/rotation | Đã tích hợp |
| Permission catalog/effective access | `/permissions`, `/users/{id}/effective-access` | Access Management | Đã tích hợp |
| Break-glass | request/approve/revoke + TTL/MFA/token revoke | Access Management | Đã tích hợp |
| Policy simulation | `/policy/simulate` | Access Management | Đã tích hợp |
| Audit log và CSV export | `/audit-logs`, table export | Identity capabilities + export | Đã tích hợp |
| Runtime settings/federation state | `/settings`, federation settings | Security providers + Identity capabilities | Đã tích hợp, chỉ hiển thị setting an toàn |
| MFA/passkey của admin hiện tại | `/auth/mfa/*`, `/auth/passkeys/*` | Security providers | Đã tích hợp cho self-service |
| Device posture P2 | policy, preview, assessments | Identity capabilities | Đã tích hợp ở observe/preview; không phải connector provisioning |
| mTLS bindings | `/admin/mtls/bindings` | Identity capabilities | Đã tích hợp read/revoke |
| RADIUS EAP-TLS | `/admin/radius/eap-tls/status` | Identity capabilities | Đã tích hợp status-only |
| SCIM/SSF/provisioning health | jobs/status/outbox endpoints | Identity capabilities | Có status/queue/retry provisioning; SSF retry còn thiếu |
| Mobile device operations | `/admin/mobile/devices`, delivery summary | Mobile operations | Đã tích hợp list/revoke/summary |
| Consents | admin GET `/admin/consents` | Consents page | Đã tích hợp read-only; revoke admin chưa có chủ ý |
| Bulk user import | `/admin/users/bulk*` | Chỉ có preview API, chưa có workflow UI đầy đủ | Khoảng trống P1 |
| User session/token incident response | self-service `/auth/account/sessions` | Chưa có admin endpoint | Khoảng trống P0 |
| Admin reset MFA/passkey | Chỉ có self-service endpoint | Chưa có admin endpoint | Khoảng trống P1 |
| SSF failed outbox replay | `/admin/security-signals/outbox/{id}/retry` | Chưa có AdminApi/UI action | Khoảng trống P1 |
| Provisioning full reconcile | `/admin/provisioning/reconcile/{target}` | Chưa có AdminApi/UI action | Khoảng trống P1 |

## Khoảng trống và đề xuất nâng cấp

### P0 — Incident access controls

1. Thêm endpoint quản trị thu hồi toàn bộ session của một user và revoke token blacklist. Endpoint phải yêu cầu `HumanAdmin`, permission riêng (`admin.sessions.revoke`), reason bắt buộc, facility boundary và audit before/after.
2. Thêm endpoint admin reset/revoke MFA và passkey theo user. Không trả secret hoặc credential material; thao tác phải tạo security event, tăng `securityVersion` và buộc re-authentication.
3. Thêm UI trong user detail/access-management: sessions, last authentication, active MFA/passkey count, nút revoke/reset có confirmation và reason.

### P1 — User lifecycle và vận hành outbox

1. Hoàn thiện Bulk Import workspace: upload CSV/XLSX, preview validation, chọn `skipExisting`/welcome policy, execute, progress/result, failed-row download. Giữ giới hạn 10 MB/10.000 users và audit correlation.
2. Thêm SSF outbox dashboard: pending/failed, last delivery, retry có reason và idempotency. Payload/signing key chỉ ở server.
3. Thêm provisioning reconcile action theo target (`scim`, `entra`, `google-workspace`) với dry-run mặc định, explicit confirmation khi chuyển live và job polling.
4. Thêm provider health/test connection cho LDAP/SAML/Google/Entra nhưng chỉ trả normalized status/error code; credential được nhập qua Vault/secret reference, không lưu trong browser state.

### P2 — Device trust và compliance pilot

Giữ thiết kế hiện tại: Admin-app quản lý policy, evidence TTL, preview, kill-switch và assessment history. Không thêm form nhập Google service-account key, Chrome attestation raw data hoặc Windows agent secret. Connector onboarding nên do platform/secret-management workflow đảm nhiệm; Admin-app chỉ hiển thị readiness và fingerprint/health đã chuẩn hóa.

## Quy tắc bảo mật bắt buộc cho các integration mới

- Mỗi mutation có permission riêng, `HumanAdmin`, facility check và structured audit event.
- Deny-by-default; không dùng endpoint self-service để thay thế admin endpoint.
- Approve/revoke session, MFA, break-glass phải invalidate token/session hiện có.
- Browser không nhận secret, private key, raw assertion, raw attestation hoặc SCIM/SSF credential.
- Mọi operation dài chạy qua outbox/job có idempotency, retry bounded và correlation ID.
- UI dùng shared foundation, i18n, theme token và permission service; 403 phải chuyển sang trạng thái forbidden rõ ràng.

## Kế hoạch tích hợp đề xuất

| Phase | Deliverable | Exit criteria |
|---|---|---|
| P0 | Admin session revoke + MFA/passkey reset | API authorization tests, audit tests, token invalidation test, user-detail UI |
| P1-A | Bulk import workspace | preview/execute/failed rows, 10 MB/10k limits, CSV/XLSX contract tests |
| P1-B | SSF + provisioning operations | retry/reconcile jobs, idempotency, failure/rollback tests |
| P1-C | Provider readiness | normalized health, Vault reference, no-secret browser test |
| P2 | Device trust pilot console | observe-first, kill-switch, external tenant/device lab gates |

## Bằng chứng hiện tại

- Admin capability validator: `ADMIN_IDENTITY_CAPABILITIES_VALIDATED`.
- Compose internal smoke: identity, frontend, dashboard, admin và authorization boundary pass.
- Identity unit suite: Domain 87/87, Application 162/162, Infrastructure 205/205 — tổng **454/454 pass**.
- Full Identity integration baseline: **285/285 pass** trong Docker; security/API additions validated **57/57 + 14/14**, test containers được cleanup sau run.
- API security contract và authorization contract: pass; 10 service API projects và 109 endpoint inventory được kiểm tra (102 protected, 4 anonymous, 0 missing).
- Test data dùng chung: `tests/IdentityService/IdentityService.Testing/IdentityTestData.cs`.
- Route contract dùng chung API/tests: `src/Shared/Contracts/His.Hope.Contracts/Identity/IdentityApiRoutes.cs`.
- Docker integration runner tái sử dụng: `scripts/run-identity-tests-docker.ps1`; latest isolated baseline **285/285**, chỉ xóa container test của chính invocation.
- Docker E2E runner: adaptive MFA source/browser contract **7/7 pass**; authenticated SSO smoke yêu cầu `E2E_PASSWORD` qua preflight và hiện `environment-blocked` vì identity container đang chạy không có bootstrap password khớp credential mặc định, không tự ý reset tenant/application containers.
- Coverage report merged current (fresh Domain/Application/Infrastructure unit + Docker integration reports, bổ sung BulkImport/Directory endpoint 15/15 và IAM control-plane 12/12): **88.30% line / 72.64% branch** sau khi loại compiler-generated classes và composition wiring khỏi mẫu đo; vẫn chưa đạt mục tiêu 90%/80%; không được coi là quality gate pass.
- Admin-app build/tests/lint: build pass, current IAM/admin validation pass; responsive authenticated menu E2E **3/3 viewport**.
- Live Google Workspace/Entra/SSF/mTLS/RADIUS/Chrome/Windows gates vẫn cần tenant/PKI/device-lab bên ngoài và phải tiếp tục ghi nhận là `skipped` khi thiếu prerequisite.

## Correction — current worktree verification 2026-08-16

Các gap P0/P1 trong ma trận lịch sử phía trên đã được triển khai sau đợt audit:

| Hạng mục trước đây ghi thiếu | Bằng chứng hiện tại | Trạng thái |
|---|---|---|
| Admin revoke toàn bộ session/token | `AdminIncidentEndpoints` + `AdminApiService.revokeAllAdminSessions` + Identity Operations UI | PASS local contract/runtime |
| Admin reset MFA/passkey | `AdminIncidentEndpoints` + `resetAdminCredentials` + reason/permission/audit | PASS local contract |
| Bulk import workspace | CSV/XLSX preview/execute, giới hạn 10 MB/10.000 users | PASS local contract/UI |
| SSF outbox replay | `SecuritySignalAdminEndpoints` retry + `retrySecuritySignal` + UI | PASS local contract/UI |
| Provisioning reconcile | SCIM/Entra/Google target + queue action + UI | PASS local contract/UI |

`validate-admin-identity-capabilities.ps1` hiện kiểm tra cả server endpoint, AdminApi facade và Identity Operations UI; kết quả gần nhất là `ADMIN_IDENTITY_CAPABILITIES_VALIDATED`. Các đánh giá vendor/PKI/device-lab ở trên vẫn là external live gates và không được suy diễn thành pass từ local contract.

Targeted Docker contract run sau correction: **18/18 pass** (incident session/credential controls, bulk import, provisioning adapters/endpoints và SSF contracts) trên network cô lập; lần chạy trước trên Docker Desktop host network bị runner lifecycle race và không được tính là pass.
