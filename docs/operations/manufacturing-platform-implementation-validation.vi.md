# Manufacturing Platform — Implementation, Deployment và Validation Report

Cập nhật: 2026-08-29  
Phạm vi: `manufacturing-buyer-app`, `internal-operator-app`,
Manufacturing/Commerce/Content backend, Docker local và production security
contracts.

## 1. Kết quả điều hành

Implementation và deployment local đã hoàn tất, có bằng chứng build, test,
container health, HTTP smoke và Playwright. Production promotion vẫn bị chặn
fail-closed bởi các prerequisite ngoài repository; không dùng kết quả local để
thay thế bằng chứng production.

## 2. Những gì đã implement

### Buyer Commerce

- Catalog/product detail, blog/blog detail, RFQ, cart/order, profile và
  cooperation routes.
- Shared frontend foundation cho theme, locale, typography và auth.
- Product/detail lazy chunks có trong production bundle.

### Manufacturing Operator

- Dashboard, inventory/lots, production, recipes, product specifications,
  quality inspections, deviations, forecast, sales allocation, procurement,
  maintenance, orders, users và content/RFQ.
- Route shell và action flows dùng shared foundation; backend vẫn là authority
  cho permission/tenant boundary.

### Backend và platform

- Manufacturing, Commerce và Content API build được từ source hiện tại.
- Authorization negative paths trả `401`/problem-details đúng contract.
- Content integration chạy đúng khi dùng dev connection contract trỏ PostgreSQL
  port `5433`; không ghi credential vào tài liệu.
- Database/storage security runbook, fail-closed promotion validator, Azure
  immutable-retention verifier và MinIO TLS/Object Lock contract đã được thêm.

### 2.1 Ma trận requirement → evidence

| Requirement | Evidence hiện tại | Trạng thái |
|---|---|---|
| Buyer catalog và product detail | Buyer build, lazy chunks, product-detail chunk và targeted E2E | PASS local |
| Buyer blog, RFQ, cart/order, profile, cooperation | Buyer route/component implementation và targeted content/commerce E2E 5/5 | PASS local |
| Operator manufacturing workspace | Operator build/lint và targeted completeness/workflow E2E 14/14 | PASS local |
| Backend Manufacturing/Commerce/Content | Source build 0 errors/0 warnings; integration evidence bên dưới | PASS local |
| Permission, tenant/facility boundary | Negative `401`/problem-details smoke và integration deny cases | PASS local |
| Docker deployment local | Buyer/operator force-recreate, health và HTTP smoke | PASS development |
| Integration coverage | [current14 matrix](../../artifacts/evidence/integration-matrix-current14/integration-test-matrix.json): 15 projects, 616/618 passed, 0 failed, 2 skipped | ENVIRONMENT-BLOCKED: 2 Manufacturing external-placement tests skipped |
| Production DB/storage secret, network và object-lock contract | [database/storage validator](../../scripts/validate-database-storage-security-contract.ps1) | BLOCKED fail-closed |
| Production storage owner attestation | [attestation verifier](../../scripts/validate-production-storage-attestation.ps1) và mẫu attestation | BLOCKED cho đến khi provider evidence đầy đủ |
| Azure immutable backup runtime | [Azure retention verifier](../../scripts/validate-azure-blob-retention.py) | BLOCKED: SAS expired/provider policy not ready |
| Production CSI/KMS/runtime restore | Production kubeconfig và platform attestation | UNVERIFIED: external prerequisite |

Quy ước: `PASS local` chỉ chứng minh source/development runtime; không thay
thế production evidence. `BLOCKED`/`UNVERIFIED` là gate không được phép bypass
bằng thay đổi UI, placeholder substitution hoặc hạ validator.

## 3. Local validation evidence

Authenticated manufacturing E2E latest: buyer route **1/1 PASS**, operator route
**2/2 PASS**, operator completeness **7/7 PASS**, manufacturing workflow **7/7 PASS**,
and content/commerce **5/5 PASS**. Buyer tests use the `end_user` pilot identity;
operator CMS/workflow tests use a separate operator identity. These local pilot
credentials are never production credentials.

