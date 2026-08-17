# Đánh giá sẵn sàng enterprise production của Identity Service

Ngày đánh giá: 2026-08-14  
Phạm vi: `IdentityService` ASP.NET Core/OpenIddict, các contract triển khai Docker/VM/Kubernetes và các capability P0–P2 hiện có trong repository.  
Phương pháp: đối chiếu **mã nguồn/configuration hiện có** với chuẩn gốc IETF, OpenID Foundation, NIST và OWASP. Đây là đánh giá engineering readiness, **không phải** chứng nhận FAPI, ISO 27001, SOC 2, HIPAA hay NIST conformance; không có tenant/PKI/SIEM/DR production nào được suy diễn là đã chứng minh.

## Kết luận điều hành

**Chưa thể kết luận Identity Service “đạt chuẩn quốc tế để chạy enterprise production”.** Nền tảng đã vượt mức prototype và có nhiều control tốt: authorization-code + PKCE bắt buộc, refresh token reference/rotation, OIDC discovery/JWKS, revoke/introspect, DPoP cho mobile, MFA/passkey, mTLS/RADIUS/device posture theo pilot, SCIM M2M, Vault-oriented key/secret, fine-grained permission và audit/outbox.

Điểm sẵn sàng theo evidence repository là **64/100**. Điều đó phù hợp để vận hành **controlled production/pilot** sau khi hoàn tất các gate P0 bên dưới, nhưng chưa đủ để tự tuyên bố enterprise-grade hoặc FAPI-grade. Điểm không phải tỷ lệ an toàn; nó thể hiện mức bao phủ control và bằng chứng hiện có. Bất kỳ control nào chưa có test độc lập, rehearsal hoặc evidence runtime đều không được tính là production-proven.

| Quyết định | Trạng thái | Điều kiện |
|---|---|---|
| Internal controlled production (nhân viên, client đã đăng ký, không open-banking) | Có điều kiện | Hoàn thành toàn bộ P0, threat model, external pentest và HA/DR rehearsal trước go-live. |
| Enterprise production cho PHI/quyền admin đặc quyền | Chưa đạt | Cần assurance policy, audit/SIEM bất biến có bằng chứng, HA/DR, access review và vận hành 24x7. |
| FAPI 2.0 / regulated ecosystem conformance | Chưa đạt | Cần confidential-client profile, sender-constrained token end-to-end, client authentication bất đối xứng, conformance test plan; JAR/PAR/JARM là profile message-signing bổ sung khi use case yêu cầu. |

## Cập nhật implementation evidence sau đợt chuẩn hóa (15-08-2026)

Đợt hardening mới đã hoàn tất phần contract/authorization hygiene trong repository
và Docker runtime, nhưng **không làm thay đổi kết luận enterprise production** ở trên:

