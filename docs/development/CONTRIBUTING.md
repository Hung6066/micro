# Hướng dẫn Đóng góp — His.Hope

> Quy trình đóng góp cho hệ thống Quản lý Bệnh viện Điện tử (EMR/EHR)

---

## 1. Quy tắc ứng xử (Code of Conduct)

- Tôn trọng mọi contributor, reviewer, maintainer.
- Feedback tập trung vào code, không cá nhân.
- Với dự án y tế, bảo mật thông tin bệnh nhân là ưu tiên số một. Tuyệt đối không commit dữ liệu thực, PII, PHI.
- Nếu phát hiện lỗ hổng bảo mật, gửi email riêng cho security team — **không mở public issue**.

---

## 2. Bắt đầu (Getting Started)

### Yêu cầu

| Công cụ | Phiên bản | Ghi chú |
|---------|-----------|---------|
| .NET SDK | 8.0.x | Backend runtime |
| Node.js | 20.x LTS | Angular build |
| Docker Desktop | 27+ | Infrastructure containerized |
| PowerShell 7 | 7.4+ | Dev scripts (`Start-EMR.ps1`) |

### Clone & Build

```powershell
git clone <repo-url> his-hope
cd his-hope

# Infrastructure (PostgreSQL 16, Redis 7, RabbitMQ 3, Elasticsearch 8, Consul, Jaeger)
docker compose -f docker\docker-compose.yml up -d postgres redis rabbitmq consul jaeger

# Backend services
dotnet build His.Hope.sln

# Frontend
cd src\Frontend\his-hope-app
npm ci
npm start
```

### Startup nhanh bằng script

```powershell
# Chế độ local (infrastructure Docker, services chạy native)
.\Start-EMR.ps1 -Mode local

# Chế độ Docker toàn bộ
.\Start-EMR.ps1 -Mode docker

# Chỉ infrastructure
.\Start-EMR.ps1 -Mode infra
```

---

## 3. Chiến lược Branch

| Branch | Mục đích | Deploy |
|--------|----------|--------|
| `main` | Production-ready code | Production |
| `develop` | Integration branch | Staging |
| `feature/*` | Tính năng mới | Không |
| `bugfix/*` | Sửa lỗi không khẩn cấp | Không |
| `hotfix/*` | Sửa lỗi production khẩn cấp | Production |
| `release/*` | Chuẩn bị release | — |

```
main ─────●────────────●────────────● (production)
           \          / \          /
develop ────●───●───●───●───●────● (integration)
             \   \         \    \
feature/a ────●───●        \    hotfix/x ───●
                             \
feature/b ─────────────────────●───●
```

### Quy tắc

