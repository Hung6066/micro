# K3s CI/CD DevSecOps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Biến K3s hiện tại thành nền tảng triển khai có kiểm soát theo GitOps, trong đó chỉ image đã kiểm thử, quét bảo mật, tạo SBOM, ký và được admission policy chấp nhận mới được promotion lên production.

**Architecture:** Tách rõ CI khỏi CD. GitHub Actions thực hiện build/test/security/supply-chain và mở promotion PR chỉ chứa image digest; Argo CD hoặc Flux đồng bộ GitOps repository vào từng environment. Admission policy trong cluster kiểm tra Pod Security, digest, signature, RBAC và resource limits trước khi workload được tạo.

**Tech Stack:** K3s/k3d, Kubernetes 1.35-compatible kubectl, GitHub Actions, Harbor, Kustomize, Argo CD hoặc Flux, Trivy, Syft, Cosign, Kyverno hoặc OPA Gatekeeper, Linkerd, Vault/CSI, SPIRE, Prometheus/Grafana/Alertmanager.

## Global Constraints

- Không xem cluster `k3d-his-hope` hiện tại là production HA; ba node đang cùng một Docker/WSL2 host.
- Không deploy hoặc restart production trong các task chuẩn bị; mọi thao tác cluster ban đầu chỉ được read-only.
- Không đọc, decode hoặc ghi secret value vào log CI, artifact hay tài liệu.
- Production image phải được tham chiếu bằng Harbor digest, không dùng `latest` hoặc tag mutable.
- Runtime namespace phải enforce Pod Security `restricted`; workload hạ tầng có exception riêng, có owner và lý do.
- Database migration phải chạy bằng job/deployer identity một lần trước rollout, không chạy cạnh tranh từ nhiều API replica.
- CI không giữ kubeconfig cluster-admin và không chạy `kubectl apply` trực tiếp vào production.
- Mọi gate phải báo rõ `pass`, `fail`, `skipped`, `unavailable` hoặc `environment-blocked`; không coi skipped là pass.
- Không promotion khi production còn `CrashLoopBackOff`, `Pending`, `ImagePullBackOff`, unavailable replica hoặc Linkerd control plane chưa healthy.

---

## Baseline đã xác nhận

Ngày 2026-08-06, context live là `k3d-his-hope`:

- K3s server `v1.35.5+k3s1`, kubectl client `v1.25.3`; version skew cần xử lý bằng toolchain pinning.
- Ba node đều Ready nhưng cùng chạy trên một Docker/WSL2 host, nên chưa có host-level HA.
- API server `/readyz` pass.
- Namespace `his-hope` và `his-hope-dev` đang enforce `privileged`, audit/warn `restricted`.
- Production có workload CrashLoop/NotReady: database continuity, lab, pharmacy và một số backend khác.
- Linkerd CNI có mặt nhưng destination, identity, proxy injector và viz control plane không healthy.
- Harbor, Trivy, Vault, CSI và SPIRE đã có trong cluster; cần chuyển từ “đã cài” sang evidence enforcement.
- Repo có quality/security workflows, SBOM và policy artifacts; chưa có Argo CD/Flux/Gatekeeper/Kyverno controller live.
- Ingress đang dùng Traefik cho Harbor, monitoring và application local hostnames.

Các baseline trên phải được lưu thành artifact trước khi sửa cluster:

```powershell
kubectl config current-context
kubectl version -o yaml
kubectl get nodes -o wide
kubectl get pods -A -o wide
kubectl get deploy -A
kubectl get events -A --sort-by=.lastTimestamp
kubectl get crd
kubectl get validatingwebhookconfiguration,mutatingwebhookconfiguration
kubectl get networkpolicy -A
kubectl get ingress -A
kubectl get namespace --show-labels
```

Không đưa output chứa secret data vào artifact.

## File ownership map

### Files sẽ tạo

