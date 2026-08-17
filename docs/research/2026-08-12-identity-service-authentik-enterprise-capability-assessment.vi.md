# Đánh giá năng lực enterprise Authentik cho His.Hope Identity Service

Ngày nghiên cứu: 2026-08-12  
Phạm vi: đối chiếu 13 tính năng Authentik được yêu cầu với Identity Service ASP.NET Core/OpenIddict hiện có. Nguồn tính năng là tài liệu chính thức Authentik tại thời điểm nghiên cứu; đây **không** phải khuyến nghị thay thế Identity Service bằng Authentik.

## Kết luận

Identity Service đã là OIDC issuer dựa trên OpenIddict, có SAML inbound, SCIM v2 inbound, passkey/MFA, mobile-device registry và audit HIPAA. Vì vậy, hướng nâng cấp đúng là bổ sung các **bounded capability** sau lớp Identity hiện hữu, không đưa ứng dụng/BFF sang phụ thuộc vào UI/flow của Authentik.

Ưu tiên theo giá trị và độ chín:

1. **P0:** audit bất biến có before/after, password-history, outbound federation OAuth/SAML có account-linking, và harden SCIM machine-to-machine OAuth.
2. **P1:** Entra/Google directory provisioning, CSV export có kiểm soát, SSF/CAEP-style security-event transmitter, client-certificate login và EAP-TLS khi có use case mạng thật.
3. **P2/preview:** device posture (kể cả Chrome Device Trust) và Windows local login. Không đặt chúng làm control bắt buộc cho clinical access trước khi pilot chứng minh coverage, availability và đường break-glass.

Các nhãn `Enterprise` và `Early Preview` bên dưới là trạng thái của Authentik, không phải đánh giá license của His.Hope.

## Baseline đã xác nhận trong mã nguồn

- `IdentityDbContext` dùng ASP.NET Identity + OpenIddict EF, có `AuditLogs`, `SecurityEvents`, `MobileDeviceRegistrations`, passkey và user/facility membership: `src/Services/IdentityService/IdentityService.Infrastructure/Persistence/IdentityDbContext.cs`.
- SAML federation inbound đã có controller; SCIM v2 hiện là **inbound server**, nhóm endpoint ghi cần `RequireRole:Admin` và rate-limit `scim`: `src/Services/IdentityService/IdentityService.Api/Controllers/SamlFederationController.cs`, `.../Endpoints/ScimEndpoints.cs`.
- Audit endpoint lưu dữ liệu do server lấy từ request cho actor/IP/UA, nhưng chi tiết payload bị truncate; đoạn model mapping hiện còn TODO cho `PreviousPasswordHashes`: `.../Endpoints/AuditLogEndpoints.cs`, `.../Persistence/IdentityDbContext.cs`.

Do đó các mục “impact” là gap/thiết kế cần làm, không suy diễn rằng một tính năng đã chạy production.

## Ma trận khả năng và tác động triển khai

