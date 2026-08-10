# Unified Runtime Observability and Secret Injection Implementation Plan

> **For agentic workers:** Execute task-by-task and preserve the validation gates below.

**Goal:** Make Docker Compose, VM/systemd, Windows Service, and Kubernetes/K3s use one application contract with environment-specific observability endpoints and secret injection.

**Architecture:** Application service endpoints remain in `config/runtime-contract.v1.json`. Platform dependencies are declared in a separate `config/platform-contract.v1.json` and validated independently. Each runtime adapter renders the same logical keys with runtime-specific DNS; secret values are injected by the runtime and never copied into the canonical contract.

**Tech Stack:** PowerShell validators, Docker Compose, ASP.NET configuration, Kustomize, WSL2 Ubuntu/systemd, Prometheus, Loki, Jaeger, OpenTelemetry, Vault/SPIRE.

## Global Constraints

- Do not remove infrastructure services from any deployment stack.
- Do not commit secret values or reuse one static secret across runtimes.
- Report unavailable live Linux validation as `ENVIRONMENT_BLOCKED`.
- Preserve existing unrelated workspace changes.
- Validate contract, adapter rendering, smoke tests, and K3s runtime separately.

---

### Task 1: Separate application and platform contracts

**Files:**
- Create: `config/platform-contract.v1.json`
- Modify: `scripts/config/validate-runtime-references.ps1`
- Modify: `scripts/config/validate-all-runtimes.ps1`
- Test: `scripts/config/validate-runtime-contract.ps1` and reference validator output

- [ ] Define platform endpoint records for Prometheus, Loki, Jaeger, OTLP, Alertmanager, Grafana, Vault, Consul, Temporal, and supporting exporters using the existing runtime shape.
- [ ] Make the reference validator compare application services only against `runtime-contract.v1.json` and platform services only against `platform-contract.v1.json`.
- [ ] Keep infrastructure services in Compose/K3s; only move their validation ownership to the platform contract.
- [ ] Run both validators and require `missing=[]`, `mismatched=[]`, and `extra=[]` within each contract domain.

### Task 2: Normalize observability adapter keys

**Files:**
- Modify: `config/runtime-contract.v1.json`
- Modify: `config/environments/development.env.example`
- Modify: `config/environments/staging.env.example`
- Modify: `config/environments/production.env.example`
- Modify: `docker/config/compose.runtime.env.ps1`
- Modify: `deploy/vm/render-runtime-env.ps1`
- Modify: `k8s/base/runtime-contract-configmap.yaml`
- Modify: `k8s/overlays/dev/runtime-contract-patch.yaml`

- [ ] Render `OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT`, `OBSERVABILITY_PROMETHEUS_URL`, `OBSERVABILITY_LOKI_URL`, `OBSERVABILITY_JAEGER_URL`, and `OBSERVABILITY_ALERTMANAGER_URL` in all adapters.
- [ ] Use Compose service DNS, VM environment-provided FQDNs, and K3s monitoring namespace DNS without application-specific hostname branches.
- [ ] Update SystemDashboard BFF configuration binding to consume the normalized Loki/Jaeger/Prometheus keys.
- [ ] Validate rendered files contain no `localhost` in staging/production and no secret values.

### Task 3: Enforce environment-specific secret injection

**Files:**
- Modify: `config/runtime-contract.schema.json`
- Modify: `scripts/config/validate-runtime-contract.ps1`
- Modify: `docker/secrets/README.md`
- Modify: `deploy/vm/systemd/his-hope-service@.service`
- Modify: `k8s/vault/vault-csi-provider.yaml`
- Test: runtime validator output for docker, vm, and kubernetes

- [ ] Validate only secret reference keys in canonical environment files.
- [ ] Reject literal password/token values in production contract files.
- [ ] Require Compose secret files or provider references for development.
- [ ] Require protected VM env file references and document Linux permissions.
- [ ] Require K3s Vault CSI/SPIRE references without static Vault root token or database password.

### Task 4: Run real Docker Compose smoke test

**Files:**
- Create: `scripts/config/smoke-compose-observability.ps1`
- Modify: `scripts/config/validate-compose-stack.ps1` only if required by the test contract

- [ ] Render the development Compose environment.
- [ ] Start the minimum complete stack with Docker Compose and wait for health endpoints.
- [ ] Validate OIDC discovery, authenticated login, `/api/resources`, identity logs, request metrics, and all-service traces.
- [ ] Stop/remove only the test project created by the script and report the exact project name.

### Task 5: Run WSL2 systemd smoke test

**Files:**
- Create: `scripts/config/smoke-wsl-systemd.ps1`
- Modify: `deploy/vm/systemd/his-hope-service@.service` if the live unit needs a portability fix
- Modify: `docs/operations/deployment-guide.md`

- [ ] Start Ubuntu WSL2 and verify systemd is PID 1.
- [ ] Render a non-production VM environment file with secret references only.
- [ ] Install a temporary unit that runs the configured health command and has `Restart=always` and `NoNewPrivileges=yes`.
- [ ] Start, inspect status, restart, and stop the unit; remove only the temporary unit.
- [ ] Report `ENVIRONMENT_BLOCKED` if systemd is not PID 1 or Docker is unavailable inside WSL2.

### Task 6: K3s regression and final evidence

**Files:**
- Modify: `docs/operations/k3s-deployment.md`
- Modify: `docs/operations/deployment-guide.md`

- [ ] Render and validate the K3s overlay.
- [ ] Confirm runtime ConfigMap observability keys and Vault/SPIRE references.
- [ ] Run authenticated dashboard BFF checks for resources, logs, metrics, and traces.
- [ ] Record PASS/FAIL/BLOCKED for each runtime and leave unrelated production gates unchanged.