- `.github/workflows/container-release.yml` — pipeline build, scan, SBOM, sign, attest và push image.
- `.github/workflows/gitops-promotion.yml` — tạo promotion PR cập nhật digest trong GitOps repository.
- `.github/workflows/k3s-backup-agent-rollout.yml` — protected Ansible check/apply path for K3s etcd-to-Azure backup agents.
- `k8s/policies/kyverno/` hoặc `k8s/policies/gatekeeper/` — admission policies và exception policy.
- `k8s/gitops/bootstrap/` — bootstrap controller và root applications nếu dùng Argo CD.
- `scripts/validate-k3s-release.ps1` — release gate read-only, không mutate cluster.
- `scripts/verify-image-attestations.ps1` — kiểm tra digest, signature và provenance.
- `scripts/validate-workflow-action-pins.py` — fail-closed check for immutable GitHub Actions commit refs.
- `scripts/validate-backup-agent-contract.py` — check systemd EnvironmentFile quoting and protected backup-agent rollout invariants.
- `docs/runbooks/k3s-release-rollback.vi.md` — rollback và incident handling.
- `docs/runbooks/k3s-backup-agent-rollout.vi.md` — approval, secret handling and runtime evidence for backup-agent rollout.

### Files sẽ cập nhật

- `.github/workflows/security-quality-gate.yml` — bổ sung IaC, manifest, secret và dependency gates.
- `.github/workflows/platform-quality-gates.yml` — nối contract, migration compatibility và E2E gates.
- `k8s/base/kustomization.yaml` — baseline labels, security context và resource defaults.
- `k8s/overlays/dev/kustomization.yaml` — dev-only configuration.
- `k8s/overlays/staging/kustomization.yaml` — staging promotion target.
- `k8s/overlays/prod/kustomization.yaml` — production digest-only target.
- `k8s/overlays/prod/image-digests.yaml` — nguồn digest duy nhất được promotion.
- `k8s/base/network-policies.yaml` — exact allow-list cho application/data paths.
- `k8s/linkerd/linkerd-control-plane.yaml`, `k8s/linkerd/server.yaml`, `k8s/linkerd/server-authorization.yaml` — sửa và kiểm thử mesh health/policy.
- `scripts/validate-production-image-signatures.ps1` — chuyển từ optional check sang fail-closed release gate.
- `scripts/validate-linkerd-spire-mtls-k3s.ps1` — xác nhận mTLS thực tế, không chỉ kiểm tra resource tồn tại.
- `scripts/validate-k8s-production-secrets.ps1` — kiểm tra secret reference/rotation mà không in secret value.

### Files cần đọc trước khi implement

- `docs/architecture/k3s-enterprise-production-upgrade.vi.md`
- `docs/architecture/production-gates.md`
- `docs/architecture/k3s-developer-technical-guide.vi.md`
- `docs/operations/k3s-production-deployment-runbook.vi.md`
- `docs/operations/failed-deployment-rollback.md`
- `docs/adr/011-image-digest-pinning.md`
- `docs/security/production-workload-identity-compose.md`
- `k8s/security/gatekeeper-constraints.yaml`
- `k8s/overlays/prod/image-digests.yaml`

---

## Task 1: Đóng baseline runtime và sửa P0 production

**Files:**

- Modify: deployment/config tương ứng với database-continuity, lab, pharmacy và backend outbox/Redis.
- Test: service-specific integration tests và `scripts/validate-reliability-platform.ps1`.
- Create: `docs/evidence/k3s/2026-08-06-runtime-baseline.md` nếu repo dùng evidence snapshot.

**Interfaces:**

- Consumes: live pod events/log metadata, Kustomize overlays, Vault/Redis/PostgreSQL configuration.
- Produces: mọi production deployment có readiness pass và không còn P0 CrashLoop.

- [x] Ghi baseline pod status, restart count, readiness condition và event reason; loại bỏ mọi secret value khỏi output.
- [ ] Sửa database-continuity bằng deployer/migration credential đúng ownership; runtime role không được tự sở hữu hoặc alter schema production.
- [ ] Tách migration/outbox index thành job chạy một lần, có `backoffLimit`, timeout và idempotent SQL.
- [ ] Xác định Redis failure là DNS, TCP, TLS CA, hostname/SAN, auth hay NetworkPolicy bằng probe không log credential.
- [ ] Sửa Redis TLS config và network policy theo service account/namespace/port.
- [ ] Chạy focused test cho từng service lỗi; expected: startup, readiness, Redis health và outbox initialization đều pass.
- [ ] Chạy `kubectl get pods -n his-hope` và xác nhận không còn P0 trạng thái lỗi trong production scope.

**Verification:**