| Gate | Evidence hiện tại | Trạng thái |
|---|---|---|
| Shared authorization vocabulary | `AuthorizationConstants` và `AuthorizationPolicyNames.Permissions` trong SharedKernel; không còn literal `Permission:<code>` trong `src/Services`. | Pass |
| Drift prevention | `scripts/config/validate-shared-authorization-vocabulary.ps1`; 60 policy names đối chiếu catalog. | Pass |
| Endpoint enforcement | `scripts/validate-authorization-endpoint-coverage.ps1 -Strict`: 103 routes, 84 protected, 0 missing. | Pass |
| Admin identity UI contract | `validate-admin-identity-capabilities.ps1`; shared foundation, theme tokens, i18n và server-backed capability/owner data. | Pass |
| Role permission mutation UI | Role create/edit dialog binds server-backed owner and permission catalog, supports group/item add/remove and submits selected permission codes; save/error feedback now uses shared foundation i18n keys (`admin.roleSaved`, `admin.roleSaveFailed`, `admin.close`). | Pass (admin build + runtime smoke) |
| Service images/runtime | 8 affected images rebuild tuần tự; 8 containers healthy; image/container digests đã kiểm tra. | Pass |
| Internal API/UI smoke | Compose internal smoke ngày 15-08-2026: identity, gateway, frontend, dashboard, admin 200; unauthenticated APIs 401; dashboard BFF resources/metrics 401; network `docker_default` resolved. | Pass (Docker runtime) |
| Public UI/port forwarding smoke | Sau khi recreate `identityservice`, `frontend`, `dashboard-app` và `api-gateway` (không đổi port), `smoke-public-ui.ps1 -RequireAll` đạt gateway `5000`, identity `5001`, frontend `8081`, dashboard `8082`, admin `8083` đều HTTP 200. | Pass (Docker Desktop host forwarding, 15-08-2026) |
| Runtime startup logs | Kiểm tra log 10 phút sau recreate: các worker Identity khởi động; không có `error`, `exception`, `critical`, `unhealthy` ở gateway/identity/dashboard/admin/frontend. Cảnh báo Kestrel binding là cấu hình endpoint dự kiến. | Pass (runtime log review) |
| Runtime configuration hardening | Clinical app có `src/runtime-config.js`; cả ba Angular app chỉ mặc định `development` trên loopback, hostname khác mặc định `production` để không vô tình tắt HTTPS guard. Admin/dashboard images đã rebuild và restart. | Pass (repository/runtime) |
| Docker runtime adapter | `docker/config/compose.runtime.env.ps1` nay render đủ các khóa P0–P2 từ `config/environments/*.env.example`; render development tạm thời và chạy `validate-runtime-contract.ps1 -Runtime docker` thành công. | Pass (repository contract) |
| BFF SSO callback exchange | `HisHopeAuthCoordinator` không còn để marker redirect 120 giây chặn BFF cookie exchange sau khi Identity trả về `/auth/login`; regression test shared foundation pass, clinical unit suite `73 suites / 480 tests` pass. Dev-only reset flag `IDENTITY_BOOTSTRAP_ADMIN_RESET_PASSWORD` mặc định `false`, secret chỉ truyền qua process environment. | Pass (code/unit/build); browser runtime still separately gated |
| Clinical dashboard BFF routing | Bổ sung service `dashboard-bff` port `5600`, gateway destination `SERVICE_DASHBOARD_BFF_URL=http://dashboard-bff:5600`, dependency health gates và runtime contract host/port. Sửa aggregation handlers thành singleton để route map không resolve scoped service từ root provider; Docker health `/health` trả `200 Healthy`, container healthy sau recreate. | Pass (Docker internal); authenticated API/browser data contract vẫn cần suite riêng |
| Dashboard downstream runtime contract | Gateway route `/api/v1/dashboard/{**catch-all}` chuyển từ `clinical` sang `dashboard-bff`. BFF dùng canonical `REDIS_URL`, gRPC/API ports thực tế của các service; compose EventBus dùng hostname `rabbitmq` thay vì URI AMQP. Dashboard nginx nay route exact `/api/v1/admin/me/permissions` về API Gateway/Identity thay vì BFF, nên permission guard không còn rơi vào `access-denied`. | Pass (Docker internal; dashboard chromium 6/6; permission/control smoke 2/2) |
| Clinical UI domain contracts | Patient list row click đã nối vào shared `hh-data-table` (`rowClickable`/`rowClick`) để mở workspace; Docker-network Chromium hiện đạt Patient **16/16**, Appointment **8 passed / 2 skipped**, Clinical **5 passed / 3 skipped**, Pharmacy **10/10**, Lab **7 passed / 1 skipped**, Billing **6 passed / 1 skipped** (không có dữ liệu fixture). | Domain contract pass; data-dependent cases intentionally skipped |
| SCIM facility boundary | SCIM Users list/read/write/delete now resolves the caller facility scope from `facility_id`/`facility_ids` or explicit machine-client `scim_facility_id`/`scim_facility_ids`, filters reads, rejects out-of-scope resources and requires an explicit facility extension on creates when `Scim:RequireFacilityScope=true`. Production appsettings enables this fail-closed mode; development/testing remains opt-in for compatibility. A first-class tenant identifier is not yet persisted on the identity model, so this is not evidence of cross-tenant isolation. | Pass (facility code/build); tenant model, vendor and cross-facility runtime fixtures pending |
| Browser E2E qua host forwarding / Docker network | Đã chuẩn hóa `tests/e2e/specs/01-12` và `auth.setup.js` sang `tests/e2e/helpers/sso-login.js`, bổ sung runtime-config same-origin cho clinical app, dùng `waitUntil=commit`, retry navigation, BFF cookie exchange và logout button theo shared i18n. Credential submit, Identity “Continue to workspace” (button/link), localized login surface (VI/EN), per-app SSO handoff và dashboard permission proxy đã được làm idempotent. Docker-network Chromium: SSO smoke **4/4**, dashboard technical routes **1/1**, shared-foundation **14/14**, auth **5/5**, dashboard **6/6**, patient **16/16**, appointments **8/8 executable**, clinical **5/5 executable**, pharmacy **10/10**, lab **7/7 executable**, billing **6/7 executable**, admin **16/16 executable**, navigation/edge **13/14** trong run có 1 auth race; targeted repeats cho appointment/admin/navigation **9/9** pass. Host forwarding sau recreate các UI/gateway container và public smoke cũng pass đủ 5/5 ports. | Docker-network + public host domain pass; full E2E vẫn còn 2 retry-only flakes và 12 skips |
| Full E2E regression | Playwright inventory có 126 tests/15 specs. Full Docker-network Chromium `--workers=1` ngày 15-08-2026 ghi nhận **112 passed, 0 failed, 2 flaky, 12 skipped** trong 16,2 phút; hai flaky (`TC-PAT-03`, `TC-PHR-08`) đều pass khi retry và đã chạy repeat độc lập **10/10**. Shared-foundation visual date drift đã được giới hạn tolerance riêng cho clinical; visual/SSO targeted repeat **6/6** pass. | Domain pass; full suite không có hard failure; còn auth-race flake và data-dependent skips cần tiếp tục theo dõi |
| Independent protocol/DR/vendor evidence | Public-ingress conformance, DPoP resource validation, AAL/recovery sign-off, SIEM/WORM, HA/DR rehearsal, vendor tenant/PKI and pentest. | Chưa có |