| Gate | Kết quả |
|---|---:|
| Buyer Angular build | PASS |
| Operator Angular build | PASS |
| Buyer/operator lint | PASS |
| Manufacturing/Commerce/Content backend build | PASS, 0 errors/0 warnings |
| Manufacturing application tests | PASS, 20/20 |
| Manufacturing integration tests | PASS, 56/56 |
| Commerce integration tests | PASS, 13/13 |
| Content integration tests | PASS, 8/8 |
| Script unit tests | PASS, 4/4 |
| Buyer Playwright | PASS, 2/2 |
| Operator public Playwright | PASS, 1/1 |
| Manufacturing smoke | PASS, `all-checks` |
| Authenticated manufacturing Playwright | PASS local — buyer 1/1, operator 2/2, completeness 7/7, workflow 7/7, content/commerce 5/5 |
| Full integration matrix | ENVIRONMENT-BLOCKED — current14: 15 projects, 618 tests: 616 passed, 0 failed, 2 skipped; Identity 460/460 và BFF 27/27 PASS |

Lệnh tái chạy các gate chính:

Authenticated manufacturing E2E local dùng secret injection trong process, không ghi
credential vào repository:

```powershell
$env:E2E_BUYER_EMAIL = '<buyer end_user identity>'
$env:E2E_BUYER_PASSWORD = '<local secret>'
$env:E2E_OPERATOR_EMAIL = '<operator identity>'
$env:E2E_OPERATOR_PASSWORD = '<local secret>'
npx playwright test --config=manufacturing-content-commerce.playwright.config.mjs --workers=1
npx playwright test --config=manufacturing-operator-completeness.playwright.config.mjs --workers=1
npx playwright test --config=manufacturing-workflow.playwright.config.mjs --workers=1
```

```powershell
rtk npm --prefix manufacturing-buyer-app run build
rtk npm --prefix manufacturing-buyer-app run lint
rtk npm --prefix internal-operator-app run build
rtk npm --prefix internal-operator-app run lint
rtk dotnet test tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/ManufacturingService.Integration.Tests.csproj --no-restore
rtk dotnet test tests/Services/CommerceService/CommerceService.Integration.Tests/CommerceService.Integration.Tests.csproj --no-restore
rtk powershell -File scripts/config/smoke-manufacturing.ps1 -SkipBuyerIntegration
rtk node tests/e2e/node_modules/playwright/cli.js test --config=tests/e2e/manufacturing-buyer.playwright.config.mjs --workers=1
rtk powershell -File scripts/run-integration-test-matrix.ps1
```

Evidence matrix mới nhất: [integration-test-matrix.json](../../artifacts/evidence/integration-matrix-current14/integration-test-matrix.json).
Current14 đã mở rộng discovery để bao phủ cả `*IntegrationTests.csproj` (BFF và Identity), và xác nhận 616/618 test pass, không có assertion failure. `DATABASE_CONTENT_URL` chỉ được inject cho ContentService tests và được khôi phục sau mỗi project.

## 4. Local Docker deployment

Đã build production image mới và force-recreate thành công:

- `his-hope-manufacturing-buyer` — port `4205`;
- `his-hope-manufacturing-operator` — port `4300`.

Lần build xác nhận Angular production bundle của buyer và operator đều hoàn tất;
container đang chạy trực tiếp từ image Compose `docker-manufacturing-buyer-app`
và `docker-internal-operator-app`.

Đã xác nhận health và HTTP smoke cho buyer/operator, Manufacturing,
Commerce và Content containers. Docker local PostgreSQL dùng `ssl=off` và
chỉ được xem là development evidence.

Runtime recheck sau lần recreate mới nhất: buyer `HTTP 200`, operator `HTTP 200`, Manufacturing
API `/health` `HTTP 200`; Commerce và Content container đều `healthy`, còn
`/health` trực tiếp trả `HTTP 401` theo authorization contract. `401` ở endpoint
protected không được diễn giải là container failure; readiness/health container
vẫn phải được kiểm tra riêng.

### Targeted E2E sau lần recreate

- Buyer UI: **2/2 PASS** — đổi ngôn ngữ/theme và callback lỗi không tạo empty-token
  console error.
- Buyer authenticated routes: **1/1 PASS** với buyer pilot identity (`end_user`).
- Operator UI: **2/2 PASS** — callback lỗi và toàn bộ authenticated route contract
  không có console error/API 5xx.
- Operator public shell: **1/1 PASS** — login public không render authenticated
  shell và typography hợp đồng.