```powershell
dotnet test tests/Services/LabService/ --configuration Release
dotnet test tests/Services/PharmacyService/ --configuration Release
pwsh ./scripts/validate-reliability-platform.ps1
kubectl get pods -n his-hope
kubectl get deploy -n his-hope -o custom-columns='NAME:.metadata.name,READY:.status.readyReplicas,AVAILABLE:.status.availableReplicas,UNAVAILABLE:.status.unavailableReplicas'
```

## Task 2: Chuẩn hóa K3s và host security

**Files:**

- Create/Modify: K3s server config ngoài repo hoặc infrastructure repository tương ứng.
- Modify: `docs/operations/k3s-deployment.md` và `docs/architecture/k3s-enterprise-production-upgrade.vi.md`.
- Test: `scripts/validate-production-ha-spire-k3s.ps1`.

**Interfaces:**

- Consumes: K3s server flags, node topology, backup/restore design.
- Produces: version-pinned, auditable K3s cluster profile.

- [x] Pin kubectl/helm/kustomize trong CI theo server minor version; không dùng client `v1.25.3` với server `v1.35.x`.
- [ ] Bật và kiểm chứng secrets encryption at rest, encryption key rotation procedure và audit logging.
- [x] Cấu hình CIS-related controls: `protect-kernel-defaults`, admission config, audit policy, kubelet timeout và terminated pod GC; static contract đã pass và runtime baseline đã kiểm chứng.
- [x] Xác định rõ cluster local/staging/prod; ghi rõ rằng k3d hiện tại không đáp ứng host-level HA.
- [ ] Thiết kế production topology trên các host/VM độc lập, backup control-plane datastore và restore drill.
- [x] Tạo node labels/taints cho system, data và application workloads; đặt resource requests/limits; Ansible template/inventory và rendered workload contract đã pass.

**Verification:**

```powershell
kubectl version -o yaml
kubectl get nodes -o wide
kubectl get --raw='/readyz?verbose'
pwsh ./scripts/validate-production-ha-spire-k3s.ps1
```

## Task 3: Sửa Linkerd và xác nhận mTLS thực tế

**Files:**

- Modify: `k8s/linkerd/linkerd-control-plane.yaml`.
- Modify: `k8s/linkerd/server.yaml`.
- Modify: `k8s/linkerd/server-authorization.yaml`.
- Modify: `scripts/configure-linkerd-cni-k3s.ps1`.
- Modify: `scripts/configure-linkerd-ha-k3s.ps1`.
- Test: `scripts/validate-linkerd-spire-mtls-k3s.ps1`.

**Interfaces:**

- Consumes: K3s CNI behavior, Linkerd proxy/CNI versions, SPIRE identity.
- Produces: healthy Linkerd control plane and tested service-to-service authorization.

- [x] Thu thập init container reason của `linkerd-destination`, `linkerd-identity` và `linkerd-proxy-injector`.
- [ ] Pin Linkerd images bằng digest hoặc approved immutable version; không dùng `edge` image trong production.
- [x] Sửa `linkerd-network-validator` failure và kiểm tra CNI ordering trên K3s.
- [x] Xác nhận injector tạo proxy cho một service canary.
- [ ] Viết negative test: workload không được phép không gọi được service/data path. (Đã có protected E2E probe; cần chạy với grpcurl image digest được phê duyệt.)
- [x] Viết positive test cho gateway → BFF → backend và backend → Redis/PostgreSQL theo policy.
- [ ] Chỉ bật Linkerd admission enforcement sau khi control plane và mTLS probe pass ổn định.

**Verification:**

```powershell
linkerd check
linkerd viz check
pwsh ./scripts/validate-linkerd-spire-mtls-k3s.ps1
kubectl get pods -n linkerd
kubectl get pods -n linkerd-viz
```

## Task 4: Đưa application namespace về Restricted

**Files:**

- Modify: `k8s/base/namespace.yaml` hoặc manifest namespace tương ứng.
- Modify: `k8s/base/*-deployment.yaml` cho từng workload cần security context.
- Create: `k8s/policies/namespace-security-exceptions.yaml`.
- Test: `scripts/validate-k8s-release.ps1` và policy engine test.

**Interfaces:**

- Consumes: existing SPIRE, Vault CSI, Linkerd and database exceptions.
- Produces: app workloads pass `restricted` without weakening cluster-wide policy.

