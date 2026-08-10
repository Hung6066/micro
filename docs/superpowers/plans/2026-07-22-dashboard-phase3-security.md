# Dashboard Phase 3: Security + CI/CD — Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development to implement task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hardening: dashboard RBAC, rate limiting, NuGet cache, chiseled images, seccomp activation.

**Architecture:** Add role-based `[Authorize]` attributes across 7 dashboard files. Register existing `PerUserRateLimitingMiddleware` in dashboard BFF. Replace Docker base images with chiseled. Add PVC for NuGet cache. Deploy seccomp DaemonSet.

**Tech Stack:** .NET 8, ASP.NET Core Authorization, Docker, Kubernetes, Tekton, Seccomp

## Global Constraints

- Existing `[Authorize]` attributes must be preserved and augmented (not replaced)
- Role claims use JWT `role` claim matching existing IdentityService pattern
- Chiseled images: use `aspnet:8.0-jammy-chiseled-extra` (has wget + certs + tzdata)
- Remove Docker HEALTHCHECK — K8s httpGet probes already handle it
- Non-root user (`app`) preserved in all Dockerfiles
- Rate limiting config uses existing `PerUserRateLimitingMiddleware` from infrastructure
- Seccomp profiles already exist in `k8s/base/seccomp-profiles.yaml`

---

### Task 1: DashboardRoles Constants

**Files:** Create `src/Bff/SystemDashboard.Bff/Authorization/DashboardRoles.cs`

```csharp
namespace SystemDashboard.Bff.Authorization;

public static class DashboardRoles
{
    public const string Admin = "admin";
    public const string Operator = "operator";
    public const string Viewer = "viewer";
    public const string ReadOnly = "admin,operator,viewer";
    public const string Manage = "admin,operator";
}
```

- [ ] Create file, build, commit: `feat(dashboard): add DashboardRoles authorization constants`

---

### Task 2: Apply RBAC to 5 Controllers + 1 Hub

**Files:** Modify 6 files — add `using SystemDashboard.Bff.Authorization;` and replace `[Authorize]` with `[Authorize(Roles = DashboardRoles.ReadOnly)]` or `[Authorize(Roles = DashboardRoles.Manage)]` per the spec.

Controllers to modify:
- `Controllers/ResourcesController.cs` — GET: ReadOnly, POST (lifecycle): Manage
- `Controllers/MetricsController.cs` — ReadOnly
- `Controllers/LogsController.cs` — ReadOnly
- `Controllers/TracesController.cs` — ReadOnly
- `Controllers/EnvironmentController.cs` — ReadOnly
- `Hubs/LogStreamHub.cs` — ReadOnly

- [ ] Modify all 6 files, build, commit: `feat(dashboard): apply role-based authorization to dashboard endpoints`

---

### Task 3: Dashboard Rate Limiting

**Files:**
- Modify `src/Bff/SystemDashboard.Bff/Program.cs` — add `builder.Services.AddHisHopeRateLimiting(builder.Configuration)` and `app.UseRateLimiting()`
- Modify `src/Bff/SystemDashboard.Bff/appsettings.json` — add RateLimiting section

```json
"RateLimiting": {
  "MaxRequestsPerIp": 100,
  "MaxRequestsPerUser": 200,
  "WindowSeconds": 60
}
```

- [ ] Modify 2 files, build, commit: `feat(dashboard): add rate limiting to dashboard BFF`

---

### Task 4: Re-enable PatientService Rate Limiting

**Files:** Modify `src/Services/PatientService/PatientService.Api/Program.cs` — uncomment `app.UseRateLimiting();` (remove the TEMP DISABLED line)

- [ ] Uncomment line, commit: `fix(patient): re-enable rate limiting middleware`

---

### Task 5: NuGet Cache PVC + NuGet.config

**Files:**
- Create `cicd/tekton/volumes/nuget-cache-pvc.yaml` — PVC 10Gi ReadWriteMany
- Modify `cicd/tekton/tasks/dotnet-build.yaml` — change nuget-cache volume from `emptyDir` to `persistentVolumeClaim: claimName: nuget-cache-pvc`
- Create `NuGet.config` at repo root — nuget.org source

- [ ] Create 2 new + modify 1, commit: `feat(ci): add persistent NuGet cache PVC and NuGet.config`

---

### Task 6: Chiseled Images — 7 Services

**Files:** Modify 7 service Dockerfiles. Change:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
HEALTHCHECK CMD curl -f http://localhost:8080/health
```
to:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra AS final
# K8s probes handle health checks — no Docker HEALTHCHECK needed
```

- [ ] Modify 7 Dockerfiles, commit: `feat(docker): migrate 7 services to chiseled-extra base images`

---

### Task 7: Chiseled Images — ApiGateway + Dashboard BFF

**Files:** Modify 2 Dockerfiles (same pattern as Task 6).

- [ ] Modify 2 Dockerfiles, commit: `feat(docker): migrate ApiGateway and Dashboard BFF to chiseled-extra`

---

### Task 8: Seccomp DaemonSet

**Files:**
- Create `k8s/base/seccomp-daemonset.yaml` — DaemonSet that copies profiles from ConfigMap to `/var/lib/kubelet/seccomp/`
- Modify `k8s/base/kustomization.yaml` — add `seccomp-daemonset.yaml` to resources

- [ ] Create + modify, commit: `feat(security): add seccomp DaemonSet to install custom profiles on nodes`

---

### Task 9: Activate Seccomp on Deployments

**Files:** Modify 9 deployment YAML files. Change:
```yaml
seccompProfile:
  type: RuntimeDefault
```
to:
```yaml
seccompProfile:
  type: Localhost
  localhostProfile: his-hope-dotnet-strict.json
```

Deployments:
1. `k8s/base/patient-service/deployment.yaml`
2. `k8s/base/identity-service/deployment.yaml`
3. `k8s/base/clinical-service/deployment.yaml`
4. `k8s/base/appointment-service/deployment.yaml`
5. `k8s/base/lab-service/deployment.yaml`
6. `k8s/base/billing-service/deployment.yaml`
7. `k8s/base/pharmacy-service/deployment.yaml`
8. `k8s/base/api-gateway/deployment.yaml`
9. `k8s/base/system-dashboard-bff/deployment.yaml`

- [ ] Modify 9 files, commit: `feat(security): activate custom seccomp profiles on all service deployments`

---

### Task 10: Build Verification

- [ ] `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj` — succeeds
- [ ] `dotnet test` — all 19 pass
- [ ] Dockerfiles: verify syntax with `docker build --dry-run` or manual review
- [ ] K8s manifests: verify YAML syntax
