# Identity Service — Test Standardization & Validation Runbook

Ngày cập nhật: 2026-08-24

Snapshot xác nhận mới nhất: Identity unit projects trực tiếp **601/601 PASS** (Domain 87, Application 265, Infrastructure 249); full Docker integration **430/430 PASS**; focused IAM/provisioning **23/23 PASS**.

Bổ sung sau snapshot: test nhánh device posture, role concurrency, admin incident/audit, directory provisioning, federation, mobile platform, gRPC/mTLS, IAM external identity catalog, HQ/customer tenant visibility, OpenIddict authorization/token-claims validation, Permission Catalog, SSF event aliases, CSV/XLSX admin export và IAM parent-kind hierarchy. `ScimGroupResponse.meta.resourceType` đã sửa về `Group` theo RFC 7643. Full Docker run dùng PostgreSQL/Redis disposable đạt **430/430 PASS** và cleanup exact. Coverage merge sạch mới nhất **90.01% line / 80.03% branch**, đạt gate 90/80.

## Nguyên tắc dùng chung

- Test data dùng chung tại `tests/IdentityService/IdentityService.Testing/IdentityTestData.cs`; integration login dùng `IdentityTestCredentials` thay vì lặp credential literal.
- API route contract dùng chung tại `src/Shared/Contracts/His.Hope.Contracts/Identity/IdentityApiRoutes.cs`; Device Posture, federation, account-linking, provisioning, ReBAC và admin security-signal routes dùng canonical constants ở API/tests.
- Các project `IdentityService.Testing`, Domain.Tests, Application.Tests, Infrastructure.Tests và IntegrationTests đều đã được đăng ký trong `His.Hope.sln`; chạy solution không còn bỏ sót hai lớp unit Domain/Application.
- Test không tự tạo password/route literal nếu đã có factory hoặc contract constant.
- Integration dùng PostgreSQL/Redis disposable; không dùng container ứng dụng đang chạy.
- Mọi runner phải cleanup exact container trong `finally` và không sweep Docker workload khác; Docker runner tự tạo/xóa user-defined network khi caller dùng network dựng sẵn `bridge` hoặc network chưa tồn tại, chạy test container detached và poll exit code để tránh Docker Desktop stream EOF. Runner tự ưu tiên `.docker-test-config` của repository nếu có, dùng workspace lock độc quyền (`artifacts/.identity-test-run.lock`) để không chạy song song và khóa DLL/coverage collector. Trước restore, runner cố gắng xóa `obj` build artifacts trong workspace mount (best-effort trên Docker Desktop ACL) để không đưa Windows-only NuGet fallback folder (ví dụ DevExpress offline path) vào Linux assets.
- Test infrastructure dependency scan hiện pass cho cả bốn Identity test projects; Testcontainers được nâng lên 4.13.0, SSH.NET được pin ở 2026.0.0, và SQLite native test dependency đã được loại khỏi các test dùng InMemory provider.

## Các runner chuẩn

| Phạm vi | Runner | Lệnh mẫu | Cleanup |
|---|---|---|---|
| Unit | `scripts/run-identity-tests.ps1` | `-ResultsDirectory <dir>` | Không tạo container |
| Integration/Docker | `scripts/run-identity-tests-docker.ps1` | `-Network bridge -Filter 'FullyQualifiedName~...'` | PostgreSQL/Redis + disposable network exact names |
| Cleanup after interrupted Docker run | `scripts/cleanup-identity-test-docker.ps1` | `-RunId <10-hex-id>` hoặc `-IncludeRunning` khi đã xác nhận run bị bỏ rơi | Chỉ resource `identity-docker-*`, không quét container ứng dụng |
| E2E/browser | `scripts/run-e2e-docker.ps1` | `-Config 'playwright.config.js' -Spec 'specs/adaptive-mfa.spec.js'` | Browser `--rm` |
| Coverage | `scripts/validate-identity-coverage.ps1` | `-CoverageRoot <dir>` | Không tạo container |

## Quality-gate matrix

