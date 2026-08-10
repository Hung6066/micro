# Hướng dẫn Phát triển — His.Hope

> Hướng dẫn toàn diện cho developer làm việc trên hệ thống EMR microservices

---

## 1. Môi trường Phát triển Local

### 1.1 Chế độ Docker Compose (khuyến nghị)

Toàn bộ infrastructure chạy trong Docker, services và frontend chạy native local.

```powershell
# 1. Khởi động infrastructure
docker compose -f docker\docker-compose.yml up -d postgres redis rabbitmq consul jaeger elasticsearch kibana

# 2. Kiểm tra infrastructure đã sẵn sàng
docker compose -f docker\docker-compose.yml ps

# 3. Build toàn bộ solution
dotnet build His.Hope.sln

# 4. Chạy từng service (mỗi service một terminal)
dotnet run --project src\Services\IdentityService\IdentityService.Api
dotnet run --project src\Services\PatientService\PatientService.Api
dotnet run --project src\Services\AppointmentService\AppointmentService.Api
dotnet run --project src\Services\ClinicalService\ClinicalService.Api

# 5. Frontend (terminal riêng)
cd src\Frontend\his-hope-app
npm start
```

### 1.2 Các service ports

| Service | HTTP Port | gRPC Port | Swagger |
|---------|-----------|-----------|---------|
| API Gateway | 5000 | — | http://localhost:5000/swagger |
| Identity Service | 5001 | — | http://localhost:5001/swagger |
| Patient Service | 5002 | 5006 | http://localhost:5002/swagger |
| Appointment Service | 5003 | 5007 | http://localhost:5003/swagger |
| Clinical Service | 5004 | 5008 | http://localhost:5004/swagger |
| Lab Service | 5010 | — | http://localhost:5010/swagger |
| Billing Service | 5020 | — | http://localhost:5020/swagger |
| Pharmacy Service | 5030 | — | http://localhost:5030/swagger |
| Frontend | 4200 | — | http://localhost:4200 |

### 1.3 Infrastructure tools

| Tool | URL | Purpose |
|------|-----|---------|
| Jaeger UI | http://localhost:16686 | Distributed tracing |
| Kibana | http://localhost:5601 | Log visualization |
| RabbitMQ UI | http://localhost:15672 | Message queue management |
| Consul UI | http://localhost:8500 | Service discovery |
| Prometheus | http://localhost:9090 | Metrics |
| Grafana | http://localhost:3000 | Dashboards |

---

## 2. Chạy từng Service riêng lẻ

Mỗi service chạy độc lập. Thứ tự khởi động khuyến nghị:

```
Identity → Patient → Appointment → Clinical → Lab/Billing/Pharmacy → API Gateway → Frontend
```

```powershell
# Patient Service (ví dụ)
cd src\Services\PatientService\PatientService.Api
dotnet run

# Kestrel sẽ lắng nghe trên:
#   - HTTP1/2: 5002 (REST API)
#   - HTTP2:   5006 (gRPC)
```

Cấu hình connection string trong `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "PatientDb": "Host=localhost;Port=5433;Database=patientdb;Username=postgres;Password=postgres"
  },
  "EventBus": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "admin",
    "Password": "admin"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

---

## 3. Database Migrations

### 3.1 Cấu trúc migration

Migration file được đặt trong `cockroach/migrations/`, đánh số tuần tự:

```
cockroach/migrations/
├── 001-create-databases.sql     # Tạo database per service
├── 002-patient-service.sql      # Patient aggregate + allergies + conditions
├── 003-identity-service.sql     # Users, roles, permissions
├── 004-appointment-service.sql  # Appointments, time slots
├── 005-clinical-service.sql     # Encounters, SOAP notes, vitals
├── 006-lab-service.sql          # Lab orders, results
├── 007-billing-service.sql      # Invoices, payments
├── 008-pharmacy-service.sql     # Medications, prescriptions
├── 009-seed-data.sql            # Seed data (dev environment)
├── 010-database-roles.sql       # Database roles cho RLS
├── 011-row-level-security.sql   # RLS policies
├── 012-audit-triggers.sql       # PHI audit logging
└── 013-identity-extensions.sql  # Additional identity fields
```

### 3.2 Tạo migration mới

```sql
-- cockroach/migrations/014-my-feature.sql
-- Migration: Thêm bảng DeviceTokens cho push notifications
-- Created: 2026-07-16
-- Service: IdentityService