- **feature/*** và **bugfix/*** branch từ `develop`, merge về `develop`.
- **hotfix/*** branch từ `main`, merge về `main` + cherry-pick về `develop`.
- Không push trực tiếp lên `main` hoặc `develop`.
- Branch name phải có issue/ticket ID: `feature/HH-123-add-patient-search`.

---

## 4. Quy ước Commit Message

Tuân thủ [Conventional Commits 1.0](https://www.conventionalcommits.org/).

```
<type>(<scope>): <mô tả ngắn gọn>

[optional body]

[optional footer]
```

### Types bắt buộc

| Type | Dùng khi |
|------|----------|
| `feat` | Tính năng mới |
| `fix` | Sửa bug |
| `docs` | Thay đổi documentation |
| `security` | Sửa lỗ hổng, thay đổi chính sách bảo mật |
| `test` | Thêm/sửa test |
| `refactor` | Refactor code không đổi behavior |
| `perf` | Cải thiện hiệu năng |
| `chore` | Build, CI/CD, tooling |
| `style` | Formatting, whitespace |

### Ví dụ

```bash
feat(patient): add search endpoint with pagination and caching
fix(clinical): resolve SOAP note concurrency conflict on save
security(identity): enforce password complexity policy
test(patient): add integration tests for patient create flow
chore(cicd): add Bazel build caching for pull requests
```

---

## 5. Yêu cầu Pull Request

### Template PR

```markdown
## Mô tả
<!-- Mô tả ngắn gọn thay đổi -->

## Loại thay đổi
- [ ] feat (tính năng mới)
- [ ] fix (sửa lỗi)
- [ ] security (bảo mật)
- [ ] refactor
- [ ] chore

## Checklist trước khi review

### Code Quality
- [ ] Clean Architecture layers được tôn trọng
- [ ] CQRS pattern đúng (Command → Result<T>, Query → T)
- [ ] Không có business logic trong controller/endpoint
- [ ] Validation dùng FluentValidation
- [ ] Permission check trên mọi endpoint (`[HasPermission]` / `RequireAuthorization`)
- [ ] gRPC service có `[Authorize]` attribute
- [ ] External calls có circuit breaker (Polly)

### Database
- [ ] Migration chỉ ADDITIVE (không destructive changes)
- [ ] Tất cả bảng có UUID primary key
- [ ] TIMESTAMPTZ cho timestamp
- [ ] Index trên FK và query columns
- [ ] Row-Level Security (RLS) qua views
- [ ] Data query qua views (không direct table access)

### Security
- [ ] Không hardcoded secret
- [ ] Secrets từ Vault, không trong code/config
- [ ] Input validation server-side (không trust client)

### Testing
- [ ] Unit test: happy path + error cases + edge cases
- [ ] Code coverage không giảm so với baseline
- [ ] Integration test với Testcontainers (nếu thay đổi DB query)

### Observability
- [ ] Logging có correlation ID
- [ ] Traces exported qua OpenTelemetry

### CI/CD
- [ ] Tất cả tests pass
- [ ] Security scan clean (không secrets, không vulnerable packages)

## Link issue
Closes #<issue-id>
```

### Yêu cầu cứng

| Yêu cầu | Bắt buộc |
|----------|----------|
| Tất cả tests pass | ✅ |
| Code coverage không giảm | ✅ |
| Security scan sạch | ✅ |
| Migration backward compatible | ✅ |
| 2 reviewers approve | ✅ |
| — 1 domain expert | ✅ |
| — 1 security reviewer | ✅ |

---

## 6. Code Review Checklist

### Architecture

- [ ] **Clean Architecture**: Domain không phụ thuộc vào layer nào. Application chỉ phụ thuộc vào Domain. Infrastructure phụ thuộc Domain + Application.
- [ ] **CQRS**: Command trả về `Result<T>`, Query trả về `T`. Không mixed concerns.
- [ ] **MediatR**: Tất cả use case thông qua `IRequest<T>` và `IRequestHandler<T, U>`.
- [ ] **Không business logic trong controller**: Endpoint chỉ gọi MediatR, không có logic nghiệp vụ.

### Validation & Error Handling

- [ ] **FluentValidation**: Mọi input DTO/command đều có validator kế thừa `AbstractValidator<T>`.
- [ ] **Result pattern** hoặc **Exception middleware**: Lỗi domain throw `DomainException`, validation throw `ValidationException` — middleware `ExceptionHandlingMiddleware` bắt và transform.

### Security

- [ ] **Permission check**: Mọi REST endpoint có `.RequireAuthorization("Permission:...")`. Mọi gRPC method có `[Authorize]`.
- [ ] **Input validation**: Server-side luôn. Không trust client-side validation.
- [ ] **Secrets**: Từ Vault. Không hardcode connection string, API key.
- [ ] **Circuit breaker**: Gọi external service phải qua Polly circuit breaker.

### Database

- [ ] **Views cho RLS**: Client query qua views, không trực tiếp vào bảng.
- [ ] **Migration additive**: Chỉ thêm column/table/index, không xóa/rename.
- [ ] **UUID PK**: Mọi bảng dùng UUID (không serial/identity).

### Observability

- [ ] **Correlation ID**: Mỗi request có correlation ID trong log và trace.
- [ ] **Structured logging**: Dùng Serilog, không `Console.WriteLine`.

### Testing

- [ ] **Happy path** có test.
- [ ] **Error cases** có test (validation fail, not found, authorization fail).
- [ ] **Edge cases** có test (null, empty, boundary values).
