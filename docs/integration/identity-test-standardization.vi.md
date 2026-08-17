# Identity Service — Test Standardization & Validation Runbook

Ngày cập nhật: 2026-08-16

Snapshot xác nhận mới nhất: Identity unit projects trực tiếp **454/454 PASS** (Domain 87, Application 162, Infrastructure 205); full Docker integration baseline **285/285 PASS**; bổ sung targeted security/API suites **57/57 + 14/14 PASS**.

Bổ sung sau snapshot: SCIM DTO RFC contract **3/3 PASS**; API low-coverage integration filter **7/7 PASS** sau khi chuẩn hóa reflection helper; `BulkImportContractTests` **8/8 PASS**; `BulkImportEndpointTests` + Directory readiness helper **7/7 PASS**; SAML runtime configuration **4/4 PASS**; OIDC initializer **4/4 PASS**; IAM control-plane targeted **12/12 PASS** (gồm guard cho group/boundary, workload-role và resource-policy). `ScimGroupResponse.meta.resourceType` đã sửa về `Group` theo RFC 7643. Coverage merge mới nhất **88.30% line / 72.64% branch**, chưa đạt gate 90/80.

## Nguyên tắc dùng chung

- Test data dùng chung tại `tests/IdentityService/IdentityService.Testing/IdentityTestData.cs`; integration login dùng `IdentityTestCredentials` thay vì lặp credential literal.
- API route contract dùng chung tại `src/Shared/Contracts/His.Hope.Contracts/Identity/IdentityApiRoutes.cs`; Device Posture, federation, account-linking, provisioning, ReBAC và admin security-signal routes dùng canonical constants ở API/tests.
- Các project `IdentityService.Testing`, Domain.Tests, Application.Tests, Infrastructure.Tests và IntegrationTests đều đã được đăng ký trong `His.Hope.sln`; chạy solution không còn bỏ sót hai lớp unit Domain/Application.
- Test không tự tạo password/route literal nếu đã có factory hoặc contract constant.
- Integration dùng PostgreSQL/Redis disposable; không dùng container ứng dụng đang chạy.
- Mọi runner phải cleanup exact container trong `finally` và không sweep Docker workload khác; Docker runner tự tạo/xóa user-defined network khi caller dùng network dựng sẵn `bridge` hoặc network chưa tồn tại, chạy test container detached và poll exit code để tránh Docker Desktop stream EOF. Runner tự ưu tiên `.docker-test-config` của repository nếu có, dùng workspace lock độc quyền (`artifacts/.identity-test-run.lock`) để không chạy song song và khóa DLL/coverage collector. Trước restore, runner xóa `obj` build artifacts trong workspace mount để không đưa Windows-only NuGet fallback folder (ví dụ DevExpress offline path) vào Linux assets.
- Test infrastructure dependency scan hiện pass cho cả bốn Identity test projects; Testcontainers được nâng lên 4.13.0, SSH.NET được pin ở 2026.0.0, và SQLite native test dependency đã được loại khỏi các test dùng InMemory provider.

## Các runner chuẩn

| Phạm vi | Runner | Lệnh mẫu | Cleanup |
|---|---|---|---|
| Unit | `scripts/run-identity-tests.ps1` | `-ResultsDirectory <dir>` | Không tạo container |
| Integration/Docker | `scripts/run-identity-tests-docker.ps1` | `-Network bridge -Filter 'FullyQualifiedName~...'` | PostgreSQL/Redis + disposable network exact names |
| Cleanup after interrupted Docker run | `scripts/cleanup-identity-test-docker.ps1` | `-RunId <10-hex-id>` hoặc `-IncludeRunning` khi đã xác nhận run bị bỏ rơi | Chỉ resource `identity-docker-*`, không quét container ứng dụng |
| E2E/browser | `scripts/run-e2e-docker.ps1` | `-Spec 'specs/adaptive-mfa.spec.js'` | Browser `--rm` |
| Coverage | `scripts/validate-identity-coverage.ps1` | `-CoverageRoot <dir>` | Không tạo container |

## Quality-gate matrix

| Gate | Evidence hiện tại | Trạng thái |
|---|---:|---|
| Domain unit | 87/87 | PASS |
| Application unit | 162/162 | PASS |
| Infrastructure unit | 205/205 | PASS |
| Identity integration | 285/285 full baseline; 57/57 security + 14/14 API targeted | PASS; exact test containers removed |
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
| API security contract | 10 service API projects | PASS |
| Authorization endpoint inventory | 109 endpoints, 0 missing (102 protected, 4 anonymous) | PASS |
| Test dependency vulnerability scan | 4/4 Identity test projects without known vulnerable packages | PASS |
| Independent OIDC conformance + penetration evidence | `scripts/verify-independent-security-evidence.ps1` requires two assessor reports; `artifacts/security/oidc-conformance/report.json` is absent | ENVIRONMENT-BLOCKED — must come from an external assessor; never synthesize locally |
| Adaptive MFA E2E | 7/7 | PASS |
| Docker/browser cleanup | no disposable test containers after run | PASS |
| Provisioning route contract | `AdminProvisioningJob` / `AdminProvisioningJobRetry` dùng chung API response và integration tests | PASS |
| Canonical native-MFA reject route | `IdentityApiRoutes.NativeMfaReject` dùng chung giữa endpoint registration và integration test | PASS |
| Full authenticated SSO E2E | requires runtime `E2E_PASSWORD` | ENVIRONMENT-BLOCKED |
| Identity coverage | 88.30% line / 72.64% branch (`artifacts/coverage-identity-next`; compiler-generated classes and composition wiring excluded) | FAIL — target 90%/80% |

`IdentityApiRoutes` hiện cũng là nguồn chuẩn cho các OIDC protocol URLs (`OidcAuthorize`, `OidcToken`, `OidcIntrospect`, `OidcRevoke`, `OidcRegister`); các DPoP/OIDC tests đã chuyển sang dùng constants này. Integration test project build lại sau thay đổi route: **0 errors** (chỉ còn warnings hiện hữu).

Lượt full integration Docker baseline ngày 2026-08-16 chạy **285/285 PASS**, 0 failed/0 skipped, với PostgreSQL/Redis disposable; các security/API targeted additions đạt **57/57 + 14/14**, BulkImport contract **8/8**, endpoint/readiness **7/7**, IAM control-plane **12/12**, runner đã xóa exact toàn bộ test containers sau run và không chạm app containers. Báo cáo coverage merge mới `artifacts/coverage-identity-next` đạt **88.30% line / 72.64% branch**, vẫn chưa đạt ngưỡng 90%/80%.

Targeted Playwright dashboard metrics test hiện fail trên Chromium/Mobile/Tablet vì sau navigation tới `/metrics` vẫn render resource shell và không xuất hiện `app-metrics-overview`; cần xác minh/rebuild dashboard runtime artifact trước khi coi E2E xanh.

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

1. Bổ sung test cho các module business logic chưa đạt coverage và chạy lại merged report đến tối thiểu 90% line / 80% branch.
2. Chạy authenticated SSO E2E với credential tenant được cấp qua secret manager.
3. Chỉ đánh dấu release gate hoàn tất khi cả hai điều kiện trên có runtime evidence.