- [ ] Đặt `pod-security.kubernetes.io/enforce=restricted` cho `his-hope` và `his-hope-dev` theo rollout từng nhóm.
- [ ] Giữ exception riêng cho SPIRE/CSI/system workloads, không nâng cả namespace thành `privileged`.
- [ ] Bắt buộc `runAsNonRoot`, `allowPrivilegeEscalation=false`, `drop: [ALL]`, `seccompProfile: RuntimeDefault`.
- [ ] Bắt buộc read-only root filesystem nếu workload không cần writable path; khai báo `emptyDir` riêng nếu cần.
- [ ] Xóa `automountServiceAccountToken` khỏi workload không gọi Kubernetes API.
- [ ] Review riêng `systemdashboard-bff` vì đang có Kubernetes integration và service token.
- [x] Kiểm thử create dry-run với pod vi phạm; expected: admission reject.

## Task 5: Cài admission policy fail-closed

**Files:**

- Create: `k8s/policies/kyverno/` hoặc `k8s/policies/gatekeeper/`.
- Modify: `k8s/security/gatekeeper-constraints.yaml` nếu chọn Gatekeeper.
- Create: `scripts/verify-admission-policy.ps1`.
- Test: `tests/k8s/admission-policy.tests.ps1` hoặc policy test framework tương ứng.

**Interfaces:**

- Consumes: rendered prod manifests and Harbor/Cosign identity rules.
- Produces: admission rules that reject unsafe manifests before scheduling.

- [ ] Chọn duy nhất một enforcement engine cho application policy để tránh hai nguồn sự thật.
- [ ] Cài engine vào staging trước; production chỉ cài sau khi backup/rollback verified.
- [ ] Rule 1: chỉ cho phép image từ Harbor approved registry.
- [ ] Rule 2: bắt buộc digest, cấm `latest` và mutable tags.
- [ ] Rule 3: bắt buộc signature identity/issuer đúng GitHub Actions release workflow.
- [ ] Rule 4: bắt buộc resource requests/limits và non-root security context.
- [ ] Rule 5: cấm hostPID, hostNetwork, privileged, hostPath ngoài exception.
- [ ] Rule 6: reject ServiceAccount token auto-mount ngoài allow-list.
- [x] Test cả reject case và approved case; không chỉ test resource tồn tại.

## Task 6: Chuẩn hóa CI security pipeline

**Files:**

- Modify: `.github/workflows/security-quality-gate.yml`.
- Modify: `.github/workflows/platform-quality-gates.yml`.
- Create: `.github/workflows/container-release.yml`.
- Create: `scripts/validate-kustomize-release.ps1`.
- Create: `scripts/verify-image-attestations.ps1`.

**Interfaces:**

- Consumes: source commit, Dockerfiles, Kustomize overlays, Harbor credentials via OIDC/short-lived secret.
- Produces: immutable image digest, SBOM, vulnerability report, signature and provenance.

- [x] Pin runner tool versions: kubectl, kustomize, helm, trivy, syft, cosign.
- [ ] Chạy `dotnet restore/build/test`, frontend lint/build/test, contract test và E2E gate theo đúng config.
- [ ] Scan source/dependencies bằng secret scanner, SAST/SCA và Trivy filesystem/IaC.
- [x] Render `dev`, `staging`, `prod`; validate schema và policy trước khi build image.
- [x] Build image theo commit SHA, không dùng mutable release tag làm deployment reference.
- [x] Generate SBOM CycloneDX/SPDX và upload artifact retention policy.
- [x] Trivy image scan; fail theo policy HIGH/CRITICAL và ghi rõ unfixed exception nếu có.
- [x] Cosign sign bằng GitHub OIDC keyless identity hoặc KMS key; tạo provenance attestation.
- [x] Push vào Harbor sau khi quality/security gate pass; release job dùng protected `production` environment và CA chain secret trước login/push.
- [x] Ghi digest vào job output và artifact; không truyền password tĩnh qua command line.

**Verification:**

```powershell
pwsh ./scripts/validate-kustomize-release.ps1 -OverlayPath k8s/overlays/prod
pwsh ./scripts/verify-image-attestations.ps1 -ImageRef '<harbor-image>@sha256:<digest>'
```

## Task 7: Thiết lập GitOps promotion