- Operator completeness: **7/7 PASS** — redirect, route graph, master-data write,
  lot reservation, supplier, production order và quality inspection.
- Content/Commerce: **5/5 PASS** — blog list/detail, cooperation, buyer RFQ và
  operator CMS/RFQ; buyer/operator dùng identity theo đúng portal class.
- Manufacturing workflow: **7/7 PASS** — workflow reference, entity steppers,
  status history và cross-entity workflow.

Full platform Playwright suite được list thành công (417 tests/21 files), nhưng chưa
được coi là PASS toàn bộ: SSO smoke suite tổng quát vẫn timeout ở runtime khi chạy
không có fixture identity tương ứng. Các targeted manufacturing suites đã chạy với
identity đúng portal class và có kết quả PASS ở trên.

## 5. Production security validation

### PASS ở source contract

- Vault production secret provider và TLS verification.
- Production workload identity yêu cầu Vault, cấm static Vault token.
- Runtime NetworkPolicy declarations.
- MinIO backup HTTPS, TLS Secret bắt buộc, Object Lock và `COMPLIANCE 30d`.
- Azure SAS không được in log; bootstrap bị chặn nếu retention chưa `Locked`/30
  ngày.
- Validator đã được nối vào DevSecOps PR gate, CNPG bootstrap và production
  go-live gate.

### BLOCKED/UNVERIFIED cần production owner cung cấp

- Azure SAS hiện tại đã hết hạn; runtime Blob access fail và retention endpoint
  trả HTTP 400.
- Azure ObjectStore manifest còn destination placeholder trước khi protected
  bootstrap render giá trị thật.
- Azure CLI read-only evidence: containers `his-hope` và `epi` đều chưa có
  immutability policy hoặc immutable versioning; encryption scope vẫn cho phép
  override. Cần storage owner áp dụng WORM/KMS policy bằng protected change.
- CSI `viettel-shared` là platform-owned; cần attestation encryption-at-rest,
  KMS binding, replication/failure domain và VolumeSnapshot restore.
- Cần production kubeconfig để chạy live CSI, restore, DR, mTLS và observability
  gates.
- Kubernetes context hiện tại `kubernetes-admin@kubernetes` không reachable
  (kubectl request timeout); không có k3d production cluster container đang chạy.
- Cần protected operator test identity để chạy authenticated route/write E2E.

Các điểm trên không được bypass bằng `AllowProduction`, placeholder substitution
thủ công, credential commit hoặc cách hạ validator xuống warning.

## 6. Production execution order

Lưu ý: authenticated E2E local đã được chạy với pilot identities tách biệt theo
portal class. Gate production vẫn yêu cầu test identity được production owner cấp
riêng, có bảo mật và vòng đời phù hợp; không tái sử dụng local pilot credentials.

1. Rotate/provision Azure SAS và immutable container policy `Locked >= 30d`.
   Có thể dùng `scripts/configure-azure-blob-immutability.ps1` ở dry-run; apply
   chỉ cho phép với `-AllowProduction -Confirmation LOCK-WORM`.
   Nếu container chưa bật immutable versioning, phải provision container mới,
   migrate/verify backup objects, đổi destination, chạy restore drill rồi mới
   lock container cũ theo retention policy.
2. Platform owner attests `viettel-shared` encryption/KMS/replication/snapshot
   behavior.
3. Cấp kubeconfig production qua protected environment; không lưu trong repo.
4. Chạy Azure Blob access + retention verifier.
5. Chạy CNPG bootstrap dry-run, sau đó apply qua approval/dual control.
6. Chạy production release, storage, CNPG, restore, DR, mTLS và observability
   gates.
7. Chỉ promote khi mọi step thành công và evidence artifact được lưu.

Runbook chi tiết: [Database và Storage Security Hardening Runbook](database-storage-security-hardening-runbook.vi.md).
Mẫu bằng chứng platform owner: [Production Storage Attestation Template](production-storage-attestation-template.vi.md).

## 7. Release decision

Local release candidate: **PASS cho development/integration** — current14 có 616/618 test pass, 0 failure; 2 test còn lại bị block bởi external database placement.  
Production release: **BLOCKED**, do runtime provider evidence và protected
credentials chưa sẵn sàng. Đây là trạng thái an toàn và có thể tiếp tục bằng
các bước ở mục 6.
