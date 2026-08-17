# Unified Runtime Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the same His.Hope service binaries deployable on Docker Compose, VM/systemd or Windows Service, and Kubernetes/K3s through one validated configuration contract.

**Architecture:** A versioned canonical contract defines logical service, data, OIDC, resilience, and secret-provider keys. Thin adapters render that contract into Compose environment files, VM service environment files, and Kubernetes ConfigMap/SecretProviderClass references. Applications consume injected URLs and settings; they do not infer runtime from `localhost`, container names, or ClusterIP values.

**Tech Stack:** .NET 8 configuration/environment variables, PowerShell 7, JSON Schema, Docker Compose, Kustomize, systemd, Windows Service environment files, Vault Agent/CSI, SPIRE JWT workload identity, xUnit.

## Global Constraints

- Preserve existing user changes and modify only files listed in each task.
- Never commit a real password, token, certificate private key, Vault token, or connection string containing a secret.
- Production rendering must fail when a required secret source is missing or when a production endpoint uses `localhost`.
- Service-to-service URLs must use internal service DNS/FQDN and internal ports; host-published ports are ingress/developer-only.
- Optional observability dependencies must not block critical APIs when `*_REQUIRED=false`.
- Every task must finish with its focused validation before the next task starts.

---

### Task 1: Create the canonical runtime contract and schema validator

**Files:**
- Create: `config/runtime-contract.schema.json`
- Create: `config/runtime-contract.v1.json`
- Create: `config/environments/development.env.example`
- Create: `config/environments/staging.env.example`
- Create: `config/environments/production.env.example`
- Create: `scripts/config/validate-runtime-contract.ps1`
- Create: `scripts/config/validate-runtime-references.ps1`
- Test: `tests/Configuration/RuntimeContract.Tests.ps1`
- Modify: `.gitignore`

**Interfaces:**
- `validate-runtime-contract.ps1 -EnvironmentFile <path> -Runtime docker|vm|kubernetes -Strict`
- `validate-runtime-references.ps1 -EnvironmentFile <path> -Runtime docker|vm|kubernetes -ComposeFile <path> -Kustomization <path>`
- Exit code `0` only when required keys, URL rules, secret-source rules, and runtime references pass.

- [ ] **Step 1: Define the schema** with required keys for `HIS_HOPE_*`, OIDC, service URLs, data endpoints, resilience, observability flags, and secret provider metadata. Production requires HTTPS public origins and forbids `localhost` in internal service URLs.
- [ ] **Step 2: Add environment examples** containing only safe values. Use Compose DNS names for development, FQDN placeholders for VM/staging, and Kubernetes service DNS examples for production. Keep secrets as `__FROM_SECRET_PROVIDER__` markers that the validator rejects in rendered production output unless a provider reference exists.
- [ ] **Step 3: Implement PowerShell validation** without printing values. Validate URI scheme/host/port, duplicate logical endpoints, required runtime keys, and forbidden placeholder values (`postgres`, `changeme`, empty production secret references).
- [ ] **Step 4: Implement reference validation** that extracts endpoints from Compose/Kustomize and compares logical service names and ports with the canonical contract. Report missing, extra, and mismatched references in machine-readable JSON plus human-readable output.
- [ ] **Step 5: Add Pester tests** for valid Docker/VM/K3s inputs, missing keys, invalid URL, production localhost, duplicate endpoint, placeholder secret, and mismatched service port.
- [ ] **Step 6: Run focused validation**:

```powershell
pwsh -File scripts/config/validate-runtime-contract.ps1 -EnvironmentFile config/environments/development.env.example -Runtime docker -Strict
pwsh -File scripts/config/validate-runtime-contract.ps1 -EnvironmentFile config/environments/production.env.example -Runtime kubernetes -Strict
Invoke-Pester tests/Configuration/RuntimeContract.Tests.ps1 -Output Detailed
```

Expected: all tests pass; no secret value is printed.

---

### Task 2: Normalize backend and BFF endpoint consumption