**Files:**

- Create: `k8s/gitops/bootstrap/`.
- Create: GitOps environment overlays hoặc repository tương ứng với `dev`, `staging`, `prod`.
- Create: `.github/workflows/gitops-promotion.yml`.
- Modify: `k8s/overlays/prod/image-digests.yaml`.
- Modify: `docs/operations/k3s-production-deployment-runbook.vi.md`.

**Interfaces:**

- Consumes: release digest from Task 6.
- Produces: reviewed Git commit that Argo CD/Flux can synchronize.

- [ ] Cài Argo CD hoặc Flux trong staging; không cài cả hai làm deployment owner.
- [ ] Tạo application per environment với namespace allow-list và project RBAC.
- [x] CI mở PR thay đổi duy nhất digest/config release; không apply trực tiếp cluster.
- [ ] Bật protected branch và required reviewers cho production GitOps path.
- [ ] Cấu hình sync wave: infrastructure → migrations → services → ingress.
- [ ] Cấu hình health check cho Deployment, Job, Service, Linkerd và migration status.
- [ ] Cấu hình sync timeout và rollback procedure về digest trước đó.
- [ ] Tách credentials: CI registry push, GitOps read-only repo, Argo/Flux cluster RBAC.

## Task 8: Migration, rollout và rollback an toàn

**Files:**

- Modify: migration job manifests dưới `k8s/jobs/` hoặc service migration path hiện hữu.
- Create: `k8s/jobs/production-migration-job.yaml` nếu chưa có job chuẩn.
- Modify: `docs/runbooks/failed-deployment-rollback.md`.
- Create: `docs/runbooks/k3s-release-rollback.vi.md`.
- Test: migration compatibility và rollback test.

**Interfaces:**

- Consumes: signed image digest and GitOps sync.
- Produces: backward-compatible schema rollout and recoverable application release.

- [ ] Áp dụng expand → deploy compatible code → backfill → contract schema migration.
- [ ] Chặn destructive migration trong cùng release với API binary chưa compatible.
- [ ] Job migration dùng identity riêng, lock/idempotency và observable exit code.
- [ ] Rollout canary cho một BFF/service trước khi mở rộng.
- [ ] Smoke test readiness, health, OIDC, 401/403, facility isolation và Redis/PostgreSQL.
- [ ] Nếu SLO fail, rollback application digest; không rollback schema destructive.
- [ ] Ghi lại release commit, image digest, migration version và rollback evidence.

## Task 9: Observability và DORA gates

**Files:**

- Modify: `k8s/observability/k3s-observability.yaml`.
- Modify: `k8s/observability/production-alertmanager-config.yaml`.
- Modify: `k8s/observability/grafana-identity-slo-dashboard.json`.
- Modify: `docs/operations/slo-error-budget-policy.md`.
- Test: synthetic monitor và alert delivery.

**Interfaces:**

- Consumes: deployment metadata, Prometheus metrics, OpenTelemetry traces, Linkerd metrics.
- Produces: automatic release health signal and measurable delivery metrics.

- [x] Alert CrashLoop, unavailable replicas, failed migration, Redis TLS failure, Vault auth failure và admission rejection.
- [x] Gắn release SHA/digest vào metric/log/traces.
- [x] Đo availability, p95/p99 latency, error rate, deployment frequency, lead time, change failure rate và MTTR (DORA collector chạy định kỳ từ GitHub promotion runs; số liệu live cần workflow chạy lần đầu).
- [x] Synthetic monitor phải chạy qua ingress và thực hiện authorization negative path.
- [x] Đặt error-budget gate: production promotion bị hold khi burn rate vượt ngưỡng.
- [ ] Test Alertmanager notification end-to-end, không chỉ kiểm tra ConfigMap (đã có protected workflow/correlation probe; còn cần provision receiver secret và chạy test thực tế).

## Task 10: Backup, DR và go-live evidence

**Files:**

- Modify: `docs/operations/disaster-recovery.md`.
- Modify: `k8s/production-ha/backup-object-store.yaml`.
- Modify: `k8s/production-ha/cnpg-barman-object-store.yaml`.
- Create: `scripts/verify-k3s-go-live.ps1`.
- Create: `docs/evidence/k3s-go-live-checklist.vi.md`.

**Interfaces:**

