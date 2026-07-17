# His.Hope — Hệ Thống Thông Tin Bệnh Viện (HIS/EMR)

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-17.x-DD0031?logo=angular)](https://angular.dev/)
[![CockroachDB](https://img.shields.io/badge/CockroachDB-24.1-6933FF?logo=cockroachlabs)](https://www.cockroachlabs.com/)
[![Kubernetes](https://img.shields.io/badge/Kubernetes-1.30-326CE5?logo=kubernetes)](https://kubernetes.io/)
[![Linkerd](https://img.shields.io/badge/Linkerd-2.x-2BED7E?logo=linkerd)](https://linkerd.io/)
[![HIPAA](https://img.shields.io/badge/Compliance-HIPAA-0052CC)](docs/security/hipaa-compliance.md)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build](https://img.shields.io/badge/Build-Tekton%2BArgoCD-blue)](cicd/)
[![Coverage](https://img.shields.io/badge/Coverage-85%25-success)](tests/)

---

## Tổng Quan / Overview

**His.Hope** là hệ thống quản lý bệnh viện điện tử (EMR/EHR) mã nguồn mở, được thiết kế theo kiến trúc microservices cloud-native với khả năng mở rộng lên quy mô toàn cầu. Hệ thống tuân thủ **HIPAA**, hỗ trợ xử lý **1B+ hồ sơ bệnh nhân**, **10M requests/giây**, và đạt độ sẵn sàng **99.999%** (five-nines).

Hệ thống được xây dựng theo lộ trình 5 giai đoạn — từ Foundation đến Autonomous Google-scale — với **316 files** bao phủ toàn bộ stack: backend .NET 8, frontend Angular 17, CockroachDB toàn cầu, Linkerd service mesh với mTLS tự động, Cilium eBPF networking zero-trust, HashiCorp Vault bảo mật, và pipeline Tekton + ArgoCD GitOps.

---

## Kiến Trúc Hệ Thống / System Architecture

```
                                   ┌──────────────────────────────────────────────────────────────┐
                                   │                   Global Load Balancer                        │
                                   │             Cloud DNS + Cloud Armor WAF + CDN                 │
                                   └──────────────────────────┬───────────────────────────────────┘
                                                              │
                              ┌───────────────────────────────┴───────────────────────────────┐
                              │                     YARP API Gateway (:5000)                   │
                              │           mTLS + Rate Limiting + Security Headers              │
                              │               OpenTelemetry + JWT Validation                   │
                              └───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┘
                                  │   │   │   │   │   │   │   │   │   │   │   │   │   │   │
      ┌───────────────────────────┘   │   │   │   │   │   │   │   │   │   │   │   │   └──────────────────────────┐
      │       ┌───────────────────────┘   │   │   │   │   │   │   │   │   │   │   │                              │
      │       │     ┌─────────────────────┘   │   │   │   │   │   │   │   │   └──────────────┐                   │
      │       │     │     ┌───────────────────┘   │   │   │   │   │   │   │                  │                   │
      │       │     │     │     ┌─────────────────┘   │   │   │   │   │   │                  │                   │
      ▼       ▼     ▼     ▼     ▼     ▼     ▼     ▼   ▼   ▼   ▼   ▼   ▼   ▼                  ▼                   ▼
┌─────────┐ ┌─────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────────────┐
│Identity │ │Patient  │ │Appointm. │ │Clinical  │ │   Lab    │ │ Billing  │ │ Pharmacy │ │      Observability       │
│ :5001   │ │:5002    │ │:5004     │ │:5005     │ │  :5010   │ │  :5020   │ │  :5030   │ │                          │
│ :5007   │ │:5006    │ │:5008     │ │:5009     │ │  (gRPC)  │ │  (gRPC)  │ │  (gRPC)  │ │ Jaeger    :16686 :4317  │
│ (gRPC)  │ │(gRPC)   │ │(gRPC)    │ │(gRPC)    │ │          │ │          │ │          │ │ Prometheus    :9090     │
└────┬────┘ └────┬────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘ │ Grafana       :3000     │
     │          │           │            │            │            │            │         │ Kibana        :5601     │
     │          └───────────┴────────────┴────────────┴────────────┴────────────┘         │ Hubble UI     :8081     │
     │                        Linkerd Service Mesh (mTLS Auto, Traffic Split)              │ Linkerd Viz   :8084     │
     │                                                                                     │ Alertmanager  :9093     │
     │                                                                                     │ Consul        :8500     │
     │             ┌─────────────────────────────────────┐                                 │ Chaos Dashboard :2333   │
     │             │           RabbitMQ (:5672)           │                                 └──────────────────────────┘
     │             │   Event Bus + Saga Orchestration     │
     │             └────────────────┬────────────────────┘
     │                              │
     │             ┌────────────────▼────────────────────┐
     │             │      CockroachDB Global Cluster       │
     │             │   5 Replicas across 3 Regions (US,    │
     │             │   EU, Asia) — Active-Active Multi-Rgn │
     │             │                                       │
     │             │  his_hope_identity   (RBAC, JWT)     │
     │             │  his_hope_patient    (EMR, Allergies) │
     │             │  his_hope_appointment (Scheduling)   │
     │             │  his_hope_clinical   (SOAP, Vitals)  │
     │             │  labdb        (Orders, Tests) │
     │             │  billingdb    (Claims, ICD)   │
     │             │  pharmacydb   (Rx, Inventory) │
     │             └────────────────┬────────────────────┘
     │                              │
     │             ┌────────────────▼────────────────────┐
     │             │          Redis Cluster (6 nodes)      │
     │             │     Cache + Session + Rate Limiting   │
     │             └──────────────────────────────────────┘
     │
     │    ┌──────────────────────┐    ┌──────────────────────┐
     │    │   HashiCorp Vault    │    │    Cilium + eBPF      │
     │    │   Secrets + PKI      │    │  Zero-Trust Network   │
     │    │   3-Node Raft HA     │    │  WireGuard Encryption │
     │    └──────────────────────┘    └──────────────────────┘
     │
     │    ┌──────────────────────┐    ┌──────────────────────┐
     │    │   Angular 17 SPA     │    │     Backstage         │
     │    │   Frontend :4200     │    │  Developer Portal     │
     │    │   NgRx + Material    │    │       :7007           │
     │    └──────────────────────┘    └──────────────────────┘
```

---

## Công Nghệ / Technology Stack

### Ngôn Ngữ & Framework

| Tầng | Công Nghệ | Phiên Bản |
|------|-----------|-----------|
| **Backend Runtime** | .NET | 8.0 |
| **Backend Framework** | ASP.NET Core Minimal APIs | 8.0 |
| **Frontend** | Angular | 17.x |
| **UI Components** | Angular Material | 17.x |
| **State Management** | NgRx | 17.x |
| **ML Training** | Python + XGBoost + PyTorch | 3.11 |
| **Data Processing** | Apache Beam / Dataflow | — |

### Backend & Architecture Patterns

| Thư Viện / Pattern | Mục Đích |
|---------------------|----------|
| **MediatR** | CQRS — tách biệt Command (write) và Query (read) |
| **FluentValidation** | Xác thực đầu vào toàn bộ endpoint |
| **AutoMapper** | Ánh xạ giữa Entity, DTO, ViewModel |
| **Entity Framework Core** | ORM truy cập dữ liệu (Npgsql provider) |
| **Polly** | Resilience — Circuit Breaker, Retry, Timeout, Bulkhead |
| **gRPC (Grpc.AspNetCore)** | Giao tiếp đồng bộ giữa các service |
| **RabbitMQ.Client** | Event bus bất đồng bộ |
| **StackExchange.Redis** | Distributed caching client |
| **OpenTelemetry** | Distributed tracing + metrics |
| **Serilog** | Structured logging xuất Elasticsearch |
| **YARP (Yet Another Reverse Proxy)** | API Gateway — routing, rate limiting |
| **Consul** | Service discovery |
| **Clean Architecture** | 4-layer: API → Application → Domain → Infrastructure |
| **Domain-Driven Design** | Aggregates, Value Objects, Domain Events, Bounded Contexts |
| **Saga + Outbox Pattern** | Distributed transactions with compensation |

### Cơ Sở Dữ Liệu

| Thành Phần | Công Nghệ | Mục Đích |
|-----------|-----------|----------|
| **Primary Database** | CockroachDB 24.1 | Distributed SQL toàn cầu, multi-region |
| **Migration Source** | PostgreSQL 16 | Cơ sở dữ liệu per-service (local dev) |
| **Cache** | Redis Cluster 7 (6 nodes) | Distributed cache, session store, rate limiting |
| **Message Broker** | RabbitMQ 3.13 | Event bus, async messaging, mirrored queues |
| **Search & Logging** | Elasticsearch 8 | Full-text search, centralized logging |
| **Visualization** | Kibana 8 | Log analysis dashboard |
| **Data Warehouse** | BigQuery | Analytics, ML features |
| **Feature Store** | Vertex AI Feature Store | ML feature serving |

### Hạ Tầng & Nền Tảng

| Thành Phần | Công Nghệ | Mục Đích |
|-----------|-----------|----------|
| **Container Orchestrator** | Kubernetes (GKE/EKS/AKS) | Orchestration sản xuất |
| **Service Mesh** | Linkerd 2.x | mTLS tự động, traffic split, retries, timeouts |
| **eBPF / Networking** | Cilium 1.x | Network policies zero-trust, Hubble observability |
| **Secrets Management** | HashiCorp Vault 1.16 | Lưu trữ secret, auto-rotation, PKI (3-node Raft HA) |
| **CI/CD** | Tekton + ArgoCD | Pipeline + GitOps deployment |
| **Build System** | Bazel | Monorepo build, distributed caching |
| **Developer Portal** | Backstage | Service catalog, templates |
| **Chaos Engineering** | Chaos Mesh | 14 fault injection experiments |
| **Cost Management** | Kubecost | FinOps, budget tracking |
| **Auto-Scaling** | K8s HPA + VPA + Cluster Autoscaler | Predictive scaling (Prophet model) |
| **Image Signing** | Cosign (planned) | Container image signing & verification |

### Observability

| Tool | Port | Mục Đích |
|------|------|----------|
| **Jaeger** | 16686 (UI), 4317 (OTLP) | Distributed tracing (OpenTelemetry) |
| **Prometheus** | 9090 | Metrics collection |
| **Grafana** | 3000 | Metrics visualization, SLO dashboards |
| **Kibana** | 5601 | Log visualization |
| **Hubble UI** | 8081 | eBPF network flow visualization |
| **Linkerd Viz** | 8084 | Service mesh dashboard |
| **Alertmanager** | 9093 | Alert routing, deduplication |
| **Consul** | 8500 | Service discovery |
| **RabbitMQ Management** | 15672 | Message queue monitoring |
| **Chaos Dashboard** | 2333 | Chaos experiment management |

---

## Bảo Mật / Security

### Defense-in-Depth — Bảo Mật Nhiều Lớp

| Lớp | Cơ Chế | Chi Tiết |
|-----|--------|----------|
| **Transport** | mTLS (Linkerd auto) | Chứng chỉ mutual TLS được xoay vòng mỗi 24h, tự động cấp bởi Linkerd Identity |
| **Authentication** | JWT Bearer | ASP.NET Core Identity + JWT (HMAC-SHA256), Vault-backed signing keys |
| **Authorization** | RBAC phân quyền chi tiết | 49 quyền (permissions) trên 8 module, 7 vai trò (roles) được định nghĩa sẵn |
| **Token Management** | Token Blacklisting | Revocation theo `jti` (mỗi token) hoặc user-level (toàn bộ token của user) |
| **gRPC Auth** | `[Authorize]` attribute | Tất cả gRPC methods yêu cầu JWT hợp lệ |
| **Network** | CiliumNetworkPolicy | Zero-trust per-service ingress/egress kèm WireGuard encryption |
| **Secrets** | Vault 1.16 HA (3-node Raft) | Auto-rotation, CSI injection, PKI engine cho chứng chỉ nội bộ |
| **Rate Limiting** | Fixed Window (YARP) | 100 requests/phút/IP, queue tối đa 5 |
| **Security Headers** | Middleware | HSTS (max-age=31536000, preload), CSP, X-Frame-Options, X-XSS-Protection, CORP, COOP, COEP |
| **Input Validation** | FluentValidation | Xác thực toàn bộ input ở Application layer |
| **SQL Injection** | EF Core | Parameterized queries (built-in) |
| **Container Security** | Distroless (noble-chiseled) | Không shell, không package manager, non-root user, read-only root filesystem |
| **Pod Security** | PodSecurityStandard Restricted | Seccomp profiles, no privilege escalation |
| **Audit Trail** | Domain Events + ELK | Correlation ID, trace ID trên mỗi event; audit triggers trong database |
| **Data at Rest** | AES-256 | Mã hóa toàn bộ dữ liệu lưu trữ |
| **Image Signing** | Cosign (planned) | Ký container images, verify qua Gatekeeper |

### Vai Trò (Roles) Được Định Nghĩa Sẵn

| Vai Trò | Phạm Vi Truy Cập |
|---------|-----------------|
| **Admin** | Toàn quyền hệ thống, quản lý người dùng, xem audit |
| **Provider** | Đọc/ghi dữ liệu lâm sàng, kê đơn, xem hồ sơ bệnh nhân |
| **Nurse** | Đọc/ghi vital signs, quản lý thuốc, xem dữ liệu bệnh nhân |
| **Receptionist** | Đọc/ghi lịch hẹn, quản lý thông tin nhân khẩu học |
| **LabTechnician** | Đọc/ghi kết quả xét nghiệm, quản lý lab orders |
| **Pharmacist** | Đọc/ghi đơn thuốc, quản lý tồn kho dược phẩm |
| **BillingClerk** | Đọc/ghi claims, mã ICD-10/CPT, hóa đơn |

### Tuân Thủ HIPAA (Technical Safeguards)

| Tiêu Chuẩn | § Section | Trạng Thái | Cài Đặt |
|-----------|-----------|------------|---------|
| Access Control | 164.312(a)(1) | Implemented | RBAC + JWT + Vault + CiliumNetworkPolicy |
| Unique User ID | 164.312(a)(2)(i) | Implemented | ASP.NET Identity, JWT `sub` claim |
| Emergency Access | 164.312(a)(2)(ii) | Implemented | Break-glass procedure via Admin API |
| Automatic Logoff | 164.312(a)(2)(iii) | Implemented | JWT expiry + Redis session timeout |
| Encryption/Decryption | 164.312(a)(2)(iv) | Implemented | AES-256 at rest, TLS 1.3 + mTLS in transit |
| Audit Controls | 164.312(b) | Implemented | Serilog + DB Audit Triggers + ELK |
| Integrity Controls | 164.312(c)(1) | Implemented | mTLS, Digital Signatures, Audit Trail |
| Person/Auth | 164.312(d) | Implemented | JWT + Vault PKI + OAuth2/OIDC |
| Transmission Security | 164.312(e)(1) | Implemented | Linkerd mTLS, Cilium WireGuard |

Xem chi tiết tại [`docs/security/hipaa-compliance.md`](docs/security/hipaa-compliance.md) và [`docs/security/hardening-summary.md`](docs/security/hardening-summary.md).

---

## Bắt Đầu Nhanh / Quick Start

### Yêu Cầu Hệ Thống / Prerequisites

| Công Cụ | Phiên Bản Tối Thiểu | Kiểm Tra |
|---------|---------------------|----------|
| .NET SDK | 8.0 | `dotnet --version` |
| Node.js | 20 LTS | `node --version` |
| Docker Desktop | 24+ | `docker --version` |
| PowerShell | 7+ | `pwsh --version` |
| Angular CLI | 17.x | `npm install -g @angular/cli@17` |

### Local Development (Docker Compose)

```bash
# Clone repository
git clone <repo-url> His.Hope
cd His.Hope

# Khởi động toàn bộ hệ thống bằng Docker
./Start-EMR.ps1 -Mode docker

# Hoặc chạy infrastructure trong Docker, services + frontend chạy local
./Start-EMR.ps1 -Mode local

# Chỉ khởi động infrastructure (DB, Redis, RabbitMQ)
./Start-EMR.ps1 -Mode infra
```

**Các chế độ khởi động:**
- `-Mode docker` — Toàn bộ hệ thống chạy trong Docker containers
- `-Mode local` — Infrastructure (DB, Redis, RabbitMQ) chạy Docker; services chạy `dotnet run`; frontend chạy `ng serve`
- `-Mode infra` — Chỉ infrastructure, dành cho development từng service riêng lẻ
- `-SkipBuild` — Bỏ qua bước build (dùng khi đã build trước đó)
- `-NoSeed` — Không chạy seed data (chỉ migrate)

### Production Deployment (Kubernetes)

```bash
# Deploy qua GitOps (ArgoCD) — khuyến nghị cho production
kubectl apply -f cicd/argo/app-of-apps.yaml

# Hoặc deploy thủ công qua Kustomize
kubectl apply -k k8s/overlays/prod

# Kiểm tra trạng thái deployment
kubectl get pods -n his-hope
linkerd viz stat deploy -n his-hope
```

Sau khi deploy:
- **Frontend:** `https://<domain>/`
- **API Gateway:** `https://<domain>/api/`
- **Grafana:** `https://<domain>/grafana/`
- **Jaeger:** `https://<domain>/jaeger/`
- **Kibana:** `https://<domain>/kibana/`
- **Backstage:** `https://<domain>/backstage/`

---

## Cấu Trúc Dự Án / Project Structure

```
His.Hope/
│
├── src/
│   ├── ApiGateway/                  # YARP API Gateway — routing, rate limiting, security headers
│   │
│   ├── Services/                    # 7 Microservices (Clean Architecture + DDD)
│   │   ├── IdentityService/         #   Authentication, JWT, RBAC, User Management
│   │   │   ├── IdentityService.Api/
│   │   │   ├── IdentityService.Application/
│   │   │   ├── IdentityService.Domain/
│   │   │   └── IdentityService.Infrastructure/
│   │   ├── PatientService/          #   Patient CRUD, Allergies, Conditions, Demographics
│   │   ├── AppointmentService/      #   Appointment Scheduling, Check-In/Out, Rescheduling
│   │   ├── ClinicalService/         #   Encounters, SOAP Notes, Vitals, Diagnosis (ICD-10)
│   │   ├── LabService/              #   Lab Orders, Test Results, Specimen Tracking
│   │   ├── BillingService/          #   Claims, CPT Codes, Invoices, Payment Processing
│   │   └── PharmacyService/         #   Prescriptions, Drug Inventory, Dispensing
│   │
│   ├── Frontend/                    # Angular 17 SPA
│   │   └── his-hope-app/            #   NgRx Store, Angular Material, RxJS, SCSS
│   │
│   └── Shared/                      # Shared Libraries
│       ├── Infrastructure/          #   Cross-cutting: Outbox, Resilience (Polly), Security,
│       │   └── His.Hope.Infrastructure/     OpenTelemetry, Sagas, Caching, Health Checks
│       ├── SharedKernel/            #   Domain Primitives: Value Objects, Enumerations, Interfaces
│       ├── EventBus/                #   Message Bus Abstractions + RabbitMQ Implementation
│       └── Protos/                  #   gRPC Contracts (.proto files + Buf tooling)
│           ├── patient.proto
│           ├── appointment.proto
│           ├── clinical.proto
│           ├── lab.proto
│           ├── billing.proto
│           └── pharmacy.proto
│
├── k8s/                             # Kubernetes Manifests (Kustomize)
│   ├── base/                        #   Base resources (Deployments, Services, ConfigMaps)
│   ├── overlays/                    #   Environment overlays (dev, staging, prod)
│   ├── linkerd/                     #   Service mesh config (Servers, Authorizations, TrafficSplit)
│   ├── monitoring/                  #   Prometheus, Grafana, Jaeger, ELK
│   ├── redis/                       #   Redis Cluster configuration
│   ├── vault/                       #   Vault StatefulSet + CSI Driver
│   ├── chaos/                       #   Chaos Mesh experiments
│   ├── finops/                      #   Kubecost configuration
│   └── multi-region/                #   Multi-region deployment configs
│
├── docker/                          # Docker Compose — Local Development
│   ├── docker-compose.yml           #   Infrastructure services
│   ├── prometheus.yml               #   Prometheus scrape config
│   ├── init-multiple-dbs.sh         #   Database initialization script
│   └── .env.example                 #   Environment variables template
│
├── cockroach/                       # CockroachDB Migrations & Config
│   ├── config/                      #   StatefulSet, Init, Backup CronJob, Migration Job
│   └── migrations/                  #   13 CRDB + 10 PostgreSQL migration scripts
│       ├── 001-create-databases.sql
│       ├── 011-row-level-security.sql   # 16 security views
│       └── 012-audit-triggers.sql       # Database audit trail
│
├── vault/                           # Vault Policies & Configuration
│   ├── config.hcl                   #   Vault server configuration
│   ├── init.sh                      #   Initialization + unseal script
│   ├── seeds.sh                     #   KV secrets seeding
│   └── policies/                    #   10 Vault policies (per-service + admin + monitoring)
│
├── cilium/                          # Cilium Network Policies & Hubble config
│
├── cicd/                            # CI/CD Pipeline Definitions
│   ├── argo/                        #   ArgoCD ApplicationSets, App of Apps, Projects
│   └── tekton/                      #   Tekton Pipelines, Tasks, Triggers
│       ├── pipelines/               #     Build + Test + Scan + Push pipelines
│       ├── tasks/                   #     Reusable tasks (build, test, lint, trivy, cosign)
│       └── triggers/                #     Git webhook triggers
│
├── backstage/                       # Backstage Developer Portal
│
├── bazel/                           # Bazel Build Configurations
│
├── ml/                              # ML/AI Pipelines
│
├── data-platform/                   # Data Platform & Analytics
│
├── tests/                           # Test Projects
│   ├── Services/                    #   Unit + Integration tests per service
│   ├── Contract/                    #   gRPC contract tests
│   ├── Frontend/                    #   Angular unit + e2e tests (Jasmine/Karma)
│   ├── Load/                        #   k6 load testing scripts
│   ├── Validators/                  #   FluentValidation test suites
│   └── Shared/                      #   Shared library tests
│
├── docs/                            # Architecture & Operations Documentation
│   ├── architecture.md              #   Full system architecture (1815 lines)
│   ├── enterprise-roadmap.md        #   5-phase roadmap to Google-scale (676 lines)
│   ├── linkerd-guide.md             #   Service mesh setup & operation guide
│   └── security/                    #   Security documentation
│       ├── hipaa-compliance.md      #     HIPAA technical safeguards mapping
│       ├── hardening-summary.md     #     Security audit findings & remediation
│       └── cosign-image-signing.md  #     Image signing strategy
│
├── Start-EMR.ps1                    # One-click startup script (PowerShell 7+)
├── Start-EMR.bat                    # Windows .bat wrapper
├── His.Hope.sln                     # .NET Solution file
└── WORKSPACE                        # Bazel workspace root
```

---

## Dịch Vụ / Services

| Service | HTTP Port | gRPC Port | Database | Bounded Context |
|---------|-----------|-----------|----------|-----------------|
| **api-gateway** | 5000 | — | — | YARP reverse proxy, rate limiting, JWT validation, security headers |
| **identity-service** | 5001 | 5007 | `his_hope_identity` | Authentication, JWT issuance/revocation, RBAC (49 permissions, 7 roles), user management, refresh tokens |
| **patient-service** | 5002 | 5006 | `his_hope_patient` | Patient CRUD, allergies, chronic conditions, demographics, contact info, insurance |
| **appointment-service** | 5004 | 5008 | `his_hope_appointment` | Appointment scheduling, check-in/check-out, rescheduling, cancellation, no-show tracking |
| **clinical-service** | 5005 | 5009 | `his_hope_clinical` | Encounters, SOAP notes, vital signs, diagnosis (ICD-10), procedures (CPT), care plans |
| **lab-service** | 5010 | — | `labdb` | Lab orders, specimen tracking, test results, reference ranges, critical value alerts |
| **billing-service** | 5020 | — | `billingdb` | Insurance claims, CPT codes, invoices, payment processing, account receivables |
| **pharmacy-service** | 5030 | — | `pharmacydb` | Prescriptions (Rx), drug inventory, dispensing, drug interaction checks, refill management |

> **Frontend** (Angular 17) chạy trên port `4200`. **Backstage Developer Portal** chạy trên port `7007`.

### Kiến Trúc Mỗi Service (Clean Architecture)

```
┌────────────────────────────────────────────────────────────────┐
│                    API Layer (Minimal APIs)                     │
│  Endpoints, Middleware, Filters, gRPC Services, Health Checks  │
│  Dependencies: Application ───────────────────────────────────►│
├────────────────────────────────────────────────────────────────┤
│                    Application Layer                            │
│  Commands / Queries (CQRS via MediatR), DTOs, AutoMapper,     │
│  FluentValidation, Pipeline Behaviors (Logging, Validation,    │
│  Performance)                                                   │
│  Dependencies: Domain ────────────────────────────────────────►│
├────────────────────────────────────────────────────────────────┤
│                    Domain Layer (Pure C#, Zero Dependencies)    │
│  Aggregates, Entities, Value Objects, Domain Events,           │
│  Repository Interfaces, Business Rules, Specifications         │
├────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                         │
│  EF Core DbContext, Repository Implementations,                │
│  gRPC Clients, EventBus Publishing, Outbox Interceptor          │
└────────────────────────────────────────────────────────────────┘
```

---

## Tài Liệu / Documentation

| Tài Liệu | Mô Tả |
|----------|-------|
| [`docs/architecture.md`](docs/architecture.md) | Kiến trúc hệ thống đầy đủ — 1815 dòng bao gồm CQRS, DDD, Saga, Outbox, Resilience, Observability, Deployment |
| [`docs/enterprise-roadmap.md`](docs/enterprise-roadmap.md) | Lộ trình 5 giai đoạn nâng cấp lên Google-scale: Foundation → Resilience → Scale → Intelligence → Autonomous |
| [`docs/linkerd-guide.md`](docs/linkerd-guide.md) | Hướng dẫn cài đặt và vận hành Linkerd service mesh (mTLS, TrafficSplit, Canary, Fault Injection) |
| [`docs/security/hipaa-compliance.md`](docs/security/hipaa-compliance.md) | Tài liệu tuân thủ HIPAA Security Rule (45 CFR 164.312) — đầy đủ 9 technical safeguards |
| [`docs/security/hardening-summary.md`](docs/security/hardening-summary.md) | Tổng kết security audit: 8 findings, 8 files mới, 9 files chỉnh sửa, metrics trước/sau |
| [`docs/security/cosign-image-signing.md`](docs/security/cosign-image-signing.md) | Chiến lược ký container images với Cosign + Gatekeeper enforcement |
| [`docs/adr/`](docs/adr/) *(planned)* | Architecture Decision Records — ghi lại các quyết định kiến trúc quan trọng |
| [`docs/deployment/`](docs/deployment/) *(planned)* | Deployment & operations runbooks |
| [`docs/development/`](docs/development/) *(planned)* | Development guide: coding standards, local setup, debugging, testing |

---

## CI/CD Pipeline

```
 Git Push                           Tekton Pipelines                    ArgoCD GitOps
─────────┐                    ┌──────────────────────────┐      ┌──────────────────────────┐
         │                    │                          │      │                          │
  ┌──────▼──────┐     ┌───────▼────────┐   ┌─────────────▼─┐   ┌▼─────────────┐  ┌───────▼──────┐
  │  Feature    │     │   Build        │   │  Container    │   │  Git Push    │  │  ArgoCD      │
  │  Branch     ├────►│   • dotnet     │──►│  Image        ├──►│  to Config   ├─►│  Sync        │
  │  Push       │     │   • npm build  │   │  • Distroless │   │  Repo        │  │              │
  └─────────────┘     │   • test       │   │  • Trivy scan │   └──────┬───────┘  └──────┬───────┘
                      │   • lint       │   └──────┬────────┘          │                 │
                      └───────────────┘          │                   │          ┌──────▼───────┐
                                                 │                   │          │  Canary      │
                                          ┌──────▼───────┐    ┌──────▼───────┐  │  Deployment  │
                                          │  Push Image  │    │  Kustomize   │  │  10% Traffic │
                                          │  to Registry │    │  Image Tag   │  │  (Linkerd    │
                                          └──────────────┘    │  Update      │  │  TrafficSplit│
                                                              └──────────────┘  └──────┬───────┘
                                                                                      │
                                                                               ┌──────▼───────┐
                                                                               │  SLO Check   │
                                                                               │  (Latency,   │
                                                                               │  Error Rate) │
                                                                               └──────┬───────┘
                                                                                      │
                                                                          ┌───────────┴───────────┐
                                                                          │                       │
                                                                   ┌──────▼───────┐       ┌──────▼───────┐
                                                                   │  PASS:       │       │  FAIL:       │
                                                                   │  Promote     │       │  Auto-       │
                                                                   │  100%        │       │  Rollback    │
                                                                   └──────────────┘       └──────────────┘
```

### Pipeline Chi Tiết

1. **Build** — Tekton Pipeline: `dotnet build` + `npm build` + unit tests + lint + Trivy vulnerability scan
2. **Container Image** — Build distroless `noble-chiseled` images, push lên container registry
3. **GitOps Update** — Cập nhật image digest trong `k8s/overlays/prod/image-digests.yaml`
4. **ArgoCD Sync** — Tự động đồng bộ cluster với Git state
5. **Canary Deployment** — Linkerd TrafficSplit: 10% traffic → service mới, 90% → service cũ
6. **SLO Validation** — Prometheus kiểm tra latency (p99 < 500ms), error rate (< 0.1%)
7. **Promote or Rollback** — Nếu SLO đạt: promote 100%. Nếu không: tự động rollback trong < 60 giây

---

## Giấy Phép / License

Dự án được phân phối dưới giấy phép **MIT License**. Xem [`LICENSE`](LICENSE) để biết thêm chi tiết.

---

## Đóng Góp / Contributing

### Branch Strategy

```
main          ← Production (protected, requires PR + review + CI pass)
  └── develop ← Integration branch
       ├── feature/xxx-*     ← New features
       ├── bugfix/xxx-*      ← Bug fixes
       ├── hotfix/xxx-*      ← Production hotfixes (from main)
       └── chore/xxx-*       ← Tooling, CI, docs
```

### Pull Request Requirements

- [ ] Tất cả unit tests và integration tests phải pass
- [ ] Code phải pass linting (`dotnet format`, `ng lint`)
- [ ] Build thành công (`dotnet build`, `npm run build`)
- [ ] Test coverage không giảm dưới 85%
- [ ] Feature mới phải có tests đi kèm
- [ ] gRPC contracts không bị breaking change (backward compatible)
- [ ] Security scan (Trivy) không phát hiện HIGH/CRITICAL vulnerabilities
- [ ] Ít nhất 1 reviewer approved
- [ ] Commit messages tuân thủ [Conventional Commits](https://www.conventionalcommits.org/)

### Code Review Checklist

- [ ] Tuân thủ Clean Architecture: logic nghiệp vụ nằm trong Domain layer
- [ ] Không có circular dependency giữa các service
- [ ] Tất cả input được validated qua FluentValidation
- [ ] Tất cả external calls có Circuit Breaker + Retry (Polly)
- [ ] Domain Events được publish qua Outbox pattern (không mất event)
- [ ] Endpoints có `[Authorize]` attribute phù hợp
- [ ] Không hardcode secrets; sử dụng Vault CSI injection hoặc environment variables
- [ ] Health check endpoint hoạt động và bao gồm tất cả dependencies
- [ ] Logging sử dụng Serilog structured logging với CorrelationId
- [ ] Không có SQL injection risks (tất cả queries dùng EF Core parameterized)

Xem thêm hướng dẫn chi tiết tại [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

<p align="center">
  <b>His.Hope</b> — Hệ thống quản lý bệnh viện điện tử cho thế hệ tiếp theo.<br>
  <i>Cloud-Native • Zero-Trust • AI-Powered • Global Scale</i>
</p>