ALTER TABLE identitydb.Users ADD COLUMN IF NOT EXISTS PushNotificationEnabled BOOL NOT NULL DEFAULT false;

CREATE TABLE identitydb.DeviceTokens (
    TokenId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES identitydb.Users(UserId) ON DELETE CASCADE,
    Platform STRING(20) NOT NULL,       -- 'ios', 'android', 'web'
    Token STRING(500) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_devicetokens_user (UserId),
    UNIQUE INDEX idx_devicetokens_token (UserId, Platform)
);
```

### 3.3 Quy tắc migration

| Quy tắc | Mô tả |
|---------|-------|
| **Additive only** | Chỉ ALTER TABLE ADD COLUMN, CREATE TABLE, CREATE INDEX |
| **IF NOT EXISTS** | Mọi DDL đều có guard clause |
| **Không destructive** | Không DROP COLUMN, DROP TABLE, RENAME |
| **Backward compatible** | Migration cũ có thể chạy lại nhiều lần |
| **TIMESTAMPTZ** | Mọi timestamp dùng `TIMESTAMPTZ NOT NULL DEFAULT now()` |
| **UUID PK** | Mọi bảng dùng `PRIMARY KEY DEFAULT gen_random_uuid()` |

### 3.4 Áp dụng migration

```powershell
# Development (CockroachDB local hoặc PostgreSQL Docker)
docker exec -i his-hope-postgres psql -U postgres < cockroach\migrations\014-my-feature.sql

# Hoặc chạy toàn bộ migration theo thứ tự
Get-ChildItem cockroach\migrations\*.sql | Sort-Object Name | ForEach-Object {
    Write-Host "Running: $($_.Name)"
    docker exec -i his-hope-postgres psql -U postgres < $_.FullName
}

# Verify
docker exec -i his-hope-postgres psql -U postgres -d patientdb -c "\dt"
docker exec -i his-hope-postgres psql -U postgres -d patientdb -c "\d Patients"
```

---

## 4. Làm việc với gRPC

### 4.1 Cấu trúc Proto files

```
src/Shared/Protos/
├── patient.proto       # PatientGrpcService: GetPatient, SearchPatients, CheckPatientExists
├── appointment.proto   # AppointmentGrpcService: ...
├── clinical.proto      # ClinicalGrpcService: ...
├── billing.proto
├── lab.proto
└── pharmacy.proto
```

### 4.2 Tạo proto definition mới

```protobuf
// src/Shared/Protos/patient.proto
syntax = "proto3";

package his.hope.patient;

option csharp_namespace = "His.Hope.PatientGrpc";

import "google/protobuf/timestamp.proto";

service PatientGrpcService {
  rpc GetPatient (PatientRequest) returns (PatientResponse);
  rpc SearchPatients (PatientSearchRequest) returns (PatientListResponse);
  rpc CheckPatientExists (PatientExistsRequest) returns (PatientExistsResponse);
}

message PatientRequest {
  string id = 1;
}

message PatientResponse {
  string id = 1;
  string full_name = 2;
  string first_name = 3;
  string last_name = 4;
  string middle_name = 5;
  google.protobuf.Timestamp date_of_birth = 6;
  string gender_code = 7;
  string gender_name = 8;
  string phone = 9;
  string email = 10;
  bool is_active = 11;
  google.protobuf.Timestamp created_at = 12;
  google.protobuf.Timestamp updated_at = 13;
}
```

### 4.3 Implement gRPC Service trên server

```csharp
// src/Services/PatientService/PatientService.Api/GrpcServices/PatientGrpcServiceImpl.cs
using Grpc.Core;
using MediatR;
using His.Hope.PatientService.Application.UseCases.Patients.Queries;
using His.Hope.PatientGrpc;
using Google.Protobuf.WellKnownTypes;