Các gate “Pass” ở đây chỉ chứng minh repository và Docker internal runtime. Chúng
không được nâng thành FAPI, NIST AAL, SCIM vendor interoperability hoặc enterprise
production certification nếu chưa có evidence độc lập tương ứng.

Các cải tiến contract và build mới không tự động cộng điểm rubric 64/100, vì rubric
chỉ tăng khi có runtime rehearsal hoặc evidence độc lập ở các miền còn thiếu.

## Evidence repository: điểm mạnh đã xác nhận

| Miền | Bằng chứng trong checkout | Đối chiếu chuẩn | Đánh giá |
|---|---|---|---|
| OAuth/OIDC cơ bản | `IdentityServiceRegistrationExtensions.cs` chỉ cho authorization code, refresh và client credentials; bắt PKCE; có authorization/token/logout/revoke/introspect; production không cho HTTP và yêu cầu RSA key persistent. | RFC 9700 yêu cầu code flow/PKCE và chống downgrade; authorization response không qua HTTP. | Tốt, cần kiểm thử protocol độc lập. |
| Refresh & token theft | Reference refresh token, reuse leeway bằng 0 và Redis family-reuse detection. DPoP token binding/response handler có mặt, mobile được cấu hình required. | RFC 9700 yêu cầu public-client refresh token phải sender-constrained hoặc rotation. | Tốt một phần; DPoP chưa được chứng minh enforce ở mọi resource server. |
| Least privilege | Scope theo domain/FHIR và claims `permissions`; `PermissionHandler` là server-side enforcement, role governance/facility boundary có trong code. | RFC 9700 khuyến nghị token audience/resource/action restriction. | Khá, cần chuẩn hoá `aud`/resource indicators trên toàn bộ service. |
| Credentials | MFA/passkey, account lockout, password history bền vững, Vault transit cho MFA/key là các capability đã hiện diện. | NIST SP 800-63B đặt yêu cầu theo AAL, cryptographic authenticator và lifecycle/recovery. | Có building blocks, chưa có assurance policy/evidence AAL. |
| Provisioning | SCIM v2 endpoint với `scim.read`/`scim.write`, rate limit; outbound provisioning/SSF dùng outbox và mặc định fail-closed. | RFC 7644 định nghĩa SCIM protocol; OAuth BCP khuyến nghị M2M auth mạnh. | Có foundation; SCIM interoperability và tenant boundary cần certification-style testing. |
| Audit | Server-side audit, redaction, append-only model guard, structured event/outbox và export permission gate đã có. | OWASP yêu cầu log security event đủ ngữ cảnh, bảo vệ integrity/confidentiality/availability. | Khá, thiếu proof WORM/SIEM/legal hold/alert drill. |
| Secrets/key lifecycle | Production config fail-fast nếu thiếu signing/encryption key; Vault Kubernetes auth/transit được cấu hình; multiple encryption key path hỗ trợ rotation. | RFC 9700 khuyến nghị metadata và crypto agility; OWASP yêu cầu không log secret/token. | Thiết kế tốt, chưa có KMS/Vault outage & key rotation drill chứng minh. |