| Khả năng Authentik | Cung cấp thực tế, điều kiện/giới hạn | Tác động đề xuất cho Identity Service |
|---|---|---|
| Google Workspace provider | **Enterprise** outbound directory provisioning: discovery ghép user theo email, group theo tên; direct sync khi thay đổi và full sync mỗi 4 giờ. Default group cần email domain; property mapping lỗi dừng sync. [Docs](https://docs.goauthentik.io/add-secure-apps/providers/gws/) | Tạo `DirectoryProvisioning` worker/outbox cho Google Admin SDK, immutable external-id mapping và reconciliation job. Chỉ một system-of-record; không xoá/disable theo event không-idempotent. Vault giữ service-account credential; log per object và dead-letter. |
| Microsoft Entra ID provider | **Enterprise** outbound sync user/group, discovery theo email/tên, direct + full sync mặc định mỗi 4 giờ; mapping group gồm mail/security flags; mapping lỗi dừng sync. [Docs](https://docs.goauthentik.io/add-secure-apps/providers/entra/) | Cùng abstraction với Google nhưng adapter Microsoft Graph, app-only least-privilege và tenant boundary. Không dùng Entra “sync” làm authentication federation; đó là một workstream khác. Giải quyết collision UPN/email và ownership trước enable write. |
| Embed external OAuth/SAML source | **Enterprise** Source stage nhúng browser OAuth/SAML vào flow (migration, IdP routing hoặc posture check); LDAP/non-browser không tương thích. Flow bị suspended có resume timeout; source flow không được tự User Login nếu không flow gốc không resume. [Docs](https://docs.goauthentik.io/add-secure-apps/flows-stages/stages/source/) | Chuẩn hóa `ExternalIdentity` (issuer, subject, tenant, userId, claims snapshot), state/nonce/PKCE correlation một lần dùng và return-URL allow-list. Kế thừa SAML hiện có, thêm OIDC federation handler; mapping/just-in-time provisioning phải fail closed trên issuer/audience/signature/clock mismatch. |
| Chrome Enterprise Device Trust | **Enterprise, 2026.5+**; Chrome/ChromeOS only vì Chrome Verified Access API. Cần Google Cloud project, API, service-account JSON và Chrome Admin connector; facts dùng trong Endpoint Stage/policy. [Docs](https://docs.goauthentik.io/endpoint-devices/device-compliance/connectors/google-chrome/) | Không đưa JSON key vào appsettings. Nếu pilot: verify attestation phía backend với Google API, lưu device evidence TTL-bound và phát `device_assurance`/risk decision server-side. Chỉ step-up hoặc deny privileged operations khi attestation stale/invalid; không tin browser-supplied headers. |
| Advanced device compliance | Device Compliance là **Early Preview**; cần Authentik Agent + browser extension. Tài liệu nói policy chuyên dụng còn “in development and inaccessible”; hiện dùng Endpoint Stage lấy facts vào flow/context. [Config](https://docs.goauthentik.io/endpoint-devices/device-compliance/configuration/), [policy](https://docs.goauthentik.io/endpoint-devices/device-compliance/device-compliance-policy/) | Xây portable `DevicePostureProvider` và policy engine trước, với signal provenance, observed-at/expiry, policy-version, exemption và audit. Pilot managed Windows trước; mobile attestation và Chrome là adapters. Thiết kế degraded mode/break-glass, không block emergency clinician chỉ vì agent outage. |
| SSF / Apple Business Manager | **Enterprise, 2025.2+**; Authentik là transmitter, OIDC app là receiver: webhook privacy-protected gửi SET khi MFA device đổi, logout/revoke session, credential đổi. Use case gồm Apple Business Manager/device management. [Docs](https://docs.goauthentik.io/add-secure-apps/providers/ssf/) | Tạo transactional `SecurityEventOutbox` và SSF transmitter (subscription lifecycle, signed SET, HTTPS receiver allow-list, retry/DLQ/idempotency). Emit only event tối thiểu; receiver phải verify issuer/aud/jti/time. Không coi webhook là authorization proof synchronous. |
| Password history | **Enterprise, 2025.4+**; lưu hash lịch sử và so với N mật khẩu gần nhất. Lịch sử chỉ bắt đầu sau khi policy được bật, không backfill. Thường gắn password-entry validation; kết hợp complexity/HIBP/expiry. [Docs](https://docs.goauthentik.io/customize/policies/types/password-uniqueness/) | P0: migration bảng `user_password_history` (user, password-hash, changed-at), transactionally append/prune sau khi Identity password change thành công. Dùng `IPasswordHasher.VerifyHashedPassword`, không mã hoá/re-hash plaintext; giữ config N và audit policy outcome. Backfill không thể làm từ hash hiện có. |
| Enhanced audit logging | **Enterprise** hiển thị event detail và object-diff old/new; OSS chỉ báo object/model thay đổi. Có map/chart event. [Docs](https://docs.goauthentik.io/sys-mgmt/events/logging-events/) | P0: append-only security/audit event schema với actor, subject, facility, correlation/causation, request source, outcome, policy-version và structured before/after đã redact. Write ở command boundary/server-side; hash-chain hoặc WORM/SIEM export, retention/legal hold, access-control và alert cho admin/key/role changes. Không ghi PHI/secret/token/cert raw vào diff. |
| Client certificate authentication | **Enterprise, 2025.6+**; browser/smartcard/PIV/hardware-token client cert, optional/required, trust CA và match subject/CN/email với username/email. Reverse proxy phải validate và forward cert; khuyến cáo private CA, không public CA. [Docs](https://docs.goauthentik.io/add-secure-apps/flows-stages/stages/mtls/) | Tách user mTLS ở edge khỏi Linkerd service mTLS. Ingress chỉ forward verified cert qua private trusted path; backend revalidates chain/EKU/SAN, revocation and exact mapping. Map certificate identity qua binding table (không email-only collision), step-up MFA và cert revoke/rotation audit. |
| SCIM OAuth authentication | **Enterprise** Authentik là **outbound** SCIM client: static token hoặc OAuth source tạo short-lived token; silent/interactive OAuth, có thể thêm `client_assertion`; sync event-driven + hourly full sync. [Docs](https://docs.goauthentik.io/add-secure-apps/providers/scim/) | Hiện His.Hope là SCIM server, nên giữ distinction. Khi provisioning **outbound**, dùng OAuth client-credentials/private_key_jwt + Vault key, token cache TTL, no static token; worker/outbox/reconciliation. Với SCIM **inbound** hiện tại, thay Admin-role-wide bằng client registration/scope `scim.*`, audience-bound token, tenant/facility constraints và audit mutation. |
| RADIUS EAP-TLS | RADIUS cần outpost và chỉ auth request; hỗ trợ EAP-TLS/PAP. EAP-TLS cần authentication flow mTLS first, RADIUS server cert và client cert từ CA; private PKI, không global CA. Password mode chỉ PAP vì không giữ reversible hashes. [Docs](https://docs.goauthentik.io/add-secure-apps/providers/radius/) | Chỉ làm nếu Wi-Fi/VPN/clinical device thực sự dùng RADIUS. Deploy HA RADIUS service/outpost segment riêng; RADIUS shared-secret rotation, private CA issuing/CRL/OCSP, cert-to-user/device mapping và network policy. Không mở RADIUS từ internet, không add reversible password store. |
| CSV export | **Enterprise** export CSV user/event, nội dung theo API object response; export lịch sử có query/user và cần view permission trên object + export permission. [Docs](https://docs.goauthentik.io/sys-mgmt/data-exports) | P1: async export job dùng snapshot/cursor, max row/time, authorization kép (`resource.read` + `export`), field-level redaction/facility scoping, encrypted short-lived download URL, AV scan nếu cần, retention/delete và audit create/download/delete. CSV injection neutralization bắt buộc. |
| Windows local login | **Enterprise, 2025.12+, Early Preview** WCP của Authentik Agent login local Windows qua browser/device token. Không RDP/offline; test Windows 11/Server 2022, AD chưa xác nhận, thay password local random và có thể ảnh hưởng encrypted dirs. Device-access group luôn required. [Docs](https://docs.goauthentik.io/endpoint-devices/authentik-agent/device-authentication/local-device-login/windows/) | Không xây vào Identity Service P0. Nếu cần, tách Windows Credential Provider/agent có signed auto-update, device enrollment/attestation, device access policy và recovery account. Pilot lab với EFS/BitLocker/AD/RDP/offline/incident scenarios; require offline emergency procedure trước rollout. |

## Rủi ro thiết kế xuyên suốt

- **Authoritative source:** Google/Entra outbound provisioning, external IdP inbound login, và SCIM có hướng dữ liệu ngược nhau. Cần record owner và conflict policy per attribute; không triển khai two-way sync ngầm.
- **Tenant/facility boundary:** External identity, directory object, device, certificate, export và SSF subscription đều phải có tenant/facility scope. Claim/group ngoài hệ thống chỉ là input mapping; `PermissionHandler` và query boundary vẫn là nơi enforce cuối cùng.
- **Keys/secrets:** service-account JSON, OAuth refresh token, SCIM credential, RADIUS shared secret và CA key chỉ ở Vault/PKI. Redact tuyệt đối trong audit/export/observability.
- **Availability:** federation, device posture và event delivery là external dependency. Login cần timeout/circuit-breaker; deny/step-up chỉ theo risk tier đã duyệt; emergency access phải time-bound, dual-control và audit.
- **License/maturity:** ít nhất GWS, Entra, SSF, password uniqueness, enhanced audit, mTLS, SCIM OAuth, CSV và Windows WCP được tài liệu gắn Enterprise; device compliance/WCP có preview/development caveat. Xác nhận entitlement, version và support SLA trước estimation.

## Lộ trình triển khai chi tiết

### P0 — identity-control foundation (3–4 sprint)

1. Viết ADR cho authoritative source, tenant/facility mapping, break-glass và event taxonomy; inventory các login/password/SCIM/audit mutation. **Gate:** threat model được Security/Compliance duyệt.
2. Implement password history + policy evaluation và tests: reuse N bị reject, hash không plaintext, concurrent reset không bypass, không false-claim backfill. **Gate:** migration/rollback thử trên clone DB.
3. Thay audit ghi rải rác bằng command-boundary security events và outbox; thêm structured redacted before/after cho user/role/client/credential/device. **Gate:** tamper/access-control/retention/SIEM-delivery test.
4. Refactor SCIM authentication thành dedicated machine client/scope/audience, rate limit per client và service-provider metadata truthful. **Gate:** no token có `scim.*` bị 403; cross-facility mutation denied; replay/expired/audience mismatch denied.
5. External OAuth/OIDC federation và hoàn tất hardening SAML hiện có: issuer metadata/JWKS pin/refresh, signed response/assertion validation, state/nonce/PKCE, account-link confirmation. **Gate:** valid login; wrong issuer/audience, replay, forged return URL và unlinked collision fail closed.

### P1 — enterprise interoperability (4–6 sprint)

1. Xây `IProvisioningTarget` + durable outbox/reconciliation; Google/Entra/SCIM outbound adapters lần lượt, bắt đầu read-only dry-run. **Gate:** idempotent create/update/disable, retry/DLQ, 4-hour/hourly reconciliation equivalent, no cross-tenant write.
2. Export service async + permissions/redaction/download TTL/CSV-injection tests. **Gate:** export cannot exceed caller facility/data permission; expired/revoked download fails.
3. SSF transmitter: OIDC receiver registration, subscription, signed SET, outbox retry và revocation/logout/MFA/password events. **Gate:** receiver verifies token; duplicate delivery idempotent; unavailable receiver không block logout.
4. User mTLS pilot for admin/high-risk role and RADIUS EAP-TLS only if network product owner confirms demand. **Gate:** private CA, revoked/unmapped cert rejection, ingress-header spoof test, device/network certificate rotation drill.

### P2 — endpoint assurance (pilot-first)

1. Define posture contract and policy facts; pilot managed Windows + authentik-agent-equivalent, then Chrome Verified Access. **Gate:** evidence freshness/provenance tests, exemption/break-glass audit, fail-safe outcome approved by clinical safety owner.
2. Windows local login laboratory pilot. **Gate:** Windows 11/Server 2022 login, recovery, agent upgrade, encrypted-directory, RDP/offline and AD compatibility results documented; do not promote if unsupported paths are business-critical.

## Đo lường và quyết định go/no-go

| Gate | Evidence tối thiểu |
|---|---|
| Federation | security test suite cho token/assertion validation, account-link collision, logout/revocation và provider outage |
| Provisioning | audit trace từ domain change → outbox → target object; reconciliation report, retry/DLQ và least-privilege API permission review |
| Compliance/audit | immutable/redacted event sample, access review, export retention/deletion, SIEM alert drill |
| Device/cert | attestation/certificate provenance, expiry/revocation, policy simulation, emergency access exercise |
| Operations | SLO/error budget cho IdP dependencies, key/cert rotation, restore drill, support/license verification |

## Trạng thái triển khai và bằng chứng xác minh (2026-08-12)

Các hạng mục P0/P1 đã được đưa vào working tree của Identity Service:

| Hạng mục | Artifact/code chính | Bằng chứng hiện có | Trạng thái |
|---|---|---|---|
| Password history | `UserPasswordHistory`, `IdentityService.ChangePasswordAsync/ResetPasswordAsync`, migration `20260812012100_AddUserPasswordHistory` | Docker-backed `PasswordHistoryTests` pass; reuse check dùng `IPasswordHasher` và transaction serializable | Đạt ở mức repository/integration |
| Immutable/redacted audit + CSV | `AuditLog` structured fields, EF append-only guard, `AuditLogEndpoints`, `AdminTableEndpoints` | Audit endpoint tests pass cho server actor, batch bound, immutability và redaction; CSV route permission-gated | Đạt ở mức repository/integration |
| External OAuth/SAML | dynamic OIDC source registry, Entra handler, SAML immutable-subject binding, account-link endpoints | API build pass; provider tenant callback/forged assertion tests chưa chạy vì thiếu IdP thật | Chưa có live-provider proof |
| SCIM M2M OAuth | `ScimAuthorization`, `ScimM2M`/read/write policies, `scim.read/scim.write`, registered `scim-provisioner` | SCIM authorization tests 4/4; read scope không được phép mutation | Đạt ở mức code/contract |
| Google/Entra/outbound SCIM provisioning | `IProvisioningTarget`, durable outbox, bindings, retry/reconcile, OAuth token cache, facility-scoped queue/reconcile, three adapters | Contract tests cover disabled fail-closed plus enabled Entra/Google/SCIM token caching and external-ID binding; API build pass; real tenant sync chưa chạy | Đạt ở mức contract, thiếu live target |
| SSF/CAEP transmitter | `SecuritySignalOutbox`, signed SET dispatcher, CAEP event envelope, `typ=secevent+jwt`, HTTPS subscription filter, retry | CAEP mapping/envelope contract tests 3/3; build và migration pass; receiver signature/delivery drill chưa chạy vì thiếu HTTPS receiver + Vault key | Chưa có live receiver proof |

### P2 pilot đã triển khai (observe-only)

| Thành phần | Artifact/code | Bằng chứng | Trạng thái |
|---|---|---|---|
| Normalized device evidence | `DevicePostureAssessment`, `DevicePostureEvidenceNormalizer` | Provider allow-list, SHA-256 evidence hash, TTL, replay và raw-secret rejection được kiểm thử | Đạt ở mức unit/contract |
| Policy evaluator | `DevicePosturePolicy`, `DevicePosturePolicyEvaluator` | 4/4 unit tests: observe mặc định, deny khi thiếu signal, stale evidence, secret rejection | Đạt ở mức unit |
| Pilot API/audit | `DevicePostureEndpoints` | Admin policy/assessment/preview và authenticated decision route; audit append-only/redacted; không nối clinical API | Đạt ở mức build; endpoint integration gate tiếp theo |
| Database artifact | migration `20260812050809_AddDevicePosturePilot`, `artifacts/identity-p2-pilot-migrations.sql` | Migration script có `device_posture_assessments` và `device_posture_policies` cùng index/foreign key | Đạt ở mức artifact |

P2 vẫn **chưa phải production enforcement**: Chrome Verified Access, Authentik Agent/Windows WCP, Google tenant và Windows 11/Server 2022 lab chưa được kết nối. Kill switch là đặt policy về `observe`; không có đường code nào tự deny clinical request trong pilot.
| mTLS/RADIUS EAP-TLS | certificate binding/revoke endpoints, CA/EKU validation, RADIUS assertion bridge | Build pass; private CA/revocation/network EAP drill chưa chạy | Chưa có PKI/network proof |

Các lệnh xác minh đã chạy:

```text
dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj --no-restore --nologo
  => 24 projects, 0 errors

dotnet test ...IdentityService.IntegrationTests.csproj
  --filter ProvisioningAdapterContractTests|ScimAuthorizationTests|PasswordHistoryTests|AuditLogEndpointTests
  => targeted P0/P1/Auth/Security suites pass: 32 integration tests, including Docker-backed PostgreSQL and host-Redis integration

dotnet test ...IdentityService.Application.Tests.csproj --filter ScimAuthorizationTests
  => 4 tests passed

git diff --check
  => no whitespace errors (chỉ còn cảnh báo line-ending của dirty worktree)
```

Migration script được xuất tại `artifacts/identity-p0-p1-migrations.sql` và đã kiểm tra có các bảng `user_password_history`, `security_signal_outbox`, `directory_provisioning_outbox`, `directory_provisioning_bindings` và `user_client_certificates`.

Contract tests bổ sung cho P1 cũng bao phủ CSV formula-injection neutralization và mTLS thumbprint/EKU rejection; chúng không thay thế bài kiểm tra với CA, ingress và network thật.

Docker Desktop port forwarding đã được khôi phục bằng restart; HTTP probe và test integration xác nhận loopback hoạt động. Fixture test dùng Redis host trên port 6380 vì Docker Desktop vẫn không forward ổn định các port Redis binary protocol, trong khi PostgreSQL tiếp tục chạy bằng Testcontainers. Production wiring không thay đổi. Live tenant Google/Entra/SCIM, Vault signing key, SSF receiver và private CA/RADIUS lab vẫn cần được chạy riêng trước go-live.

### Admin-app và foundation integration (cập nhật cuối phiên)

- `admin-app/src/app/features/identity-capabilities/identity-capabilities-page.component.ts` cung cấp workspace P0/P1/P2: settings/audit, dry-run provisioning, mTLS metadata, RADIUS/SSF health, CSV audit export và P2 observe/step-up/deny preview.
- `admin-app/src/app/core/services/identity-capabilities.service.ts` là typed facade; tải từng capability fail-soft, chuẩn hóa lỗi `{status, code, correlationId}` và không trả response body/secret cho UI.
- `admin-app/src/app/core/guards/capability-permission.guard.ts` yêu cầu `admin.settings.read`; server vẫn là authority. Route `/identity-capabilities` và alias `/security/identity` đều có forbidden state riêng.
- UI dùng shared foundation, theme tokens và dictionary EN/vi-VN; các thao tác đổi policy/thu hồi certificate yêu cầu xác nhận.
- Gate `scripts/config/validate-admin-identity-capabilities.ps1` kiểm tra tự động route/permission guard, foundation components, i18n dictionaries, theme tokens và secret boundary; gate đã pass cùng `validate-all-runtimes.ps1`.
- Live Docker smoke sau restart: Identity `:5001/Account/Login`, gateway `:5000/health`, frontend `:8081`, dashboard `:8082` và admin `:8083` đều trả HTTP 200; Docker containers tương ứng healthy. Host forwarding Windows đôi lúc cần retry ngắn, nhưng không tái hiện lỗi ứng dụng trong container.
- Admin capability page hiện dùng `HisHopePermissionService` để ẩn/khóa action theo `admin.users.write`, `admin.settings.write`, `admin.clients.write` và `admin.audit.read`; permission server vẫn được giữ làm enforcement authority.
- Bổ sung `capability-permission.guard.spec.ts`: kiểm chứng route được phép khi có `admin.settings.read` và trả forbidden state khi snapshot server thiếu quyền; Angular suite hiện **13/13 pass**.
- Bổ sung `DevicePostureEndpointTests`: policy/preview/decision đều được kiểm chứng fail-closed khi anonymous; targeted integration **3/3 pass** với PostgreSQL Testcontainer.
- Chuẩn hóa `admin-app/src/environments/environment.prod.ts` dùng `RuntimeConfigService`/`window.__HISHOPE_CONFIG__`; đã loại bỏ fallback `localhost` khỏi production bundle. Production Angular build và admin identity gate đều pass.
- Đã rebuild/recreate image `admin-app`; container healthy và `:8083/` trả 200. Sau restart gateway/Identity, Docker Desktop Windows host forwarding `:5000/:5001` lại dao động/timeout dù container state healthy; đánh dấu live host smoke là **environment-flaky**, không coi là pass ổn định.
- Thêm `scripts/config/smoke-public-ui.ps1` với retry/backoff và phân loại `SMOKE_ENVIRONMENT_FLAKY`; lần chạy hiện tại ghi nhận Identity/frontend/dashboard/admin pass sau retry, gateway health flaky do host forwarding, không biến thành false pass.
- Thêm `scripts/config/validate-identity-live-prerequisites.ps1`, được gọi bởi `validate-all-runtimes.ps1`; hiện báo rõ 7 live gates Google/Entra/SSF/mTLS/RADIUS/Chrome/Windows là `SKIPPED` vì môi trường chưa có tenant/credential/lab.
- Runbook vận hành bật/rollback theo từng gate đã được ghi tại `docs/runbooks/identity-live-gates.md`, giữ nguyên nguyên tắc không coi thiếu external evidence là pass.
- Readiness gate đã được harden: Google/Entra yêu cầu đủ toàn bộ biến bắt buộc; SSF kiểm tra URL tuyệt đối; mTLS kiểm tra CA file tồn tại trước khi có thể báo `READY`.
- P2 assessment table đã tích hợp end-to-end: `GET /api/v1/admin/device-posture/assessments` trả metadata, freshness, expiry, decision, policy/correlation và chỉ hash prefix; admin UI render qua typed facade, không trả raw evidence. Contract authorization test hiện **4/4 pass**.
- Đã rebuild/recreate `identityservice` và `admin-app` sau thay đổi assessment; cả hai container healthy, production Angular image build thành công (chỉ còn các warning bundle budget/CommonJS hiện hữu).
- Verification sau deploy image: admin identity gate và `validate-all-runtimes.ps1` pass; runtime contract Compose/VM/Kustomize pass, live prerequisites vẫn được phân loại `SKIPPED`.
- Assessment response privacy contract được tách thành mapper và test riêng: chỉ hash prefix 12 ký tự/metadata, không có `EvidenceHash` hoặc `SignalsJson`; test **1/1 pass**.
- Gate mới: `npm run build` pass; `npm test -- --watch=false --browsers=ChromeHeadless` pass **11/11**; targeted Identity integration pass **24/24**; `validate-all-runtimes.ps1` pass contract/Compose/Kustomize/VM rendering. Docker hiện có **33** service running/healthy.
- Host port smoke trên Windows/Docker Desktop vẫn không ổn định theo từng request (đặc biệt 8082/8083), trong khi container probes trả 200; vì vậy đây là `environment-flaky`, chưa được nâng thành live UI pass. Public Identity port vẫn giữ **5001**.
- Runtime hardening bổ sung: `DirectoryProvisioningDispatcher` không gọi target khi `PROVISIONING_MODE` là `disabled`, `dry-run` hoặc `observe`; `SecuritySignalDispatcher` không gửi SET nếu `SSF_ENABLED`/`SecuritySignals:Enabled` là false. Compose/VM/K8s đã có cùng nhóm biến adapter với default fail-closed.
- Sau rebuild Identity, một test password-history chạy riêng pass với `TESTCONTAINERS_RYUK_DISABLED=true`; lần full suite sau đó bị timeout Testcontainers/Ryuk, nên chưa dùng lần chạy này để tuyên bố full-suite pass. Build API và runtime validators vẫn pass; đây là gate hạ tầng test cần xử lý riêng.
- Đã chạy lại cùng 24 test theo hai nhóm tuần tự với `TESTCONTAINERS_RYUK_DISABLED=true`: nhóm adapter/SCIM/security/mTLS/export **13/13 pass**, nhóm password-history/audit/federation **11/11 pass**. Lần chạy toàn bộ một lệnh vẫn có thể timeout do Testcontainers trên Docker Desktop; bằng chứng phân nhóm hiện đủ 24/24.
- Bổ sung facility-scope nhất quán cho các đường đọc/ghi mới: posture assessments, mTLS bindings và provisioning jobs đều lọc theo `FacilityContext`/active `UserFacility` khi caller không có cross-facility quyền; retry/bind/revoke ngoài phạm vi trả `403`. API build pass và các contract/authorization tests chạy lại với `TESTCONTAINERS_RYUK_DISABLED=true` **12/12 pass**.
- Gate Angular targeted sau thay đổi: identity-capabilities service/permission guard **4/4 pass**; static Foundation/i18n/theme/secret-boundary gate **pass**. Đây là bằng chứng repository/UI contract, không thay thế browser smoke qua host forwarding khi Docker Desktop đang flaky.
- Tách least-privilege authorization theo route: provisioning/mTLS read dùng `admin.users.read`/`admin.clients.read`, mutation dùng quyền `write`; static admin gate kiểm tra cả hai nhánh. Sau thay đổi, API build **0 lỗi**, P2 authorization/privacy contract **5/5 pass**.
- Chuẩn hóa Docker Compose development fallback cho `ConnectionStrings__IdentityDb` và `ConnectionStrings__Redis`; khi không có host `.env`, Identity vẫn khởi động bằng service names nội bộ thay vì crash vì connection string rỗng. Recreate image xác nhận `his-hope-identity` **running/healthy**, host mapping vẫn `5001->5003`.
- Public smoke mới nhất: gateway và admin pass, Identity/frontend/dashboard bị timeout host-forwarding; script phân loại đúng `SMOKE_ENVIRONMENT_FLAKY` (container health vẫn pass). Không coi đây là UI regression hay live-provider pass.
- Verification loop mới nhất: Angular production build **pass** (cảnh báo bundle budget 42.98 kB và CommonJS `qrcode` hiện hữu), Angular unit suite **13/13 pass**, Identity API build **0 warning/0 error** trên incremental build, vendor-secret static scan **NO_VENDOR_SECRET_REFERENCES**. Container `his-hope-identity` vẫn `running/healthy` sau recreate.
- Thêm `scripts/config/smoke-compose-internal.ps1`: probe trực tiếp trong `docker_default` trả HTTP 200 cho Identity, gateway, frontend, dashboard và admin (`COMPOSE_INTERNAL_SMOKE_PASS`). Đây là bằng chứng runtime mạnh hơn host-forwarding smoke khi Windows Docker Desktop timeout; public port mapping vẫn không đổi.
- Sửa facility boundary cho trường hợp JWT chỉ có một `facility_id`: helper scope mới hợp nhất `FacilityId` với `AuthorizedFacilities`, loại trùng không phân biệt hoa thường và được áp dụng cho posture/mTLS/provisioning. Contract test mới **5/5 pass**; image Identity đã rebuild/recreate và internal smoke lại **5/5 HTTP 200**.
- Bổ sung dispatcher evidence: `DirectoryProvisioningDispatcherTests` xác nhận `PROVISIONING_MODE=dry-run` đánh dấu outbox `dry_run_no_external_call` mà không gọi target; `SecuritySignalContractTests` xác nhận SSF disabled return trước khi tạo scope/receiver. Hai gate lần lượt **1/1** và **4/4 pass**.
- Harden `PROVISIONING_MODE` thành allow-list fail-closed: chỉ `enabled`/`live` mới được gọi target; `disabled`/`off`, `dry-run`/`observe` và giá trị không nhận diện đều không tạo external call. `UnknownModeFailsClosedWithoutMutatingOutbox` và dry-run gate **2/2 pass** khi chạy với Docker/Testcontainers; image `his-hope-identity` healthy và compose internal smoke **5/5 HTTP 200**.
- Foundation boundary được chuẩn hóa thêm: các app import `RuntimeConfigService` qua public package `@his-hope/frontend-foundation`, command palette không còn hardcoded identity label, và mọi mutation provisioning trong workspace yêu cầu confirmation/i18n. Foundation build, i18n boundary, admin build và admin tests **13/13 pass**.
- External OAuth/OIDC source flow được nối thật ở frontend: nút Google/Microsoft gọi `GET /api/v1/auth/external-login/{provider}` của Identity Service (không lặp lại OIDC nội bộ), whitelist provider/encode `returnUrl`, không đưa secret vào browser. Login regression test **10/10 pass**; frontend production build pass. Full frontend Jest suite vẫn còn các test nền không liên quan (mock session-status/component legacy).
- Identity challenge/callback giữ `returnUrl` trong `AuthenticationProperties`, chỉ nhận local path (`/`, không nhận `//` hoặc absolute URL), rồi truyền vào completion sau callback. Federation integration suite **7/7 pass**; Identity API build pass; image rebuild/recreate và internal smoke **5/5 HTTP 200**.
- Login frontend nay lấy danh sách provider từ `GET /api/v1/auth/external-providers`, render các source OIDC/SAML đã cấu hình với fallback Google/Microsoft khi server chưa công bố provider; mọi click vẫn đi qua server challenge, không lưu secret. Frontend build pass và login regression **10/10 pass**.
- Facade external-provider chỉ phát ra `provider`, `displayName`, `icon`, loại bỏ field ngoài contract (kể cả `clientSecret`) trước khi UI nhận; HTTP contract test cho endpoint provider pass khi chạy riêng. Endpoint runtime `/api/v1/auth/external-providers` trả `200`; frontend image đã rebuild/recreate, container healthy và internal smoke **6/6 HTTP 200** (bổ sung external-provider probe).

## Nguồn chính thức

- [Google Workspace provider](https://docs.goauthentik.io/add-secure-apps/providers/gws/), [Microsoft Entra ID provider](https://docs.goauthentik.io/add-secure-apps/providers/entra/).
- [Source stage for OAuth/SAML](https://docs.goauthentik.io/add-secure-apps/flows-stages/stages/source/), [Mutual TLS stage](https://docs.goauthentik.io/add-secure-apps/flows-stages/stages/mtls/).
- [Google Chrome connector](https://docs.goauthentik.io/endpoint-devices/device-compliance/connectors/google-chrome/), [device compliance configuration](https://docs.goauthentik.io/endpoint-devices/device-compliance/configuration/), [device compliance policy status](https://docs.goauthentik.io/endpoint-devices/device-compliance/device-compliance-policy/).
- [SSF provider](https://docs.goauthentik.io/add-secure-apps/providers/ssf/), [password uniqueness](https://docs.goauthentik.io/customize/policies/types/password-uniqueness/), [event logging and audit export](https://docs.goauthentik.io/sys-mgmt/events/logging-events/).
- [SCIM provider and OAuth token modes](https://docs.goauthentik.io/add-secure-apps/providers/scim/), [RADIUS/EAP-TLS](https://docs.goauthentik.io/add-secure-apps/providers/radius/), [CSV data exports](https://docs.goauthentik.io/sys-mgmt/data-exports), [Windows local device login](https://docs.goauthentik.io/endpoint-devices/authentik-agent/device-authentication/local-device-login/windows/).