namespace His.Hope.PatientService.Api.GrpcServices;

[Authorize]   // ← Bắt buộc: mọi gRPC method cần authorization
public class PatientGrpcServiceImpl : PatientGrpcService.PatientGrpcServiceBase
{
    private readonly IMediator _mediator;

    public PatientGrpcServiceImpl(IMediator mediator) => _mediator = mediator;

    public override async Task<PatientResponse> GetPatient(
        PatientRequest request, ServerCallContext context)
    {
        var patient = await _mediator.Send(
            new GetPatientByIdQuery(Guid.Parse(request.Id)),
            context.CancellationToken);

        if (patient is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Patient not found"));

        return new PatientResponse
        {
            Id = patient.Id.ToString(),
            FullName = patient.FullName,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = Timestamp.FromDateTimeOffset(patient.DateOfBirth),
            GenderCode = patient.GenderCode,
            Phone = patient.Phone,
            IsActive = patient.IsActive
        };
    }
}
```

### 4.4 Testing gRPC với grpcurl

```powershell
# Liệt kê services
grpcurl -plaintext localhost:5006 list

# Liệt kê methods
grpcurl -plaintext localhost:5006 describe his.hope.patient.PatientGrpcService

# Gọi GetPatient
grpcurl -plaintext -d '{"id":"550e8400-e29b-41d4-a716-446655440000"}' `
  localhost:5006 his.hope.patient.PatientGrpcService/GetPatient

# Search patients
grpcurl -plaintext -d '{"search_term":"John","page":1,"page_size":10}' `
  localhost:5006 his.hope.patient.PatientGrpcService/SearchPatients
```

### 4.5 gRPC UI (grpcui)

```powershell
# Cài đặt
go install github.com/fullstorydev/grpcui/cmd/grpcui@latest

# Launch interactive UI
grpcui -plaintext localhost:5006
# → Mở http://localhost:<port> trong browser
```

---

## 5. Chiến lược Testing

### 5.1 Phân loại test

```
tests/
├── Services/           # Service-level tests
│   ├── PatientService/
│   │   ├── PatientService.Domain.Tests/       # Unit tests - Domain logic
│   │   └── PatientService.Application.Tests/  # Unit tests - Application layer
│   ├── IdentityService/
│   ├── ClinicalService/
│   └── AppointmentService/
├── Contract/           # Inter-service contract tests
├── Frontend/           # Angular tests (Jasmine/Karma)
├── Load/               # Load tests (k6)
├── Validators/         # Validation edge case tests
└── Shared/             # Shared kernel tests
```

### 5.2 Unit Tests (.NET)

**Stack**: xUnit + FluentAssertions + Moq

```csharp
// tests/Services/PatientService/PatientService.Domain.Tests/PatientTests.cs
using FluentAssertions;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.ValueObjects;

public class PatientTests
{
    private static readonly PersonName DefaultName = new("John", "Doe", "M");
    private static readonly DateTime DefaultDob = new(1990, 1, 15);
    private static readonly Gender DefaultGender = Gender.Male;
    private static readonly ContactInfo DefaultContact = new("+1234567890", "john@example.com");
    private static readonly Address DefaultAddress = new("123 Main St", "Downtown", "Metropolis", "State", "12345", "USA");

    [Fact]
    public void Register_WithValidParameters_ShouldCreateActivePatient()
    {
        // Arrange (dùng factory method helper)
        // Act
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        patient.Should().NotBeNull();
        patient.Name.Should().Be(DefaultName);
        patient.IsActive.Should().BeTrue();
        patient.Allergies.Should().BeEmpty();
    }

