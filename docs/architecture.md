# His.Hope — Hệ thống Quản lý Bệnh viện Điện tử

> Enterprise Microservices Architecture — .NET 8 + Angular 17 + CockroachDB + Linkerd + Cilium + AI

| Phase | Status | Files |
|-------|--------|-------|
| **Phase 1: Foundation** | ✅ | 60 files — K8s, Linkerd, CI/CD, Vault, Multi-region |
| **Phase 2: Resilience** | ✅ | 34 files — CockroachDB, Chaos Mesh, SLO/SLI |
| **Phase 3: Scale** | ✅ | 30 files — Bazel, Backstage, Cilium, Redis Cluster, FinOps |
| **Phase 4: Intelligence** | ✅ | 20 files — Data Platform, ML Pipelines, Feature Store |
| **Phase 5: Autonomous** | ✅ | 11 files — AI Diagnosis, Auto-Remediation, NoOps, 1B Scale |

**Total: 316 files** — [Enterprise Roadmap](enterprise-roadmap.md)

---

## Mục lục

1. [Tổng quan hệ thống](#1-tổng-quan-hệ-thống)
2. [Nguyên lý kiến trúc](#2-nguyên-lý-kiến-trúc)
3. [Technology Stack](#3-technology-stack)
4. [Microservices Architecture](#4-microservices-architecture)
5. [Clean Architecture (per Service)](#5-clean-architecture-per-service)
6. [Domain-Driven Design](#6-domain-driven-design)
7. [Inter-service Communication](#7-inter-service-communication)
8. [Enterprise Features](#8-enterprise-features)
9. [Data Architecture](#9-data-architecture)
10. [API Design](#10-api-design)
11. [Deployment Architecture](#11-deployment-architecture)
12. [CI/CD Pipeline](#12-cicd-pipeline)
13. [Service Mesh (Linkerd)](#13-service-mesh-linkerd)
14. [eBPF Observability (Cilium)](#14-ebpf-observability-cilium)
15. [Chaos Engineering](#15-chaos-engineering)
16. [SLO/SLI Framework](#16-slosli-framework)
17. [Data Platform & Analytics](#17-data-platform--analytics)
18. [ML/AI Pipeline](#18-mlai-pipeline)
19. [Auto-Remediation & NoOps](#19-auto-remediation--noops)
20. [Global Scale (1B+)](#20-global-scale-1b)
21. [Development Guide](#21-development-guide)
22. [Project Structure](#22-project-structure)

---

## 1. Tổng quan hệ thống

```
                                  ┌────────────────────────────────────────────────────┐
                                  │                  Global Load Balancer              │
                                  │           (Cloud DNS + Cloud Armor WAF)            │
                                  └──────────────────────┬─────────────────────────────┘
                                                         │
                                                   ┌─────▼──────┐
                                                   │   CDN      │
                                                   │ (CloudFlare)│
                                                   └─────┬──────┘
                                                         │
┌──────────────┐    ┌────────────────────────────────────┴──────────────────────────────┐
│   Angular    │    │                       YARP API Gateway                            │
│  Frontend    │───►│  mTLS + Rate Limiting + Security Headers + OpenTelemetry          │
│  :4200       │    └──┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬──┘
└──────────────┘       │   │   │   │   │   │   │   │   │   │   │   │   │   │   │   │
                       │   │   │   │   │   │   │   │   │   │   │   │   │   │   │   │
          ┌────────────┘   │   │   │   │   │   │   │   │   │   │   │   │   │   └────────────┐
          ▼                ▼   ▼   ▼   ▼   ▼   ▼   ▼   ▼   ▼   ▼   ▼   ▼   ▼                ▼
   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────────┐
   │ Identity │   │ Patient  │   │Appointm. │   │ Clinical │   │ Backstage│   │  Monitoring  │
   │ :5003    │   │ :5002    │   │ :5004    │   │ :5005    │   │ :7007    │   │              │
   │ :5007    │   │ :5006    │   │ :5008    │   │ :5009    │   │ Portal   │   │  Grafana:3000│
   │ (grpc)   │   │ (grpc)   │   │ (grpc)   │   │ (grpc)   │   │          │   │  Prometheus  │
   └────┬─────┘   └────┬─────┘   └────┬─────┘   └────┬─────┘   └──────────┘   │  Alertmanager│
        │              │              │              │                        └──────────────┘
        │       ┌──────▼──────┐  ┌────▼──────┐       │
        │       │   gRPC + mTLS (Linkerd Auto) │       │
        │       └──────┬──────┘  └────┬──────┘       │
        │              │              │               │
        └──────────────┴──────────────┴───────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │       RabbitMQ          │
                    │  Event Bus + Saga       │
                    └────────────┬────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │   CockroachDB Cluster    │
                    │  (Global, Multi-Region)  │
                    │  5 replicas, 3 regions   │
                    └─────────────────────────┘

   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐
   │ Redis    │   │ Cilium   │   │ Chaos    │   │ Jaeger   │   │ ELK      │
   │ Cluster  │   │ eBPF     │   │ Mesh     │   │ Tracing  │   │ Logs     │
   │ 6 nodes  │   │ Hubble   │   │ 14 exp.  │   │          │   │          │
   └──────────┘   └──────────┘   └──────────┘   └──────────┘   └──────────┘

   ┌──────────┐   ┌──────────┐   ┌──────────┐
   │ Kubecost │   │ Vault    │   │ Consul   │
   │ FinOps   │   │ Secrets  │   │ Discovery│
   └──────────┘   └──────────┘   └──────────┘
```

**His.Hope** là hệ thống quản lý bệnh viện điện tử (EMR/EHR) mã nguồn mở, kiến trúc microservices với 316 files trải dài 5 phase từ foundation đến autonomous Google-scale.

---

## 2. Nguyên lý kiến trúc

| Nguyên lý | Áp dụng |
|-----------|---------|
| **Single Responsibility** | Mỗi service chỉ quản lý một bounded context duy nhất |
| **Database per Service** | Mỗi service có database riêng, không share trực tiếp |
| **Stateless Services** | State lưu trong DB, cache (Redis Cluster), message queue (RabbitMQ) |
| **Asynchronous First** | Giao tiếp sync qua gRPC (queries), async qua EventBus (commands) |
| **CQRS** | Tách riêng Command (write) và Query (read) via MediatR |
| **Event-driven** | Domain events → Integration events → Outbox → EventBus |
| **Resilience by Design** | Circuit breaker, retry, timeout, bulkhead mặc định |
| **Security by Default** | mTLS (Linkerd auto), JWT, Vault secrets, rate limiting, security headers |
| **Observability** | OpenTelemetry + Jaeger + Prometheus + Grafana + ELK + eBPF (Cilium) |
| **Zero Trust Networks** | CiliumNetworkPolicy per-service, never trust always verify |
| **Automation** | GitOps (ArgoCD), auto-scaling, auto-remediation, NoOps |
| **AI/ML Driven** | No-show prediction, ICD-10 suggestion, readmission risk, image analysis |

---

## 3. Technology Stack

### 3.1 Languages & Frameworks

| Layer | Technology | Version |
|-------|-----------|---------|
| **Backend Runtime** | .NET | 8.0 |
| **Backend Framework** | ASP.NET Core Minimal APIs | 8.0 |
| **Frontend** | Angular | 17.x |
| **UI Component** | Angular Material | 17.x |
| **State Management** | NgRx | 17.x |
| **ML Training** | Python + XGBoost + PyTorch | 3.11 |
| **ML Serving** | Vertex AI | — |
| **Data Processing** | Apache Beam / Dataflow | — |

### 3.2 Data Layer

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Primary Database** | CockroachDB 24.1 (global) | Distributed SQL, multi-region |
| **Migration Source** | PostgreSQL 16 | Legacy per-service databases |
| **Cache** | Redis Cluster 7 (6 nodes) | Distributed cache, session store |
| **Message Broker** | RabbitMQ 3.13 | Event bus, async messaging |
| **Search & Logging** | Elasticsearch 8 | Full-text search, centralized logs |
| **Visualization** | Kibana 8 | Log analysis dashboard |
| **Data Warehouse** | BigQuery | Analytics, ML features |
| **Data Lake** | Cloud Storage | Raw data, backups |
| **Feature Store** | Vertex AI Feature Store | ML feature serving |

### 3.3 Backend Libraries

| Library | Purpose |
|---------|---------|
| **MediatR** | CQRS / in-process messaging |
| **FluentValidation** | Input validation |
| **AutoMapper** | Object mapping |
| **Entity Framework Core** | ORM, database access |
| **Npgsql** | PostgreSQL provider |
| **Polly** | Resilience (retry, circuit breaker, timeout, bulkhead) |
| **OpenTelemetry** | Distributed tracing, metrics |
| **Serilog** | Structured logging |
| **YARP** | Reverse proxy / API Gateway |
| **Grpc.AspNetCore** | gRPC server |
| **Grpc.Net.Client** | gRPC client |
| **RabbitMQ.Client** | Message queue client |
| **StackExchange.Redis** | Redis client |
| **Consul** | Service discovery |
| **Google.Cloud.AIPlatform.V1** | Vertex AI predictions |
| **Google.Cloud.PubSub.V1** | Event streaming |

### 3.4 Infrastructure & Platform

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Container Orchestrator** | Kubernetes (GKE/EKS/AKS) | Production orchestration |
| **Service Mesh** | Linkerd 2.x | mTLS, traffic split, retries, timeouts |
| **eBPF / Networking** | Cilium 1.x | Network policies, Hubble observability |
| **API Gateway** | YARP | Routing, rate limiting, security |
| **Secrets** | HashiCorp Vault 1.16 | Secret storage, rotation, PKI |
| **CI/CD** | Tekton + ArgoCD | Pipelines, GitOps |
| **Internal Developer Portal** | Backstage | Service catalog, templates |
| **Build System** | Bazel | Monorepo build, distributed caching |
| **Chaos Engineering** | Chaos Mesh | Fault injection experiments |
| **Cost Management** | Kubecost | FinOps, budget tracking |
| **Auto-scaling** | K8s HPA + VPA + Cluster Autoscaler | Predictive scaling |

### 3.5 Monitoring & Observability

| Tool | Port | Purpose |
|------|------|---------|
| **Jaeger** | 16686, 4317 | Distributed tracing (OpenTelemetry) |
| **Prometheus** | 9090 | Metrics collection |
| **Grafana** | 3000 | Metrics visualization, SLO dashboards |
| **Kibana** | 5601 | Log visualization |
| **Hubble UI** | 8081 | eBPF network flow visualization |
| **Linkerd Viz** | 8084 | Service mesh dashboard |
| **Alertmanager** | 9093 | Alert routing, deduplication |
| **Consul** | 8500 | Service discovery |
| **RabbitMQ UI** | 15672 | Message queue management |
| **Chaos Dashboard** | 2333 | Chaos experiment management |

---

## 4. Microservices Architecture

### 4.1 Service Map

| Service | Port HTTP | Port gRPC | Database | Description |
|---------|-----------|-----------|----------|-------------|
| **identity-service** | 5003 | 5007 | his_hope_identity | Authentication, JWT, RBAC |
| **patient-service** | 5002 | 5006 | his_hope_patient | Patient CRUD, allergies, conditions |
| **appointment-service** | 5004 | 5008 | his_hope_appointment | Appointment scheduling, check-in/out |
| **clinical-service** | 5005 | 5009 | his_hope_clinical | Encounters, SOAP notes, vitals, diagnosis |
| **api-gateway** | 5000 | — | — | YARP reverse proxy, rate limiting |
| **frontend** | 4200 | — | — | Angular SPA |
| **backstage** | 7007 | — | postgres | Developer Portal |

### 4.2 Service Dependencies (Phase 5 view)

```
                    ┌─────────────────────────────────────────────────────┐
                    │              Global Load Balancer                    │
                    │         Cloud DNS + Cloud Armor WAF                 │
                    └──────────────────────┬──────────────────────────────┘
                                           │
                                    ┌──────▼──────┐
                                    │  API Gateway │
                                    │  (YARP)      │
                                    └──────┬───────┘
                   ┌───────────────────────┼───────────────────────┐
                   │                       │                       │
            ┌──────▼──────┐        ┌──────▼──────┐        ┌──────▼──────┐
            │ Identity    │        │  Patient    │        │ Appointment │
            │ Service     │        │  Service    │        │  Service    │
            │ :5003/5007  │        │ :5002/5006  │        │ :5004/5008  │
            └──────┬──────┘        └──────┬──────┘        └──────┬──────┘
                   │                      │                       │
                   │              ┌───────▼────────┐              │
                   │              │  gRPC mTLS     │              │
                   │              │  (Linkerd)     │              │
                   │              └───────┬────────┘              │
                   │                      │                       │
                   └──────────────────────┼───────────────────────┘
                                          │
                                   ┌──────▼──────┐
                                   │   Clinical  │
                                   │   Service   │
                                   │  :5005/5009  │
                                   └──────┬──────┘
                                          │
                     ┌────────────────────┼────────────────────┐
                     │                    │                    │
              ┌──────▼──────┐    ┌───────▼───────┐    ┌──────▼──────┐
              │   RabbitMQ  │    │  CockroachDB  │    │  Redis      │
              │  Event Bus  │    │  Global SQL   │    │  Cluster    │
              └─────────────┘    └───────────────┘    └─────────────┘
```

### 4.3 Bounded Contexts

```
┌─────────────────────────────────────────────────────────────────────┐
│                         His.Hope System                              │
│                                                                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐│
│  │  Identity   │  │   Patient   │  │ Appointment │  │  Clinical   ││
│  │  Context    │  │   Context   │  │   Context   │  │  Context    ││
│  │             │  │             │  │             │  │             ││
│  │ • Users     │  │ • Patient   │  │ • Schedule  │  │ • Encounter ││
│  │ • Roles     │  │ • Allergy   │  │ • Check-in  │  │ • SOAP Note ││
│  │ • Auth      │  │ • Condition │  │ • Check-out │  │ • Diagnosis ││
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘│
│                                                                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐│
│  │   Billing   │  │  Pharmacy   │  │    Lab      │  │   ML/AI     ││
│  │  Context    │  │  Context    │  │  Context    │  │  Context    ││
│  │ (future)    │  │ (future)    │  │ (future)    │  │             ││
│  │             │  │             │  │             │  │ • No-Show   ││
│  │             │  │             │  │             │  │ • Readmiss. ││
│  │             │  │             │  │             │  │ • ICD-10    ││
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. Clean Architecture (per Service)

### 5.1 Layer Structure

Mỗi service tuân theo Clean Architecture với 4 layers:

```
┌─────────────────────────────────────────────────────────────────────┐
│                     API Layer (Minimal APIs)                          │
│  Endpoints, Middleware, Filters, gRPC Services, Health Checks       │
│  Dependencies: Application                                           │
├─────────────────────────────────────────────────────────────────────┤
│                   Application Layer                                   │
│  Commands / Queries (CQRS), DTOs, AutoMapper, FluentValidation      │
│  Pipeline Behaviors: Logging, Validation, Performance               │
│  Dependencies: Domain                                                │
├─────────────────────────────────────────────────────────────────────┤
│                     Domain Layer (Pure C#)                            │
│  Aggregates, Entities, Value Objects, Domain Events                 │
│  Repository Interfaces, Business Rules, Specifications              │
│  Dependencies: None                                                  │
├─────────────────────────────────────────────────────────────────────┤
│                  Infrastructure Layer                                 │
│  EF Core DbContext, Repository Implementations                      │
│  gRPC Clients, EventBus Publishing, Outbox Interceptor              │
│  Dependencies: Domain, Application                                  │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.2 Dependency Rule

```
API → Application → Domain
Infrastructure → Domain, Application
Enterprise Features (Shared Infrastructure) → All services
```

### 5.3 Enterprise Shared Infrastructure

```
┌─────────────────────────────────────────────────────────────────────┐
│              His.Hope.Infrastructure (Shared Library)                 │
├─────────────────────────────────────────────────────────────────────┤
│  Outbox Pattern    │  Resilience (Polly)     │  OpenTelemetry       │
│  • OutboxMessage   │  • Circuit Breaker      │  • Distributed Trace │
│  • DbInterceptor   │  • Retry + Jitter       │  • Metrics Export    │
│  • BgProcessor     │  • Timeout / Bulkhead   │  • Jaeger Exporter   │
├────────────────────┼─────────────────────────┼──────────────────────┤
│  Security          │  Caching                │  Saga Orchestrator   │
│  • Security Hdrs   │  • ICacheService        │  • ISagaStep<T>      │
│  • Rate Limiting   │  • Redis Distributed    │  • Compensation      │
│  • mTLS Config     │  • Cache-Aside Pattern  │  • Rollback          │
├────────────────────┼─────────────────────────┼──────────────────────┤
│  Health Checks     │  Database Resilience    │  Service Discovery   │
│  • RabbitMQ        │  • EF Retry Strategy    │  • Consul Register   │
│  • Redis           │  • Connection Pooling   │  • Health Check      │
│  • gRPC            │  • Multi-Target Writes  │  • Deregister        │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 6. Domain-Driven Design

### 6.1 Ubiquitous Language

| Thuật ngữ | Định nghĩa |
|-----------|------------|
| **Patient** | Bệnh nhân, người được chăm sóc y tế |
| **Encounter** | Lần khám/chăm sóc y tế |
| **Appointment** | Lịch hẹn khám bệnh |
| **Provider** | Nhân viên y tế (bác sĩ, y tá) |
| **Diagnosis** | Chẩn đoán (ICD-10) |
| **Procedure** | Thủ thuật (CPT) |
| **Vital Signs** | Dấu hiệu sinh tồn |
| **SOAP Note** | Ghi chú lâm sàng (Subjective, Objective, Assessment, Plan) |

### 6.2 Aggregate Roots

| Aggregate | Root Entity | Key Behaviors | Domain Events |
|-----------|-------------|---------------|---------------|
| **Patient** | `PatientId` | Register(), UpdatePersonalInfo(), Deactivate(), Reactivate() | PatientRegistered, PatientUpdated, PatientDeactivated |
| **Appointment** | `AppointmentId` | Schedule(), Reschedule(), Cancel(), CheckIn(), CheckOut() | AppointmentScheduled |
| **Encounter** | `EncounterId` | Start(), RecordVitals(), AddDiagnosis(), Complete() | EncounterStarted |
| **User** | `Guid` | — (managed by ASP.NET Identity) | — |

### 6.3 Value Objects

```
PersonName       { FirstName, LastName, MiddleName, FullName }
Address          { Street, District, City, Province, PostalCode, Country }
ContactInfo      { Phone, Email }
VitalSigns       { Temperature, HeartRate, RespiratoryRate, SystolicBP, DiastolicBP, O2Sat, Height, Weight, Bmi }
HistoryPresentIllness { Onset, Location, Duration, Characteristics, Aggravating, Relieving, Treatments }
Diagnosis        { ConditionName, Icd10Code, IsPrimary }
Procedure        { ProcedureName, CptCode, PerformedDate }
```

### 6.4 Enumerations

```
Gender       { Male, Female, Other, Unknown }
BloodType    { APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative }
AppointmentStatus  { Scheduled, CheckedIn, InProgress, Completed, Cancelled, Rescheduled, NoShow }
AppointmentType    { Checkup, Consultation, FollowUp, Emergency, Procedure, Vaccination, LabWork, Telehealth }
EncounterType      { Outpatient, Inpatient, Emergency, Telehealth, FollowUp, AnnualWellness }
EncounterStatus    { InProgress, Completed, Signed }
Race, MaritalStatus
```

### 6.5 Domain Events → Integration Events Flow

```
┌──────────────────────────────┐     ┌──────────────────────────────┐
│       Domain Event           │     │    Integration Event          │
│ (In-process, before commit)  │     │ (Cross-service, after commit) │
├──────────────────────────────┤     ├──────────────────────────────┤
│ PatientRegisteredDomainEvent │────►│ PatientRegisteredIntegration  │
│ PatientUpdatedDomainEvent    │────►│ PatientUpdatedIntegration     │
│ PatientDeactivatedDomainEvent│────►│ (no integration event)        │
│ AppointmentScheduledEvent    │────►│ AppointmentScheduledIntegr.   │
│ EncounterStartedDomainEvent  │────►│ EncounterStartedIntegration   │
└──────────────────────────────┘     └──────────────────────────────┘
                                               │
                                         ┌─────▼─────┐
                                         │   Outbox  │
                                         │   Pattern │
                                         │ (atomic!) │
                                         └─────┬─────┘
                                               │
                                         ┌─────▼─────┐
                                         │  RabbitMQ │
                                         │  EventBus │
                                         └───────────┘
```

---

## 7. Inter-service Communication

### 7.1 Communication Matrix

| Pattern | Protocol | Use Case | Example |
|---------|----------|----------|---------|
| **Request-Response (sync)** | gRPC (HTTP/2) | Query dữ liệu từ service khác | Appointment → Patient: CheckPatientExists |
| **Event-driven (async)** | RabbitMQ | Command không cần response ngay | Patient → EventBus: PatientRegistered |
| **REST (sync)** | HTTPS/1.1 | Client → API Gateway | Frontend → Gateway → Service |
| **gRPC Streaming** | HTTP/2 | Real-time updates | Clinical → Patient: GetPatientStream |

### 7.2 gRPC Contracts

```protobuf
// patient.proto
service PatientGrpcService {
  rpc GetPatient (PatientRequest) returns (PatientResponse);
  rpc SearchPatients (PatientSearchRequest) returns (PatientListResponse);
  rpc CheckPatientExists (PatientExistsRequest) returns (PatientExistsResponse);
}

// appointment.proto
service AppointmentGrpcService {
  rpc GetAppointment (AppointmentRequest) returns (AppointmentResponse);
  rpc GetPatientAppointments (PatientAppointmentsRequest) returns (AppointmentListResponse);
  rpc CheckAppointmentExists (AppointmentExistsRequest) returns (AppointmentExistsResponse);
}
```

### 7.3 gRPC Call Flow with Linkerd mTLS

```
Appointment Service                     Linkerd Proxy          Patient Service
┌─────────────────┐                    ┌──────────────┐       ┌─────────────────┐
│ ScheduleAppt()  │                    │   mTLS Auto  │       │                 │
│      │          │   CheckPatient     │  (linkerd-   │       │  GetPatientById │
│      ├──────────┼────────────────────►│   proxy)    ├──────►│                 │
│      │          │                    │  identity    │       │                 │
│      │          │◄───────────────────┤   issued     │◄──────┤                 │
│      ▼          │   PatientExistsResponse◄───1h cert───┤    │    (exists=true)│
│ CreateAppt()    │                    │              │       │                 │
└─────────────────┘                    └──────────────┘       └─────────────────┘
```

### 7.4 Event Bus (RabbitMQ)

```
Exchanges:                              Queues:
┌──────────────────────┐               ┌──────────────────────┐
│ his_hope_patient     │──────────────►│ clinical.patient     │
│   PatientRegistered  │               │ notification.patient │
│   PatientUpdated     │               │ analytics.patient    │
└──────────────────────┘               └──────────────────────┘
┌──────────────────────┐               ┌──────────────────────┐
│ his_hope_appointment │──────────────►│ notification.appt    │
│   AppointmentSched.  │               │ billing.appt         │
└──────────────────────┘               └──────────────────────┘
┌──────────────────────┐               ┌──────────────────────┐
│ his_hope_clinical    │──────────────►│ billing.encounter    │
│   EncounterStarted   │               │ analytics.encounter  │
└──────────────────────┘               └──────────────────────┘
```

---

## 8. Enterprise Features

### 8.1 Scalability

| Mechanism | Implementation | Details |
|-----------|---------------|---------|
| **Horizontal Scaling** | Stateless services + HPA | Min 3, Max 20 pods per service, CPU 70% target |
| **Predictive Scaling** | VPA + Cluster Autoscaler | Prophet model predicts load 15m ahead |
| **Global Distribution** | CockroachDB + Multi-region K8s | 3 regions (US, EU, Asia), 5 replicas |
| **Service Mesh** | Linkerd + Cilium | mTLS, traffic splitting, network policies |
| **Distributed Caching** | Redis Cluster (6 nodes) | Cache-aside pattern, TTL 5m (detail), 2m (search) |
| **gRPC Multiplexing** | HTTP/2 | Multiple requests over single TCP connection |
| **Async Processing** | RabbitMQ + Outbox | Non-blocking operations |
| **Database Pooling** | CockroachDB connection pool | Min 5, Max 100 connections |
| **CDN** | CloudFlare | Static assets, cache hit ratio 95% |
| **Partitioning** | Database per service | Natural partition by bounded context |

### 8.2 Resilience

| Mechanism | Implementation | Configuration |
|-----------|---------------|---------------|
| **Circuit Breaker** | Polly | Break sau 5 failures, 30s break, min throughput 5 |
| **Retry** | Polly | 3 lần, exponential backoff + jitter (200ms base) |
| **Timeout** | Polly | 10s per call |
| **Bulkhead** | Polly | Max 10 parallel, 50 queued |
| **Outbox Retry** | BackgroundService | Max 3 retries, polling 5s, batch 50 messages |
| **DB Connection Retry** | EF Core | 5 lần, 30s max delay |
| **Health Checks** | ASP.NET Core | DB, RabbitMQ, Redis, gRPC, disk space |
| **Chaos Engineering** | Chaos Mesh | 14 experiments, weekly GameDays |
| **Self-Healing** | Auto-remediation engine | Automatic pod restart, scaling, rollback |

#### Resilience Pipeline (Polly)

```
Client Request
     │
     ▼
┌────────────┐
│  Timeout   │  10s — TimeoutException
└─────┬──────┘
      ▼
┌────────────┐
│   Retry    │  200ms → 600ms → 1.8s (jitter)
└─────┬──────┘
      ▼
┌────────────┐
│  Circuit   │  5 failures → Open 30s → Half-Open → Success → Closed
│  Breaker   │                                         Fail → Open
└─────┬──────┘
      ▼
┌────────────┐
│  Bulkhead  │  Max 10 concurrent, 50 queued
└─────┬──────┘
      ▼
    Execute → Success → Return
      │
      └── Fail → Fallback (cached response)
```

### 8.3 Distributed Transactions (Saga + Outbox)

#### Outbox Pattern Flow

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Command    │    │  DbContext   │    │   Database   │
│   Handler    │    │  Interceptor │    │ (Same Tx!)   │
│              │    │              │    │              │
│ SaveChanges()├───►│ Capture      │───►│ INSERT INTO  │
│              │    │ DomainEvents │    │ Patient      │
│              │    │ Create       │    │ INSERT INTO  │
│              │    │ OutboxMsg    │    │ OutboxMsg    │
└──────────────┘    └──────────────┘    └──────┬───────┘
                                               │
                                          ┌────▼────────┐
                                          │   Outbox    │
                                          │  Processor  │
                                          │ (Bg Service)│
                                          │  Poll: 5s   │
                                          └────┬────────┘
                                               │
                                          ┌────▼────────┐
                                          │  RabbitMQ   │
                                          │  EventBus   │
                                          └─────────────┘
```

#### Saga Orchestrator

```csharp
// Saga step with compensation
public class CreatePatientSagaStep : ISagaStep<PatientSagaData>
{
    public async Task ExecuteAsync(PatientSagaData data, CancellationToken ct)
    {
        var patient = Patient.Register(...);
        await _repository.AddAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        data.PatientId = patient.Id.Value;
    }

    public async Task CompensateAsync(PatientSagaData data, CancellationToken ct)
    {
        var patient = await _repository.GetByIdAsync(PatientId.From(data.PatientId), ct);
        if (patient is not null)
        {
            patient.Deactivate();
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}

// Orchestration
var saga = new SagaOrchestrator<PatientSagaData>(logger)
    .AddStep(new CreatePatientSagaStep(repo, uow))
    .AddStep(new PublishPatientEventStep(eventBus))
    .AddStep(new IndexPatientInElasticsearchStep(elasticClient));

await saga.ExecuteAsync(data, ct);
```

### 8.4 Security

| Layer | Mechanism | Details |
|-------|-----------|---------|
| **Transport** | mTLS (Linkerd auto) | Mutual TLS, certificates rotated hourly |
| **Authentication** | JWT Bearer | ASP.NET Identity + JWT (HMAC-SHA256), token includes "permissions" + "role" claims |
| **Authorization** | Permission-Based RBAC | 49 granular permissions × 8 modules × 7 roles (see §8.5) |
| **REST Auth** | `[HasPermission("code")]` | Permission attribute on all REST endpoints |
| **gRPC Auth** | `[Authorize]` attribute + `PermissionHandler` | JWT "permissions" claim + role fallback |
| **mTLS (gRPC)** | Client certificates | Self-signed via Vault PKI |
| **Token Blacklisting** | Redis-backed JWT revocation | Blacklisted on logout/rotation, checked per-request |
| **Refresh Tokens** | Redis persistence + family tracking | Durable token rotation with theft detection |
| **Rate Limiting** | Fixed Window | 100 requests/minute per IP, queued up to 5 |
| **Security Headers** | Middleware | HSTS, CSP, X-Frame-Options, X-XSS-Protection |
| **Input Validation** | FluentValidation | Validate tất cả input đầu vào |
| **SQL Injection** | EF Core | Parameterized queries (built-in) |
| **Audit Trail** | `audit_log` table + triggers | SQL-level audit on all PHI tables (see §9.4) |
| **Secrets** | Vault 1.16 HA | 3-node Raft, auto-rotation, CSI injection, PKI (24h TTL + CRL + OCSP) |
| **HIPAA Compliance** | Full mapping | See [HIPAA Compliance Matrix](security/hipaa-compliance.md) |
| **CORS** | Configured | Restricted to frontend origin |
| **Network Policies** | CiliumNetworkPolicy | Zero-trust per-service ingress/egress + K8s NetworkPolicies (defense-in-depth) |
| **CSP** | Content Security Policy | Restricted script-src, style-src |

#### Security Headers

```
HTTP/1.1 200 OK
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: camera=(), microphone=(), geolocation=()
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Resource-Policy: same-origin
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; ...
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1709301234
```

#### Vault Policies & PKI

| Policy | Purpose |
|--------|---------|
| `patient-service` | Read static secrets + dynamic DB creds for `his_hope_patient` |
| `identity-service` | Read static secrets + dynamic DB creds for `his_hope_identity` |
| `clinical-service` | Read static secrets + dynamic DB creds for `his_hope_clinical` |
| `appointment-service` | Read static secrets + dynamic DB creds for `his_hope_appointment` |
| `lab-service` | Read static secrets + dynamic DB creds for `his_hope_lab` |
| `billing-service` | Read static secrets + dynamic DB creds for `his_hope_billing` |
| `pharmacy-service` | Read static secrets + dynamic DB creds for `his_hope_pharmacy` |
| `admin` | Full access for vault operators |
| `approle` | AppRole auth method configuration |
| **`token-blacklist`** | Read/write Redis-backed JWT blacklist (NEW) |
| **`readonly-monitoring`** | Read-only health/metrics for monitoring stack (NEW) |

**PKI Enhancements:**
- Certificate TTL: **24 hours** (was 30 days) — limits exposure window
- **CRL** (Certificate Revocation List) endpoint enabled
- **OCSP** (Online Certificate Status Protocol) responder active
- CSI injection for auto-mounted mTLS certs at `/vault/secrets/tls.{crt,key}`

### 8.5 Authorization & RBAC

His.Hope implements a **permission-based authorization** system with 49 granular permissions across 8 modules:

| Module | Key Permissions |
|--------|----------------|
| **Patients** | `patients.view`, `patients.create`, `patients.edit`, `patients.delete`, `patients.export` |
| **Appointments** | `appointments.view`, `appointments.create`, `appointments.edit`, `appointments.cancel`, `appointments.checkin`, `appointments.checkout` |
| **Clinical** | `clinical.view`, `clinical.create`, `clinical.edit`, `clinical.sign`, `clinical.diagnose`, `clinical.vitals` |
| **Lab** | `lab.view`, `lab.order`, `lab.result`, `lab.verify`, `lab.cancel` |
| **Billing** | `billing.view`, `billing.create`, `billing.edit`, `billing.approve`, `billing.void` |
| **Pharmacy** | `pharmacy.view`, `pharmacy.dispense`, `pharmacy.verify`, `pharmacy.inventory` |
| **Admin** | `admin.users`, `admin.roles`, `admin.audit`, `admin.settings`, `admin.security` |
| **Reports** | `reports.view`, `reports.export`, `reports.schedule`, `reports.financial` |

#### Role Hierarchy (7 Roles)

| Role | Scope | Permission Count |
|------|-------|-----------------|
| **Admin** | Full system access | All 49 |
| **Provider** | Clinical, patients, appointments, limited billing/lab | ~28 |
| **Nurse** | Clinical view, vitals, patient view, appointments | ~14 |
| **Receptionist** | Patient registration, appointment scheduling, check-in/out | ~12 |
| **LabTechnician** | Lab orders, results, verification | ~8 |
| **Pharmacist** | Pharmacy dispensing, verification, inventory | ~7 |
| **BillingClerk** | Billing view, create, edit | ~6 |

#### Architecture

```
[HasPermission("patients.view")]
REST Controller / Minimal API
        │
        ▼
AuthorizationPoliciesExtensions.cs    ← Registers named policies per permission
        │
        ▼
PermissionHandler.cs                  ← ASP.NET Core AuthorizationHandler
        │
        ├── Extracts "permissions" claim from JWT (comma-separated codes)
        ├── Checks exact permission code match
        └── Falls back to "role" claim → role → mapped permissions
```

**JWT Token Claims:**
```json
{
  "sub": "user-guid",
  "role": "Provider",
  "permissions": "patients.view,patients.create,appointments.view,clinical.view,...",
  "exp": 1709301234,
  "iss": "his-hope-identity-service"
}
```

**Code Locations:**
- `src/Shared/SharedKernel/HisHopePermissions.cs` — Permission constants (49 codes)
- `src/Shared/Infrastructure/AuthorizationPoliciesExtensions.cs` — Maps permissions → ASP.NET policies
- `src/Services/IdentityService/.../PermissionHandler.cs` — Runtime authorization handler

#### Angular Frontend Guards

| Guard | Purpose |
|-------|---------|
| **RoleGuard** | Route-level role check (`data.roles: ['Admin', 'Provider']`) |
| **PermissionGuard** | Route-level permission check (`data.permissions: ['patients.view']`) |
| **`*hasPermission`** | Structural directive — hides/shows elements based on permission |
| **`*hasRole`** | Structural directive — hides/shows elements based on role |

#### Database Schema

```sql
CREATE TABLE Roles (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description TEXT
);

CREATE TABLE Permissions (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Code VARCHAR(100) NOT NULL UNIQUE,
    Module VARCHAR(50) NOT NULL,
    Description TEXT
);

CREATE TABLE RolePermissions (
    RoleId UUID NOT NULL REFERENCES Roles(Id),
    PermissionId UUID NOT NULL REFERENCES Permissions(Id),
    PRIMARY KEY (RoleId, PermissionId)
);
```

All seed data (49 permissions, 7 roles, role-permission mappings) is applied in migration **013-identity-extensions**.

### 8.6 Availability

| Mechanism | Implementation |
|-----------|---------------|
| **Health Checks** | `/health` endpoint with DB/RabbitMQ/Redis/gRPC checks |
| **Liveness Probe** | HTTP GET /health (port 5002) |
| **Readiness Probe** | HTTP GET /health/ready (port 5002) |
| **Startup Probe** | HTTP GET /health/startup (port 5002) |
| **Graceful Shutdown** | IHostedService deregisters from Consul |
| **Database HA** | CockroachDB 5 replicas across 3 regions |
| **Message HA** | RabbitMQ mirrored queues (HA policy) |
| **Cache HA** | Redis Cluster with 1 replica per shard |
| **Multi-region** | Active-active (US, EU, Asia), auto-failover |
| **Auto-remediation** | Self-healing engine (pod restart, scale, rollback) |
| **Backup** | CockroachDB daily backup to S3 (30-day retention) |
| **Disaster Recovery** | Cross-region failover, RPO 60s, RTO 30s |

#### Health Check Response

```json
{
  "status": "Healthy",
  "duration": 123.45,
  "checks": [
    { "name": "cockroachdb-patient", "status": "Healthy", "duration": 5.2 },
    { "name": "rabbitmq", "status": "Healthy", "duration": 12.1 },
    { "name": "redis-cluster", "status": "Healthy", "duration": 3.8 },
    { "name": "grpc-identity-service", "status": "Healthy", "duration": 8.1 },
    { "name": "linkerd-mtls", "status": "Healthy", "duration": 1.2 }
  ]
}
```

### 8.7 Observability

#### OpenTelemetry Pipeline

```
┌──────────┐    ┌──────────────┐    ┌──────────────┐    ┌───────────┐
│   App    │───►│ OpenTelemetry│───►│   Collector  │───►│  Jaeger   │
│ Service  │    │  SDK (.NET)  │    │  (DaemonSet) │    │ (Trace)   │
└──────────┘    └──────┬───────┘    └──────┬───────┘    └───────────┘
                       │                   │
                       │            ┌──────▼───────┐
                       │            │  Prometheus  │
                       ├───────────►│  (Metrics)   │
                       │            └──────┬───────┘
                       │                   │
                       │            ┌──────▼───────┐
                       │            │   Grafana    │
                       │            │ (Dashboards) │
                       │            └──────────────┘
                       │
                  ┌────▼─────┐
                  │   eBPF   │
                  │ (Cilium  │
                  │  Hubble) │
                  └──────────┘
```

#### Distributed Tracing (W3C Trace Context)

```
Span: POST /api/v1/patients [trace_id=abc123]
  ├── Span: CreatePatientCommand.Handle [span_id=def456]
  │     ├── Span: db.save (PatientDbContext.SaveChangesAsync) [5ms]
  │     │     ├── Span: outbox.create (OutboxDomainEventInterceptor) [1ms]
  │     │     └── Span: db.commit (CockroachDB) [4ms]
  │     └── Span: cache.remove (Redis Cluster) [2ms]
  ├── Span: eventbus.publish (PatientRegisteredIntegrationEvent) [15ms]
  │     └── Span: rabbitmq.publish (Exchange: his_hope_patient) [12ms]
  └── Span: cache.remove (patients:search:prefix) [1ms]
```

#### Metrics Exposed (Prometheus)

```
http_server_duration_ms{method="POST", route="/api/v1/patients", service="patient-service"}
http_server_duration_ms{method="GET", route="/api/v1/patients/{id}", service="patient-service"}
grpc_server_duration_ms{method="CheckPatientExists", service="patient-service"}
grpc_client_duration_ms{method="CheckPatientExists", target="patient-service:5006", source="appointment-service"}
db_query_duration_ms{table="Patients", database="cockroachdb"}
rabbitmq_publish_count{exchange="his_hope_patient"}
rabbitmq_consume_count{queue="clinical.patient"}
redis_cache_hit_ratio{cluster="his-hope-redis"}
linkerd_proxy_requests_total{direction="inbound", target="patient-service"}
linkerd_proxy_requests_total{direction="outbound", source="patient-service"}
cilium_forward_count{policy="patient-service-ingress"}
chaos_mesh_experiment_count{type="pod-kill", result="success"}
auto_remediation_actions_total{action="restart_pod", status="success"}
kubecost_namespace_monthly_cost{namespace="his-hope"}
```

#### Structured Logging (Serilog + ELK)

```json
{
  "@timestamp": "2024-03-01T12:00:00.123Z",
  "level": "Information",
  "service": "patient-service",
  "traceId": "abc123def456",
  "spanId": "789ghi",
  "messageTemplate": "Processing request: {Name} {@Request}",
  "properties": {
    "Name": "CreatePatientCommand",
    "Request": { "FirstName": "John", "LastName": "Doe" },
    "ElapsedMilliseconds": 45
  }
}
```

---

## 9. Data Architecture

### 9.1 Database per Service → CockroachDB Global

```
┌─────────────────────────────────────────────────────────────────┐
│                    CockroachDB Cluster (24.1)                     │
│                  5 replicas across 3 regions                      │
│                                                                   │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │  his_hope_   │    │  his_hope_   │    │  his_hope_   │       │
│  │  identity    │    │  patient     │    │  appointment │       │
│  │              │    │              │    │              │       │
│  │ • Users      │    │ • Patients   │    │ • Appointments│       │
│  │ • Roles      │    │ • Allergies  │    │ • (scheduled) │       │
│  │ • RefreshTkns│    │ • Conditions │    │              │       │
│  │ • OutboxMsgs │    │ • OutboxMsgs │    │ • OutboxMsgs │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
│                                                                   │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │  his_hope_   │    │  his_hope_   │    │  his_hope_   │       │
│  │  clinical    │    │  billing     │    │  analytics   │       │
│  │              │    │  (future)    │    │  (BigQuery)  │       │
│  │ • Encounters │    │              │    │              │       │
│  │ • Vitals     │    │              │    │ • Patient    │       │
│  │ • Diagnoses  │    │              │    │   Facts      │       │
│  │ • Procedures │    │              │    │ • Clinical   │       │
│  │ • Notes      │    │              │    │   Facts      │       │
│  │ • OutboxMsgs │    │              │    │ • No-Show    │       │
│  └──────────────┘    └──────────────┘    │   Predictions│       │
│                                           └──────────────┘       │
└─────────────────────────────────────────────────────────────────┘
```

### 9.2 Multi-Region Zone Configuration

```sql
-- Global table: replicated everywhere
ALTER DATABASE patientdb CONFIGURE ZONE USING
  constraints = '{"+us-east1=2,+europe-west1=2,+asia-east1=1}';

-- Each database has a primary region
ALTER DATABASE patientdb PRIMARY REGION "us-east1";
ALTER DATABASE patientdb ADD REGION "europe-west1";
ALTER DATABASE patientdb ADD REGION "asia-east1";

-- Regional tables (pinned to primary)
ALTER TABLE patientdb.Patients CONFIGURE ZONE USING
  num_voters = 3, constraints = '{"+us-east1=2,+europe-west1=1}';
```

### 9.3 OutboxMessages Table

```sql
CREATE TABLE OutboxMessages (
    Id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type              VARCHAR(500) NOT NULL,
    Content           JSONB NOT NULL,
    CorrelationId     VARCHAR(200),
    CausationId       VARCHAR(200),
    OccurredOn        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ProcessedOn       TIMESTAMPTZ,
    Status            VARCHAR(50) NOT NULL DEFAULT 'Pending',
    Error             TEXT,
    RetryCount        INT NOT NULL DEFAULT 0,
    LastRetryOn       TIMESTAMPTZ,
    LockExpiresAt     TIMESTAMPTZ
);

CREATE INDEX idx_outbox_status ON OutboxMessages (Status, OccurredOn)
    WHERE Status = 'Pending' AND (LockExpiresAt IS NULL OR LockExpiresAt < NOW());
```

### 9.4 Database Security Architecture

#### Per-Service Database Users (Least Privilege)

7 dedicated CockroachDB users with column-level GRANT — one per service:

| User | Database | Scope |
|------|----------|-------|
| `svc_identity` | `his_hope_identity` | Users, roles, permissions, refresh tokens |
| `svc_patient` | `his_hope_patient` | Patients, allergies, conditions |
| `svc_appointment` | `his_hope_appointment` | Appointments, scheduling |
| `svc_clinical` | `his_hope_clinical` | Encounters, vitals, diagnoses, SOAP notes |
| `svc_lab` | `his_hope_lab` | Lab orders, results, panels |
| `svc_billing` | `his_hope_billing` | Invoices, payments, claims |
| `svc_pharmacy` | `his_hope_pharmacy` | Medications, dispensing, inventory |

Each user is granted `SELECT, INSERT, UPDATE, DELETE` only on its own tables — no `DROP`, no `ALTER`, no cross-database access. Defined in migration **010-database-roles**.

#### Row-Level Security (Multi-Tenant Isolation)

16 security views with `current_setting()` session variables enforce row-level isolation:

```sql
CREATE VIEW vw_tenanted_patients AS
SELECT * FROM Patients
WHERE tenant_id = current_setting('his_hope.tenant_id');

CREATE VIEW vw_tenanted_appointments AS
SELECT * FROM Appointments
WHERE tenant_id = current_setting('his_hope.tenant_id');
```

All service queries go through security views — the `tenant_id` session variable is set at connection open by the backend. Defined in migration **011-row-level-security**.

#### Audit Trail

Every PHI table has an audit trigger via `sp_insert_audit_log`:

```sql
CREATE TABLE audit_log (
    Id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    TableName     VARCHAR(200) NOT NULL,
    RecordId      UUID NOT NULL,
    Operation     VARCHAR(10) NOT NULL,   -- INSERT, UPDATE, DELETE
    OldValues     JSONB,
    NewValues     JSONB,
    ChangedBy     VARCHAR(100),
    ChangedAt     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CorrelationId VARCHAR(200)
);

CREATE PROCEDURE sp_insert_audit_log(
    p_table_name VARCHAR(200),
    p_record_id UUID,
    p_operation VARCHAR(10),
    p_old_values JSONB,
    p_new_values JSONB,
    p_changed_by VARCHAR(100)
) AS $$ ... $$ LANGUAGE plpgsql;
```

Audit triggers are attached to all PHI tables in migration **012-audit-triggers**.

#### Additional Identity Tables (Migration 013)

| Table | Purpose |
|-------|---------|
| `RefreshTokenStore` | Durable token rotation with family tracking and theft detection |
| `SystemSettings` | Centralized configuration (key-value with JSONB values) |
| `Permissions`, `Roles`, `RolePermissions` | Full RBAC seed data (see §8.5) |

```sql
CREATE TABLE RefreshTokenStore (
    Id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId      VARCHAR(100) NOT NULL,
    Token       VARCHAR(500) NOT NULL UNIQUE,
    FamilyId    VARCHAR(100) NOT NULL,
    IsRevoked   BOOLEAN NOT NULL DEFAULT FALSE,
    RevokedReason VARCHAR(200),
    CreatedAt   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ExpiresAt   TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_refresh_token_family ON RefreshTokenStore (FamilyId);
CREATE INDEX idx_refresh_token_user ON RefreshTokenStore (UserId);
```

#### Complete Migration Chain

```
001-create-databases.sql          ← 7 databases
002-patient-service.sql           ← Patients, allergies, conditions
003-identity-service.sql          ← Users, roles (original)
004-appointment-service.sql       ← Appointments, scheduling
005-clinical-service.sql          ← Encounters, vitals, diagnoses, SOAP
006-lab-service.sql               ← Lab orders, panels, results
007-billing-service.sql           ← Invoices, payments, claims
008-pharmacy-service.sql          ← Medications, dispensing, inventory
009-seed-data.sql                 ← Initial lookup data
010-database-roles.sql            ← Per-service DB users + column GRANT
011-row-level-security.sql        ← 16 RLS security views
012-audit-triggers.sql            ← audit_log + triggers on all PHI tables
013-identity-extensions.sql       ← RefreshTokenStore, SystemSettings, RBAC seed data
```

---

## 10. API Design

### 10.1 RESTful Endpoints

#### Identity Service (`/api/v1/auth`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/login` | No | Login, returns JWT |
| POST | `/register` | No | Register new user |
| POST | `/refresh` | No | Refresh access token |
| POST | `/logout` | Yes | Invalidate refresh token |
| GET | `/me` | Yes | Get current user info |

#### Patient Service (`/api/v1/patients`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/{id:guid}` | Yes | Get patient by ID |
| GET | `/search?q=&page=&pageSize=` | Yes | Search patients |
| POST | `/` | Yes | Create new patient |
| PUT | `/{id:guid}` | Yes | Update patient info |
| PATCH | `/{id:guid}/deactivate` | Yes | Deactivate patient |
| PATCH | `/{id:guid}/reactivate` | Yes | Reactivate patient |

#### Appointment Service (`/api/v1/appointments`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/` | Yes | Schedule appointment |
| PUT | `/{id:guid}/cancel` | Yes | Cancel appointment |
| PUT | `/{id:guid}/checkin` | Yes | Check-in patient |
| PUT | `/{id:guid}/checkout` | Yes | Check-out patient |

#### Clinical Service (`/api/v1/encounters`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/` | Yes | Start encounter |
| POST | `/{id:guid}/vitals` | Yes | Record vital signs |
| POST | `/{id:guid}/diagnosis` | Yes | Add diagnosis (ICD-10) |
| PUT | `/{id:guid}/complete` | Yes | Complete encounter |

### 10.2 gRPC Endpoints

#### PatientGrpcService (`patient-service:5006`)

| RPC | Request | Response | Description |
|-----|---------|----------|-------------|
| `GetPatient` | `PatientRequest` (id) | `PatientResponse` | Get patient by ID |
| `SearchPatients` | `PatientSearchRequest` (term) | `PatientListResponse` | Search patients |
| `CheckPatientExists` | `PatientExistsRequest` (id) | `PatientExistsResponse` | Check if patient exists |

#### AppointmentGrpcService (`appointment-service:5008`)

| RPC | Request | Response | Description |
|-----|---------|----------|-------------|
| `GetAppointment` | `AppointmentRequest` (id) | `AppointmentResponse` | Get appointment by ID |
| `GetPatientAppointments` | `PatientAppointmentsRequest` (pid) | `AppointmentListResponse` | Get all appointments |
| `CheckAppointmentExists` | `AppointmentExistsRequest` (id) | `AppointmentExistsResponse` | Check if appointment exists |

### 10.3 Integration Events

| Event | Publisher | Exchange | Consumers |
|-------|-----------|----------|-----------|
| `PatientRegisteredIntegrationEvent` | Patient Service | `his_hope_patient` | Clinical, Notification, Analytics |
| `PatientUpdatedIntegrationEvent` | Patient Service | `his_hope_patient` | Clinical, Index, Analytics |
| `AppointmentScheduledIntegrationEvent` | Appointment Service | `his_hope_appointment` | Notification, Billing, Analytics |
| `EncounterStartedIntegrationEvent` | Clinical Service | `his_hope_clinical` | Billing, Report, Analytics |

---

## 11. Deployment Architecture

### 11.1 Multi-Region K8s Clusters

```
Region: us-east1 (Primary)      Region: europe-west1 (Secondary)   Region: asia-east1 (Tertiary)
┌─────────────────────────┐     ┌─────────────────────────┐       ┌─────────────────────────┐
│  GKE Cluster            │     │  GKE Cluster            │       │  GKE Cluster            │
│  his-hope-us-east1      │     │  his-hope-europe-west1  │       │  his-hope-asia-east1    │
│                         │     │                         │       │                         │
│  ┌───────────────────┐  │     │  ┌───────────────────┐  │       │  ┌───────────────────┐  │
│  │  Services (x3)    │  │     │  │  Services (x3)    │  │       │  │  Services (x2)    │  │
│  │  - patient 3 pods │  │     │  │  - patient 3 pods │  │       │  │  - patient 2 pods │  │
│  │  - identity 3     │  │     │  │  - identity 3     │  │       │  │  - identity 2     │  │
│  │  - appointment 3  │  │     │  │  - appointment 3  │  │       │  │  - appointment 2  │  │
│  │  - clinical 3     │  │     │  │  - clinical 3     │  │       │  │  - clinical 2     │  │
│  └───────────────────┘  │     │  └───────────────────┘  │       │  └───────────────────┘  │
│                         │     │                         │       │                         │
│  CockroachDB: 2 voters  │     │  CockroachDB: 2 voters  │       │  CockroachDB: 1 voter   │
│  Redis: 2 shards        │     │  Redis: 2 shards        │       │  Redis: 2 shards        │
│  RabbitMQ: 3 nodes      │     │  RabbitMQ: 3 nodes      │       │  RabbitMQ: 3 nodes      │
└─────────────────────────┘     └─────────────────────────┘       └─────────────────────────┘
         │                               │                                │
         └───────────────┬───────────────┴────────────────┬───────────────┘
                         │                                │
                   ┌─────▼─────┐                   ┌──────▼──────┐
                   │  Global   │                   │   CDN       │
                   │  HTTPS LB │                   │  (CloudFlare)│
                   │  + WAF    │                   └─────────────┘
                   └───────────┘
```

### 11.2 Node Specifications

| Node Pool | Instance Type | Min | Max | Purpose |
|-----------|---------------|-----|-----|---------|
| **system** | n2-standard-4 | 3 | 5 | System components, Linkerd, Cilium |
| **services** | n2-standard-8 | 10 | 50 | Application microservices (60% spot) |
| **data** | n2-highmem-16 | 5 | 10 | CockroachDB, Redis, RabbitMQ |
| **ml** | n1-highmem-8 + T4 | 1 | 5 | ML inference pods |

### 11.3 Resource Requirements (per Region)

| Component | CPU | Memory | Storage | Replicas |
|-----------|-----|--------|---------|----------|
| **CockroachDB** | 4 cores | 16 GB | 100 GB SSD | 2-3 (5 global) |
| **Redis Cluster** | 2 cores | 4 GB | 20 GB SSD | 2 shards + 2 replicas |
| **RabbitMQ** | 2 cores | 4 GB | 10 GB | 3 |
| **Per .NET service** | 0.5-2 cores | 512 MB-2 GB | — | 3-20 (HPA) |
| **Angular Frontend** | 0.5 core | 256 MB | — | 3 |
| **Backstage** | 2 cores | 4 GB | — | 3 |
| **Vault** | 1 core | 2 GB | 10 GB | 3 (global) |
| **Jaeger** | 1 core | 2 GB | 20 GB | 2 |
| **Prometheus** | 2 cores | 8 GB | 100 GB SSD | 2 (HA) |
| **Grafana** | 1 core | 2 GB | — | 1 |

### 11.4 K8s Security Hardening

| Mechanism | Implementation | Details |
|-----------|---------------|---------|
| **NetworkPolicies** | K8s native + Cilium (defense-in-depth) | Default-deny per namespace + per-service allow rules |
| **Label Alignment** | `app:` label | Both CiliumNetworkPolicy and K8s NetworkPolicy use `app:` selector |
| **Seccomp** | `RuntimeDefault` on all containers | Custom `restricted`/`strict` profiles available for production |
| **QoS Class** | `Guaranteed` | `resources.requests == resources.limits` on all services |
| **Image Digest Pinning** | Kustomize component | Production overlays pin images by SHA256 digest |
| **Service Account** | `automountServiceAccountToken: false` | All app pods block default token mount |

#### K8s NetworkPolicy (defense-in-depth with Cilium)

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: patient-service-deny-all
  namespace: his-hope
spec:
  podSelector:
    matchLabels:
      app: patient-service
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: api-gateway
    ports:
    - port: 5002
      protocol: TCP
    - port: 5006
      protocol: TCP
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: cockroachdb
    ports:
    - port: 26257
      protocol: TCP
```

#### Pod Security Context

```yaml
securityContext:
  runAsNonRoot: true
  runAsUser: 1654
  runAsGroup: 1654
  fsGroup: 1654
  allowPrivilegeEscalation: false
  capabilities:
    drop: ["ALL"]
  readOnlyRootFilesystem: true
  seccompProfile:
    type: RuntimeDefault
```

#### Image Digest Pinning (Kustomize Component — Production Only)

```yaml
# k8s/overlays/prod/kustomization.yaml
components:
  - ../../components/image-digest

images:
  - name: patient-service
    digest: sha256:abc123...
  - name: identity-service
    digest: sha256:def456...
```

### 11.5 Container Security Architecture

#### Distroless Migration

| Component | Old Image | New Image | Rationale |
|-----------|-----------|-----------|-----------|
| **All .NET services** | `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` | `mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled` | Zero shell, zero package manager, FIPS-compliant |
| **Health checks** | Built-in curl | `curlimages/curl` (multi-stage copy) | Chiseled images have no shell/curl |

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS final
FROM curlimages/curl:latest AS curl
FROM final
COPY --from=curl /usr/bin/curl /usr/bin/curl
USER app
```

#### Non-Root Execution

All containers run as non-root user `app` (UID 1654):

```yaml
securityContext:
  runAsNonRoot: true
  runAsUser: 1654
  allowPrivilegeEscalation: false
  capabilities:
    drop: ["ALL"]
  readOnlyRootFilesystem: true
```

#### PodSecurityStandard

Namespace-level `restricted` enforcement:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: his-hope
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/enforce-version: latest
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
```

---

## 12. CI/CD Pipeline

### 12.1 Pipeline Architecture

```
Developer Push
      │
      ▼
┌─────────────────────────────────────────────────────────────┐
│                  Tekton CI Pipeline                          │
│                                                              │
│  Step 1: dotnet restore + build + test + code coverage      │
│  Step 2: SonarQube analysis (SAST)                          │
│  Step 3: Kaniko container build + Trivy scan                │
│  Step 4: Cosign container signing                           │
│  Step 5: Deploy to dev namespace                            │
│  Step 6: Smoke tests (Newman)                               │
│  Step 7: Promote to staging (manual gate)                   │
└─────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────┐
│                 ArgoCD GitOps Pipeline                       │
│                                                              │
│  1. ApplicationSet detects new image tag                    │
│  2. Deploy canary with 10% traffic (Linkerd TrafficSplit)   │
│  3. Run smoke tests for 5 minutes                           │
│  4. Analyze metrics (error rate, latency)                   │
│  5. If healthy: progressive 25% → 50% → 100%               │
│  6. If failed: rollback to 0% → alert SRE                  │
│  7. Notify Slack with deployment status                     │
└─────────────────────────────────────────────────────────────┘
```

### 12.2 Tekton Task Examples

```yaml
# Canary deploy using Linkerd TrafficSplit
apiVersion: tekton.dev/v1
kind: Task
metadata:
  name: canary-deploy
spec:
  steps:
  - name: deploy-canary-10pct
    script: |
      kubectl patch trafficsplit patient-service-split -n his-hope \
        --type=json \
        -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "900m"},
             {"op": "replace", "path": "/spec/backends/1/weight", "value": "100m"}]'
      sleep 60  # Wait for traffic shift
  
  - name: promote-to-100pct
    script: |
      kubectl patch trafficsplit patient-service-split -n his-hope \
        --type=json \
        -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "0m"},
             {"op": "replace", "path": "/spec/backends/1/weight", "value": "1000m"}]'
```

---

## 13. Service Mesh (Linkerd)

### 13.1 Architecture

```
┌─ Pod ──────────────────────────────────────────────────┐
│  ┌──────────────────────────────────────────────────┐  │
│  │ Patient Service (container)                      │  │
│  │ Ports: 5002 (HTTP), 5006 (gRPC)                  │  │
│  └──────────────────┬───────────────────────────────┘  │
│  ┌──────────────────▼───────────────────────────────┐  │
│  │ linkerd-proxy (sidecar)                          │  │
│  │ • mTLS every request (automatic)                 │  │
│  │ • Retries & timeouts (ServiceProfile)            │  │
│  │ • Traffic splitting (TrafficSplit)               │  │
│  │ • Success rates, latency, request volume         │  │
│  │ • Inbound: 4143, Outbound: 4140, Admin: 4191    │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

### 13.2 Components

| Component | Namespace | Description |
|-----------|-----------|-------------|
| **linkerd-destination** | linkerd | Service discovery, traffic routing |
| **linkerd-identity** | linkerd | mTLS certificate issuance (1h certs) |
| **linkerd-proxy-injector** | linkerd | Mutating webhook for auto-injection |
| **linkerd-viz** | linkerd-viz | Dashboard, tap, stats, edges |
| **linkerd-jaeger** | linkerd-jaeger | Distributed tracing extension |
| **linkerd-multicluster** | linkerd-multicluster | Cross-cluster service mirroring |

### 13.3 Traffic Splitting (Canary)

```yaml
apiVersion: split.smi-spec.io/v1alpha4
kind: TrafficSplit
metadata:
  name: patient-service-split
  namespace: his-hope
spec:
  service: patient-service
  backends:
    - service: patient-service
      weight: 900m    # 90%
    - service: patient-service-v2
      weight: 100m    # 10%
```

### 13.4 Service Profiles (Retries & Timeouts)

```yaml
apiVersion: linkerd.io/v1alpha2
kind: ServiceProfile
metadata:
  name: patient-service.his-hope.svc.cluster.local
  namespace: his-hope
spec:
  routes:
  - name: GET /api/patients/{id}
    condition:
      method: GET
      pathRegex: /api/patients/[^/]+
    isRetryable: true
    retries:
      budget:
        minRetriesPerSecond: 10
        retryRatio: 0.2
    timeout: 5s
  - name: POST /patient.PatientService/GetPatient
    condition:
      method: POST
      pathRegex: /patient\\.PatientService/GetPatient
    isRetryable: true
    retries:
      budget:
        minRetriesPerSecond: 5
        retryRatio: 0.2
    timeout: 10s
```

### 13.5 Authorization (Server + ServerAuthorization)

Every service now has a `Server` + `ServerAuthorization` pair, including **lab**, **billing**, and **pharmacy** services:

| Service | Server | Authorization | Status |
|---------|--------|---------------|--------|
| **patient-service** | `patient-service` | `patient-service-grpc` | ✅ |
| **identity-service** | `identity-service` | `identity-service-http` (fixed), `identity-service-grpc` | ✅ |
| **appointment-service** | `appointment-service` | `appointment-service-grpc` | ✅ |
| **clinical-service** | `clinical-service` | `clinical-service-grpc` | ✅ |
| **lab-service** | `lab-service` | `lab-service-grpc` | ✅ |
| **billing-service** | `billing-service` | `billing-service-grpc` | ✅ |
| **pharmacy-service** | `pharmacy-service` | `pharmacy-service-grpc` | ✅ |

> **Fixed:** The `identity-service-http` Server previously allowed unauthenticated access (`unauthenticated: true`) — this has been corrected to require meshTLS identities for all clients.

```yaml
apiVersion: policy.linkerd.io/v1beta1
kind: Server
metadata:
  name: identity-service
  namespace: his-hope
spec:
  podSelector:
    matchLabels:
      app: identity-service
  port: 5003
  proxyProtocol: HTTP/1
---
apiVersion: policy.linkerd.io/v1beta1
kind: ServerAuthorization
metadata:
  name: identity-service-http
  namespace: his-hope
spec:
  server:
    name: identity-service
  client:
    meshTLS:
      identities:
        - "*.his-hope.serviceaccount.identity.cluster.local"
---
apiVersion: policy.linkerd.io/v1beta1
kind: ServerAuthorization
metadata:
  name: patient-service-grpc
  namespace: his-hope
spec:
  server:
    name: patient-service
  client:
    meshTLS:
      identities:
        - "*.his-hope.serviceaccount.identity.cluster.local"
```

---

## 14. eBPF Observability (Cilium)

### 14.1 Architecture

```
┌─ Node ──────────────────────────────────────────────────┐
│                                                            │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐ │
│  │  Pod A       │    │  Pod B       │    │  Pod C       │ │
│  │  Patient Svc │    │  Clinical    │    │  Redis       │ │
│  └──────┬───────┘    └──────┬───────┘    └──────────────┘ │
│         │                  │                               │
│  ┌──────▼──────────────────▼──────────────────────────────┐│
│  │              Cilium Agent (eBPF)                       ││
│  │  • Network policies enforcement (L3/L4/L7)             ││
│  │  • Load balancing (kube-proxy replacement)             ││
│  │  • Encryption (WireGuard)                              ││
│  │  • Hubble: flow monitoring, service map                ││
│  └────────────────────────────────────────────────────────┘│
└────────────────────────────────────────────────────────────┘
```

### 14.2 Zero-Trust Network Policies

```yaml
apiVersion: cilium.io/v2
kind: CiliumNetworkPolicy
metadata:
  name: clinical-service-ingress
  namespace: his-hope
spec:
  endpointSelector:
    matchLabels:
      app: clinical-service
  ingress:
  - fromEndpoints:
    - matchLabels:
        app: api-gateway
    toPorts:
    - ports:
      - port: "5005"
        protocol: TCP
  - fromEndpoints:
    - matchLabels:
        app: identity-service
    toPorts:
    - ports:
      - port: "5009"
        protocol: TCP
```

### 14.3 Hubble UI

Provides real-time network flow visualization:

```bash
# View service-to-service communication
hubble observe --from-pod clinical-service --to-pod patient-service

# Check dropped packets
hubble observe --verdict DROPPED

# Service map
hubble status
```

---

## 15. Chaos Engineering

### 15.1 Experiment Schedule

| Schedule | Experiment | Type | Duration |
|----------|------------|------|----------|
| Every 6h | Pod Kill (1 pod) | PodChaos | 30s |
| Every 4h | Network Delay 50ms | NetworkChaos | 5m |
| Every 8h | Packet Loss 25% | NetworkChaos | 3m |
| Every 6h | CPU Stress 80% | StressChaos | 5m |
| Every 4h | HTTP 500 Errors (10%) | HTTPChaos | 5m |
| Every 6h | gRPC Unavailable (5%) | HTTPChaos | 3m |
| Weekly | Game Day: 30% pods killed | PodChaos | 10m |
| Weekly | Game Day: 2s latency | NetworkChaos | 10m |
| Monthly | Full Chaos: all experiments | Mixed | 60m |

### 15.2 Game Day Protocol

```
Pre-GameDay (1h before):
  ✅ Notify all teams via Slack #his-hope-chaos
  ✅ Record baseline metrics (latency, error rate, throughput)
  ✅ Ensure on-call engineer is available
  ✅ Verify rollback script is accessible

During GameDay:
  ✅ Execute experiments in order (increasing severity)
  ✅ Monitor auto-remediation engine behavior
  ✅ Record MTTR (Mean Time To Remediate)

Post-GameDay:
  ✅ Rollback all experiments
  ✅ Run blameless postmortem within 48h
  ✅ Update runbooks based on findings
  ✅ Track action items to completion
```

### 15.3 Rollback Script

```bash
#!/bin/bash
# Emergency rollback all chaos experiments
kubectl delete podchaos --all -n his-hope
kubectl delete networkchaos --all -n his-hope
kubectl delete stresschaos --all -n his-hope
kubectl delete httpchaos --all -n his-hope
kubectl delete dnschaos --all -n his-hope
kubectl delete iochaos --all -n his-hope
echo "All chaos experiments stopped."
```

---

## 16. SLO/SLI Framework

### 16.1 Service Objectives

| Service | Availability SLO | Latency SLO (p99) | Error Budget (30d) |
|---------|-----------------|-------------------|---------------------|
| **patient-service** | 99.9% | 500ms | 43.2m |
| **identity-service** | 99.95% | 300ms | 21.6m |
| **appointment-service** | 99.9% | 1s | 43.2m |
| **clinical-service** | 99.99% | 500ms | 4.32m |

### 16.2 Multi-Window, Multi-Burn-Rate Alerts

| Alert | Condition | Burn Rate | Window | Severity |
|-------|-----------|-----------|--------|----------|
| **SLOErrorBudgetBurnCritical** | Error budget > 2x burn rate | > 2 | 1h + 5m | Critical |
| **SLOErrorBudgetBurnWarning** | Error budget > 1x burn rate | > 1 | 6h | Warning |
| **HighLatencyP99** | p99 > 500ms for 5m | — | 5m | Warning |
| **ServiceDown** | No metrics for 30s | — | 30s | Critical |

### 16.3 Recording Rules

```yaml
rules:
- record: job:slo_availability_30d:ratio
  expr: |
    sum(rate(http_requests_total{code=~"2.."}[30d]))
    /
    sum(rate(http_requests_total[30d]))

- record: job:slo_burn_rate_1h:ratio
  expr: |
    (1 - job:slo_availability_1h:ratio) / 0.001
```

---

## 17. Data Platform & Analytics

### 17.1 Event Streaming Architecture

```
┌──────────────┐    ┌──────────┐    ┌──────────┐    ┌──────────────┐
│  CockroachDB │───►│ Debezium │───►│  Pub/Sub │───►│   Dataflow   │
│  (CDC)       │    │ Connector│    │ (Topics) │    │   Pipeline   │
└──────────────┘    └──────────┘    └────┬─────┘    └──────┬───────┘
                                         │                 │
                                         │           ┌─────▼──────┐
                                         │           │  BigQuery   │
                                         │           │ (Analytics) │
                                         │           └─────┬──────┘
                                         │                 │
                                         │           ┌─────▼──────┐
                                         │           │  Vertex AI │
                                         │           │  Feature   │
                                         │           │  Store     │
                                         └───────────┴────────────┘
```

### 17.2 BigQuery Tables

| Table | Partition | Clustering | Description |
|-------|-----------|------------|-------------|
| `patient_facts` | registration_date (month) | gender, age_group | Patient analytics |
| `clinical_facts` | encounter_date (month) | — | Clinical encounter metrics |
| `appointment_facts` | scheduled_date (month) | — | Scheduling analytics |
| `provider_performance` | date | — | Provider metrics |
| `no_show_predictions` | prediction_date | — | ML predictions |
| `readmission_predictions` | prediction_date | — | ML predictions |
| `icd10_diagnosis_summary` | month | — | Diagnosis trends |

### 17.3 Dataflow Pipelines

| Pipeline | Source | Sink | Purpose |
|----------|--------|------|---------|
| `patient-pipeline` | Pub/Sub patient-events | BigQuery | Patient data enrichment |
| `clinical-pipeline` | Pub/Sub clinical-events | BigQuery + Feature Store | PHI de-identification |
| `ml-features-pipeline` | BigQuery | Vertex AI Feature Store | ML feature computation |

---

## 18. ML/AI Pipeline

### 18.1 Model Inventory

| Model | Type | Algorithm | Features | Serving |
|-------|------|-----------|----------|---------|
| **No-Show Predictor** | Classification | XGBoost | 9 features | n1-standard-4, 1-5 replicas |
| **Readmission Risk** | Classification | XGBoost + LSTM | 15+ features | n1-standard-4, 1-10 replicas |
| **ICD-10 Suggester** | Multi-label | BioBERT | Clinical text | n1-highmem-8 + T4, 1-3 replicas |
| **Medical Image** | Multi-class | DenseNet-121 | DICOM images | n1-highmem-16 + A100, 2-10 replicas |
| **Clinical NLP** | NER + Summarization | BioBERT + BART | Clinical notes | n1-standard-8, 2-20 replicas |
| **Treatment Recommender** | Causal + RL | Causal Forest | 25+ features | n1-highmem-32 + 2xA100, 1-3 replicas |
| **Anomaly Detection** | Time-series | LSTM Autoencoder | Metrics | n1-standard-4, 1-3 replicas |
| **Population Health** | Ensemble | Prophet + XGBoost | Aggregated data | Batch daily |

### 18.2 Training Pipeline (No-Show Example)

```
                   ┌─────────────────────────────┐
                   │   Feature Extraction         │
                   │   BigQuery → DataFrame       │
                   │   · patient demographics     │
                   │   · appointment history      │
                   │   · clinical features        │
                   └─────────────┬───────────────┘
                                 │
                   ┌─────────────▼───────────────┐
                   │   Preprocessing               │
                   │   · Label Encoding            │
                   │   · Standard Scaling          │
                   │   · Train/Test Split (80/20)  │
                   └─────────────┬───────────────┘
                                 │
                   ┌─────────────▼───────────────┐
                   │   Model Training              │
                   │   · XGBoost Classifier        │
                   │   · Hyperparameter tuning     │
                   │   · scale_pos_weight (imbal.) │
                   │   · Early stopping (20 rounds)│
                   └─────────────┬───────────────┘
                                 │
                   ┌─────────────▼───────────────┐
                   │   Evaluation                  │
                   │   · Accuracy, Precision       │
                   │   · Recall, F1, ROC-AUC      │
                   │   · Feature importance        │
                   └─────────────┬───────────────┘
                                 │
                   ┌─────────────▼───────────────┐
                   │   Deploy to Vertex AI        │
                   │   · Upload model to registry │
                   │   · Create endpoint          │
                   │   · Deploy with autoscaling  │
                   └─────────────────────────────┘
```

### 18.3 Online Prediction (C# Client)

```csharp
public async Task<NoShowPrediction> PredictNoShowAsync(NoShowFeatures features)
{
    var endpoint = $"projects/{_projectId}/locations/{_region}/endpoints/{_endpointId}";

    var instance = new Value
    {
        StructValue = new Struct
        {
            Fields =
            {
                ["age_group"] = new Value { NumberValue = features.AgeGroup },
                ["gender"] = new Value { NumberValue = features.Gender },
                ["total_encounters"] = new Value { NumberValue = features.TotalEncounters },
                ["lead_time_days"] = new Value { NumberValue = features.LeadTimeDays }
            }
        }
    };

    var response = await _client.PredictAsync(endpoint, new List<Value> { instance });

    return new NoShowPrediction
    {
        WillNoShow = response.Predictions[0].StructValue.Fields["prediction"].NumberValue > 0.5,
        Probability = (float)response.Predictions[0].StructValue.Fields["probability"].NumberValue
    };
}
```

---

## 19. Auto-Remediation & NoOps

### 19.1 Remediation Engine

```
Prometheus Alert
      │
      ▼
┌─────────────────────────────────────────────────────────────┐
│              Auto-Remediation Engine                        │
│                                                              │
│  1. Receive alert (type, severity, context)                 │
│  2. Check cooldown (avoid storm)                            │
│  3. Look up remediation policy by incident type             │
│  4. Execute actions in order (lowest risk first)            │
│  5. Wait for verification (5s)                              │
│  6. If success → log, reset counter                         │
│  7. If failure → next action, increment counter             │
│  8. If all failed → escalate to human (PagerDuty)           │
└─────────────────────────────────────────────────────────────┘
      │
      ├── Success → Close alert, record MTTR
      └── Fail → Escalate to SRE on-call
```

### 19.2 Remediation Policies

| Incident Type | Actions (in order) | Cooldown | Max Attempts |
|---------------|-------------------|----------|--------------|
| **Service Down** | Restart pod → Scale up → Rollback → Failover region | 5m | 3 |
| **High Latency** | Scale HPA → Increase cache TTL → Enable circuit breaker | 10m | 3 |
| **DB Failover** | Promote replica → Reroute traffic → Switch region | 15m | 2 |
| **Circuit Breaker Open** | Half-open → Scale caller → Increase timeout | 5m | 5 |
| **Outbox Backlog** | Scale processor → Restart processor → Reprocess | 2m | 10 |
| **Cert Expiry** | Auto-renew → Redeploy | 60m | 3 |
| **Disk Full** | Cleanup logs → Expand PVC → Enable rotation | 30m | 3 |

### 19.3 Self-Healing Config

```yaml
self_healing:
  pod_crash_loop:
    enabled: true
    max_restarts: 3
    cooldown_minutes: 10

  predictive_autoscaling:
    enabled: true
    look_ahead_minutes: 15
    model: prophet

  database_failover:
    enabled: true
    max_downtime_seconds: 30

  certificate_renewal:
    enabled: true
    renew_before_days: 30
```

---

## 20. Global Scale (1B+)

### 20.1 Target Metrics

| Metric | Value |
|--------|-------|
| **Patients** | 1,000,000,000 |
| **Requests/second** | 10,000,000 |
| **Transactions/second** | 100,000 |
| **Data volume/day** | 50 TB |
| **p50 Latency** | 50ms |
| **p99 Latency** | 500ms |
| **Availability** | 99.999% |
| **RPO** | 60s |
| **RTO** | 30s |

### 20.2 Infrastructure per Region

| Resource | Value |
|----------|-------|
| **Pods** | 500 |
| **Nodes** | 50 |
| **CPU Cores** | 2,000 |
| **Memory** | 8 TB |
| **CockroachDB nodes** | 100 |
| **Storage per DB node** | 10 TB |
| **Redis shards** | 256 |
| **Redis memory per shard** | 64 GB |
| **CDN PoPs** | 200 (CloudFlare) |

### 20.3 Cost Model

| Item | Monthly Cost |
|------|-------------|
| **Infrastructure** | $5,000,000 |
| **Cost per request** | $0.00005 |
| **Data transfer** | 10,000 TB/mo |
| **Optimization** | 60% spot, 40% reserved |

---

## 21. Development Guide

### 21.1 Prerequisites

```bash
dotnet --version          # 8.0.x
node --version            # 20.x
docker --version          # 24.x
kubectl version           # 1.28+
linkerd version           # 2.x
helm version              # 3.x
```

### 21.2 Quick Start (K8s)

```bash
# 1. Create cluster (GKE example)
gcloud container clusters create his-hope \
  --region us-east1 \
  --num-nodes 5

# 2. Install Linkerd
linkerd install --crds | kubectl apply -f -
linkerd install | kubectl apply -f -
linkerd viz install | kubectl apply -f -
linkerd check

# 3. Install Cilium
helm install cilium cilium/cilium --namespace kube-system \
  --set hubble.enabled=true \
  --set hubble.relay.enabled=true \
  --set hubble.ui.enabled=true

# 4. Deploy His.Hope
kubectl apply -k k8s/overlays/prod/
linkerd viz stat deployments -n his-hope

# 5. Deploy CockroachDB
kubectl apply -f cockroach/config/cockroachdb-statefulset.yaml
kubectl apply -f cockroach/config/migration-job.yaml

# 6. Deploy Vault
kubectl apply -f k8s/vault/vault-statefulset.yaml
bash vault/init.sh
```

### 21.3 Creating a New Service

1. Use Backstage template: `https://developer.hishop.com/create` → "Create Microservice"
2. Or manually copy PatientService structure
3. Update `RootNamespace` in all `.csproj` files
4. Define Domain → Application → Infrastructure → API
5. Add Protobuf for gRPC
6. Add K8s manifests in `k8s/base/`
7. Register in `cicd/argo/app-of-apps.yaml`
8. Add to Backstage catalog

### 21.4 Running Migrations

CockroachDB migrations are applied sequentially via K8s Job. The full chain:

```
001-create-databases.sql          ← 7 databases (identity, patient, appointment, clinical, lab, billing, pharmacy)
002-patient-service.sql           ← Patients, allergies, conditions
003-identity-service.sql          ← Users, roles (ASP.NET Identity tables)
004-appointment-service.sql       ← Appointments, scheduling
005-clinical-service.sql          ← Encounters, vitals, diagnoses, SOAP notes
006-lab-service.sql               ← Lab orders, panels, results
007-billing-service.sql           ← Invoices, payments, claims
008-pharmacy-service.sql          ← Medications, dispensing, inventory
009-seed-data.sql                 ← Initial lookup data (genders, blood types, etc.)
010-database-roles.sql            ← 7 per-service least-privilege CRDB users + column-level GRANT
011-row-level-security.sql        ← 16 security views for multi-tenant row isolation
012-audit-triggers.sql            ← audit_log table + sp_insert_audit_log + triggers on all PHI tables
013-identity-extensions.sql       ← RefreshTokenStore, SystemSettings, RBAC tables + seed data (49 permissions, 7 roles)
```

```bash
# CockroachDB migrations (K8s Job — applies all 13 migrations sequentially)
kubectl apply -f cockroach/config/migration-job.yaml

# EF Core migrations (development)
dotnet ef database update \
  -p src/Services/PatientService/PatientService.Infrastructure \
  -s src/Services/PatientService/PatientService.Api
```

---

## 22. Project Structure

```
His.Hope/
├── His.Hope.sln                                               # Solution
├── WORKSPACE                                                  # Bazel workspace
│
├── docker/
│   ├── docker-compose.yml                                     # Full stack orchestration
│   ├── init-multiple-dbs.sh                                   # PostgreSQL init
│   └── prometheus.yml                                         # Scrape config
│
├── docs/
│   ├── architecture.md                                        # This document
│   ├── enterprise-roadmap.md                                  # Google-scale roadmap
│   ├── linkerd-guide.md                                       # Linkerd setup guide
│   └── security/
│       ├── hipaa-compliance.md                                # HIPAA control mapping (§164.3xx)
│       ├── hardening-summary.md                               # Container + K8s hardening log
│       └── cosign-image-signing.md                            # Cosign signing workflow
│
├── k8s/
│   ├── base/                                                  # Base K8s manifests
│   │   ├── patient-service.yaml                               # Deployment + Service + HPA + PDB
│   │   ├── identity-service.yaml
│   │   ├── appointment-service.yaml
│   │   ├── clinical-service.yaml
│   │   ├── lab-service.yaml
│   │   ├── billing-service.yaml
│   │   ├── pharmacy-service.yaml
│   │   ├── postgres.yaml                                      # PostgreSQL StatefulSet
│   │   ├── rabbitmq.yaml                                      # RabbitMQ StatefulSet
│   │   ├── redis.yaml                                         # Redis StatefulSet
│   │   ├── namespace.yaml                                     # All namespaces
│   │   └── kustomization.yaml
│   ├── components/                                            # Reusable Kustomize components
│   │   └── image-digest/                                      # Image SHA256 digest pinning
│   ├── overlays/                                              # Environment overlays
│   │   ├── dev/kustomization.yaml
│   │   ├── staging/kustomization.yaml
│   │   └── prod/kustomization.yaml
│   ├── linkerd/                                               # Service mesh config
│   │   ├── namespace.yaml
│   │   ├── linkerd-control-plane.yaml
│   │   ├── server.yaml                                       # Server resources
│   │   ├── server-authorization.yaml                          # RBAC policies
│   │   ├── traffic-split.yaml                                # Canary traffic
│   │   ├── service-profiles.yaml                             # Retries & timeouts
│   │   ├── viz.yaml                                         # Dashboard
│   │   ├── multicluster.yaml                                 # Multi-region
│   │   ├── jaeger.yaml                                       # Tracing
│   │   └── pod-monitor.yaml
│   ├── monitoring/                                            # Observability
│   │   ├── prometheus-rules.yaml                             # SLO rules + alerts
│   │   ├── prometheus-config.yaml
│   │   ├── grafana-dashboards.yaml
│   │   ├── alertmanager-config.yaml
│   │   ├── slo-exporter-config.yaml
│   │   ├── incident-response.yaml                            # Runbooks
│   │   └── kustomization.yaml
│   ├── chaos/                                                # Chaos Engineering
│   │   ├── chaos-mesh-install.yaml
│   │   ├── pod-kill.yaml                                     # Every 6h
│   │   ├── network-delay.yaml                                # Every 4h
│   │   ├── network-loss.yaml                                 # Every 8h
│   │   ├── network-partition.yaml                            # Every 12h
│   │   ├── cpu-stress.yaml                                   # Every 6h
│   │   ├── memory-stress.yaml                                # Every 8h
│   │   ├── http-fault.yaml                                   # 10% errors
│   │   ├── grpc-fault.yaml                                   # 5% unavailable
│   │   ├── dns-chaos.yaml                                    # DNS errors
│   │   ├── io-fault.yaml                                     # Disk latency
│   │   ├── game-day-schedule.yaml                            # Weekly + Monthly
│   │   ├── rollback-script.yaml
│   │   └── experiment-dashboard.yaml
│   ├── vault/                                                # Vault HA
│   │   ├── vault-statefulset.yaml
│   │   ├── vault-agent-injector.yaml
│   │   └── vault-csi-provider.yaml
│   ├── multi-region/                                         # Global deployment
│   │   ├── region-us-east1.yaml
│   │   ├── region-europe-west1.yaml
│   │   ├── region-asia-east1.yaml
│   │   ├── global-loadbalancer.yaml
│   │   ├── cert-manager.yaml
│   │   └── external-dns.yaml
│   ├── redis/                                                # Redis Cluster
│   │   ├── redis-cluster.yaml
│   │   └── redis-cluster-init.yaml
│   ├── network-policies/                                      # K8s native NetworkPolicies
│   │   ├── default-deny.yaml                                  # Default deny per namespace
│   │   └── per-service.yaml                                   # Per-service allow rules
│   ├── finops/                                               # Cost management
│   │   ├── kubecost-install.yaml
│   │   ├── resource-quotas.yaml
│   │   ├── namespace-budgets.yaml
│   │   ├── vertical-pod-autoscaler.yaml
│   │   └── cluster-autoscaler.yaml
│   └── auto-remediation/                                     # Self-healing
│       ├── self-healing.yaml
│       └── pod-health-operator.yaml
│
├── cicd/
│   ├── tekton/
│   │   ├── tasks/
│   │   │   ├── dotnet-build.yaml
│   │   │   ├── container-build.yaml
│   │   │   ├── deploy-k8s.yaml
│   │   │   ├── smoke-test.yaml
│   │   │   └── canary-deploy.yaml
│   │   ├── pipelines/
│   │   │   ├── ci-pipeline.yaml
│   │   │   └── cd-pipeline.yaml
│   │   └── triggers/
│   │       ├── github-listener.yaml
│   │       └── trigger-template.yaml
│   └── argo/
│       ├── application-set.yaml
│       ├── app-of-apps.yaml
│       └── project.yaml
│
├── cilium/
│   ├── cilium-install.yaml
│   ├── hubble-ui.yaml
│   ├── network-policies.yaml
│   └── pixie-config.yaml
│
├── cockroach/
│   ├── config/
│   │   ├── cockroachdb-statefulset.yaml
│   │   ├── cockroachdb-service.yaml
│   │   ├── cockroachdb-init.yaml
│   │   ├── backup-config.yaml
│   │   ├── backup-cronjob.yaml
│   │   ├── migration-job.yaml
│   │   └── migration-configmap.sh
│   └── migrations/
│       ├── 001-create-databases.sql
│       ├── 002-patient-service.sql
│       ├── 003-identity-service.sql
│       ├── 004-appointment-service.sql
│       ├── 005-clinical-service.sql
│       ├── 006-lab-service.sql
│       ├── 007-billing-service.sql
│       ├── 008-pharmacy-service.sql
│       ├── 009-seed-data.sql
│       ├── 010-database-roles.sql
│       ├── 011-row-level-security.sql
│       ├── 012-audit-triggers.sql
│       ├── 013-identity-extensions.sql
│       └── run-migrations.sh
│
├── vault/
│   ├── config.hcl
│   ├── init.sh
│   ├── seeds.sh
│   └── policies/
│       ├── patient-service.hcl
│       ├── identity-service.hcl
│       ├── appointment-service.hcl
│       ├── clinical-service.hcl
│       ├── lab-service.hcl
│       ├── billing-service.hcl
│       ├── pharmacy-service.hcl
│       ├── admin.hcl
│       ├── approle.hcl
│       ├── token-blacklist.hcl
│       └── readonly-monitoring.hcl
│
├── backstage/
│   ├── app-config.yaml
│   ├── deployment.yaml
│   ├── catalog/
│   │   ├── all.yaml
│   │   ├── software.yaml
│   │   ├── apis.yaml
│   │   ├── resources.yaml
│   │   └── domains.yaml
│   ├── templates/create-microservice/
│   │   ├── template.yaml
│   │   └── skeleton/catalog-info.yaml
│   └── packages/app/src/components/Root/Root.tsx
│
├── bazel/
│   ├── .bazelrc
│   ├── BUILD
│   ├── nuget.bzl
│   ├── src/Services/{Patient,Identity,Appointment,Clinical}Service/BUILD
│   └── tools/ci/build.sh
│
├── data-platform/
│   ├── pubsub/topics.yaml
│   ├── pubsub/subscriptions.yaml
│   ├── bigquery/analytics-dataset.yaml
│   ├── bigquery/views.yaml
│   ├── dataflow/patient-pipeline.yaml
│   ├── dataflow/clinical-pipeline.yaml
│   ├── dataflow/ml-features-pipeline.yaml
│   ├── change-data-capture.yaml
│   ├── iam.yaml
│   └── k8s/debezium-connector.yaml
│
├── ml/
│   ├── features/feature-store-config.yaml
│   ├── serving/
│   │   ├── vertex-endpoint.yaml
│   │   ├── PredictionClient.cs
│   │   └── PredictionMiddleware.cs
│   ├── training/
│   │   ├── no-show-prediction/pipeline.yaml
│   │   ├── no-show-prediction/config.yaml
│   │   ├── readmission-prediction/pipeline.yaml
│   │   ├── icd10-suggestion/config.yaml
│   │   └── schedule-config.yaml
│   ├── monitoring/model-monitoring.yaml
│   └── autonomous/
│       ├── medical-image-analysis/pipeline.yaml
│       ├── nlp-clinical-notes/pipeline.yaml
│       ├── personalized-treatment/pipeline.yaml
│       ├── population-health/pipeline.yaml
│       ├── auto-remediation/engine.py
│       ├── auto-remediation/kubernetes-remediator.py
│       ├── anomaly-detection/config.yaml
│       ├── noops-dashboard-config.yaml
│       └── performance-engine/global-scale.yaml
│
├── src/
│   ├── ApiGateway/                                          # YARP Gateway
│   ├── Shared/
│   │   ├── Protos/                                          # gRPC contracts
│   │   ├── SharedKernel/                                    # DDD foundation + HisHopePermissions.cs
│   │   ├── EventBus/                                        # Messaging
│   │   └── Infrastructure/                                  # Enterprise features
│   └── Services/
│       ├── PatientService/                                  # Reference service
│       │   ├── Domain/                                      # Aggregates, Value Objects
│       │   ├── Application/                                 # CQRS, DTOs, Validation
│       │   ├── Infrastructure/                              # EF Core, Repositories
│       │   └── Api/                                         # Minimal + gRPC
│       ├── IdentityService/
│       ├── AppointmentService/
│       └── ClinicalService/
│
└── src/Frontend/his-hope-app/                               # Angular 17 SPA
```