**Files:**
- Create: `src/Shared/Configuration/His.Hope.Configuration/His.Hope.Configuration.csproj`
- Create: `src/Shared/Configuration/His.Hope.Configuration/ServiceEndpointOptions.cs`
- Create: `src/Shared/Configuration/His.Hope.Configuration/RuntimeConfigurationExtensions.cs`
- Create: `src/Shared/Configuration/His.Hope.Configuration.Tests/ServiceEndpointOptionsTests.cs`
- Modify: `src/Services/*/*Api/*.csproj` where shared project references are required
- Modify: `src/ApiGateway/Program.cs` and route configuration
- Modify: `src/Bff/*/Program.cs` and `src/Bff/SystemDashboard.Bff/Program.cs`
- Modify: relevant `appsettings.json` files only to remove endpoint defaults that conflict with the contract

**Interfaces:**
- `AddHisHopeRuntimeConfiguration(IConfiguration, string serviceName)` binds `SERVICE_*_URL`, `DATABASE_*_URL`, `REDIS_URL`, and `RABBITMQ_URL`.
- `ServiceEndpointOptions.GetRequired(string logicalName)` returns a validated absolute URI.
- Missing critical endpoint throws a startup configuration exception; optional endpoint returns unavailable state according to `*_REQUIRED`.

- [ ] **Step 1: Add tests** proving logical service URL binding, malformed URI rejection, production localhost rejection, and optional Prometheus/Elasticsearch behavior.
- [ ] **Step 2: Implement the shared configuration package** with environment-variable binding, normalized trailing slash behavior, and named options validation.
- [ ] **Step 3: Migrate gateway destinations** from hard-coded `http://identity-service`, `patientservice`, or host ports to logical endpoint keys while preserving current route names.
- [ ] **Step 4: Migrate BFF clients and SystemDashboard resource aggregation** to the shared endpoint options. Keep Docker Compose and K3s values external to code.
- [ ] **Step 5: Preserve local developer defaults only in `appsettings.Development.json`; reject those defaults when `HIS_HOPE_ENVIRONMENT=production`.
- [ ] **Step 6: Run focused .NET tests and build the affected projects.**

```powershell
dotnet test src/Shared/Configuration/His.Hope.Configuration.Tests/His.Hope.Configuration.Tests.csproj --no-restore
dotnet test src/Bff/SystemDashboard.Bff.Tests/SystemDashboard.Bff.Tests.csproj --no-restore
dotnet build src/ApiGateway/ApiGateway.csproj --no-restore
```

---

### Task 3: Render and validate the Docker Compose adapter

**Files:**
- Create: `docker/config/compose.runtime.env.example`
- Create: `docker/config/compose.runtime.env.ps1`
- Create: `scripts/config/validate-compose-stack.ps1`
- Modify: `docker/docker-compose.yml`
- Modify: `docker/docker-compose.spiffe.yml`
- Modify: `docker/docker-compose.identity-local-azure.yml`
- Modify: `docker/docker-compose.identity-production.yml`
- Test: `tests/Configuration/ComposeRuntime.Tests.ps1`

**Interfaces:**
- `compose.runtime.env.ps1 -Environment development|staging|production -OutputFile <path>` renders only non-secret values.
- `validate-compose-stack.ps1 -ComposeFile <path> -EnvironmentFile <path> -Strict` runs `docker compose config` and contract/reference validation.

- [ ] **Step 1: Replace duplicated service endpoint and dependency values** with `${SERVICE_*_URL}`, `${DATABASE_*_URL}`, `${REDIS_URL}`, and `${RABBITMQ_URL}` references.
- [ ] **Step 2: Keep host port publishing only in Compose port mappings**; ensure no container-to-container URL uses published host ports.
- [ ] **Step 3: Separate secret references** from non-secret environment values and retain existing Vault/Docker secret paths.
- [ ] **Step 4: Add Compose health/readiness checks** for critical services without using `depends_on` as health proof.
- [ ] **Step 5: Add tests** for rendered config, absence of literal production secrets, service DNS references, and `docker compose config` success.
- [ ] **Step 6: Run:**

```powershell
pwsh -File scripts/config/validate-compose-stack.ps1 -ComposeFile docker/docker-compose.yml -EnvironmentFile config/environments/development.env.example -Strict
docker compose -f docker/docker-compose.yml --env-file config/environments/development.env.example config --quiet
Invoke-Pester tests/Configuration/ComposeRuntime.Tests.ps1 -Output Detailed
```

---

### Task 4: Add VM adapters for systemd and Windows Service