    [Fact]
    public void Register_WithNullName_ShouldThrow()
    {
        // Act
        var act = () => Patient.Register(null!, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Register_WithFutureDateOfBirth_ShouldThrowDomainException()
    {
        // Arrange
        var futureDob = DateTime.Today.AddDays(1);

        // Act
        var act = () => Patient.Register(DefaultName, futureDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Patient age cannot be negative.");
    }
}
```

**Chạy unit tests**:

```powershell
dotnet test tests\Services\PatientService\PatientService.Domain.Tests
dotnet test tests\Services\PatientService\PatientService.Application.Tests
# Toàn bộ unit tests
dotnet test --filter "Category=Unit"
```

### 5.3 Integration Tests (Testcontainers)

```csharp
// tests/Services/PatientService/PatientService.Integration.Tests/PatientRepositoryTests.cs
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Builders;

public class PatientRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("patientdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private PatientDbContext _dbContext;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PatientDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new PatientDbContext(options, Substitute.For<IMediator>());
        await _dbContext.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistPatient()
    {
        // Arrange
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
        var repo = new PatientRepository(_dbContext);

        // Act
        await repo.AddAsync(patient, CancellationToken.None);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Assert
        var saved = await repo.GetByIdAsync(patient.Id, CancellationToken.None);
        saved.Should().NotBeNull();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

**Chạy integration tests**:

```powershell
dotnet test --filter "Category=Integration"
```

### 5.4 E2E Tests (Cypress/Playwright)

```typescript
// tests/Frontend/e2e/patient-flow.cy.ts
describe('Patient Management Flow', () => {
  before(() => {
    cy.loginAsDoctor();
  });

  it('should create a new patient', () => {
    cy.visit('/patients');
    cy.get('[data-cy=new-patient-btn]').click();
    cy.get('[data-cy=first-name]').type('Jane');
    cy.get('[data-cy=last-name]').type('Smith');
    cy.get('[data-cy=phone]').type('+1234567890');
    cy.get('[data-cy=save-btn]').click();

    cy.get('[data-cy=snackbar]').should('contain', 'Patient created successfully');
    cy.url().should('include', '/patients/');
  });

  it('should show validation errors for empty required fields', () => {
    cy.visit('/patients');
    cy.get('[data-cy=new-patient-btn]').click();
    cy.get('[data-cy=save-btn]').click();

    cy.get('[data-cy=error-first-name]').should('be.visible');
    cy.get('[data-cy=error-last-name]').should('be.visible');
  });
});
```

**Chạy E2E tests**:

```powershell
npx cypress run
npx cypress open  # GUI mode
```

### 5.5 Load Tests (k6)

```javascript
// tests/Load/patient-search.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '1m', target: 50 },  // Ramp up to 50 users
        { duration: '3m', target: 100 }, // Ramp up to 100 users
        { duration: '1m', target: 0 },   // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],  // 95% requests under 500ms
        http_req_failed: ['rate<0.01'],     // < 1% error rate
    },
};

export default function () {
    const res = http.get('http://localhost:5002/api/v1/patients/search?q=John&page=1&pageSize=20');
    check(res, {
        'status is 200': (r) => r.status === 200,
        'response time < 500ms': (r) => r.timings.duration < 500,
    });
    sleep(1);
}
```

**Chạy load tests**:

```powershell
k6 run tests\Load\patient-search.js
```

### 5.6 Chaos Tests (Chaos Mesh)

```yaml
# k8s/chaos/pod-kill.yaml
apiVersion: chaos-mesh.org/v1alpha1
kind: PodChaos
metadata:
  name: patient-service-kill
spec:
  action: pod-kill
  mode: one
  selector:
    namespaces:
      - his-hope
    labelSelectors:
      app.kubernetes.io/name: patient-service
  duration: "60s"
  scheduler:
    cron: "@every 30m"
```

**Áp dụng**:

```powershell
kubectl apply -f k8s\chaos\pod-kill.yaml
kubectl get podchaos -n his-hope
```

---

## 6. Debugging

### 6.1 .NET Debugging

| IDE | Phương pháp |
|-----|-------------|
| **Rider** | Attach to process: `Run → Attach to Process → dotnet.exe` |
| **Visual Studio** | `Debug → Attach to Process` hoặc set `PatientService.Api` làm startup project |
| **VS Code** | `launch.json` với `"type": "coreclr"` |

```json
// .vscode/launch.json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "PatientService.Api",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/Services/PatientService/PatientService.Api/bin/Debug/net8.0/PatientService.Api.dll",
            "cwd": "${workspaceFolder}/src/Services/PatientService/PatientService.Api",
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development",
                "ConnectionStrings__PatientDb": "Host=localhost;Port=5433;Database=patientdb;Username=postgres;Password=postgres"
            }
        }
    ]
}
```

### 6.2 Angular Debugging

| Công cụ | Cách sử dụng |
|---------|-------------|
| **Browser DevTools** | `F12` → Sources tab → đặt breakpoint trong TypeScript qua source maps |
| **Redux DevTools** | Extension browser, inspect NgRx store/actions/state changes |
| **Angular DevTools** | Extension browser, component tree + profiling |
| **Debugger statement** | Thêm `debugger;` trong code |

### 6.3 gRPC Debugging

```powershell
# Test gRPC call với verbose output
grpcurl -v -plaintext -d '{"id":"550e8400-e29b-41d4-a716-446655440000"}' `
  localhost:5006 his.hope.patient.PatientGrpcService/GetPatient

# Reflection (nếu bật)
grpcurl -plaintext localhost:5006 list

# Web UI cho gRPC testing
grpcui -plaintext localhost:5006
```

### 6.4 Tracing với Jaeger

1. Mở Jaeger UI: http://localhost:16686
2. Chọn service: `patient-service`
3. Tìm trace theo operation hoặc correlation ID
4. Drill-down vào từng span để xem timing, tags, logs

```csharp
// Correlation ID tự động được OpenTelemetry inject vào mọi span
// Tìm trong log:
_logger.LogInformation("Processing patient {PatientId}, CorrelationId: {CorrelationId}",
    patientId, Activity.Current?.TraceId);
```

### 6.5 Logs với Kibana

1. Mở Kibana: http://localhost:5601
2. Index pattern: `logstash-*`
3. Filter: `service: "patient-service" AND level: "Error"`
4. Timeline view để xem tần suất lỗi theo thời gian

### 6.6 Network issues với Hubble

```powershell
# Hubble UI
kubectl port-forward -n kube-system svc/hubble-ui 8081:80
# → http://localhost:8081

# Theo dõi traffic của patient-service
hubble observe -n his-hope --from-pod app=patient-service

# Xem dropped packets (network policy violation)
hubble observe -n his-hope --verdict DROPPED
```

---

## 7. Thêm Microservice Mới

### Checklist đầy đủ

Khi thêm một microservice mới (ví dụ: `NotificationService`), cần tạo các thành phần sau:

| # | Thành phần | Đường dẫn mẫu |
|---|-----------|---------------|
| 1 | **Solution folder + 4-layer projects** | `src/Services/NotificationService/NotificationService.{Domain,Application,Infrastructure,Api}/` |
| 2 | **Proto definition** | `src/Shared/Protos/notification.proto` |
| 3 | **Database migration** | `cockroach/migrations/014-notification-service.sql` |
| 4 | **Dockerfile** | `src/Services/NotificationService/NotificationService.Api/Dockerfile` |
| 5 | **K8s manifests** | `k8s/base/notification-service.yaml` |
| 6 | **CI/CD pipeline** | Cập nhật `cicd/tekton/` hoặc `.github/workflows/` |
| 7 | **Vault policy** | `vault/policies/notification-service.hcl` |
| 8 | **Network policies** | Thêm vào `k8s/base/network-policies.yaml` |
| 9 | **Backstage catalog** | `backstage/catalog/notification-service.yaml` |
| 10 | **Docker Compose service** | Thêm service vào `docker/docker-compose.yml` |

### 7.1 Cấu trúc 4-layer project

```
src/Services/NotificationService/
├── NotificationService.Domain/
│   ├── Aggregates/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   └── Repositories/INotificationRepository.cs
├── NotificationService.Application/
│   ├── UseCases/Notifications/Commands/
│   ├── UseCases/Notifications/Queries/
│   ├── DTOs/
│   ├── Common/Behaviours/
│   └── DependencyInjection.cs
├── NotificationService.Infrastructure/
│   ├── Persistence/
│   │   ├── NotificationDbContext.cs
│   │   ├── Configurations/
│   │   └── Repositories/
│   └── DependencyInjection.cs
└── NotificationService.Api/
    ├── Program.cs
    ├── GrpcServices/
    ├── Middleware/
    └── Dockerfile
```

### 7.2 Program.cs template

```csharp
// src/Services/NotificationService/NotificationService.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration);

builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddHisHopeAuthorization();

builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration, "notification-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

builder.Services.AddResiliencePolicies();
builder.Services.AddOutbox<NotificationDbContext>();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});

builder.Services.AddRabbitMQEventBus(options =>
{
    options.HostName = builder.Configuration.GetValue("EventBus:HostName", "localhost")!;
    options.Port = builder.Configuration.GetValue("EventBus:Port", 5672);
    options.UserName = builder.Configuration.GetValue("EventBus:UserName", "admin")!;
    options.Password = builder.Configuration.GetValue("EventBus:Password", "admin")!;
    options.ExchangeName = "his_hope_notification";
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>(name: "notification-db", tags: ["database"])
    .AddRabbitMQCheck(...)
    .AddRedisCheck(...);

var app = builder.Build();

app.UseSecurityHeaders();
app.UseRateLimiting();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
var notifications = app.MapGroup("/api/v1/notifications").RequireAuthorization();

notifications.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
{
    // ...
}).RequireAuthorization("Permission:notifications.view");

notifications.MapPost("/", async (...) =>
{
    // ...
}).RequireAuthorization("Permission:notifications.create");

// gRPC
app.MapGrpcService<NotificationGrpcServiceImpl>();
app.MapGrpcHealthChecksService();

app.MapHealthChecks("/health", ...).AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
```

### 7.3 Dockerfile template

```dockerfile
# src/Services/NotificationService/NotificationService.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/NotificationService/NotificationService.Api/", "Services/NotificationService/NotificationService.Api/"]
COPY ["src/Services/NotificationService/NotificationService.Application/", "Services/NotificationService/NotificationService.Application/"]
COPY ["src/Services/NotificationService/NotificationService.Domain/", "Services/NotificationService/NotificationService.Domain/"]
COPY ["src/Services/NotificationService/NotificationService.Infrastructure/", "Services/NotificationService/NotificationService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/NotificationService/NotificationService.Api/NotificationService.Api.csproj"
RUN dotnet publish "Services/NotificationService/NotificationService.Api/NotificationService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
USER app
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
```

### 7.4 Vault policy template

```hcl
# vault/policies/notification-service.hcl
path "secret/data/his-hope/notification-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/notificationdb" {
  capabilities = ["read"]
}

path "secret/data/his-hope/rabbitmq" {
  capabilities = ["read"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
```

### 7.5 Backstage catalog

```yaml
# backstage/catalog/notification-service.yaml
apiVersion: backstage.io/v1alpha1
kind: Component
metadata:
  name: notification-service
  description: Push notification và email service cho His.Hope
  tags:
    - dotnet
    - grpc
    - healthcare
  annotations:
    backstage.io/techdocs-ref: dir:.
spec:
  type: service
  lifecycle: production
  owner: platform-team
  system: his-hope
  dependsOn:
    - component:rabbitmq
    - component:patient-service
  providesApis:
    - notification-grpc
```
