# Dashboard Phase 3: Security + CI/CD — Design Spec

**Date:** 2026-07-22
**Status:** Draft
**Author:** Lead System Architect

## 1. Problem Statement

Phase 3 hardens security (RBAC + rate limiting) and CI/CD (NuGet cache, chiseled images, seccomp). Dashboard has authentication but no role-based authorization — any user sees everything. Dashboard BFF has no rate limiting. CI builds are slow due to ephemeral NuGet cache. Docker images use full Debian (110MB) instead of chiseled (45MB). Custom seccomp profiles are defined but never activated.

## 2. Goals

| Goal | Metric | Target |
|------|--------|--------|
| Dashboard RBAC | Role-based access on all endpoints | 3 roles: admin, operator, viewer |
| Dashboard rate limiting | Requests throttled | 100/min IP, 200/min user |
| CI build speed | NuGet restore time | < 10s (vs ~60s fresh) |
| Image size reduction | Compressed image size | < 50MB (vs ~110MB) |
| Seccomp enforcement | Custom profiles active | All services on `his-hope-dotnet-strict` |

---

## 3. Section 1: Dashboard RBAC

### 3.1 Roles

```csharp
public static class DashboardRoles
{
    public const string Admin = "admin";
    public const string Operator = "operator";
    public const string Viewer = "viewer";
    public const string ReadOnly = "admin,operator,viewer";
    public const string Manage = "admin,operator";
}
```

### 3.2 Endpoint Policies

| Endpoint | Method | Role |
|----------|--------|------|
| `/api/resources` | GET | ReadOnly |
| `/api/resources/*/start` | POST | Manage |
| `/api/resources/*/stop` | POST | Manage |
| `/api/resources/*/restart` | POST | Manage |
| `/api/metrics` | GET | ReadOnly |
| `/api/logs` | GET | ReadOnly |
| `/api/traces` | GET | ReadOnly |
| `/api/environment` | GET | ReadOnly |
| `/ws/logshub` | SignalR | ReadOnly |

### 3.3 Files

| File | Action |
|------|--------|
| `src/Bff/SystemDashboard.Bff/Authorization/DashboardRoles.cs` | Create |
| `src/Bff/SystemDashboard.Bff/Controllers/ResourcesController.cs` | Modify — add roles |
| `src/Bff/SystemDashboard.Bff/Controllers/MetricsController.cs` | Modify — add roles |
| `src/Bff/SystemDashboard.Bff/Controllers/LogsController.cs` | Modify — add roles |
| `src/Bff/SystemDashboard.Bff/Controllers/TracesController.cs` | Modify — add roles |
| `src/Bff/SystemDashboard.Bff/Controllers/EnvironmentController.cs` | Modify — add roles |
| `src/Bff/SystemDashboard.Bff/Hubs/LogStreamHub.cs` | Modify — add roles |

---

## 4. Section 2: Rate Limiting

### 4A: Dashboard BFF — Add Rate Limiting

Register `PerUserRateLimitingMiddleware` from `His.Hope.Infrastructure`:

```csharp
// Program.cs
builder.Services.AddHisHopeRateLimiting(builder.Configuration);
// ... middleware pipeline:
app.UseRateLimiting(); // after UseAuthorization
```

Add to `appsettings.json`:
```json
"RateLimiting": {
  "MaxRequestsPerIp": 100,
  "MaxRequestsPerUser": 200,
  "WindowSeconds": 60
}
```

### 4B: Re-enable PatientService Rate Limiting

Uncomment in `PatientService.Api/Program.cs`:
```csharp
// Line 144: uncomment
app.UseRateLimiting();
```

### 4.3 Files

| File | Action |
|------|--------|
| `src/Bff/SystemDashboard.Bff/Program.cs` | Modify — register + use |
| `src/Bff/SystemDashboard.Bff/appsettings.json` | Modify — add RateLimiting section |
| `src/Services/PatientService/PatientService.Api/Program.cs` | Modify — uncomment |

---

## 5. Section 3: NuGet Persistent Cache