- Consumes: backup systems, GitOps repo, image registry and observability evidence.
- Produces: signed go-live decision with pass/fail/skipped/unavailable status.

- [ ] Test PostgreSQL backup and restore to isolated namespace.
- [ ] Test Vault recovery/unseal and secret rotation without exposing values.
- [ ] Test Harbor metadata/image availability from a clean node.
- [ ] Test control-plane rebuild from documented infrastructure state.
- [ ] Test application restore followed by migration and smoke suite.
- [ ] Record measured RTO/RPO, not only procedure existence.
- [x] Go-live report phải fail nếu còn P0/P1 blocker hoặc evidence unavailable.

---

## Release gate hợp nhất

Tạo `scripts/validate-k3s-release.ps1` với các phase và exit code sau:

```text
10  toolchain/version mismatch
20  rendered manifest/schema failure
30  security/policy failure
40  image digest/signature/attestation failure
50  test/contract/E2E failure
60  migration failure
70  cluster health/readiness failure
80  SLO/smoke failure
0   all required gates passed
```

Script phải xuất JSON evidence có các field:

```json
{
  "release": "<git-sha>",
  "environment": "staging|production",
  "imageDigests": [],
  "checks": [],
  "status": "pass|fail|skipped|unavailable|environment-blocked",
  "startedAtUtc": "",
  "finishedAtUtc": ""
}
```

Không ghi kubeconfig, token, password, private key hoặc secret value vào JSON.

## Definition of Done

- [ ] P0 runtime failures đã được sửa và có test evidence.
- [ ] K3s toolchain được pin và version skew đã xử lý.
- [ ] `his-hope`/`his-hope-dev` không còn enforce `privileged` cho application workload.
- [ ] Linkerd control plane healthy và mTLS negative/positive tests pass.
- [ ] Admission policy reject được privileged, unsigned, mutable-tag và out-of-registry image.
- [x] CI tạo image digest, SBOM, vulnerability report, signature và provenance.
- [ ] Production promotion chỉ xảy ra qua reviewed GitOps commit.
- [ ] Migration job chạy một lần, idempotent và có rollback-compatible strategy.
- [ ] Post-deploy authz, facility isolation, OIDC và smoke tests pass.
- [ ] Backup/restore và DR drill có measured RTO/RPO.
- [ ] Go-live evidence không có trạng thái `skipped` hoặc `environment-blocked` cho required gate.

## Thứ tự thực thi đề xuất

1. Task 1 — sửa runtime P0.
2. Task 2 — chuẩn hóa K3s/toolchain/host boundary.
3. Task 3 — khôi phục Linkerd.
4. Task 4 — chuyển Pod Security theo namespace/workload.
5. Task 5 — admission policy staging rồi production.
6. Task 6 — hoàn thiện CI supply chain.
7. Task 7 — cài GitOps và promotion flow.
8. Task 8 — migration/progressive rollout/rollback.
9. Task 9 — observability và error-budget gates.
10. Task 10 — DR drill và go-live approval.

Không nên bắt đầu Task 7 bằng cách cho CI chạy `kubectl apply` trực tiếp; điều đó sẽ bỏ qua chính separation-of-duties và auditability mà GitOps cần cung cấp.

---

## Cập nhật thực thi 2026-08-11: release GitHub, scan và Git mirror

### Trạng thái đã quan sát