**Files:**
- Create: `deploy/vm/runtime.env.example`
- Create: `deploy/vm/render-runtime-env.ps1`
- Create: `deploy/vm/systemd/his-hope-service@.service`
- Create: `deploy/vm/systemd/his-hope-service@.env.example`
- Create: `deploy/vm/windows/Install-HisHopeService.ps1`
- Create: `deploy/vm/windows/Validate-HisHopeService.ps1`
- Create: `scripts/config/validate-vm-runtime.ps1`
- Test: `tests/Configuration/VmRuntime.Tests.ps1`
- Modify: `docs/operations/deployment-guide.md`

**Interfaces:**
- `render-runtime-env.ps1 -ServiceName <name> -EnvironmentFile <path> -OutputDirectory <path>` writes a protected service environment file without secret values.
- Linux service template consumes `/etc/his-hope/<service>.env` with `EnvironmentFile`.
- Windows installer consumes an ACL-protected environment/secret directory and registers the same logical service name.

- [ ] **Step 1: Define VM inventory mapping** for internal FQDN/loopback endpoints and service ports, separate from public ingress ports.
- [ ] **Step 2: Implement protected env rendering** with Linux mode `0640`, service-account ownership, and Windows ACL validation.
- [ ] **Step 3: Add systemd template** with restart policy, readiness health command, `NoNewPrivileges`, and secret file path references.
- [ ] **Step 4: Add Windows Service installer/validator** without storing credentials in the service command line.
- [ ] **Step 5: Add tests** for endpoint rendering, forbidden localhost in production, file permission policy, and service-name consistency.
- [ ] **Step 6: Run VM dry-run validation** on Windows and Linux-compatible file checks; do not claim a live Linux systemd deployment from Windows.

---

### Task 5: Convert K3s base and overlays to the canonical contract

**Files:**
- Create: `k8s/base/runtime-contract-configmap.yaml`
- Create: `k8s/base/runtime-contract-rbac.yaml`
- Create: `k8s/overlays/dev/runtime-contract-patch.yaml`
- Create: `k8s/overlays/staging/runtime-contract-patch.yaml`
- Create: `k8s/overlays/prod/runtime-contract-patch.yaml`
- Create: `k8s/overlays/prod/runtime-secret-provider-class.yaml`
- Modify: `k8s/base/kustomization.yaml`
- Modify: `k8s/overlays/dev/kustomization.yaml`
- Modify: `k8s/overlays/staging/kustomization.yaml`
- Modify: `k8s/overlays/prod/kustomization.yaml`
- Modify: backend/BFF deployment manifests that currently hard-code endpoints
- Test: `scripts/config/validate-kustomize-runtime.ps1`

**Interfaces:**
- `configMap/data` contains canonical non-secret values.
- `SecretProviderClass` references the environment-specific Vault path and workload identity binding.
- Deployment checksum annotation changes whenever rendered runtime config changes.

- [ ] **Step 1: Add the runtime ConfigMap and environment patches** using Service DNS names, not ClusterIP or NodePort.
- [ ] **Step 2: Replace hard-coded connection strings and endpoint values** in base manifests with ConfigMap references and secret-provider references.
- [ ] **Step 3: Keep dev local patches explicit** for k3d/Windows exceptions, while staging/prod retain full network and workload identity policy.
- [ ] **Step 4: Add Vault CSI/SPIRE references** without embedding Vault token, client secret, private key, or database password.
- [ ] **Step 5: Add Kustomize validator** for duplicate ports, missing Service targets, missing SecretProviderClass references, and production localhost.
- [ ] **Step 6: Run:**

```powershell
kubectl kustomize k8s/overlays/dev | kubectl apply --dry-run=server -f -
kubectl kustomize k8s/overlays/staging | kubectl apply --dry-run=client -f -
kubectl kustomize k8s/overlays/prod | kubectl apply --dry-run=client -f -
pwsh -File scripts/config/validate-kustomize-runtime.ps1 -Overlay dev
```

---

### Task 6: Standardize Angular and mobile runtime configuration