## Rubric có trọng số

Chấm theo 0–5: 0 không có; 1 ý tưởng/config; 2 code hoặc unit test; 3 integration/repository evidence; 4 runtime rehearsal có artifact; 5 independent conformance/operational evidence liên tục. Điểm = trọng số × mức/5. “Điểm hiện tại” không thay thế go/no-go gate.

| Hạng mục | Trọng số | Mức hiện tại | Điểm | Cơ sở và khoảng trống quyết định |
|---|---:|---:|---:|---|
| OAuth/OIDC protocol security | 20 | 4 | 16 | PKCE, code flow, rotation, OIDC endpoints và HTTPS production có trong code. Chưa chứng minh exact redirect URI/issuer/aud/nonce/mix-up/replay bằng external conformance suite; chưa có high-assurance message-signing profile. |
| Identity assurance & credential lifecycle | 15 | 3 | 9 | MFA/passkey/lockout/password history có. Chưa có AAL target theo journey, phishing-resistant requirement, reauthentication/max-age, recovery proofing và device-bound assurance. |
| Authorization & tenant/facility governance | 15 | 4 | 12 | Permission policy, facility scope, role governance/access review/break-glass có. Cần quyền delegation matrix, periodic recertification evidence và end-to-end `aud` enforcement ở service. |
| Audit, detection & compliance evidence | 10 | 3 | 6 | Audit/redaction/export/outbox có. Chưa có immutable external sink, retention/legal hold, SIEM correlation/alert runbook và tamper/restore drill. |
| Keys, secrets & supply chain | 10 | 3 | 6 | Persistent RSA, Vault path/transit và no static production Vault token configured. Chưa có HSM/KMS evidence, rotation/revocation drill, SBOM/signature/vulnerability SLA evidence. |
| Availability, data & disaster recovery | 10 | 2 | 4 | Health endpoints, Redis/data protection, Compose continuity artifacts tồn tại. Chưa có production multi-AZ quorum, tested RPO/RTO, backup restore proof, load/failover game day. |
| Operations & security assurance | 10 | 2 | 4 | Có validation scripts/targeted tests. Full integration suite từng bị giới hạn Docker/Testcontainers; chưa có independent pentest, SLO/error budget, on-call and incident exercises. |
| Enterprise interoperability | 10 | 3 | 7 | SAML/OIDC federation, SCIM M2M, provisioning/SSF/mTLS/RADIUS designs tồn tại. Vendor tenant, PKI, RADIUS, Chrome/Windows proof còn live-gated/pilot. |
| **Tổng** | **100** |  | **64** | Repository maturity khá; production proof chưa đủ. |