- Run GitHub Actions mới nhất cho commit `b593547bd186b38da9284c9b5fea0169eea76374` (`scope filesystem scan away from raw kubernetes manifests`) là **failure**: [Container Release Supply Chain](https://github.com/Hung6066/micro/actions/runs/31473684021), [Identity Service Security Scan](https://github.com/Hung6066/micro/actions/runs/31473684045), và [Frontend Foundation](https://github.com/Hung6066/micro/actions/runs/31473683969). Do Container Release dừng ở preflight, run này chưa tạo digest được ký, attestation hay GitOps promotion hợp lệ.
- Trivy preflight có HIGH finding thật: migration Job CockroachDB thiếu security context (`KSV-0118`) và các Dockerfile thiếu `USER` non-root (`DS-0002`). Không được làm yếu scan hoặc bỏ qua các file này để release xanh.
- Kubeconfig production trong workspace trỏ tới `https://127.0.0.1:16443`, nhưng API từ chối kết nối và Docker Desktop daemon cũng không chạy từ workstation này. Vì vậy trạng thái live của K3s, Argo CD và Gitea Git mirror là **unavailable**, không phải pass hay fail cluster.
- Static source cho thấy Gitea là `ClusterIP` nội bộ, một replica dùng SQLite/PVC và image tag mutable `gitea/gitea:1.24.6-rootless`. Các Argo Application tham chiếu repository nội bộ nhưng `targetRevision` là branch feature mutable. Cấu hình này chưa đủ bằng chứng cho production promotion.

### Task 11: Khôi phục release preflight và kiểm chứng supply-chain artifact

**Files:**

- Modify: Dockerfile bị Trivy báo `DS-0002` và migration Job bị báo `KSV-0118`.
- Modify: `.github/workflows/container-release.yml` chỉ nếu cần thiết để quét đúng nguồn build; không thêm blanket exclusion cho workload/migration manifest.
- Create: redacted release evidence artifact chứa run URL, commit, digest, SBOM URI, scan result, signature verification và provenance verification.

**Interfaces:**

- Consumes: commit SHA, Dockerfile/migration manifest, GitHub Actions OIDC, Harbor.
- Produces: immutable Harbor digest chỉ được promotion sau khi scan, SBOM, signature và SLSA provenance đều pass.

- [ ] Khắc phục từng finding HIGH theo nguyên nhân: Job phải đáp ứng `runAsNonRoot`, `allowPrivilegeEscalation: false`, capability drop, seccomp và resources; Dockerfile phải chạy runtime bằng user non-root có thể ghi vào các thư mục thực sự cần thiết.
- [ ] Chạy lại Trivy với cùng scope; mỗi exception phải có owner, expiry và lý do, không dùng ignore không thời hạn.
- [ ] Sau khi preflight pass, kiểm tra run release có digest Harbor, SBOM, vulnerability report, `cosign verify` và `cosign verify-attestation` trên **cùng digest**.
- [ ] Chỉ cho phép workflow promotion mở PR sau khi artifact evidence có trạng thái `pass`; không chấp nhận tag hay digest từ run failure.

**Verification:**

```powershell
gh run view <container-release-run-id> --json conclusion,url,headSha
pwsh ./scripts/validate-container-build-contract.ps1
pwsh ./scripts/validate-k3s-release.ps1 -Environment staging
pwsh ./scripts/verify-image-attestations.ps1 -ImageRef '<harbor-image>@sha256:<digest>'
```

### Task 12: Chuyển Git mirror thành GitOps source đáng tin cậy

**Files:**

- Modify: `k8s/git-mirror/deployment.yaml`, `k8s/git-mirror/pvc.yaml`, `k8s/git-mirror/network-policy.yaml`.
- Modify: `k8s/gitops/bootstrap/app-project.yaml`, `k8s/gitops/bootstrap/applications.yaml`.
- Create: `scripts/verify-git-mirror.ps1` và runbook Git mirror recovery/promotion.

**Interfaces:**

- Consumes: approved GitHub commit/PR, internal Gitea repository and Argo CD repo-server.
- Produces: a verified mirror commit that Argo CD can fetch, auditable source revision and no unreviewed branch deployment.

- [ ] Pin Gitea image by digest; retain rootless/non-root constraints and verify its writable paths, backup and restore procedure.
- [ ] Replace production `targetRevision` feature branch with a reviewed immutable commit SHA or protected release ref. Promotion PR phải thay đổi revision/digest tối thiểu và có reviewer.
- [ ] Thiết lập mirror sync từ GitHub theo least privilege (deploy key/token không ghi log), xác minh mirror HEAD bằng commit SHA và chặn sync khi SHA không khớp artifact release.
- [ ] Giới hạn network Gitea/Argo repo-server theo DNS, namespace và port thực tế; không expose Gitea public nếu không có use case cần thiết.
- [ ] Chạy staging negative test: source branch không được duyệt, revision thiếu, repository unreachable và SHA mismatch phải làm Application `Degraded`/sync fail, không fallback sang branch khác.
- [ ] Chạy staging positive test: Argo CD repo-server fetch được mirror, render đúng revision và reports `Synced`/`Healthy`; ghi lại commit SHA và timestamp vào evidence.

**Live read-only verification (chỉ sau khi kubeconfig/tunnel hoạt động):**

```powershell
kubectl --kubeconfig artifacts/kubeconfig-production.yaml get pods,svc,pvc -n git-mirror
kubectl --kubeconfig artifacts/kubeconfig-production.yaml get applications.argoproj.io -n argocd
kubectl --kubeconfig artifacts/kubeconfig-production.yaml get application his-hope-production -n argocd -o jsonpath='{.status.sync.status}{" "}{.status.health.status}{"\n"}'
pwsh ./scripts/verify-git-mirror.ps1 -ExpectedRevision '<approved-commit-sha>'
```

### Gate giao release/GitOps

| Gate | Trạng thái 2026-08-11 | Điều kiện chuyển trạng thái |
| --- | --- | --- |
| GitHub container preflight | fail | Khắc phục toàn bộ HIGH finding hoặc exception hết hạn có phê duyệt. |
| Signed image/SBOM/provenance | unavailable | Release preflight pass và artifact cùng digest được verify. |
| GitHub promotion PR | unavailable | Chỉ mở sau release artifact pass. |
| Git mirror live revision | unavailable | Khôi phục API/tunnel, sau đó verify mirror HEAD và Argo repo-server. |
| Argo CD sync/health | unavailable | Argo CD và Application phải tồn tại, revision immutable fetch thành công. |
| Production rollout | blocked | Tất cả gate trên, Pod Security, signature admission, storage/restore và go-live gate đều pass. |

### Thứ tự bổ sung

11. Task 11 — khôi phục release preflight và xác minh artifact cùng digest.
12. Task 12 — staging Git mirror/Argo immutable-revision tests.
13. Chỉ sau đó mới tiếp tục Task 7 production promotion, Task 10 DR/go-live và workflow mutation được bảo vệ.

### Sổ cái evidence và trạng thái kế hoạch

`artifacts/evidence/go-live-latest.json` (2026-08-10T07:59:08Z) là snapshot go-live live mới nhất có trong workspace. Nó chứng minh API, 5 nodes, application health, Linkerd, immutable render và secret scan đều pass tại thời điểm chạy; nó **không** chứng minh production ready. Mọi lần review phải chạy lại gate qua kubeconfig/tunnel đang reachable và thay thế snapshot này, không copy trạng thái cũ.

| Task | Trạng thái evidence | Bằng chứ / blocker còn lại |
| --- | --- | --- |
| 1. Runtime P0 | partial | Application health đã pass trong snapshot, nhưng image drift và runtime contract mapping Appointment còn fail. |
| 2. K3s/host security | partial | Host hardening có evidence; toolchain phải dùng kubectl 1.35 và DR/topology cần drill. |
| 3. Linkerd/mTLS | partial | Linkerd control plane pass; negative mTLS/admission rollout chưa có production evidence. |
| 4. Restricted Pod Security | fail | Live `his-hope` vẫn `enforce=privileged`; source hardening không thay thế controlled rollout. |
| 5. Admission policy | blocked | Gatekeeper có mặt, nhưng signature provider/Ratify hoặc Sigstore Policy Controller chưa ready. |
| 6. CI supply chain | partial | Contracts/source pass nhưng GitHub container preflight mới nhất fail (Task 11). |
| 7. GitOps promotion | blocked | Argo CD chưa có live evidence; Git mirror/revision immutable phải qua Task 12. |
| 8. Migration/rollback | partial | Source contract có; production migration execution và restore evidence còn unavailable. |
| 9. Observability/DORA | partial | OTEL readiness pass; Alertmanager delivery thực tế và release health evidence chưa đủ. |
| 10. Backup/DR | fail | Năm evidence drill bắt buộc (database, Vault, Harbor, control-plane, application restore) đều unavailable. |
| 11. Release preflight | fail | Trivy HIGH findings chặn run GitHub; chưa có signed artifact cùng digest. |
| 12. Git mirror integrity | unavailable | API endpoint local của kubeconfig không reachable trong workspace; không có live mirror HEAD/Argo fetch evidence. |

**Quy tắc ghi evidence:** artifact phải chứa thời gian UTC, git SHA/revision, environment, tool version và trạng thái chuẩn. Artifact chỉ từ source render không được dùng để đóng task runtime; `unavailable`, `blocked` và `skipped` luôn chặn production promotion.