### 5.1 PVC for CI Pipeline

```yaml
# cicd/tekton/volumes/nuget-cache-pvc.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: nuget-cache-pvc
  namespace: tekton-pipelines
spec:
  accessModes: [ReadWriteMany]
  resources: { requests: { storage: 10Gi } }
```

### 5.2 Update dotnet-build Task

Change volume mount from `emptyDir` to PVC reference.

### 5.3 NuGet.config

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
```

### 5.4 Files

| File | Action |
|------|--------|
| `cicd/tekton/volumes/nuget-cache-pvc.yaml` | Create |
| `cicd/tekton/tasks/dotnet-build.yaml` | Modify — PVC ref |
| `NuGet.config` (repo root) | Create |

---

## 6. Section 4: Chiseled Images

### 6.1 Base Image Change

```dockerfile
# BEFORE:
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
HEALTHCHECK CMD curl -f http://localhost:8080/health

# AFTER:
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra AS final
# No HEALTHCHECK — K8s probes handle health verification
# No apt-get, no curl, no shell
USER app
```

### 6.2 Why chiseled-extra (not chiseled)

`chiseled-extra` includes `wget`, `ca-certificates`, `tzdata` — needed for:
- HTTPS outbound calls (ca-certificates)
- Timezone handling (tzdata)
- Optional debugging (wget)

Image size: ~50MB compressed vs ~110MB full Debian.

### 6.3 K8s Health Probes

Docker HEALTHCHECK removed. K8s liveness/readiness probes use `httpGet` from kubelet — no tool needed in container. All deployments already have proper K8s probes.

### 6.4 Files (9 Dockerfiles)

| Service | Dockerfile |
|---------|-----------|
| PatientService | `src/Services/PatientService/Dockerfile` |
| IdentityService | `src/Services/IdentityService/Dockerfile` |
| ClinicalService | `src/Services/ClinicalService/Dockerfile` |
| AppointmentService | `src/Services/AppointmentService/Dockerfile` |
| LabService | `src/Services/LabService/Dockerfile` |
| BillingService | `src/Services/BillingService/Dockerfile` |
| PharmacyService | `src/Services/PharmacyService/Dockerfile` |
| ApiGateway | `src/ApiGateway/Dockerfile` |
| SystemDashboard.Bff | `src/Bff/SystemDashboard.Bff/Dockerfile` |

---

## 7. Section 5: Seccomp Activation

### 7.1 DaemonSet to Install Profiles

```yaml
# k8s/base/seccomp-daemonset.yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: seccomp-profile-installer
  namespace: his-hope
spec:
  selector:
    matchLabels:
      app: seccomp-installer
  template:
    spec:
      initContainers:
        - name: install
          image: busybox:1.36
          command: ["sh", "-c", "cp /profiles/*.json /host/ && echo 'installed'"]
          volumeMounts:
            - name: profiles
              mountPath: /profiles
            - name: host-seccomp
              mountPath: /host
      containers:
        - name: sleep
          image: busybox:1.36
          command: ["sleep", "infinity"]
      volumes:
        - name: profiles
          configMap:
            name: seccomp-profiles
        - name: host-seccomp
          hostPath:
            path: /var/lib/kubelet/seccomp
            type: DirectoryOrCreate
```

### 7.2 Activate on Deployments

In each deployment, change seccompProfile from `RuntimeDefault` to `Localhost`:
```yaml
seccompProfile:
  type: Localhost
  localhostProfile: his-hope-dotnet-strict.json
```

### 7.3 Files

| File | Action |
|------|--------|
| `k8s/base/seccomp-daemonset.yaml` | Create |
| `k8s/base/kustomization.yaml` | Modify — add DaemonSet |
| `k8s/base/patient-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/identity-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/clinical-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/appointment-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/lab-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/billing-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/pharmacy-service/deployment.yaml` | Modify — seccomp |
| `k8s/base/api-gateway/deployment.yaml` | Modify — seccomp |
| `k8s/base/system-dashboard-bff/deployment.yaml` | Modify — seccomp |