## Chuẩn gốc và áp dụng cụ thể

### OAuth 2.0/OIDC: baseline nên lấy RFC 9700, không tự gắn nhãn OAuth 2.1

[RFC 9700 – OAuth 2.0 Security BCP](https://www.rfc-editor.org/rfc/rfc9700) là Best Current Practice tháng 01-2025. Nó yêu cầu exact string matching redirect URI (trừ localhost native), public client dùng PKCE, chống PKCE downgrade, không dùng ROPC, refresh token public client phải rotation hoặc sender constraint, và resource server từ chối token sai audience. OAuth 2.1 vẫn được RFC này mô tả là đang phát triển, nên tiêu chuẩn release phải là **RFC 9700 compliance matrix**, không phải claim “OAuth 2.1 certified”.

Hiện trạng phù hợp: `RequireProofKeyForCodeExchange()`, reference refresh token và reuse leeway 0 là baseline mạnh. Cần đóng các gap sau:

1. Tạo automated interoperability suite cho discovery, JWKS rollover, exact redirect URI, PKCE `S256` only, state/nonce/issuer mix-up, `aud`, scope/resource, revoke/introspect và refresh-family replay. Test phải chạy cả qua ingress public, không chỉ in-process.
2. Công bố/kiểm tra metadata đầy đủ theo [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) và OIDC Discovery; pin issuer canonically, không dùng hostname nội bộ container.
3. DPoP hiện mới evidence ở token endpoint/mobile. Resource server phải validate proof (`htu`, `htm`, `iat`, `jti`, `ath`) và `cnf.jkt` cho mọi route yêu cầu DPoP; nếu chưa làm được, dùng short-lived audience-specific token hoặc mTLS giữa client/resource server cho workload high risk.
4. Client credentials phải ưu tiên [private_key_jwt (RFC 7523)](https://www.rfc-editor.org/rfc/rfc7523) hoặc [OAuth mTLS (RFC 8705)](https://www.rfc-editor.org/rfc/rfc8705), tránh client secret dài hạn trong CI/runtime.

### FAPI 2.0: mục tiêu profile riêng, không phải mặc định của OpenIddict

[OpenID FAPI 2.0 Security Profile Final](https://openid.net/specs/fapi-security-profile-2_0.html) là profile cho môi trường high-value/eHealth/eGovernment. Profile này yêu cầu confidential client, loại ROPC, sender-constrained access token (mTLS/DPoP), client authentication mTLS hoặc `private_key_jwt`, discovery và network protections. Code hiện chưa có test conformance FAPI; DPoP mới có evidence tại token endpoint/mobile, chưa chứng minh resource-server enforcement end-to-end.

Khuyến nghị: phân tách **FAPI profile** chỉ cho external regulated partners/admin high-risk. P0 là spike khả thi OpenIddict hoặc gateway extension; P1 implement `private_key_jwt`/mTLS/DPoP end-to-end. [FAPI Message Signing 2.0 Final](https://openid.net/specs/fapi-message-signing-2_0-final.html) mới là profile bổ sung JAR/PAR/JARM/JWT introspection khi non-repudiation/request-response protection cần thiết. P2 chạy official conformance/certification test trước khi công bố hỗ trợ. Không ép FAPI lên browser/internal SPA nếu không có use case vì rủi ro interoperability/operation cao.

### NIST digital identity: policy assurance phải đi trước tính năng

[NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b.html) đặt authentication theo Authentication Assurance Level (AAL), xác định yêu cầu authenticator, phishing resistance, reauthentication, session và recovery. AAL2 đòi hai factor distinct và có tùy chọn phishing-resistant; AAL3 đòi public-key cryptography phishing-resistant. [NIST SP 800-63A-4](https://pages.nist.gov/800-63-4/sp800-63a.html) tách identity proofing/enrollment (IAL). [NIST SP 800-63C-4](https://pages.nist.gov/800-63-4/sp800-63c.html) yêu cầu federation assertion validation gồm signature, audience và replay defense; `sub` federation phải namespaced theo issuer. Passkey/MFA trong code không tự động tạo ra AAL2/AAL3; đó là kết luận của policy + verifier + operational evidence.

Đề xuất lập `AssurancePolicy` versioned theo journey/resource:

| Journey | Mục tiêu ban đầu | Policy bắt buộc |
|---|---|---|
| Clinical read thường | AAL2-equivalent nội bộ | MFA hoặc passkey; session timeout; risk-based step-up. |
| Prescribe, export PHI, privileged admin | phishing-resistant AAL2/AAL3 theo risk assessment | WebAuthn/passkey hoặc PIV/mTLS; fresh authentication; no SMS-only fallback. |
| Break-glass | emergency, time-bound | dual approval nếu khả dụng, reason, strict TTL, post-event review và audit alert. |

P0 cần định nghĩa account recovery: identity proofing evidence, prohibited weak recovery factor, cooldown, revoke session/token/credential, notification và fraud review. Mỗi successful/failed enrollment, recovery, factor reset, admin grant/revoke phải sinh security event.

### SCIM và federation: hợp chuẩn là contract behavior, không chỉ endpoint

[RFC 7644](https://www.rfc-editor.org/rfc/rfc7644) yêu cầu SCIM protocol behavior; [RFC 7643](https://www.rfc-editor.org/rfc/rfc7643) định nghĩa core schema. Service hiện công bố `ServiceProviderConfig`/`ResourceTypes`, bảo vệ Users/Groups bằng M2M scope và rate limit: đây là hướng đúng. Tuy nhiên, code được kiểm tra cho thấy Groups map thẳng tới Role, list Users chưa thấy facility/tenant filter trong endpoint, và cần kiểm tra đầy đủ filter, schema discovery, PATCH semantics, ETag/`If-Match`, pagination/count bounds, per-client tenant isolation và audit cho toàn bộ mutation.

P0: dùng SCIM conformance fixtures của ít nhất hai client (ví dụ Entra và một HRIS), contract test negative (cross-facility, duplicate/external-id collision, replay/expired/wrong-aud token). Khi outbound provisioning, outbox/reconcile là đúng hướng; phải chọn một system of record cho từng attribute và cấm two-way write ngầm.

### Audit, logging và response: chứng cứ phải chống sửa đổi và dùng được khi sự cố

[NIST SP 800-53 Rev. 5, AU family](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) yêu cầu xác định event/audit content, timestamp, review/report và bảo vệ audit information; AU-9 đề cập cryptographic protection cho integrity. [OWASP Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html) bổ sung cách triển khai log đủ context và bảo vệ confidentiality/integrity/availability. [OWASP OAuth2 Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/OAuth2_Cheat_Sheet.html) bổ sung các control redirect/state/PKCE/token phù hợp vận hành OAuth.

P0 production gate:

- Event taxonomy bắt buộc: authentication, MFA/passkey, recovery, federation, token, client, role/permission, access review, break-glass, SCIM/provisioning, cert/device, export và policy decision; có actor, subject, tenant/facility, outcome, correlation/causation, policy version.
- Audit primary DB append-only chỉ là một lớp. Gửi signed/batched copy tới SIEM/WORM/object-lock độc lập; kiểm thử database admin không thể sửa/xóa event mà không phát hiện.
- Redact password, bearer/access/refresh token, MFA seed, private key, certificate private material, cookie và PHI không cần thiết; tiến hành retention/legal-hold/delete và query-access audit theo chính sách đã duyệt.
- Diễn tập ít nhất: suspicious role grant, refresh replay, break-glass, external IdP outage và audit sink outage. Sink outage không được làm mất event silently.

## Lộ trình cải thiện enterprise production

### P0 – release blockers (0–6 tuần)

1. **Protocol conformance gate.** RFC 9700 test matrix qua public ingress; exact redirect, PKCE S256/downgrade, nonce/state/issuer, token `aud` + `iss` + clock skew, refresh replay/revocation, DPoP resource validation.  
   *Exit:* CI pass và artifact evidence cho mỗi client type.
2. **Assurance & recovery policy.** ADR AAL/IAL-equivalent, factor enrollment/recovery/step-up/fresh-authentication rule và break-glass governance; remove weak fallback từ high-risk journey.  
   *Exit:* threat model signed-off bởi Security/Clinical Safety; tests chứng minh recovery không bypass MFA policy.
3. **Tenant-safe SCIM/RBAC.** Facility scope mọi read/write, dedicated client registrations/scopes, role governance/delegation ceiling, SCIM contract suite + audit mutation.  
   *Exit:* cross-tenant/cross-facility and over-delegation đều 403; two interoperating SCIM client tests pass.
4. **Audit/SIEM integrity.** External immutable audit delivery, alert rule, retention/legal hold, tamper and sink-outage test.  
   *Exit:* security incident drill truy xuất được end-to-end và audit delivery SLO đạt.
5. **Production resilience.** HA topology, key/Vault/Redis/DB dependency failure mode, backup restore and regional/cluster failover runbook.  
   *Exit:* observed RPO/RTO restore drill, not just manifest/config.

### P1 – enterprise scale (6–12 tuần)

1. Audience/resource indicator standardization ([RFC 8707](https://www.rfc-editor.org/rfc/rfc8707)), token exchange/service identity if needed, `private_key_jwt` registry and automated JWKS rotation.
2. Adaptive risk and continuous access policy: device posture remains observe/step-up until vendor/device lab proves coverage; add policy simulation and policy-change approval.
3. Admin control plane: role owner catalog, approval workflow for high-risk permission bundles, quarterly access certification, delegated admin boundary and CSV export DLP/TTL.
4. Security operations: SLO/error budget, dashboards, 24x7 alert ownership, vulnerability remediation SLA, SBOM/image signing and annual external penetration test.

### P2 – only when business/regulatory use case requires it

1. FAPI partner profile: confidential client, mTLS/private_key_jwt, DPoP/mTLS enforcement at every resource server and conformance certification; thêm PAR/JAR/JARM theo FAPI Message Signing khi use case yêu cầu.
2. Chrome/Windows/RADIUS/device compliance: private PKI, managed-device lab, revocation/CRL/OCSP, offline/break-glass, clinical-safety approval. Keep P2 at `observe` by default.
3. Multi-region active/passive (or active/active only after data consistency design), chaos/game-day program and independent assurance audit.

## Backlog triển khai có thể truy nguyên

| Work package | Điểm bắt đầu trong repository | Artifact bắt buộc để đóng gate |
|---|---|---|
| Protocol matrix + public ingress | Identity API registration, OpenIddict handlers, ingress smoke scripts | RFC 9700 test report qua URL public, gồm issuer/redirect/PKCE/nonce/state/aud/refresh replay |
| Resource-server sender constraint | Shared authentication/authorization middleware và từng service API | DPoP/mTLS negative tests chứng minh proof, `cnf` và audience được kiểm tra ở từng resource |
| Assurance/recovery | Identity MFA/passkey/recovery endpoints và policy store | ADR versioned AAL/IAL, threat model, clinical-safety approval, recovery/step-up test artifact |
| Tenant-safe provisioning | SCIM endpoints, directory provisioning outbox, facility scope | SCIM client fixtures, cross-tenant 403, PATCH/ETag/pagination/reconcile/rollback evidence |
| Immutable audit/SIEM | Audit service, structured fields, outbox and export path | Signed/WORM delivery, retention/legal hold, tamper and sink-outage drill with alert SLO |
| HA/DR | Compose/Kubernetes continuity manifests, Redis/PostgreSQL/Vault dependencies | Observed RPO/RTO, restore logs, failover game-day and owner-approved runbook |
| Admin control plane | Admin-app role/permission UI and shared foundation i18n/theme | Access certification report, delegation ceiling, high-risk approval and UI/API negative tests |

Mỗi work package phải cập nhật đồng thời code, test, runbook và evidence link; chỉ
đổi checkbox trong tài liệu không được xem là hoàn thành control.

## Release gate bắt buộc

Không đặt status “enterprise production ready” trước khi tất cả mục dưới đây có evidence truy xuất được:

1. RFC 9700 matrix + external security test/pentest không còn Critical/High không được chấp nhận chính thức.
2. Secrets không có trong source/image/log; Vault/KMS key rotation and revocation tested; production issuer TLS/headers/ingress trust boundary verified.
3. AAL/step-up/recovery/break-glass policy được Security, Compliance và Clinical Safety phê duyệt.
4. `aud`/issuer/permission/facility enforcement được test ở **mọi** resource service, không chỉ Identity UI/guard.
5. SCIM/federation vendor tenant test, negative test và reconciliation/rollback evidence.
6. Immutable audit/SIEM, alert on privileged changes, access review evidence và retention/legal hold test.
7. Load, failover, backup restore có observed RPO/RTO; runbook/on-call owner được diễn tập.

## Các nguồn chuẩn gốc

- IETF: [RFC 9700 – OAuth 2.0 Security BCP](https://www.rfc-editor.org/rfc/rfc9700), [RFC 7636 – PKCE](https://www.rfc-editor.org/rfc/rfc7636), [RFC 8414 – Authorization Server Metadata](https://www.rfc-editor.org/rfc/rfc8414), [RFC 8705 – OAuth mTLS](https://www.rfc-editor.org/rfc/rfc8705), [RFC 9449 – DPoP](https://www.rfc-editor.org/rfc/rfc9449), [RFC 8707 – Resource Indicators](https://www.rfc-editor.org/rfc/rfc8707), [RFC 7523 – JWT client authentication](https://www.rfc-editor.org/rfc/rfc7523).
- OpenID Foundation: [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html), [OpenID Connect Discovery](https://openid.net/specs/openid-connect-discovery-1_0.html), [FAPI 2.0 Security Profile Final](https://openid.net/specs/fapi-security-profile-2_0.html), [FAPI Message Signing 2.0 Final](https://openid.net/specs/fapi-message-signing-2_0-final.html).
- NIST: [SP 800-63B-4 – Authentication and Authenticator Management](https://pages.nist.gov/800-63-4/sp800-63b.html), [SP 800-63A-4 – Identity Proofing and Enrollment](https://pages.nist.gov/800-63-4/sp800-63a.html), [SP 800-63C-4 – Federation and Assertions](https://pages.nist.gov/800-63-4/sp800-63c.html), [SP 800-53 Rev. 5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final).
- SCIM: [RFC 7643 – Core Schema](https://www.rfc-editor.org/rfc/rfc7643), [RFC 7644 – Protocol](https://www.rfc-editor.org/rfc/rfc7644).
- OWASP: [OAuth2 Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/OAuth2_Cheat_Sheet.html), [Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html), [API Security Top 10](https://owasp.org/API-Security/editions/2023/en/0x11-t10/).

## Giới hạn bằng chứng

- Đánh giá chỉ xác nhận artifact repository và targeted verification đã ghi nhận; không xác nhận certificate, tenant vendor, CA/RADIUS, HSM, SIEM/WORM, Kubernetes HA, backup restore hay external pentest thực tế.
- P0–P2 implementation hiện có là accelerator hữu ích, nhưng integration/unit tests và Docker internal smoke không thay thế live external-provider, PKI, DR và browser/public-ingress evidence.
- Các điểm/gap cần được cập nhật sau mỗi release, incident, penetration test và game day; người sở hữu là Identity Platform, Security Operations, Compliance và các service owner.