| Gate | Evidence hiện tại | Trạng thái |
|---|---:|---|
| Domain unit | 87/87 | PASS |
| Application unit | 265/265 | PASS |
| Infrastructure unit | 249/249 | PASS |
| Identity integration | 430/430 full run; 23/23 IAM/provisioning and 7/7 IAM tenant edge focused | PASS; exact test containers removed |
| Support Elevation endpoint branch regression | 4/4 focused | PASS; InMemory model test, Docker full rerun still pending |
| OIDC route smoke after API route-constant standardization | 5/5 in Docker (`OidcFlowTests`) | PASS; exact test containers removed |
| Access Governance security regression (validation/REBAC/policy simulation cases) | 5/5 targeted | PASS |
| IAM control-plane authorization regression (new anonymous-route cases) | 6/6 targeted | PASS |
| Access Governance lifecycle/deny regression | 7/7 targeted | PASS |
| Access Governance deep lifecycle/contract regression | 10/10 targeted; bundle/lint/compile, access-review gates, unknown roles, break-glass validation and revoke lifecycle | PASS |
| Access Governance targeted coverage | `AccessGovernanceEndpoints` 72.4% line / 50% branch in fresh 10-test run | MEASURED — remaining publish/approval branches require MFA-capable fixture |
| Table analysis endpoint regression (catalog/auth/validation/aggregate/formula) | 7/7 targeted | PASS |
| Table analysis targeted coverage | 100% line / 100% branch for `TableAnalysisEndpoints` mapping and exercised handlers | PASS |
| HR webhook + mTLS security contract regression | 20/20 targeted | PASS; downstream signed-endpoint branches remain fixture-blocked because `HrWebhook:Secret` is not injected |
| Radius EAP-TLS endpoint contract | 3/3 targeted | PASS; exact test containers removed |
| SCIM DTO RFC contract | 3/3 targeted | PASS; Group metadata emits `resourceType=Group` |
| API low-coverage helper/health/worker/config contract | 11 facts in full 285 run; endpoint contracts 14/14 targeted | PASS; exact test containers removed |
| Passkey fail-closed/positive regression | 8/8 targeted; no pending challenge, empty account, enrolled challenge and explicit userId isolation | PASS |
| MFA fail-closed/recovery regression | 9/9 targeted; unauthenticated/status/verify/enroll plus invalid and valid recovery-code paths | PASS |
| gRPC identity + certificate pinning unit regression | 17/17 targeted; introspection, permissions, user lookup, token revocation and certificate reload/pin validation | PASS |
| gRPC/certificate targeted coverage | `GrpcIdentityService` 88% line / 75% branch; `MetadataCertificateValidator` 100% line / 87.5% branch | MEASURED |
| Vault MFA + token binding unit regression | 13/13 targeted; transit encrypt/decrypt/error handling, Vault HTTP/cache/error payloads and token-binding normalization/TTL | PASS |
| User session tracker unit regression | 5/5 targeted; Redis key isolation, add/get/remove/clear and seven-day TTL | PASS |
| DPoP token-binding handler regression | 10/10 targeted; principal/binding mismatch, malformed proof and fail-closed paths | PASS |
| DPoP token-response regression | 6/6 targeted; required-client selection, case sensitivity, missing configuration and blank token fail-closed behavior | PASS |
| Facility authorization boundary regression | 5/5 isolated; missing target, route/query fallback, case-insensitive facility matching and strict-mode behavior | PASS |
| LDAP synchronization regression | 17/17 isolated; existing-user reuse, create failure, connection containment, cancellation, missing-user deactivation and config validation | PASS |
| IAM control-plane lifecycle regression | 8/8 targeted; anonymous denial plus scope/permission-set/group/boundary/policy/workload-role flows | PASS |
| IAM control-plane targeted coverage | 73.73% line / 26.92% branch; collector completed in isolated clean run | MEASURED — further branches remain |
| Incident-response controls regression | 5/5 targeted; session/credential reason validation and unknown-user fail-closed paths | PASS |
| Incident-response targeted coverage | 59.18% line / 100% branch for `AdminIncidentEndpoints` | MEASURED — success/mutation paths remain |
| API security contract | `verify-api-security.ps1` checks 11 service API projects | PASS; CommerceService now uses shared DPoP middleware |
| Authorization endpoint inventory | 109 endpoints, 0 missing (102 protected, 4 anonymous) | PASS |
| Test dependency vulnerability scan | 4/4 Identity test projects without known vulnerable packages | PASS |
| OIDC conformance + penetration evidence | `scripts/verify-independent-security-evidence.ps1` PASS; local reports are automated RFC 9700/security-remediation evidence (`artifacts/security/**`) | PASS for automated gate; signed external assessor/pentest still required before regulated production |
| Adaptive MFA E2E | 7/7 | PASS |
| Docker/browser cleanup | no disposable test containers after run | PASS |
| Provisioning route contract | `AdminProvisioningJob` / `AdminProvisioningJobRetry` dùng chung API response và integration tests | PASS |
| Canonical native-MFA reject route | `IdentityApiRoutes.NativeMfaReject` dùng chung giữa endpoint registration và integration test | PASS |
| Full authenticated SSO E2E | requires runtime `E2E_PASSWORD` | ENVIRONMENT-BLOCKED |
| Identity coverage | 90.01% line / 80.03% branch (merged Cobertura; compiler-generated classes and composition wiring excluded) | PASS — target 90%/80% |