**Files:**
- Create: `shared/frontend-foundation/src/lib/runtime/runtime-config.contract.ts`
- Create: `shared/frontend-foundation/src/lib/runtime/runtime-config.service.ts`
- Create: `shared/mobile-foundation/src/runtime/runtime-config.contract.ts`
- Create: `shared/mobile-foundation/src/runtime/runtime-config.service.ts`
- Modify: `admin-app/src/app/app.config.ts`
- Modify: `dashboard-app/src/app/app.config.ts`
- Modify: `src/Frontend/his-hope-app/src/app/app.config.ts`
- Modify: `mobile-app/public/runtime-config.js`
- Modify: `mobile-app/capacitor.config.ts`
- Test: existing foundation unit tests plus runtime config tests in each app

**Interfaces:**
- Web apps consume `/runtime-config.js` with the same logical keys and never use compile-time environment files for deploy-specific origins.
- Mobile consumes the same contract from Capacitor native config/build flavor and validates OIDC authority, API origin, and TLS policy at startup.
- `RuntimeConfigService.require()` returns an immutable validated config object.

- [ ] **Step 1: Add contract tests** for missing authority, invalid origin, HTTP production rejection, and app-specific redirect URI.
- [ ] **Step 2: Move admin, dashboard, and frontend app configuration** to the foundation runtime service while preserving existing route behavior.
- [ ] **Step 3: Normalize mobile Android/iOS runtime config** so API/OIDC origins are injected by build flavor, not hard-coded in app-specific services.
- [ ] **Step 4: Ensure `x-timezone`, locale, and API base URL behavior remain centralized in the shared foundation.**
- [ ] **Step 5: Run Angular builds, foundation tests, and mobile typecheck/build configuration tests.**

---

### Task 7: Add end-to-end drift, smoke, and rollback gates

**Files:**
- Create: `scripts/config/validate-all-runtimes.ps1`
- Create: `scripts/config/smoke-runtime-stack.ps1`
- Create: `scripts/config/compare-runtime-contracts.ps1`
- Create: `tests/Configuration/RuntimeMatrix.Tests.ps1`
- Modify: `docs/operations/deployment-guide.md`
- Modify: `docs/operations/k3s-deployment.md`
- Modify: `docs/operations/mobile-deployment.md`
- Modify: CI workflow files that build/deploy Docker/K3s artifacts

**Interfaces:**
- `validate-all-runtimes.ps1` runs schema, Compose, VM, and Kustomize gates and returns a summary with `PASS`, `FAIL`, `SKIPPED`, or `ENVIRONMENT_BLOCKED`.
- `smoke-runtime-stack.ps1` verifies OIDC discovery, login redirect shape, logout, service health, BFF API authentication, and frontend runtime config without printing credentials.
- `compare-runtime-contracts.ps1` reports semantic drift by logical key, not raw file ordering.

- [ ] **Step 1: Add a runtime matrix** covering Docker local, VM dry-run, K3s dev, and production manifest validation.
- [ ] **Step 2: Add smoke checks** for identity, gateway, all BFFs, frontend/admin/dashboard origins, Redis, RabbitMQ, and optional observability behavior.
- [ ] **Step 3: Add rollback checks** using image digest plus config checksum and verify that a previous pair can be selected without changing secret values.
- [ ] **Step 4: Update deployment runbooks** with the one flow: render → validate → deploy → rollout → smoke → rollback.
- [ ] **Step 5: Run the complete available gate set** and report unavailable live environments explicitly.

```powershell
pwsh -File scripts/config/validate-all-runtimes.ps1 -Environment development -Runtime docker
pwsh -File scripts/config/validate-all-runtimes.ps1 -Environment dev -Runtime kubernetes
pwsh -File scripts/config/compare-runtime-contracts.ps1 -Left docker -Right kubernetes
pwsh -File scripts/config/smoke-runtime-stack.ps1 -Runtime kubernetes -Namespace his-hope-dev
```

## Completion criteria

- All services consume logical endpoint keys rather than runtime-specific hostnames in application code.
- Docker Compose, VM dry-run, and K3s overlays pass the same contract/reference validator.
- No production rendered output contains plaintext secret or forbidden localhost endpoint.
- Angular admin/dashboard/frontend and mobile use the same runtime contract for OIDC/API origins.
- K3s rollout automatically changes when the runtime ConfigMap checksum changes.
- Critical service health/API smoke tests pass; optional dependencies may be reported unavailable without blocking the critical path.
- Documentation contains one canonical deployment workflow and explicit environment limitations.
