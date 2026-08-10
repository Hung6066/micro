# His.Hope Agent Usage Guide

> **Tài liệu hướng dẫn chi tiết sử dụng hệ thống 16 AI Agents cho His.Hope**
> 
> Version: 1.0 | Last updated: 2026-07-16 | Maintainer: @architect

---

## Mục Lục

1. [Tổng Quan Hệ Thống Agent](#1-tổng-quan-hệ-thống-agent)
2. [Mô Hình Hoạt Động](#2-mô-hình-hoạt-động)
3. [Danh Sách Agent & Vai Trò](#3-danh-sách-agent--vai-trò)
4. [Pipeline Orchestrator - Quy Trình Chuẩn](#4-pipeline-orchestrator---quy-trình-chuẩn)
5. [Hướng Dẫn Sử Dụng Từng Agent](#5-hướng-dẫn-sử-dụng-từng-agent)
6. [Use Cases - Trường Hợp Sử Dụng Thực Tế](#6-use-cases---trường-hợp-sử-dụng-thực-tế)
7. [Quality Gates - Kiểm Soát Chất Lượng](#7-quality-gates---kiểm-soát-chất-lượng)
8. [Best Practices](#8-best-practices)
9. [Troubleshooting](#9-troubleshooting)
10. [Cheatsheet - Tra Cứu Nhanh](#10-cheatsheet---tra-cứu-nhanh)

---

## 1. Tổng Quan Hệ Thống Agent

His.Hope sử dụng **16 AI Agents chuyên biệt** được điều phối bởi **Architect Agent** (primary) và **Orchestrator Agent** (pipeline coordinator). Mỗi agent đảm nhận một lĩnh vực chuyên môn riêng, giao tiếp với nhau thông qua `task` tool, và tuân thủ quy trình kiểm soát chất lượng nghiêm ngặt trước khi code được commit lên GitHub.

### Sơ đồ tổng quan

```
┌──────────────────────────────────────────────────────────────────┐
│                        @architect (Primary)                      │
│              Kiến trúc sư trưởng - Điều phối chiến lược          │
└──────────────────────────┬───────────────────────────────────────┘
                           │
          ┌────────────────┼────────────────┐
          ▼                ▼                 ▼
   ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐
   │ @orchestrator│ │  Triển khai  │ │   Triển khai     │
   │  (Pipeline)  │ │  trực tiếp   │ │   đơn giản       │
   └──────┬───────┘ └──────────────┘ └──────────────────┘
          │
   ┌──────┴──────────────────────────────────────────────┐
   │              5-Phase Pipeline                       │
   │                                                     │
   │  Phase 0: Pre-Flight          @orchestrator         │
   │  Phase 1: Plan                @plan                 │
   │  Phase 2: Implement   @dotnet @angular @dba         │
   │                       @devops @docs @ml-ai          │
   │  Phase 3: Test        @testing-backend              │
   │                       @testing-frontend @qa         │
   │  Phase 4: Validate    @validate @check-ui           │
   │                       @security @docs               │
   │  Phase 5: Commit      @git → GitHub                 │
   └─────────────────────────────────────────────────────┘
```

### Nguyên tắc cốt lõi

| Nguyên tắc | Mô tả |
|---|---|
| **Chuyên môn hóa** | Mỗi agent chỉ làm đúng lĩnh vực của mình |
| **Phân quyền rõ ràng** | Agent không tự ý vượt quyền - phải qua gate |
| **Gate trước Commit** | Code chỉ lên GitHub khi TẤT CẢ quality gates xanh |
| **Song song hóa** | Các agent độc lập chạy đồng thời để tối ưu thời gian |
| **Retry & Escalate** | Tự động thử lại 3 lần, sau đó leo thang lên @architect |
| **Audit trail** | Mọi quyết định đều có ADR hoặc commit message rõ ràng |

---

## 2. Mô Hình Hoạt Động

### 2.1 Quy trình thông minh với @dispatcher

Mọi yêu cầu đều đi qua `@dispatcher` để phân tích và chọn đường dẫn tối ưu:

```
User yêu cầu → @dispatcher (phân tích scope + complexity)
  │
  ├── PATH_DIRECT (trivial/simple, 1-2 agents):
  │     @architect delegate trực tiếp → test → @git commit
  │     VD: sửa typo, thêm comment, fix bug 1 file
  │
  ├── PATH_LITE (medium, 2-3 domains, 3-5 agents):
  │     @architect điều phối lite pipeline → @git commit
  │     VD: thêm API + UI, thêm migration + entity
  │
  └── PATH_FULL (complex, 4+ domains, multi-service):
        @orchestrator chạy 5 Phase đầy đủ → @git commit
        VD: tạo service mới, breaking proto change, refactor lớn
```

**Lợi ích**: Tiết kiệm 30-50% thời gian so với luôn chạy full pipeline. Không agent nào chạy nếu không cần thiết.

### 2.2 Cách giao tiếp với Agent

Tất cả agent được gọi thông qua Architect (primary agent). Cú pháp:

```
<yêu cầu công việc>

Ví dụ:
"Thêm tính năng kiểm tra dị ứng chéo khi kê đơn thuốc trong PatientService"
"Sửa lỗi double-booking trong AppointmentService"
"Tạo migration thêm bảng patient_allergies"
"Kiểm tra bảo mật cho API mới của BillingService"
"Review UI của màn hình đăng ký bệnh nhân"
```

---

## 3. Danh Sách Agent & Vai Trò

### 3.1 Agent điều phối (Coordination)

| Agent | Model | Vai trò | Khi nào dùng |
|---|---|---|---|
| `@architect` | deepseek-v4-pro | Kiến trúc sư trưởng, điều phối toàn team | Luôn luôn - là entry point |
| `@dispatcher` | deepseek-v4-pro | Phân tích thông minh, chọn agent tối ưu, chọn pipeline path | **Đầu tiên** - trước mọi triển khai |
| `@plan` | deepseek-v4-pro | Lập kế hoạch chi tiết trước khi code | Trước feature lớn, cần phân tích |
| `@orchestrator` | opencode-go/deepseek-v4-flash | Điều phối pipeline 5 phase CHO COMPLEX FEATURES | Chỉ khi @dispatcher chọn PATH_FULL |

### 3.2 Agent triển khai (Implementation) - Phase 2

| Agent | Model | Chuyên môn | Phạm vi |
|---|---|---|---|
| `@dotnet` | opencode-go/deepseek-v4-flash | .NET 8, Clean Architecture, CQRS, DDD, gRPC, EF Core | `src/Services/*/` |
| `@angular` | opencode-go/deepseek-v4-flash | Angular 17, NgRx, Angular Material, RxJS | `src/Frontend/` |
| `@dba` | opencode-go/deepseek-v4-flash | CockroachDB, SQL migrations, performance | `cockroach/`, EF Core configs |
| `@devops` | opencode-go/deepseek-v4-flash | K8s, Docker, CI/CD, Linkerd, Cilium, Bazel | `k8s/`, `docker/`, `cicd/` |
| `@docs` | opencode-go/deepseek-v4-flash | ADRs, API docs, READMEs, changelogs, runbooks | `docs/`, `*.md` |
| `@ml-ai` | opencode-go/deepseek-v4-flash | ML pipelines, Vertex AI, model training | `ml/` |
| `@data-platform` | opencode-go/deepseek-v4-flash | BigQuery, Dataflow, Pub/Sub, dbt | `data-platform/` |

### 3.3 Agent kiểm thử (Testing) - Phase 3

| Agent | Model | Chuyên môn | Công cụ |
|---|---|---|---|
| `@testing-backend` | opencode-go/deepseek-v4-flash | .NET xUnit, Testcontainers, gRPC contract | xUnit, FluentAssertions, PactNet |
| `@testing-frontend` | opencode-go/deepseek-v4-flash | Angular unit, Cypress E2E, Playwright, axe | Jasmine, Karma, Cypress |
| `@qa` | opencode-go/deepseek-v4-flash | Integration, chaos, load tests, quality gates | k6, Chaos Mesh, NBomber |

### 3.4 Agent kiểm định (Validation) - Phase 4

| Agent | Model | Chuyên môn | Phạm vi |
|---|---|---|---|
| `@validate` | opencode-go/deepseek-v4-flash | Build, proto lint, FluentValidation, secrets scan | Toàn repo |
| `@check-ui` | opencode-go/deepseek-v4-flash | Material theme, WCAG 2.1 AA, design system | `src/Frontend/` |
| `@security` | opencode-go/deepseek-v4-flash | Vault, JWT, RBAC, Cilium policies, HIPAA | `vault/`, `k8s/`, IdentityService |
| `@docs` | opencode-go/deepseek-v4-flash | Doc coverage, ADR freshness, link validity | `docs/` |

### 3.5 Agent xuất bản (Release) - Phase 5

| Agent | Model | Chuyên môn | Công cụ |
|---|---|---|---|
| `@git` | opencode-go/deepseek-v4-flash | Git commit, push, branch, PR | `git`, `gh` CLI |

### 3.6 Model Tiers

| Tier | Model chính |
|------|-------------|
| **Pro** (architect, plan, harness-runner, loop-engineer) | `deepseek-v4-pro` |
| **Flash** (dotnet, angular, orchestrator, qa, devops, dba, etc.) | `opencode-go/deepseek-v4-flash` |

Fallback plugin [`@razroo/opencode-model-fallback`](https://github.com/razroo/opencode-model-fallback) chỉ áp dụng cho Pro tier khi deepseek bị rate limit.

---

## 4. Pipeline Orchestrator - Quy Trình Chuẩn

### 4.1 Sơ đồ 5 Phase

```
FEATURE REQUEST
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 0: PRE-FLIGHT                                             │
│ @orchestrator kiểm tra: git status, branch convention,          │
│ xác định scope, phát hiện xung đột                              │
│ Gate: Clean working tree + Valid branch name                     │
└────────────────────────────┬────────────────────────────────────┘
                             │ PASS
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 1: PLAN                                                   │
│ @plan phân tích: liệt kê files cần tạo/sửa, xác định            │
│ cross-service impacts, breaking changes, ước lượng scope        │
│ Gate: Plan được architect phê duyệt                              │
└────────────────────────────┬────────────────────────────────────┘
                             │ APPROVED
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 2: IMPLEMENT                                              │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐ │
│ │ @dotnet  │ │ @angular │ │  @dba   │ │ @devops  │ │ @docs  │ │
│ │ Backend  │ │ Frontend │ │   DB    │ │  Infra   │ │  Docs  │ │
│ └──────────┘ └──────────┘ └──────────┘ └──────────┘ └────────┘ │
│ Tất cả chạy SONG SONG                                            │
│ Gate: dotnet build + npm run build thành công                    │
└────────────────────────────┬────────────────────────────────────┘
                             │ BUILD PASS
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 3: TEST                                                   │
│ ┌────────────────────┐ ┌────────────────────┐                   │
│ │ @testing-backend   │ │ @testing-frontend  │                   │
│ │ xUnit, Testcontain │ │ Jasmine, Cypress   │                   │
│ └────────┬───────────┘ └────────┬───────────┘                   │
│          └──────────┬───────────┘                               │
│                     ▼                                           │
│              ┌─────────────┐                                    │
│              │    @qa      │                                    │
│              │ Contract,   │                                    │
│              │ E2E, Load   │                                    │
│              └─────────────┘                                    │
│ Gate: TẤT CẢ tests xanh, coverage ≥ 80%                         │
└────────────────────────────┬────────────────────────────────────┘
                             │ ALL TESTS PASS
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 4: VALIDATE                                               │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐            │
│ │@validate │ │@check-ui │ │@security │ │  @docs   │            │
│ │Build/Lint│ │WCAG/UI   │ │HIPAA/VLT │ │Doc Audit │            │
│ └──────────┘ └──────────┘ └──────────┘ └──────────┘            │
│ Tất cả chạy SONG SONG                                            │
│ Gate: ZERO [MUST FIX] violations                                 │
└────────────────────────────┬────────────────────────────────────┘
                             │ ALL GATES GREEN ✓
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 5: COMMIT                                                 │
│ @git: Stage → Conventional Commit → Push → PR (nếu cần)         │
│ Gate: Commit message convention, no secrets, no binaries         │
└─────────────────────────────────────────────────────────────────┘
                             │
                             ▼
                     🚀 GITHUB
```

### 4.2 Thời gian ước tính mỗi Phase

| Phase | Thời gian | Ghi chú |
|---|---|---|
| Phase 0 | < 1 phút | Kiểm tra git state |
| Phase 1 | 2-5 phút | Phân tích và lập kế hoạch |
| Phase 2 | 5-30 phút | Tùy độ phức tạp feature |
| Phase 3 | 3-15 phút | Tùy số lượng tests |
| Phase 4 | 2-10 phút | Validation & audit |
| Phase 5 | < 1 phút | Git commit & push |
| **Tổng** | **15-60 phút** | Cho một feature trung bình |

---

## 5. Hướng Dẫn Sử Dụng Từng Agent

### 5.0 @dispatcher - Điều phối thông minh (DÙNG ĐẦU TIÊN)

**Vai trò**: Phân tích yêu cầu, xác định domains bị ảnh hưởng, đánh giá độ phức tạp, chọn tập agent tối ưu và pipeline path. **Đây là SMART ENTRY POINT - luôn chạy trước khi triển khai.**

**Khi nào dùng**: **Luôn luôn** - mọi yêu cầu code đều qua @dispatcher trước.

**Cách hoạt động**:

```
Bạn nói: "Sửa lỗi double-booking trong AppointmentService"

@dispatcher phân tích:
  → Domain: backend
  → Complexity: simple (1 service, bug fix)
  → Selected: @dotnet, @testing-backend, @validate, @git
  → Skipped: @angular, @dba, @devops, @security, @qa, @check-ui, @docs (9 agents)
  → Path: PATH_DIRECT
  → Time saved: 70% vs full pipeline
```

**Ví dụ câu lệnh**:

```
"Phân tích yêu cầu: Thêm tính năng export báo cáo bệnh nhân ra PDF"
"Dispatch yêu cầu: Tạo màn hình dashboard mới với bento-grid layout"
"Đánh giá scope: Thêm trường blood_type vào PatientService"
```

**Output mong đợi** - @dispatcher trả về báo cáo phân tích:

```markdown
## 📊 Feature Analysis Report

### Domain Analysis
| Domain | Triggered? |
|---|---|
| backend | ✅ |
| frontend | ❌ |
| database | ✅ (new column) |
| ...

### Complexity Rating: simple
### Selected Agents (4 of 17): @dotnet, @dba, @testing-backend, @git
### Skipped Agents (13 of 17): @angular, @devops, @security...
### Recommended Path: PATH_DIRECT
### Estimated Time: 10-15 phút (vs 45 phút full pipeline)
```

**Quy tắc chọn path**:

| Path | Khi nào | Số agent |
|---|---|---|
| `PATH_DIRECT` | 1 domain, trivial/simple | 1-2 |
| `PATH_LITE` | 2-3 domains, medium | 3-5 |
| `PATH_FULL` | 4+ domains, complex, new service, breaking change | 6+ |

**Lưu ý**: @dispatcher KHÔNG tự triển khai code. Sau khi phân tích, nó báo cáo cho @architect để điều phối theo path đã chọn.

---

### 5.1 @architect - Kiến trúc sư trưởng

**Vai trò**: Entry point duy nhất. Điều phối toàn bộ team, quyết định kiến trúc.

**Khi nào dùng**: Luôn luôn - mọi yêu cầu bắt đầu từ đây.

**Ví dụ câu lệnh**:

```
"Thiết kế kiến trúc cho module quản lý nhà thuốc (PharmacyService)"
"Đánh giá tác động khi thêm trường patient.prefered_language vào toàn hệ thống"
"Review kiến trúc hiện tại của BillingService, đề xuất cải tiến"
"Phê duyệt ADR-0015 về chiến lược caching cho PatientService"
```

**Cách delegate**:

```
@architect → @orchestrator (nếu là feature đa service)
@architect → @dotnet (nếu chỉ liên quan backend)
@architect → @angular (nếu chỉ liên quan frontend)
@architect → @dba (nếu chỉ liên quan database)
```

---

### 5.2 @plan - Lập kế hoạch

**Vai trò**: Phân tích yêu cầu, tạo kế hoạch chi tiết trước khi code.

**Khi nào dùng**: Feature mới, refactor lớn, thay đổi nhiều service.

**Ví dụ câu lệnh**:

```
"Lập kế hoạch triển khai tính năng đặt lịch hẹn online cho bệnh nhân"
"Phân tích những file nào bị ảnh hưởng khi thêm trường ICD-11 vào ClinicalService"
"Lên kế hoạch migrate từ PostgreSQL lên CockroachDB cho IdentityService"
```

**Output mong đợi**:

- Danh sách files cần tạo/sửa
- Cross-service dependencies
- Risk assessment
- Breaking changes (nếu có)
- Timeline ước tính

---

### 5.3 @dotnet - Backend .NET

**Vai trò**: Phát triển toàn bộ code C# backend.

**Khi nào dùng**: Bất kỳ thay đổi nào trong `src/Services/`, `src/Shared/`, `src/ApiGateway/`.

**Ví dụ câu lệnh**:

```
"Tạo PatientService với CRUD patient, allergy, condition"
"Thêm gRPC endpoint GetPatientAllergies trong PatientService"
"Implement Outbox Pattern cho ClinicalService để publish EncounterCreated event"
"Tạo FluentValidation cho CreateAppointmentCommand"
"Sửa lỗi race condition trong double-booking ở AppointmentService"
```

**File thường làm việc**:

```
src/Services/<Service>/Domain/       ← Entities, ValueObjects, Aggregates
src/Services/<Service>/Application/  ← Commands, Queries, Handlers, Validators
src/Services/<Service>/Infrastructure/ ← EF Core, Repositories, External services
src/Services/<Service>/Api/          ← Minimal API endpoints, gRPC services
src/Shared/SharedKernel/             ← Domain primitives
src/Shared/Protos/                   ← gRPC contract definitions
```

---

### 5.4 @angular - Frontend Angular

**Vai trò**: Phát triển toàn bộ code Angular frontend.

**Khi nào dùng**: Bất kỳ thay đổi nào trong `src/Frontend/his-hope-app/`.

**Ví dụ câu lệnh**:

```
"Tạo màn hình đăng ký bệnh nhân mới với Reactive Forms"
"Thêm sidebar navigation cho module Billing"
"Tạo component hiển thị danh sách dị ứng dạng badge"
"Implement NgRx store cho Appointment state management"
"Sửa lỗi responsive trên màn hình clinical notes ở tablet 960px"
```

**File thường làm việc**:

```
src/Frontend/his-hope-app/src/app/features/<feature>/
src/Frontend/his-hope-app/src/app/core/
src/Frontend/his-hope-app/src/app/shared/
src/Frontend/his-hope-app/src/styles/_theme.scss
src/Frontend/his-hope-app/src/styles/styles.scss
```

---

### 5.5 @dba - Database

**Vai trò**: Thiết kế schema, viết migration, tối ưu query.

**Khi nào dùng**: Bất kỳ thay đổi schema, migration, hoặc vấn đề hiệu năng DB.

**Ví dụ câu lệnh**:

```
"Tạo migration thêm bảng patient_allergies với FK tới patients"
"Thiết kế schema cho LabService - bảng lab_orders, lab_results"
"Tối ưu query tìm kiếm bệnh nhân theo tên - hiện tại chậm >500ms"
"Tạo composite index trên (encounter_date, patient_id) trong clinical_encounters"
"Kiểm tra backward-compatibility của migration 0024"
```

**File thường làm việc**:

```
cockroach/migrations/           ← SQL migration files
src/Services/*/Infrastructure/  ← EF Core DbContext, entity configurations
src/Shared/Infrastructure/     ← Outbox, interceptors
```

---

### 5.6 @devops - DevOps/SRE

**Vai trò**: Quản lý hạ tầng, K8s, CI/CD, Docker, monitoring.

**Khi nào dùng**: Thay đổi infrastructure, deployment, pipeline, monitoring.

**Ví dụ câu lệnh**:

```
"Thêm Kubernetes deployment cho LabService mới"
"Cấu hình HorizontalPodAutoscaler cho PatientService"
"Thêm ServiceMonitor để Prometheus scrape metrics từ BillingService"
"Cập nhật CI pipeline thêm bước SonarQube scan"
"Fix lỗi Linkerd mTLS giữa AppointmentService và PatientService"
```

**File thường làm việc**:

```
k8s/base/           ← Kubernetes manifests
k8s/overlays/       ← Environment-specific overlays
k8s/monitoring/     ← ServiceMonitors, dashboards
docker/             ← Dockerfiles, docker-compose
cicd/               ← Tekton pipelines, ArgoCD apps
bazel/              ← Bazel BUILD files
```

---

### 5.7 @docs - Tài liệu

**Vai trò**: Tạo và kiểm tra tài liệu: ADRs, API docs, READMEs, changelogs.

**Khi nào dùng**: Feature mới cần ADR, API mới cần document, hoặc audit doc.

**Ví dụ câu lệnh**:

```
"Tạo ADR cho quyết định dùng Redis cache cho PatientService"
"Viết API document cho gRPC LabService từ file lab.proto"
"Kiểm tra tất cả service READMEs đã đầy đủ các section bắt buộc chưa"
"Cập nhật CHANGELOG.md cho release v1.2.0"
"Tạo deployment runbook cho quy trình rollback dịch vụ"
```

**Phase 2 (Generate)**:

```
"Tạo ADR, API docs, README cho [feature]"
```

**Phase 4 (Verify)**:

```
"Kiểm tra doc coverage, link validity, ADR freshness"
```

---

### 5.8 @testing-backend - Kiểm thử Backend

**Vai trò**: Viết và chạy unit tests, integration tests, contract tests cho .NET.

**Khi nào dùng**: Sau khi @dotnet hoặc @dba hoàn thành code.

**Ví dụ câu lệnh**:

```
"Viết unit test cho PatientAggregate trong PatientService.Domain"
"Tạo integration test cho PatientRepository với Testcontainers"
"Viết contract test giữa AppointmentService và PatientService gRPC"
"Chạy toàn bộ tests trong PatientService và báo cáo coverage"
```

---

### 5.9 @testing-frontend - Kiểm thử Frontend

**Vai trò**: Viết và chạy unit tests, component tests, E2E tests cho Angular.

**Khi nào dùng**: Sau khi @angular hoàn thành code.

**Ví dụ câu lệnh**:

```
"Viết unit test cho PatientRegistrationComponent"
"Tạo Cypress E2E test cho luồng đăng ký bệnh nhân mới"
"Chạy axe accessibility audit trên tất cả màn hình clinical"
"Kiểm tra responsive layout trên mobile/tablet/desktop"
```

---

### 5.10 @qa - Đảm bảo chất lượng

**Vai trò**: Chiến lược test tổng thể, contract tests, E2E, load tests, chaos.

**Khi nào dùng**: Sau khi unit/integration tests pass, để verify end-to-end.

**Ví dụ câu lệnh**:

```
"Chạy contract test giữa tất cả service gRPC"
"Thực hiện load test k6 cho API đặt lịch hẹn - 1000 concurrent users"
"Thiết kế chaos experiment: kill pod PatientService khi đang xử lý request"
"Kiểm tra quality gates cho release v1.2.0"
```

---

### 5.11 @validate - Kiểm định

**Vai trò**: Build integrity, proto lint, secrets scan, FluentValidation, config audit.

**Khi nào dùng**: Trước khi commit, kiểm tra toàn bộ codebase.

**Ví dụ câu lệnh**:

```
"Validate toàn bộ solution: build + proto lint + secrets scan"
"Kiểm tra tất cả FluentValidation rules trong BillingService"
"Quét hardcoded secrets trong toàn bộ src/"
"Kiểm tra format tất cả appsettings.json files"
```

---

### 5.12 @check-ui - Kiểm tra UI/UX

**Vai trò**: Material theme, WCAG 2.1 AA, design system, anti-slop checklist.

**Khi nào dùng**: Mọi thay đổi frontend, trước khi merge.

**Ví dụ câu lệnh**:

```
"Kiểm tra WCAG accessibility cho màn hình đăng ký bệnh nhân"
"Audit design system compliance: không hardcoded colors, đúng border-radius"
"Review anti-slop: kiểm tra shadows, gradients, AI-purple colors"
"Kiểm tra responsive layout ở các breakpoints 600/960/1280/1920px"
```

---

### 5.13 @security - Bảo mật

**Vai trò**: Vault secrets, JWT auth, RBAC, Cilium network policies, HIPAA.

**Khi nào dùng**: Feature mới cần audit bảo mật, hoặc thay đổi auth.

**Ví dụ câu lệnh**:

```
"Audit bảo mật cho API mới của BillingService"
"Kiểm tra Cilium network policies cho LabService - đảm bảo default-deny"
"Review HIPAA compliance cho chức năng export dữ liệu bệnh nhân"
"Kiểm tra Vault policies cho PharmacyService"
```

---

### 5.14 @git - GitHub Integration

**Vai trò**: Stage, commit (Conventional Commits), push, branch, PR.

**Khi nào dùng**: CHỈ KHI tất cả quality gates đã xanh (có tín hiệu từ @orchestrator).

**Ví dụ câu lệnh**:

```
"Commit thay đổi PatientService với message feat(patient): add allergy cross-check"
"Tạo PR cho branch feature/patient/allergy-cross-check"
"Tạo release branch v1.2.0 từ main"
```

**KHÔNG BAO GIỜ** gọi @git trực tiếp nếu chưa có gate-pass từ @orchestrator.

---

### 5.15 @ml-ai - Machine Learning

**Vai trò**: ML model training, Vertex AI, feature store, predictive analytics.

**Ví dụ câu lệnh**:

```
"Train model dự đoán no-show cho AppointmentService"
"Tạo feature pipeline cho patient readmission risk"
"Deploy model lên Vertex AI endpoint"
```

---

### 5.16 @data-platform - Data Platform

**Vai trò**: BigQuery, Dataflow, Pub/Sub, dbt, analytics pipelines.

**Ví dụ câu lệnh**:

```
"Tạo Dataflow pipeline đồng bộ clinical data lên BigQuery"
"Thiết kế dbt models cho báo cáo thống kê bệnh nhân"
"Tạo Pub/Sub topic cho real-time analytics events"
```

---

## 6. Use Cases - Trường Hợp Sử Dụng Thực Tế

### 6.1 UC-01: Thêm tính năng kiểm tra dị ứng chéo khi kê đơn

**Mô tả**: Khi bác sĩ kê đơn thuốc, hệ thống tự động kiểm tra bệnh nhân có dị ứng với thuốc hoặc tương tác thuốc không.

**Scope**: PatientService (allergy data) + PharmacyService (drug data) + ClinicalService (prescription).

**Quy trình**:

```
Bước 1: User gửi yêu cầu → @architect
  "Thêm tính năng kiểm tra dị ứng chéo khi kê đơn thuốc"

Bước 2: @architect đánh giá → Đây là feature đa service → Delegate sang @orchestrator

Bước 3: @orchestrator chạy pipeline:

  Phase 0: Pre-Flight
    ✓ Git status clean
    ✓ Branch: feature/patient/allergy-cross-check

  Phase 1: @plan
    ✓ Files cần tạo/sửa: 12 files
    ✓ Cross-service: PatientService ↔ PharmacyService
    ✓ Breaking changes: None

  Phase 2: Implement (song song)
    ✓ @dba: Migration thêm drug_interactions table
    ✓ @dotnet: AllergyCrossCheckCommand + Handler trong ClinicalService
    ✓ @angular: AllergyWarningBanner component
    ✓ @docs: ADR-0015, cập nhật ClinicalService API docs

  Phase 3: Test (song song rồi tuần tự)
    ✓ @testing-backend: 15 unit tests + 3 integration tests
    ✓ @testing-frontend: 8 component tests
    ✓ @qa: 2 E2E tests cho luồng kê đơn

  Phase 4: Validate (song song)
    ✓ @validate: Build sạch, proto lint pass, không secrets
    ✓ @check-ui: WCAG AA pass, allergy banner đúng design system
    ✓ @docs: Doc coverage 100%, ADR được architect phê duyệt
    ✓ @security: Không PHI leak, audit log đầy đủ

  Phase 5: Commit
    → @git commit: "feat(clinical): add allergy cross-check on medication order"
```

---

### 6.2 UC-02: Sửa lỗi double-booking lịch hẹn

**Mô tả**: 2 bệnh nhân đặt cùng 1 slot khám trong cùng thời điểm.

**Scope**: AppointmentService (chỉ 1 service).

**Quy trình**:

```
Bước 1: User → @architect
  "Sửa lỗi double-booking trong AppointmentService"

Bước 2: @architect đánh giá → Fix nhỏ, 1 service → Delegate trực tiếp

Bước 3: Triển khai nhanh:
  @dotnet: Sửa BookingCommand - thêm distributed lock/optimistic concurrency
  @testing-backend: Viết test race condition
  @validate: Build check
  @git: commit "fix(appointment): resolve double-booking race condition"
```

**Thời gian ước tính**: 10-15 phút.

---

### 6.3 UC-03: Tạo service mới - LabService

**Mô tả**: Xây dựng microservice quản lý xét nghiệm từ đầu.

**Scope**: Service mới hoàn toàn - 4 projects (Domain, Application, Infrastructure, Api).

**Quy trình**:

```
Bước 1: User → @architect
  "Tạo LabService mới để quản lý xét nghiệm"

Bước 2: @architect → @plan
  "Lập kế hoạch cho service mới LabService"

Bước 3: @plan output → @architect phê duyệt → @orchestrator

Bước 4: @orchestrator pipeline:

  Phase 2 (song song):
    @dotnet: Tạo 4 projects + gRPC proto + Domain entities
    @dba: Thiết kế schema: lab_orders, lab_results, lab_panels
    @devops: K8s manifests + Dockerfile + Cilium policies
    @docs: README + API docs + ADR

  Phase 3:
    @testing-backend: Unit + integration tests
    @qa: Contract tests

  Phase 4 (song song):
    @validate: Build + proto lint + secrets
    @security: Vault policies + network policies + HIPAA
    @docs: Doc audit

  Phase 5:
    @git: commit "feat(lab): add LabService for lab order management"
```

**Thời gian ước tính**: 45-60 phút.

---

### 6.4 UC-04: Migration database - Thêm cột không backward-incompatible

**Mô tả**: Thêm trường `preferred_language` vào bảng `patients`.

**Scope**: PatientService + Frontend.

**Quy trình**:

```
Bước 1: User gửi yêu cầu → @dispatcher
  @dispatcher phân tích: domains=[backend, database, frontend], complexity=medium
  → Chọn PATH_LITE: @dba, @dotnet, @angular, @testing-backend, @check-ui, @git
  → Bỏ qua: @devops, @security, @qa, @orchestrator, @docs (không cần)

Bước 2: @architect thực thi PATH_LITE:
  @dba: Migration thêm cột DEFAULT 'vi', không cần backfill
  → @validate đồng kiểm tra backward-compatibility

Bước 3: Song song:
  @dotnet: Cập nhật Patient entity + UpdatePatientCommand
  @angular: Thêm dropdown ngôn ngữ trong form đăng ký

Bước 4: Test + Validate + Commit
  @testing-backend: Tests
  @check-ui: UI review
  @git: commit "feat(patient): add preferred_language field"
```

---

### 6.5 UC-05: Security Audit - Chuẩn bị HIPAA audit

**Mô tả**: Kiểm tra toàn bộ hệ thống trước đợt audit HIPAA.

**Quy trình**:

```
Bước 1: User → @architect
  "Chuẩn bị cho HIPAA audit - kiểm tra toàn bộ hệ thống"

Bước 2: @architect → Chạy tuần tự:

  @security: Audit Vault policies, RBAC, network policies, encryption
  @validate: Quét hardcoded secrets, kiểm tra audit logging
  @dba: Kiểm tra encryption-at-rest, backup policies
  @devops: Kiểm tra PodSecurityStandards, seccomp profiles
  @docs: Cập nhật HIPAA compliance document, tạo audit evidence

Bước 3: Tổng hợp báo cáo → @architect review → Commit
```

---

### 6.6 UC-06: UI Redesign - Làm mới giao diện Dashboard

**Mô tả**: Thiết kế lại trang Dashboard theo style minimalism.

**Quy trình**:

```
Bước 1: User → @architect
  "Redesign Dashboard theo minimalist-ui style"

Bước 2: @architect → @angular
  @angular: Implement layout mới với bento-grid

Bước 3: Validate UI:
  @check-ui: Kiểm tra anti-slop (không shadows, không gradients)
              WCAG AA accessibility
              Responsive ở tất cả breakpoints
              Đúng clinical green palette

Bước 4: Nếu có lỗi → @angular sửa → @check-ui kiểm tra lại

Bước 5: @testing-frontend: Unit tests + E2E
         @git: commit "style(frontend): redesign dashboard with bento-grid layout"
```

---

### 6.7 UC-07: Tạo tài liệu cho service mới

**Mô tả**: PharmacyService vừa code xong, cần document đầy đủ.

**Quy trình**:

```
Bước 1: User → @architect
  "Tạo document cho PharmacyService mới"

Bước 2: @architect → @docs

  @docs (Phase 2 - Generate):
    ✓ Tạo src/Services/PharmacyService/README.md
    ✓ Tạo docs/api/grpc/pharmacy.md (từ pharmacy.proto)
    ✓ Tạo docs/api/rest/pharmacy.md (từ Swagger)
    ✓ Tạo ADR nếu có architectural decision
    ✓ Cập nhật CHANGELOG.md

  @docs (Phase 4 - Verify):
    ✓ Doc coverage: 100%
    ✓ Link validity: all links resolve
    ✓ README completeness: all required sections present
    ✓ Markdown lint: zero errors
```

---

## 7. Quality Gates - Kiểm Soát Chất Lượng

### 7.1 Danh sách tất cả Gates

| Phase | Gate ID | Severity | Agent |
|---|---|---|---|
| **0** | `clean-working-tree` | block | orchestrator |
| **0** | `branch-convention` | block | orchestrator |
| **2** | `dotnet-build` | block | validate |
| **2** | `angular-build` | block | validate |
| **2** | `proto-lint` | block | validate |
| **2** | `proto-breaking` | block | validate |
| **2** | `no-secrets-hardcoded` | block | validate |
| **3** | `backend-unit-tests` | block | testing-backend |
| **3** | `backend-coverage` (≥80%) | block | testing-backend |
| **3** | `frontend-unit-tests` | block | testing-frontend |
| **3** | `contract-tests` | block | qa |
| **3** | `e2e-critical-path` | block | qa |
| **3** | `load-test-threshold` | warn | qa |
| **4** | `ui-anti-slop` | block | check-ui |
| **4** | `wcag-accessibility` | block | check-ui |
| **4** | `design-system` | block | check-ui |
| **4** | `migration-backward-compat` | block | validate |
| **4** | `vault-secrets` | block | security |
| **4** | `rbac-network-policies` | block | security |
| **4** | `hipaa-compliance` | block | security |
| **4** | `doc-coverage` | block | docs |
| **4** | `adr-freshness` | block | docs |
| **4** | `api-doc-accuracy` | block | docs |
| **4** | `link-validity` | block | docs |
| **4** | `readme-completeness` | block | docs |
| **4** | `changelog-updated` | block | docs |
| **4** | `markdown-lint` | block | docs |
| **4** | `proto-comments` | warn | docs |
| **5** | `commit-message-convention` | block | git |
| **5** | `no-secrets-in-commit` | block | git |
| **5** | `no-binaries-staged` | block | git |
| **5** | `no-merge-conflicts` | block | git |

**Tổng cộng**: 32 quality gates (30 block, 2 warn).

### 7.2 Conditional Gates

Một số gates chỉ chạy khi điều kiện cụ thể được đáp ứng:

| Điều kiện | Gates kích hoạt |
|---|---|
| `migrations-changed` | `migration-backward-compat` |
| `protos-changed` | `api-doc-accuracy`, `proto-comments` |
| `phi-data-changed` | `hipaa-compliance` |
| `inter-service-calls-changed` | `rbac-network-policies` |
| `architecture-changed` | `adr-freshness` |
| `new-service` | `readme-completeness` |
| `performance-sensitive-change` | `load-test-threshold` |

---

## 8. Best Practices

### 8.1 Quy tắc vàng

1. **Luôn bắt đầu từ @architect** — Không tự ý gọi agent khác
2. **Feature lớn → @orchestrator** — Đừng tự chạy pipeline thủ công
3. **Không skip gate** — Mỗi gate tồn tại vì lý do an toàn
4. **Document cùng lúc với code** — Đừng để "viết doc sau"
5. **Test trước khi validate** — Đúng thứ tự pipeline

### 8.2 Parallel vs Sequential

```
NÊN chạy song song:
  ✓ @dotnet, @angular, @dba, @devops, @docs (Phase 2)
  ✓ @testing-backend, @testing-frontend (Phase 3)
  ✓ @validate, @check-ui, @security, @docs (Phase 4)

PHẢI chạy tuần tự:
  ! Phase 2 xong → mới Phase 3
  ! Phase 3 xong → mới Phase 4
  ! TẤT CẢ gates xanh → mới Phase 5 (commit)
```

### 8.3 Commit Message Convention

```
<type>(<scope>): <mô tả ngắn gọn, tối đa 72 ký tự>

Types:
  feat     - Tính năng mới
  fix      - Sửa lỗi
  docs     - Tài liệu
  style    - Format code (không đổi logic)
  refactor - Tái cấu trúc code
  perf     - Cải thiện hiệu năng
  test     - Thêm/sửa tests
  chore    - Build, CI, dependencies
  ci       - CI/CD pipeline
  security - Bảo mật
  db       - Database migrations

Scopes:
  identity, patient, appointment, clinical, lab,
  billing, pharmacy, apigateway, frontend, shared,
  infra, k8s, cicd, vault, db, docs, security
```

### 8.4 Khi nào cần ADR?

Tạo ADR khi:
- Quyết định kiến trúc ảnh hưởng nhiều service
- Chọn công nghệ mới (VD: Redis vs Memcached)
- Thay đổi pattern (VD: chuyển từ REST sang gRPC)
- Quyết định trade-off quan trọng (VD: consistency vs availability)

### 8.5 Retry Policy

```
Gate fail:
  Lần 1: Tự động retry
  Lần 2: Tự động retry (với log chi tiết hơn)
  Lần 3: Tự động retry (lần cuối)
  Lần 4: ESCALATE → @architect

Ngoại lệ:
  Security issues: KHÔNG retry → escalate ngay
  Breaking changes: KHÔNG retry → escalate ngay
```

---

## 9. Troubleshooting

### 9.1 Pipeline bị kẹt ở Phase 2 (Build fail)

```
Triệu chứng: dotnet build hoặc npm run build fail

Cách xử lý:
1. @orchestrator báo lỗi build
2. Xem log lỗi → xác định file gây lỗi
3. @orchestrator gửi lại cho agent tương ứng (@dotnet/@angular)
4. Agent sửa → build lại → nếu pass → tiếp tục pipeline
5. Nếu fail 3 lần → escalate lên @architect
```

### 9.2 Test fail trong Phase 3

```
Triệu chứng: Một hoặc nhiều tests fail

Cách xử lý:
1. @orchestrator nhận báo cáo test failure
2. Phân loại:
   - Unit test fail → gửi lại @testing-backend/@testing-frontend
   - Integration test fail → kiểm tra Testcontainers config
   - Contract test fail → kiểm tra proto thay đổi
   - E2E fail → kiểm tra môi trường test
3. Agent fix → chạy lại tests
4. Nếu flaky test → retry 1 lần, nếu vẫn fail → escalate
```

### 9.3 Gate fail trong Phase 4 (Validate)

```
Triệu chứng: @validate, @check-ui, @security, hoặc @docs báo lỗi

Cách xử lý:
1. Đọc danh sách violations
2. Phân loại [MUST FIX] vs [SHOULD FIX] vs [NIT]
3. [MUST FIX]: Phải sửa ngay → gửi về agent triển khai
4. [SHOULD FIX]: Có thể defer với architect approval
5. [NIT]: Không block, tự động pass
6. Sau khi sửa → chạy lại validate gate
```

### 9.4 @git từ chối commit

```
Triệu chứng: @git báo "Have all quality gates passed?"

Nguyên nhân: @git không nhận được tín hiệu green-light từ @orchestrator

Cách xử lý:
1. Kiểm tra lại tất cả gates trong pipeline
2. Nếu có gate chưa pass → quay lại Phase tương ứng
3. Nếu tất cả đã pass → gửi xác nhận cho @git
4. @git sẽ kiểm tra lại và commit
```

### 9.5 Merge conflict khi commit

```
Triệu chứng: @git báo merge conflict

Cách xử lý:
1. @git báo cáo conflict files
2. Escalate lên @architect ngay (không tự resolve)
3. @architect đánh giá và quyết định:
   - Rebase branch lên main mới nhất
   - Manual resolve conflict
   - Hoặc coordinate với team
```

---

## 10. Cheatsheet - Tra Cứu Nhanh

### 10.1 Lệnh thường dùng theo ngữ cảnh

| Tôi muốn... | Gõ lệnh |
|---|---|
| **Phân tích yêu cầu & chọn agent tối ưu** | `"Phân tích/dispatch yêu cầu: [mô tả]"` |
| Triển khai feature mới đa service | `"Triển khai [mô tả feature]"` → @dispatcher → auto PATH_FULL qua orchestrator |
| Sửa bug 1 service | `"Sửa lỗi [mô tả] trong [ServiceName]"` → @dispatcher → PATH_DIRECT |
| Tạo API mới | `"Thêm gRPC/REST endpoint [mô tả] trong [ServiceName]"` |
| Thêm bảng DB | `"Tạo migration thêm bảng [tên bảng] trong [ServiceName]"` |
| Sửa giao diện | `"Sửa/Cập nhật [component] trong [module]"` |
| Viết test | `"Viết test cho [class/feature] trong [ServiceName]"` |
| Kiểm tra bảo mật | `"Audit bảo mật cho [ServiceName/feature]"` |
| Kiểm tra UI | `"Review UI cho [màn hình/component]"` |
| Tạo tài liệu | `"Tạo document cho [ServiceName/feature]"` |
| Commit code | `"Commit thay đổi với message [message]"` |
| Tạo PR | `"Tạo PR cho branch [branch-name]"` |
| Chuẩn bị release | `"Chuẩn bị release v[X.Y.Z]"` |

### 10.2 Agent matrix

| Lĩnh vực | Agent chính | Agent test | Agent validate |
|---|---|---|---|
| Backend .NET | @dotnet | @testing-backend | @validate |
| Frontend Angular | @angular | @testing-frontend | @check-ui |
| Database | @dba | @testing-backend | @validate |
| Infrastructure | @devops | @qa | @security |
| Bảo mật | @security | @qa | @security |
| Tài liệu | @docs | — | @docs |
| ML/AI | @ml-ai | @qa | @validate |
| Data Platform | @data-platform | @qa | @validate |

### 10.3 File paths nhanh

```
src/Services/<Service>/Domain/         ← Domain entities, aggregates
src/Services/<Service>/Application/    ← Commands, Queries, Handlers
src/Services/<Service>/Infrastructure/ ← EF Core, repositories
src/Services/<Service>/Api/            ← Endpoints, gRPC services
src/Frontend/his-hope-app/src/app/     ← Angular app
src/Shared/Protos/                     ← gRPC contracts
src/Shared/SharedKernel/               ← Domain primitives
src/ApiGateway/                        ← YARP gateway
cockroach/migrations/                  ← SQL migrations
k8s/base/                              ← K8s manifests
cicd/tekton/                           ← CI/CD pipelines
docs/                                  ← Documentation
vault/                                 ← Vault policies
```

### 10.4 Quy trình rút gọn cho task đơn giản

```
Fix bug 1 service:
  @architect → @dotnet → @testing-backend → @validate → @git

Thay đổi UI:
  @architect → @angular → @testing-frontend → @check-ui → @git

Migration DB:
  @architect → @dba → @validate (backward-compat) → @testing-backend → @git

Tài liệu:
  @architect → @docs (generate) → @docs (verify) → @git
```

---

## Phụ Lục

### A. Danh sách đầy đủ 32 Quality Gates

Xem chi tiết tại: [`cicd/quality-gates/gates.yaml`](../../cicd/quality-gates/gates.yaml)

### B. Danh sách 16 Agent Definitions

Xem chi tiết tại: [`.opencode/agents/`](../../.opencode/agents/)

### C. Kiến trúc tổng thể

Xem chi tiết tại: [`docs/architecture.md`](../architecture.md)

### D. Enterprise Roadmap

Xem chi tiết tại: [`docs/enterprise-roadmap.md`](../enterprise-roadmap.md)

---

> **Last updated**: 2026-07-16 | **Maintainer**: @architect | **Next review**: 2026-08-16