`IdentityApiRoutes` hiện cũng là nguồn chuẩn cho các OIDC protocol URLs (`OidcAuthorize`, `OidcToken`, `OidcIntrospect`, `OidcRevoke`, `OidcRegister`); các DPoP/OIDC tests đã chuyển sang dùng constants này. Integration test project build lại sau thay đổi route: **0 errors** (chỉ còn warnings hiện hữu).

Lượt full integration Docker mới nhất chạy **430/430 PASS**, 0 failed/0 skipped, với PostgreSQL/Redis disposable; runner đã xóa exact toàn bộ test containers sau run và không chạm app containers. Báo cáo coverage merge sạch từ integration final27, application final5 và infrastructure final6 đạt **90.01% line / 80.03% branch**, validator trả `IDENTITY_COVERAGE_GATE_PASS`.

Focused Playwright admin identity-menu test chạy xanh trên Chromium/Mobile/Tablet **3/3 PASS**; authenticated SSO vẫn cần secret runtime riêng.

## Cách chạy authenticated E2E

Không dùng password mặc định trong CI hoặc production-like runtime. Cấp secret ngoài repo:

```powershell
$env:E2E_PASSWORD = '<local-secret>'
$env:E2E_AUTH_REQUIRED = 'true'
try {
  .\scripts\run-e2e-docker.ps1 -Spec 'specs/00-sso-smoke.spec.js'
} finally {
  Remove-Item Env:E2E_PASSWORD,Env:E2E_AUTH_REQUIRED -ErrorAction SilentlyContinue
}
```

Nếu secret không có hoặc không khớp tenant, trạng thái phải giữ là `environment-blocked`; không reset hoặc xóa container ứng dụng để làm xanh test.

## Exit criteria còn lại

1. Chạy authenticated SSO E2E với credential tenant được cấp qua secret manager.
2. Thay automated OIDC/pentest evidence bằng signed external assessor evidence trước regulated production go-live.
3. Chỉ đánh dấu release gate hoàn tất khi cả hai điều kiện trên có runtime evidence.

## Public manufacturing E2E

Các suite public không cần credential và có thể chạy độc lập:

```powershell
.\scripts\run-e2e-docker.ps1 -Config 'manufacturing-buyer.playwright.config.mjs'
.\scripts\run-e2e-docker.ps1 -Config 'manufacturing-operator-public.playwright.config.mjs'
```

Runner dùng dependency directory tạm riêng cho từng container để các suite chạy
song song không ghi đè `node_modules`. Suite authenticated vẫn bắt buộc
`E2E_EMAIL` và `E2E_PASSWORD` từ secret storage; không dùng credential mặc định.
