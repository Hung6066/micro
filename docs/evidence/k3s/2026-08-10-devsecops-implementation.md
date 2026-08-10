# K3s DevSecOps implementation evidence — 2026-08-10

## Implemented in repository

- CI container release workflow builds by commit SHA, runs Trivy, emits CycloneDX SBOM, pushes Harbor, signs and attests with GitHub OIDC, then verifies the digest.
- GitOps promotion workflow changes only the canonical production digest component and opens a reviewed PR; it does not run `kubectl apply`.
- Unified release validator emits JSON with explicit statuses and tool/manifest/image/cluster exit-code mapping.
- Gatekeeper production policy bundle covers approved registries, digest-only images, restricted workload security context and resource requests/limits.
- Admission source validator and production Kustomize render gates are wired into the K3s workflow.
- Argo CD project/application bootstrap is present as a template; repository URL must be configured before installation.
- Rollback runbook documents digest rollback, migration safety, smoke tests and evidence requirements.
- Production PgBouncer sidecars are now digest-pinned through the canonical image-digests component.

## Verification performed locally

- `kubectl kustomize k8s/overlays/dev` — pass
- `kubectl kustomize k8s/overlays/staging` — pass
- `kubectl kustomize k8s/overlays/prod` — pass
- `scripts/verify-admission-policy.ps1` — pass
- `scripts/validate-k8s-release.ps1` — pass
- `dotnet build His.Hope.sln --no-restore --configuration Release` — pass, 0 errors; existing warnings remain
- `scripts/validate-k3s-release.ps1 -Environment prod` — image/manifest checks pass; live cluster check is `environment-blocked` without a reachable kubeconfig
- With `artifacts/kubeconfig-production.yaml`, the release gate reports cluster connectivity, 5 Ready nodes, application health and Linkerd control-plane health as `pass`.
- The required Pod Security gate correctly reports `fail` because `his-hope` is still labelled `pod-security.kubernetes.io/enforce=privileged`.

## Required before enabling production sync

- Configure Harbor registry/project and robot/OIDC secrets as GitHub environment secrets.
- Replace the Argo CD repository URL placeholders and install Argo CD in staging.
- Install/configure a Gatekeeper signature ExternalData provider (or approved Ratify deployment); the source policy is fail-closed but has not been applied to production.
- Run negative/positive admission tests in staging, then enable production sync approval.
- Complete HTTPS, Azure backup authentication, CSI storage and runtime Linkerd/observability gates.

## Public Harbor HTTPS follow-up

- DNS verification: `harbor.myduchospital.com` resolves to `172.16.102.100`.
- Source HAProxy configuration now targets public TCP/443 and forwards to Harbor HTTPS NodePort `30003` on `.10` and `.12`.
- Added `k8s/harbor/harbor-public-ingress.yaml` and `scripts/validate-harbor-public-https.ps1`.
- Current live gate is **fail/blocked**: VIP TCP/443 is closed, the public ingress and `harbor-public-tls` secret are absent, and the existing certificate is only for `harbor.his-hope.local` under the local CA.
- Do not cut over Harbor `externalURL` or enable production sync until a trusted certificate with SAN `harbor.myduchospital.com` is installed and `scripts/validate-harbor-public-https.ps1` returns no fail/blocked statuses.

## Vault PKI runtime follow-up

- Recovered a Vault operator token using 3/5 recovery keys from the production initialization artifact; the value is stored only at `D:\secure\his-hope\vault_operator_token`.
- Initialized Vault `pki` root and `pki_int` intermediate mounts; CA private keys remain inside Vault.
- Created least-privilege policy/role `cert-manager` and narrowly scoped PKI role `harbor-public` for `harbor.myduchospital.com`.
- Configured cert-manager secretless Kubernetes authentication using the `cert-manager` ServiceAccount; the static `cert-manager-vault-token` Secret was removed.
- Live `vault-issuer-harbor` is `Ready=True (VaultVerified)` and `harbor/harbor-public` is `Ready=True`, with secret `harbor-public-tls` and public ingress applied.
- HAProxy/Keepalived was rolled out successfully to `.13` and `.14`; both services are active and listen on TCP/443 and 6443.
- Public HTTPS validation now passes: DNS/VIP, TCP/443, ingress host/TLS secret, Harbor `/api/v2.0/health`, and registry `/v2/` (expected 401 without credentials).

## GitOps continuation — 2026-08-10

- The container release matrix now covers the production application set, including Patient/Clinical/Lab/Billing/Pharmacy BFFs and `database-continuity`; each entry has a checked-in Dockerfile and is built from the repository root so shared projects restore correctly.
- Production digest promotion now uses the same Harbor reference shape as CI (`harbor.his-hope.local:9443/his-hope/<image>`); rendered output contains no duplicated `his-hope/his-hope` path.
- Production namespace intent is now `pod-security.kubernetes.io/enforce=restricted`; this is a repository change and still requires a controlled cluster rollout after workload exceptions are reviewed.
- Current local gates: dev/staging/prod Kustomize render **pass**, admission source gate **pass**, Harbor path invariant **pass**, Dockerfile coverage **pass**.
- Production release gate with the explicit production kubeconfig: toolchain, digest pinning, manifest secret scan, cluster connectivity, five Ready nodes, application health and Linkerd control plane **pass**; live Pod Security **fail** because the running namespace remains labelled `privileged` and has not yet been rolled out to the new restricted label.
- BFF project builds for Patient/Clinical/Lab/Billing/Pharmacy and database-continuity now **pass** under the pinned .NET SDK `8.0.319` after refreshing stale local assets; CI still performs a clean restore before image publication.
- Gatekeeper is running live (`gatekeeper-system` has healthy audit/controller pods), but the live cluster currently exposes only the older digest constraint; the new approved-registry/restricted/resources bundle is now owned by the GitOps policy Application and still needs a reviewed sync.
- The live cluster has no Argo CD namespace/Application resources, so GitOps reconciliation is not yet evidenced. Normal build/promotion CI continues to avoid direct production `kubectl apply`; only protected, explicit bootstrap workflows have an apply path.
- The restricted-workload readiness gate now reports **6 non-compliant containers** after hardening application/BFF containers and SPIRE sidecars. The remaining data-store containers and node-level seccomp profile installer require namespace isolation or an explicitly reviewed system exception.
- Added GitOps-managed `his-hope-data` and `his-hope-system` boundary namespaces with owner, purpose and expiry metadata. They are intentionally not populated yet; moving stateful workloads requires DNS, NetworkPolicy, PVC and restore-drill validation.
- Added migration-ready `data-plane` and `system-plane` GitOps applications. Their standalone renders pass namespace and digest checks (data: 4 images, system: 2 images); sync remains manual until endpoint and restore evidence is complete.
- Live verification confirms `his-hope-data`, `his-hope-system` and `argocd` are not present yet; production release gate remains **fail** only at the existing `his-hope` Pod Security label (`privileged`).
- Added checksum-verified Argo CD bootstrap at [bootstrap-argocd.ps1](../../../scripts/bootstrap-argocd.ps1) and the staging runbook [argocd-bootstrap.vi.md](../../runbooks/argocd-bootstrap.vi.md); it is an operator-controlled install and has not been run against production.
- Remaining live-gate report: signature provider **blocked** (no Gatekeeper ExternalData/Ratify), Pod Security **fail**, CSI storage **blocked** (local-path only), GitOps controller **blocked** (Argo CD absent), observability **pass**, Azure backup **blocked** from this workstation.
- Added `scripts/validate-storage-backup-contract.ps1` to make the storage/backup prerequisites explicit in CI. The current repository evidence is: CNPG manifest/schedule **pass**, CNPG object-store naming **pass**, restore safety **pass**, but Azure destination injection, replicated CSI storage, Velero account configuration and `VolumeSnapshotClass` are **blocked** until the production platform values and storage driver are supplied. The validator emits `artifacts/evidence/storage-backup-contract-current.json` and exits `70` for those unresolved environment gates.
- The storage contract now also scans production PVC patches; it reports `production-pvc-storage=blocked` while RabbitMQ, Redis and PostgreSQL still select `local-path`. This is intentional fail-closed behavior; changing those patches before a CSI restore drill would make workloads unschedulable or create an unverified data migration.
- Verification-loop snapshot: `dotnet build His.Hope.sln --no-restore --configuration Release` passed with 0 errors (existing warnings remain); all PowerShell scripts parsed successfully; security grep found no credential literal, with pre-existing `console.log` in frontend RUM code noted for frontend cleanup. Full `dotnet test His.Hope.sln --no-build` is not green in this workstation run: unit/contract suites passed, while integration suites and cross-service Testcontainers tests failed on PostgreSQL/runtime connection setup. Test fixtures now share a single lifecycle and explicitly probe the mapped PostgreSQL host port; the targeted run confirms Docker's internal readiness succeeds but Windows host port `127.0.0.1:<mapped>` is unreachable. This is recorded as environment-blocked, not a release pass.
- Corrected the CNPG backup validator defaults to the Azure production names (`spire-postgres-azure-store` and `spire-postgres-azure-backup`) and removed the mutable `:latest` tag from the MinIO client reference while retaining its digest pin.
- Added a reviewed Sigstore Policy Controller ClusterImagePolicy for the private Harbor image glob, using the non-secret Cosign public key from the secure key inventory. The policy is rendered by k8s/gitops/signature-policy and has a manual Argo Application; namespace enforcement remains deliberately disabled until the controller is installed and staging negative/positive admission tests pass.
- Current repository checks after this change: dev/staging/prod renders pass, admission source gate pass, GitOps bootstrap render pass, signature-policy render pass. Live gate status remains unchanged and is recorded above.
- The signature policy records the approved GitHub OIDC keyless identity (`container-release.yml` on `main` or semver tags), issuer and SLSA provenance predicate; identity rotation must update the policy and run a staging admission test before production activation.
- Pod Security hardening now removes the six remaining rendered exceptions: PostgreSQL and Redis explicitly run non-root with privilege escalation disabled; RabbitMQ runs as UID/GID 999 without the root permission init container; and the node-level seccomp installer is owned only by the `his-hope-system` GitOps plane. `python scripts/check-restricted-workloads.py artifacts/k8s/prod.yaml` now reports `TOTAL_NONCOMPLIANT_CONTAINERS=0`. The live namespace label still requires a controlled rollout.
- Added `scripts/rollout-pod-security-production.ps1` with a fail-closed preflight and explicit `-Apply` switch. The current live preflight passes render/checker and 5 Ready nodes, then stops because `his-hope-system` does not yet exist; this confirms the system boundary must be bootstrapped before changing the `his-hope` label.
- Added `scripts/bootstrap-k3s-security-boundaries.ps1`. Its production dry-run renders the boundary manifests and reports both `his-hope-data` and `his-hope-system` as pending without applying anything; `-Apply` is the only mutation path and verifies the resulting Pod Security labels.
- Argo CD bootstrap verification: HA manifest `v3.4.1` SHA-256 `2e6211d381b84394b5a7c98f5b285d24d48cbe2a2917c4181623d825109bd088` was downloaded and checksum-verified; `-WhatIf` performed no cluster mutation. The bootstrap GitOps render and namespace contract validator both pass. Production bootstrap is now explicitly guarded by `-AllowProduction`; no staging kubeconfig exists in this workspace, and live `argocd` remains absent, so the GitOps controller gate is still blocked pending a staging installation/change window.
- Added `scripts/validate-k3s-go-live.ps1` and `docs/evidence/k3s-go-live-checklist.vi.md`. The first explicit production run reaches the API, confirms 5 Ready nodes, healthy application pods and Linkerd, but correctly fails on the live privileged namespace label and reports all five required DR/restore evidence files as unavailable. It exits non-zero; no go-live claim is made.
- Pinned CI SDK/toolchain inputs to the repository contract (`.NET SDK 8.0.319`, Node.js `22.14.0`) and constrained the GitOps promotion dispatch to the production digest source; staging remains a separate Argo application and is not silently rewritten by a production promotion.
- Read-only SSH validation of all five production VMs (`172.16.102.7/.8/.9/.10/.12`) confirms swap count `0`, 16 GiB RAM each, 100,221 MiB root disks with 61,560–80,868 MiB available, and active `k3s`/`k3s-agent` services. Control-plane nodes have the `NoSchedule` taint; application/data/observability worker labels are present. On all three servers, K3s secrets encryption is enabled with matching hashes, audit policy/encryption files are present, `protect-kernel-defaults=true`, `host-gw` networking and the VIP `172.16.102.100` SAN are configured. This is host-security evidence; it does not clear the remaining admission, CSI, GitOps-controller or DR gates.
- Ansible syntax checks for `00-preflight`, `10-bootstrap-k3s` and `20-verify-cluster` pass under WSL using the encrypted vault password file; no playbook was applied.
- The corresponding redacted host snapshot is stored at `artifacts/evidence/k3s-host-security-current.json`; it contains capacity and security posture only, never credentials or kubeconfig data.
- Migration contract now covers all eight EF contexts (identity, appointment, clinical, lab, billing, patient write/read and pharmacy). Local generation produced idempotent SQL plus SHA-256 manifest; the validator reports no destructive SQL and confirms all seven production API migration flags are `false`. A real production migration execution/restore evidence is still required before go-live.
- Observability contract now includes deployment/unavailable replicas, crash/image pull, migration failure, Gatekeeper admission denials, Vault/Redis target loss and critical error-budget burn alerts. Alertmanager critical/resolved routing is validated, runtime credentials remain placeholders, and all seven observability images are digest-pinned. The standalone contract/render gate passes; live notification delivery still requires a staging alert test.
- Admission constraints now fail closed on mutable image references, privileged/host namespace/hostPath workloads, service-account token automount, missing non-root/seccomp/capability-drop controls and missing resources for init containers as well as main containers. The source contract and rendered production checks pass. The new `test-admission-policy.ps1` deliberately reports `skipped`/non-zero without a staging kubeconfig; the manual `admission-staging-gate.yml` workflow is ready to prove one accepted and one rejected Pod using server-side dry-run.
- Argo CD bootstrap now renders health handlers for Deployment, Job, Service and Linkerd Server, retry/backoff on all 8 Applications, and keeps production manual-sync only. `scripts/validate-argocd-bootstrap.ps1` passes these checks; the live controller is still absent until the staging installation window.

## Latest continuation evidence — 2026-08-10

- `.github/workflows/argocd-bootstrap.yml` now applies the reviewed
  `k8s/gitops/bootstrap` resources only when the protected workflow input is
  `apply=true`; it waits for `applications.argoproj.io` to be Established and
  uses server-side apply. The default dry-run path remains non-mutating.
- `scripts/validate-k3s-remaining-gates.ps1` now accepts `-OutputPath` and emits
  the standard `{status, checks, generatedAtUtc}` evidence shape. The explicit
  production run is saved at `artifacts/evidence/devsecops-remaining-gates-current.json`.
- Added `.github/workflows/pod-security-production-rollout.yml`. It is protected
  by the `production` environment, defaults to preflight-only, creates the
  `his-hope-data`/`his-hope-system` boundaries only under `apply=true`, requires
  `-AllowProduction` on both boundary and label scripts, then applies the
  restricted label and reruns the release validator. This closes the previous
  manual-only path without granting apply to the normal build/promotion
  workflows.
- The current live gate remains intentionally non-green: Pod Security is
  `fail` (`his-hope=enforce=privileged`), signature provider/CSI/GitOps/Azure
  backup are `blocked`, and observability is `pass`. The repository render and
  restricted-workload source checks pass; no production mutation was performed
  during this continuation.
- Tightened the Argo `AppProject` from `namespaceResourceWhitelist: '*'` to an
  explicit namespaced kind allow-list and added `PriorityClass` as the only new
  cluster-scoped workload dependency. `validate-argocd-bootstrap.ps1` now
  fails if a wildcard returns; the least-privilege contract passes. All 7
  Applications also use `FailOnSharedResource=true` to prevent silent ownership
  collisions between GitOps Applications.
- Added `scripts/validate-argocd-project-scope.py`, which renders all 8 local
  Application sources and compares 49 observed resource kinds against the
  AppProject cluster/namespaced allow-lists. The scope check passes; a future
  unlisted kind will fail the DevSecOps workflow before merge.
- Corrected `.github/workflows/security-quality-gate.yml` to build every
  Dockerfile with repository-root context. The previous `dirname(Dockerfile)`
  context was incompatible with shared-project `COPY`/restore paths and could
  produce false container security failures. The release matrix currently
  covers 19 existing Dockerfiles.
- Pinned all 37 repository Dockerfiles' 41 .NET base-image references to
  immutable Microsoft Container Registry digests. `global.json` retains the
  CI SDK minimum `8.0.319` with `latestFeature` roll-forward because the pinned
  `dotnet/sdk:8.0` image currently contains SDK `8.0.423`; the local Dashboard
  BFF root-context build now restores and publishes successfully.
- Added `scripts/validate-container-build-contract.py`; it passes the digest,
  SDK and Dockerfile inventory checks before container release/security jobs.
- Container release now scans the exact pushed Harbor digest after `docker pull`
  and before Cosign signing, so the signed artifact cannot bypass the scan that
  was run on the local pre-push build.
- The signing step now generates an in-toto/SLSA provenance predicate containing
  the pushed digest, commit and workflow reference, then calls
  `cosign attest --predicate ... --type slsaprovenance`; the shell step parses
  successfully in a local `bash -n` check.
- Both a chiseled-extra BFF image and the jammy Identity Service image were
  built locally from the pinned digests; restore and publish completed for both.
- Updated the authoritative production storage profile to use Longhorn for
  PostgreSQL, RabbitMQ, Redis and the MinIO backup object store; added the
  checked-in Longhorn `VolumeSnapshotClass` with `Retain` policy and Velero CSI
  label. The source storage contract now reports replicated storage and
  snapshot-class **pass**. Runtime Longhorn installation and an isolated
  snapshot/restore drill are still required before the CSI gate can pass.
- Added the production `database-continuity-backups` PVC patch to Longhorn;
  the rendered `prod` overlay now contains zero `storageClassName: local-path`
  references and renders successfully.
- Added an authoritative section to `docs/operations/disaster-recovery.md` and
  explicitly marked the older CockroachDB/GCS/GKE procedure as legacy. The
  current profile points operators to K3s etcd, CNPG/Barman Azure Blob, Vault,
  Harbor clean-node, Longhorn snapshot and application-restore evidence; no
  production restore evidence was fabricated.
- Added the reviewed `his-hope-production-ha` Argo Application for the
  `prod-spire-azure` data platform. Its CNPG ObjectStore, ScheduledBackup,
  Longhorn VolumeSnapshotClass, SPIRE ClusterRoles and backup namespace are now
  covered by explicit AppProject scope. Scope validation passes with 8
  Applications and 67 observed resource kinds.
- Re-ran the explicit production runtime gate at 2026-08-10T04:26Z. The live
  result remains `fail`: signature provider, Pod Security, CSI storage, Argo CD
  and Azure-node backup are respectively blocked/fail; OTEL observability is
  the only remaining-gates check that passes. Repository changes do not change
  this runtime result until the protected bootstrap and restore operations are
  executed.
- The new production-HA Argo source initially exposed an unpinned `busybox`
  init image in SPIRE. Added the approved Harbor mirror digest to the
  production image component; `validate-gitops-plane.ps1` now passes for
  `k8s/overlays/prod-spire-azure` with 6 digest-pinned images.
- Added the production-HA plane to `.github/workflows/k3s-devsecops-gate.yml`,
  so CI now validates the same CNPG/SPIRE/backup source that the new Argo
  Application reconciles; workflow YAML parsing and all four GitOps scope/plane
  checks pass locally.
- Added protected `.github/workflows/k3s-backup-agent-rollout.yml` and its
  runbook. It performs Ansible syntax/check mode on every run, requires a
  production environment plus explicit approval code for mutation, and records
  only redacted timer/service state after apply. It has not been applied to
  production in this workstation session.
- Local Ansible syntax validation for `playbooks/30-backup-agents.yml` passed
  using the encrypted Vault password file; no host task was executed.
- The backup-agent workflow YAML and all six embedded Bash blocks pass local
  parsing; the workflow remains dry-run by default and has no live runtime
  evidence until an approved apply run is performed.
- Pinned all 90 external GitHub Actions references across 19 workflows to
  resolved 40-character commit SHAs, retaining the reviewed tag in a comment.
  Added `scripts/validate-workflow-action-pins.py`; it passes locally and is
  now part of the K3s DevSecOps gate, preventing mutable action tags from
  re-entering CI.
- Container release tool bootstrap now pins the Trivy/Syft installer scripts
  by commit SHA and verifies the downloaded Cosign v2.4.1 binary against its
  SHA-256 before adding it to `PATH`; the embedded release Bash blocks parse
  successfully.
- Read-only SSH validation at 2026-08-10T04:44Z captured in
  `artifacts/evidence/k3s-backup-agent-runtime-current.json`: all three backup
  timers are active but all three snapshot services are `failed` with exit
  status 1. The protected env files contain a SAS value with length 2 (value
  never recorded); the new Ansible/script guard now rejects this before any
  upload. Correct the Vault-encrypted SAS and run the approved backup-agent
  workflow before treating the Azure backup gate as pass.
- After correcting the workflow flag from unsupported `--diff=false` to plain
  `--check`, a real Ansible check-mode run reached all three servers and passed
  the new credential-shape assertion with `unreachable=0 failed=0`. It predicts
  the uploader/service files need refresh; no task was applied.
- Root cause remediation is now in the repository: `backup.env.j2` emits all
  systemd EnvironmentFile values with Jinja `to_json` quoting, preserving SAS
  `&`, `=` and `%` characters. Offline Ansible rendering with a dummy SAS
  confirmed endpoint/container/SAS values are quoted; the new
  `validate-backup-agent-contract.py` gate passes. A production apply is still
  required to refresh the three host units.
- Post-fix Ansible check mode at 2026-08-10T04:51Z reached all three K3s
  servers with `ok=9 changed=3 unreachable=0 failed=0` on each host. The three
  predicted changes are the quoted environment file, hardened service unit,
  and timer/service refresh; this remains a dry-run and does not change hosts.
- Live production gate refresh at 2026-08-10T04:51Z still reports the
  following non-passing controls: namespace Pod Security is `privileged`, no
  ready signature admission provider, only `local-path` CSI, no Argo CD
  controller, five DR evidence files are absent, and backup-agent apply is
  not yet available from this workstation. Nodes, rendered immutable images,
  manifest secret scan, application readiness, Linkerd control plane, and OTEL
  collector readiness pass.
- The pull-request K3s gate now also executes the signed migration artifact
  contract and container-build contract on the production matrix leg, and
  uploads migration evidence. Local verification passed: workflow YAML parsed,
  91 action references are immutable, migration contract passed, and all 37
  Dockerfiles/41 .NET base images passed the container contract.
- `validate-k3s-release.ps1` now compares client/server minor versions and fails
  with exit 10 on excessive skew. The current workstation probe correctly
  detects kubectl `v1.25.3` versus server `v1.35.5+k3s1` (skew 10), so that
  production check is intentionally not green until the pinned kubectl 1.35
  toolchain is used.
- Static verification at 2026-08-10T04:55Z passed PowerShell parsing for all 90
  scripts, workflow YAML parsing, immutable action pin validation (91 refs),
  backup/container contracts, production Kustomize rendering, and the SPIRE
  GitOps plane. The explicit live release probe fails only on the detected
  kubectl/server version skew (exit 10); the full go-live probe still has the
  independent platform/DR blockers recorded above.
- Added the planned `scripts/verify-k3s-go-live.ps1` compatibility entry point;
  it delegates to the canonical validator and was executed against the
  production kubeconfig. It correctly returned exit 80 because Pod Security
  remains privileged and required DR evidence files are unavailable.
- Fresh live validation at 2026-08-10T04:57Z is unchanged: 5/5 nodes,
  immutable images, secret scan, application health, Linkerd and OTEL pass;
  go-live exits 80 and remaining-gates exits 30 for Pod Security, signature
  provider, CSI/Longhorn, Argo CD, backup reachability and missing DR drills.
- Protected mutation workflows now all declare a protected environment,
  concurrency lock, explicit `inputs.apply` guard and a 30-minute timeout.
  `validate-protected-workflow-contract.py` is part of the PR gate and passes;
  workflow YAML and immutable-action validation remain green.
- Downloaded kubectl `v1.35.5` into the ignored local toolchain directory and
  verified it against the official SHA-256 before use. With that binary,
  `validate-k3s-release.ps1` now passes the client/server skew gate, reaches
  all five nodes, and passes application/Linkerd health. Go-live still fails
  only on the independent Pod Security and DR/platform evidence conditions.
- Added `scripts/install-k3s-kubectl.ps1`, which is fail-closed to the reviewed
  v1.35.5 Windows amd64 artifact and verifies SHA-256 before installation.
- With the verified local binary, the production release gate passed at
  2026-08-10T05:03Z, including toolchain skew, cluster connectivity, 5/5 nodes,
  application health and Linkerd. The full go-live gate remains fail only for
  the previously recorded production security, storage, GitOps, backup and DR
  prerequisites.
- Read-only bootstrap preflights at 2026-08-10T05:06Z: security-boundary
  bootstrap passed render and identified the two intended namespaces
  (`his-hope-data`, `his-hope-system`) as missing; Longhorn 1.12.0 dry-run
  passed; Sigstore Policy Controller 0.10.5 dry-run passed after correcting a
  metadata-regex bug in its bootstrap script. No cluster mutation was made.
- Added `scripts/validate-reliability-platform.ps1` to the PR gate. Live
  validation at 2026-08-10T05:07Z passes migration isolation, 48/48 pod
  readiness and 20/20 deployment availability. Static workflow validation now
  covers 92 immutable action references.
- Server-side admission dry-run at 2026-08-10T05:10Z passed both cases on the
  production API: the compliant digest/non-root Pod was accepted and the
  privileged/mutable-tag/hostPath Pod was rejected. This proves the existing
  policy chain behavior, but does not replace the missing production signature
  provider readiness gate.
- The production go-live workflow now executes that positive/negative
  admission probe and the reliability runtime contract before evaluating DR
  evidence. Workflow YAML, action pin and protected workflow checks remain
  green after this addition.
- HA/mesh runtime audit at 2026-08-10T05:11Z found concrete production drift:
  CNPG is not 3/3 ready; backend pods lack the Linkerd/SPIRE init containers;
  Linkerd NetworkPolicy naming is namespace-prefixed; and the appointment
  service mTLS health probe returns HTTP 503. Validators were updated to accept
  the explicit production kubeconfig, resolve prefixed service/policy names,
  and skip undeployed dev-only checks. These runtime failures remain blockers;
  no cluster mutation was performed.
- Production go-live workflow now runs the HA/SPIRE and Linkerd/SPIRE runtime
  validators with the protected kubeconfig. Static workflow validation passed
  after adding these required runtime gates; current live results remain fail
  on CNPG readiness, missing SPIRE/Linkerd init containers and appointment
  service HTTP 503.
- The validator fixes were syntax-checked with all 93 PowerShell scripts and
  the full workflow/action-pin/protected-workflow/container/backup contracts
  remained green. Runtime failures are preserved as fail evidence rather than
  being downgraded to skipped.
- HA/SPIRE validator now emits structured JSON evidence. The current live
  artifact reports CNPG not 3/3, one ready SPIRE endpoint, unavailable agents,
  and no failover event; these are explicit `fail` checks rather than opaque
  command output.
- Final verification at 2026-08-10T05:18Z populated immutable image evidence
  for all 22 rendered production images in both release and go-live artifacts.
  The release gate is `pass`; the go-live gate remains `fail` only on the
  previously recorded Pod Security and DR evidence blockers. Reliability
  runtime evidence is `pass` (48/48 Ready pods, 20/20 available deployments,
  migration isolation pass). No production mutation was performed.
- Contract-test gate at 2026-08-10T05:25Z passed all 100 tests across 7
  projects. A stale Patient gRPC expectation was corrected to match the
  canonical `PersonName.FullName` contract (`LastName MiddleName FirstName`);
  no service behavior was weakened.
- The CNPG backup validator now accepts an explicit production kubeconfig and
  is wired into the protected go-live workflow. Its current read-only result
  is recorded as `fail` because the production Barman ObjectStore is
  absent/not Ready; the validator now emits redacted JSON evidence even on
  failure. No backup or restore job was started.
- Static GitOps/admission verification at 2026-08-10T05:29Z passed for the
  data plane (3 images), system plane (2 images), SPIRE/Azure plane (6 images),
  Argo bootstrap render and the production admission policy source bundle.
- Observability hardening at 2026-08-10T05:31Z closed a real gate gap: the
  synthetic login/search/logout CronJob now propagates Playwright failures,
  requires username/password from its Kubernetes Secret, and uses digest-
  pinned Playwright images. The monitoring kustomization now renders
  successfully, and the observability contract reports all 8 checks `pass`.
- The synthetic monitor now mounts its Vault-backed `synthetic-monitor-secrets`
  provider and materializes the required `synthetic-monitor-credentials`
  Secret through its dedicated monitoring SecretProviderClass; both
  observability and monitoring overlays render successfully. Playwright
  dependency versions are exact and browser-install failures are no longer
  suppressed.
- Contract sweep at 2026-08-10T05:33Z: backup-agent, migration, signature,
  Argo bootstrap and observability contracts passed. Storage remains
  explicitly blocked only for out-of-band Azure destination injection and the
  missing Longhorn restore evidence; no placeholder was promoted to a pass.
- Logging hardening at 2026-08-10T05:41Z removed the privileged Elasticsearch
  sysctl/filesystem init containers, added host-level `vm.max_map_count`
  ownership to the runbook, Vault-backed Elasticsearch/Kibana credentials,
  and digest-pinned Jaeger/Elasticsearch/Kibana/Blackbox images. Monitoring
  render now has zero restricted-workload violations and the expanded
  observability contract passes 11/11. Promtail remains the only explicit
  non-root exception because it reads host pod logs; the exception inventory
  records owner, reason and expiry without enabling privileged mode.
- Live production recheck at 2026-08-10T05:41Z: release checks confirm
  kubectl/server v1.35.5, 5 Ready nodes, 48 Ready pods and 20 available
  deployments. The remaining-gates result is unchanged: Pod Security,
  signature provider, replicated CSI, Argo CD and host-side Azure backup are
  still explicit blockers; no mutation was performed.
- Manifest secret hardening at 2026-08-10T05:44Z replaced dev/base/Grafana
  literal fixtures with explicit runtime placeholders and replaced the shell
  grep with a redaction-safe `validate-manifest-secret-contract.py` gate. The
  gate passes without printing candidate secret values.
- Grafana admin credentials are now materialized through the observability
  Vault SecretProviderClass instead of a manifest fixture; the observability
  overlay renders successfully and the manifest secret gate remains pass.
- The rendered dev/staging/prod release checks now pass digest and secret
  scanning; dev/staging local cluster checks are correctly reported as
  `environment-blocked` when no kubeconfig is supplied, never as pass.
- Production observability GitOps closure at 2026-08-10T05:52Z: the new
  `k8s/observability/overlays/prod` renders successfully, adds only non-
  duplicated monitoring resources, and pins six stateful PVCs to Longhorn
  (`local-path` count 0). The observability GitOps plane validator passes with
  12 digest-pinned images, and the Argo bootstrap validator passes with nine
  Applications including a manual-sync `his-hope-observability-production`
  Application. Static workflow pins (93/93), protected workflow contracts
  (6/6), observability (11/11) and manifest secret contracts pass. Live apply
  and restore evidence remain external blockers; no cluster mutation was done.
- Application/toolchain verification at 2026-08-10T05:58Z: contract tests
  passed 100 tests across seven projects; the Release solution build completed
  with 0 errors (existing nullable/analyzer warnings remain). Shared
  foundation validation, design-token validation, all three Angular app
  production builds and repository lint completed successfully (lint reports
  warnings only). Kustomize render passed for dev, staging, prod,
  observability production, data/system GitOps, SPIRE/Azure and Argo bootstrap.
- Live remaining-gates recheck at 2026-08-10T06:01Z remains fail-closed: no
  ready signature provider, live `his-hope` still reports `enforce=privileged`,
  only `local-path` CSI is available, Argo CD is not installed, and the Azure
  systemd backup check is unavailable from this workstation. OTEL collector
  readiness passes (2 pods). These are runtime/apply blockers, not converted
  to static-repository passes.
- Argo scope hardening at 2026-08-10T06:02Z added the monitoring destination and
  explicit CronJob/PrometheusRule/ServiceMonitor allow-list entries. The
  project scope validator now passes for all nine Applications and 85 rendered
  resource kinds; bootstrap and observability GitOps validators also pass.
- Release gate tooling at 2026-08-10T06:03Z now accepts an explicit
  `-Kubeconfig` path and the protected production workflows pass it directly.
  With `artifacts/kubeconfig-production.yaml`, live release validation reaches
  v1.35.5+k3s1, confirms 5 Ready nodes, application health and Linkerd control
  plane; it fails only the real Pod Security boundary (`his-hope` remains
  `privileged`).
- Frontend supply-chain closure at 2026-08-10T06:13Z: Angular 21.2.19
  runtime/compiler packages and 21.2.20 CLI/build tooling are declared and
  lockfile installation succeeds. Full lint (0 errors), shared foundation,
  admin, dashboard and clinical production builds pass. `npm audit
  --omit=dev --audit-level=high` reports 0 vulnerabilities in the runtime
  graph; the complete audit is retained as an artifact and still records four
  high dev-toolchain findings requiring a future Angular build-tool upgrade.
- Full solution test invocation at 2026-08-10T06:15Z exceeded the local
  120-second budget while integration/container-backed suites were running;
  it is not reported as pass. The focused contract gate remains the verified
  result (100/100), and the CI workflow retains the broader test jobs.
- Argo health hardening at 2026-08-10T06:18Z adds explicit health handlers
  for PrometheusRule, ServiceMonitor and Vault CSI SecretProviderClass. The
  bootstrap validator now checks 11 health/boundary contracts and passes;
  Argo project scope remains pass for nine Applications and 85 resource kinds.
- Go-live validator at 2026-08-10T06:19Z reaches the live API and confirms
  immutable images, secret scan, five Ready nodes, application health and
  Linkerd health. It correctly fails closed on `privileged` Pod Security and
  marks all five required DR drills unavailable because measured evidence files
  are still absent.
- The strengthened DR evidence validator at 2026-08-10T06:19Z rejects the
  current evidence set as `blocked` with five unavailable drills; no synthetic
  pass files were created.
- Live gate refresh at 2026-08-10T06:21Z is unchanged: API, five nodes,
  application health, Linkerd and OTEL remain healthy, while Pod Security,
  signature provider, replicated CSI, Argo CD, Azure host backup and five DR
  evidence files remain unresolved.
- Workspace SCA recheck at 2026-08-10T06:22Z: `npm audit --omit=dev
  --audit-level=high` passes for admin-app, dashboard-app and his-hope-app
  individually (0 vulnerabilities each), matching the CI security workflow.
- 2026-08-10T06:31Z — Closed repository-side observability release contracts: production telemetry now receives reviewed release SHA and aggregate image-digest metadata through a non-secret ConfigMap and OTEL resource attributes; GitOps promotion updates the metadata atomically with the digest-only PR. The synthetic monitor now includes an unauthenticated protected-API negative path and propagates 401/403 failures. Added a static error-budget promotion contract plus a protected live Prometheus burn-rate gate (missing protected Prometheus URL remains explicitly blocked). Kustomize production render, observability contract, error-budget static gate, workflow pin/protected-workflow/manifest-secret validators, and focused OpenTelemetry/Infrastructure builds pass. Full solution build remains environment-blocked by zero free space on C: and stale testhost locks.
- 2026-08-10T06:34Z — Synthetic monitor was bound to the reviewed internal ingress origin (`https://app.his-hope.local/`) through a monitoring ConfigMap; it no longer defaults to a cluster-local frontend Service. Monitoring and production observability renders and the observability contract pass.
- 2026-08-10T06:35Z — Error-budget alert and protected promotion query now use a two-window 5-minute + 1-hour burn-rate condition at the 14.4x threshold. Production observability render and static error-budget gate pass; live query remains deliberately blocked until the protected `PROMETHEUS_PRODUCTION_URL` is provisioned.
- 2026-08-10T06:35Z — Read-only live refresh: 5 nodes, application health, Linkerd and OTEL remain pass. Production still fails Pod Security (`his-hope` is `privileged`), and signature provider, replicated CSI, Argo CD, Azure host backup plus five DR evidence files remain blocked/unavailable. No cluster mutation was performed.
- 2026-08-10T06:38Z — Added and ran the sanitized baseline capture against the explicit production kubeconfig. Context, version, nodes, pods/restarts/readiness, deployments, events, CRDs, webhooks, NetworkPolicies, Ingresses and namespace labels all collected with no secret fields; baseline artifact status is `pass`. It records the live kubectl/server skew (v1.25.3 vs v1.35.5) for remediation rather than hiding it.
- 2026-08-10T06:39Z — Confirmed CI toolchain pinning: protected workflows use kubectl v1.35.5, Helm v3.17.3 and standalone kustomize v5.6.0; the workstation’s v1.25.3 client remains outside CI and is reported as a live skew.
- 2026-08-10T06:40Z — Final static refresh after baseline workflow addition: workflow action pin, protected-workflow contract, production Kustomize render and diff checks all pass. No production mutation was issued.
- 2026-08-10T06:40Z — Baseline script was tightened to capture event reason/object columns only (no event message payload), then rerun successfully with all 11 read-only checks passing.
- 2026-08-10T06:41Z — Go-live validator now requires a fresh `k3s-baseline.json` with all 11 read-only checks passing in the same evidence directory. A controlled test with the current baseline passed this new check; overall go-live correctly remains failed on Pod Security and missing DR drills.
- 2026-08-10T06:46Z — Protected bootstrap preflight was rerun against the explicit production kubeconfig without mutation: Argo CD HA manifest v3.4.1 checksum verification passed under `-AllowProduction -HighAvailability -WhatIf`; Longhorn 1.12.0 and Sigstore Policy Controller 0.10.5 Helm client dry-runs passed. Pod Security rollout correctly stopped because the required `his-hope-system` boundary namespace is absent. The boundary bootstrap render passed and dry-run identified `his-hope-data` and `his-hope-system` as the two namespaces requiring an approved apply. No cluster resources were changed.
- 2026-08-10T06:46Z — Static GitDevOps refresh passed: workflow action pins (19 workflows/94 refs), protected workflow contract (6 workflows), manifest-secret contract, observability contract, production Kustomize render (161 documents), GitOps observability-plane contract (12 images) and `git diff --check`. Live remaining-gates status is still exit 30: Pod Security fails and signature provider, replicated CSI, Argo CD and Azure host-backup evidence remain blocked; OTEL observability passes.
- 2026-08-10T06:46Z — Baseline recaptured and go-live validator rerun: baseline capture passed; immutable images (22), manifest secret scan, API connectivity, five Ready nodes, application health, Linkerd control plane and baseline evidence passed. Go-live remains correctly failed only on `his-hope` Pod Security (`privileged`) plus five missing measured DR evidence files.
- 2026-08-10T06:49Z — Hardened `validate-protected-workflow-contract.py` so Helm, kubectl label/create/patch and `-Apply` mutation paths require an explicit `inputs.apply` guard. It now also verifies that the Pod Security workflow bootstraps `his-hope-system`/`his-hope-data` before restricted rollout and that Argo CD uses the reviewed HA manifest with a retained `-WhatIf` path. Validator passes for all six protected workflows.
- 2026-08-10T06:49Z — Static contract sweep: migration artifact/API isolation, Argo health/project scope, signature-controller source contract and production storage/PVC render checks pass. Storage remains blocked only for runtime Azure/Velero values and the unperformed Longhorn restore drill; DR contract remains blocked by the five missing measured restore drills. No synthetic evidence was generated.
- 2026-08-10T06:50Z — Read-only production API admission probe passed both cases using server-side dry-run: compliant Pod accepted and privileged/mutable-tag/hostPath Pod rejected. This closes the positive/negative admission probe requirement without creating a workload; signature-provider installation remains a separate blocked gate.
- 2026-08-10T06:50Z — Final static refresh after the admission probe passed protected-workflow, workflow-action-pin, manifest-secret, observability, production-Kustomize and diff checks. No production mutation was issued.
- 2026-08-10T06:52Z — Improved CNPG backup validator diagnostics with secret-safe error details. Read-only production check now identifies the concrete runtime blocker (`ObjectStore is not ready`) instead of emitting only a generic failure; no backup or restore operation was run.
- 2026-08-10T06:53Z — Confirmed the CNPG evidence artifact now records the redacted concrete failure (`ObjectStore is not ready (phase=)`), with exit code 1. This remains a real runtime failure, not an unavailable/green result.
- 2026-08-10T06:56Z — Runtime probe found appointment-service `/health` returning 503 because its `GrpcServices:PatientService` health dependency fell back to the obsolete appsettings hostname; the production Deployment had no `GrpcServices__PatientService` env mapping. Added the explicit `ADAPTER_GRPC_PATIENT_URL` ConfigMap mapping in `k8s/base/appointment-service.yaml`. Production Kustomize render now includes the mapping; staging and production runtime-contract validators pass statically. A reviewed image/manifest rollout is still required before claiming the live 503 is fixed.
- 2026-08-10T06:58Z — Extended the offline Kustomize runtime contract validator to parse YAML without API discovery, validate gRPC consumer-to-ConfigMap mappings, and avoid a PowerShell `continue` control-flow bug in CSI reference scanning. Corrected staging service DNS names for the `his-hope-` name prefix. Dev, staging and production runtime-contract checks all pass; the DevSecOps workflow now runs this check for every matrix environment.
- 2026-08-10T07:12Z — Closed a CI reproducibility gap: the runtime-contract validator uses PyYAML for every `dev`/`staging`/`prod` matrix job, so `.github/workflows/k3s-devsecops-gate.yml` now installs the pinned `pyyaml==6.0.2` dependency unconditionally. Protected-workflow, action-pin, manifest-secret, container-build, all three runtime-contract overlays and `git diff --check` pass.
- 2026-08-10T07:15Z — Focused source verification passed for the appointment API and Dashboard BFF (`dotnet build --no-restore`, 0 errors). Read-only production inspection confirms the runtime ConfigMap contains the expected HTTP/gRPC service DNS targets and all BFF pods are Ready; the live appointment 503/BFF cascade still requires a reviewed image rollout, which was not performed in this read-only pass.
- 2026-08-10T07:15Z — Fresh `validate-k3s-go-live.ps1` against the explicit production kubeconfig: image digests, manifest secret scan, API connectivity, five Ready nodes, application health, Linkerd control plane and sanitized baseline pass. Required Pod Security still fails (`his-hope` is `privileged`); all five measured DR drill files remain unavailable. Exit 80 is retained; no false green was recorded.
- 2026-08-10T07:16Z — Parsed all 19 GitHub workflow YAML files with PyYAML successfully after the dependency-install change.
- 2026-08-10T07:16Z — Revalidated the signed migration artifact contract: all 8 DbContext scripts and hashes match, no destructive SQL pattern is present, and all 7 production API startup-migration flags are false.
- 2026-08-10T07:20Z — Added the production runtime-contract/gRPC validator to `.github/workflows/container-release.yml` before Harbor login/build. Release contract checks now pass together with protected-workflow, action-pin, workflow YAML parse (19 files), production runtime render and diff checks.
- 2026-08-10T07:20Z — Strengthened `validate-k3s-go-live.ps1` with a live, read-only appointment runtime mapping check. The fresh production run correctly reports `runtime-contract-mappings=fail` because the current Deployment lacks `GrpcServices__PatientService`; this makes the known appointment 503/BFF cascade an explicit go-live failure instead of hiding behind Ready pod status. Script parser, protected-workflow, action-pin, runtime-render and diff checks pass.
- 2026-08-10T07:20Z — Full repository contract sweep passes: protected workflow, action pins, manifest secret scan, container build contract, production runtime contract, migration contract and data/system GitOps plane renders all exit 0.
- 2026-08-10T07:24Z — Hardened `k3s-production-go-live-gate.yml` to continue every read-only validation step, upload complete evidence, then fail in a final aggregation step when any gate fails. This prevents the first Pod Security failure from hiding DR, CNPG, Azure, admission, Linkerd or error-budget results. Workflow YAML and action-pin validation pass.
- 2026-08-10T07:27Z — Added the protected `production` GitHub Environment and a 15-minute timeout to the digest-only GitOps promotion job. The promotion remains PR-based and renders the production overlay before opening the PR; workflow YAML, action-pin and diff checks pass.
- 2026-08-10T07:30Z — GitOps promotion now installs pinned PyYAML and runs the production runtime-contract/gRPC validator before creating the digest-only PR. Workflow YAML, action-pin, protected-workflow, runtime-render and diff checks pass.
- 2026-08-10T07:31Z — Direct production Kustomize render confirms the appointment Deployment contains `GrpcServices__PatientService` mapped to `his-hope-runtime-contract-config/ADAPTER_GRPC_PATIENT_URL`; the current live Deployment is stale and still requires the reviewed image/manifest rollout.
- 2026-08-10T07:32Z — Read-only image drift check: live appointment Deployment runs `harbor.../appointment-service:prod-20260809-grpc2@sha256:404327d9…`, while the reviewed production Kustomize render pins `.../appointment-service:spiffe-20260807@sha256:2cf18410…`. The mismatch is recorded as deployment drift; no live patch or rollout was issued.
- 2026-08-10T07:33Z — Go-live validator now compares live Deployment image digests with the reviewed production render. The fresh run correctly fails `image-drift` for the current workload set, in addition to Pod Security, stale appointment runtime mapping and missing DR evidence. Script parser, protected-workflow, action-pin and production runtime checks pass.
- 2026-08-10T07:36Z — Fixed GitOps promotion diff allow-list: `release-metadata.yaml`, which is intentionally updated with the digest promotion, is now excluded alongside the digest source and generated artifacts. Promotion workflow YAML, action-pin, protected-workflow and diff checks pass.
- 2026-08-10T07:38Z — Full contract sweep: Argo bootstrap/project scope, signature-controller policy, observability, reliability (48 Ready pods/20 available deployments), error-budget static rule and backup-agent contracts pass. Storage remains explicitly blocked on out-of-band Azure/Velero values and Longhorn restore evidence; DR remains blocked on five measured drill files. No synthetic evidence was created.
- 2026-08-10T07:42Z — Split storage validation modes: PR/static CI now invokes `validate-storage-backup-contract.ps1 -StaticOnly`, which passes manifest/storage shape while explicitly marking protected Azure/Velero/CSI runtime inputs as `skipped`; strict production mode still exits 70 on the real placeholders/missing restore evidence. Parser, action-pin, protected-workflow and diff checks pass.
- 2026-08-10T07:45Z — Split DR validation modes: PR/static CI now invokes `validate-dr-evidence.ps1 -StaticOnly` and passes with five explicit `skipped` checks; strict production mode still exits 70 with the five required drill files unavailable. No evidence files were fabricated.
- 2026-08-10T07:48Z — Local simulation of the complete static `k3s-devsecops-gate` matrix passes for dev/staging/prod: Kustomize renders (197/193/161 documents), runtime contracts, workflow/action pins, secret/container contracts, admission source, Argo scope, signature policy, StaticOnly storage/DR, observability, error-budget and reliability all exit 0.
- 2026-08-10T07:35Z — Release gate now auto-prepends the repository-local pinned kubectl when `.runtime/toolchain/kubectl.exe` exists. A fresh production read-only run uses kubectl v1.35.5 against K3s v1.35.5+k3s1: toolchain-skew, connectivity, nodes, application health and Linkerd control-plane pass; Pod Security remains a real fail because live `his-hope` is still `enforce=privileged`. No cluster mutation was issued.
- 2026-08-10T07:36Z — Fresh production go-live read-only validation exits 80. Passing: immutable images in rendered manifests, secret scan, API connectivity, five Ready nodes, application readiness, Linkerd control plane and baseline contract. Failing/unavailable: live `his-hope` Pod Security is `privileged`; all compared workload images drift from the reviewed production render; live appointment-service lacks the reviewed `ADAPTER_GRPC_PATIENT_URL` mapping; five measured DR evidence files are unavailable. No synthetic evidence or live mutation was issued.
- 2026-08-10T07:38Z — Verification loop: `dotnet build His.Hope.sln --no-restore` passes with 0 errors (375 pre-existing warnings); non-integration/unit/contract suite passes 632 tests. Full suite remains environment-blocked: Testcontainers cannot connect to Docker and identity integration tests consequently fail during fixture startup. Secret-pattern scan reports zero matches for the checked high-risk patterns; workflow YAML parses successfully.
- 2026-08-10T07:40Z — Read-only `kubectl diff -f artifacts/k8s/prod.yaml` confirms cutover needs a controlled migration: live pods would violate restricted Pod Security, and the existing `his-hope-database-continuity-backups` PVC is `local-path` while the reviewed manifest requests `longhorn`; Kubernetes rejects that bound PVC storage-class change as immutable. No apply was attempted.
- 2026-08-10T07:43Z — Added `storage-class-drift` to `validate-k3s-go-live.ps1`. Fresh live validation now reports the bound backup PVC mismatch explicitly (`local-path` → `longhorn`, immutable; migration/restore required) instead of hiding it inside a generic diff failure. Script parser passes; production gate remains correctly failed.
- 2026-08-10T07:45Z — Re-ran protected-workflow, action-pin, manifest-secret, production runtime-contract, StaticOnly storage and diff checks after the new cutover gate; all repository/static checks pass. The live gate remains fail-closed on Pod Security, image drift, appointment runtime mapping, storage-class drift and missing DR evidence.
- 2026-08-10T07:47Z — Extended `docs/runbooks/k3s-devsecops-remaining-gates.vi.md` with the database-continuity PVC migration procedure: preserve the bound local PVC, create/restore to Longhorn, prove checksum/RTO/RPO, then switch `claimName` through a separate reviewed GitOps PR. The runbook explicitly prohibits patching an immutable `storageClassName`.
- 2026-08-10T07:49Z — Added protected `.github/workflows/database-continuity-pvc-migration.yml` and `scripts/migrate-database-continuity-pvc.ps1`. Local production preflight passed without mutation: source `local-path` PVC is pinned to `k3s-worker-2`, target is a new `longhorn` PVC, and the script defaults to dry-run, requires `-AllowProduction` for apply, writes redacted status evidence, verifies a checksum marker and preserves the old PVC for rollback. Protected-workflow/action-pin/YAML/parser/static storage checks pass.
- 2026-08-10T07:51Z — Removed the unsafe direct `database-continuity-storage-patch.yaml` reference from the production overlay. The reviewed render intentionally remains on the existing `local-path` PVC until migration evidence exists; StaticOnly reports this as `skipped`, while strict production storage validation reports it as `blocked`. This prevents Argo from attempting an immutable PVC storage-class update.
- 2026-08-10T07:52Z — Tightened go-live storage gate: even if live and rendered PVCs match, production now fails when the reviewed backup PVC still selects `local-path`/`standard`. Fresh read-only go-live reports this migration blocker explicitly along with the existing Pod Security, image, runtime mapping and DR blockers.
- 2026-08-10T07:54Z — Final post-change static verification: protected workflows=7, immutable action references=97, production runtime Kustomize validation PASS, StaticOnly storage contract PASS with the intentional PVC migration `skipped`, both PowerShell script parsers PASS, and `git diff --check` clean apart from existing line-ending warnings.
- 2026-08-10T07:56Z — Pod Security rollout now inspects live Deployment/StatefulSet/DaemonSet templates before labeling the namespace. Production dry-run correctly fails with 23 non-compliant live containers (application services, Postgres/Rabbit/Redis, Vault CSI and seccomp installer); rendered production workload checker remains `TOTAL_NONCOMPLIANT_CONTAINERS=0`. No label or restart was issued.
- 2026-08-10T07:58Z — Documented the live Pod Security preflight in the remaining-gates runbook so `apply=true` cannot be used as a substitute for a reviewed workload rollout.
- 2026-08-10T08:00Z — Audited the five strict DR evidence schemas. The repository intentionally does not fabricate `database-restore-drill.json`, `vault-recovery-drill.json`, `harbor-clean-node-test.json`, `control-plane-rebuild-drill.json` or `application-restore-smoke.json`; each requires a real protected operation and measured RPO/RTO. The strict validator remains fail-closed until those owner-run drills produce evidence without secret fields.
- 2026-08-10T08:10Z — Connected the digest-only GitOps promotion workflow to a pinned Cosign v2.4.1 verifier. Every promotion now validates Harbor project/image/digest format and verifies both the keyless signature and SLSA provenance against the approved `container-release.yml` GitHub OIDC identity before opening a PR. Added `validate-gitops-promotion-contract.py` and wired it into the DevSecOps gate; promotion contract, workflow action pins (20 workflows/97 references), protected-workflow contract, production render, runtime contracts and diff checks pass. Local signature verification remains intentionally blocked because Cosign/Harbor credentials are not present on the workstation; no production mutation was performed.
- 2026-08-10T08:20Z — Added a mandatory `quality-security` preflight to `container-release.yml`; the matrix release/push/sign jobs now require backend restore/build, frontend production dependency audit/build, Trivy filesystem secret/vulnerability/misconfiguration scan, manifest policy/secret/container contracts and production runtime validation to pass first. Workflow YAML, action pins (20 workflows/101 references), GitOps promotion contract, protected-workflow contract, production render, runtime contracts and diff checks pass. Harbor push remains CI-only and no cluster mutation was performed.
- 2026-08-10T08:25Z — Fresh read-only production go-live validation against `artifacts/kubeconfig-production.yaml`: API connectivity, five Ready nodes, application readiness, immutable rendered images, secret scan, Linkerd control plane and sanitized baseline pass. The gate remains fail-closed on live `his-hope` Pod Security=`privileged`, workload image drift, stale appointment gRPC runtime mapping, reviewed database-continuity PVC=`local-path`, and five missing measured DR evidence files. No production mutation was attempted.
- 2026-08-10T08:30Z — Added `validate-container-release-contract.py` and wired it into the DevSecOps gate and the release preflight itself. It enforces that `quality-security` precedes the matrix, Harbor push remains explicitly gated, and Cosign signing/attestation remain present. Container-release contract, GitOps promotion contract, workflow YAML, action pins (20 workflows/101 references), protected-workflow contract and Python compilation pass.
- 2026-08-10T08:40Z — Added the protected Harbor clean-node DR drill: `test-harbor-clean-node.ps1` requires a Harbor digest, pins execution to an operator-selected node, records redacted measured RTO and pull verification, cleans the isolated namespace, and blocks production by default. Added `.github/workflows/harbor-clean-node-drill.yml`; DR validation now supports `-OnlyFile` so a single real drill can be validated without fabricating the other four. Dry-run, PowerShell parsers, workflow YAML, action pins (21 workflows/104 references), protected-workflow contract and diff checks pass. No cluster mutation was performed.
- 2026-08-10T08:45Z — Read-only review of the production Pod Security workflow confirms the boundary bootstrap and restricted-workload preflight run before any label mutation, and production apply remains protected by `inputs.apply`, `-AllowProduction` and the GitHub `production` environment. No workload label or restart was issued.
- 2026-08-10T08:50Z — Final repository static sweep after the Harbor DR addition: workflow pins (21/104), protected workflows (8), container-release and GitOps promotion contracts, manifest-secret scan, container-build contract, production runtime contract and 161-document Kustomize render all pass. This does not convert the live go-live blockers or unperformed DR drills into pass.
- 2026-08-10T09:00Z — Added protected `cnpg-restore-drill.yml` and `test-cnpg-restore-drill.ps1`. The drill requires a reviewed manifest and isolated namespace, rejects production namespaces, waits for a healthy CNPG cluster, verifies `SELECT 1`, records measured RPO/RTO without secret fields, and deletes the temporary target. Dry-run and parser checks pass; no database restore or production mutation was performed.
- 2026-08-10T09:10Z — Added protected `vault-recovery-drill.yml` and `test-vault-recovery-drill.ps1`. The drill restarts one Vault StatefulSet member only after approval, verifies TLS Vault status remains initialized/unsealed after restart (Azure Key Vault auto-unseal/Raft recovery), records redacted RTO and never emits recovery keys/tokens. Dry-run, parser, workflow YAML, action pins (23 workflows/110 references) and protected-workflow contract (10 workflows) pass; no Vault pod was restarted.
- 2026-08-10T09:15Z — Fresh read-only `validate-k3s-remaining-gates.ps1` against production: OTEL collectors pass (2 Ready), while signature provider, Pod Security (`privileged`), CSI/replicated storage, Argo CD installation and host Azure-backup service evidence remain blocked/fail. No cluster mutation was performed.
- 2026-08-10T09:20Z — Argo CD bootstrap preflight against the explicit production kubeconfig verified the pinned HA v3.4.1 manifest checksum and retained `WhatIf`; the GitOps bootstrap contract passes (9 Applications, retry/shared-resource guards, seven health customizations, explicit project allow-list and manual production sync). Argo CD remains absent live because no apply was authorized.
- 2026-08-10T09:30Z — Hardened the protected K3s backup-agent workflow to inventory the newest embedded-etcd snapshot on all three control-plane servers and fail unless every snapshot is at most 30 minutes old. The evidence is host/file/age metadata only; SAS, Vault and SSH values are not emitted. Backup-agent contract, workflow YAML, action pins (23/111) and protected-workflow contract pass; no Ansible apply or host mutation was performed.
- 2026-08-10T09:40Z — Consolidated static verification after backup-agent freshness changes: workflow pins (23/111), protected workflows (10), backup-agent, container-release, GitOps promotion, manifest-secret, container-build, production runtime contracts and 161-document Kustomize render all pass. Existing LF/CRLF notices are non-failing Git diagnostics.
- 2026-08-10T09:45Z — Added signed/unsigned server-side admission probes to the protected Sigstore Policy Controller workflow. The signed Harbor digest must be accepted and the unsigned Harbor digest must be rejected after controller apply; no Pod is created. Signature controller source contract now includes this probe and passes; PowerShell parser, workflow YAML, action pins (23/111) and protected-workflow contract pass. The live probe was not claimed because the controller is not installed.
- 2026-08-10T09:50Z — Read-only ingress smoke from the workstation: `http://app.his-hope.local/` returned 200, while unauthenticated `/api/v1/patients/search` and `/api/v1/dashboard/stats` both returned 401 as required by the authorization-negative path. No authenticated API correctness was claimed because no protected token was used.
- 2026-08-10T10:00Z — Final parser/contract refresh: all six new/changed PowerShell scripts parse; workflow pins (23/111), protected workflows (10), backup-agent contract and signature-controller contract (including admission probe) pass. Live admission, authenticated API, DR and production rollout gates remain unclaimed until their protected operations run.
- 2026-08-10T08:18Z — Closed the repository-side DORA measurement gap: added a dependency-free GitHub Actions collector for production promotion runs that emits auditable JSON/OpenMetrics for deployment frequency, commit-to-production lead time (p50/p95), change-failure rate and MTTR (p50/p95). Added the scheduled/manual producer workflow, production Grafana queries, and DORA contract checks; DORA validator, observability contract, action-pin/protected-workflow contracts and the 161-document production render pass. The first real time-series requires the scheduled workflow to run with the protected GitHub token; no fabricated metrics were recorded.
- 2026-08-10T08:19Z — Added a protected Alertmanager notification E2E workflow and correlation-aware test script. It posts a synthetic critical alert, verifies Alertmanager acceptance, and requires a dedicated receiver to confirm delivery; dry-run remains the default and production execution requires explicit `run_test=true` plus the protected receiver URL/token. Static observability and protected-workflow contracts pass; end-to-end notification delivery remains unclaimed until the protected receiver secrets are provisioned and the workflow is executed.
- 2026-08-10T08:20Z — Wired the Alertmanager notification E2E into the production go-live aggregation. A missing protected URL/receiver now fails the collected gate instead of allowing a ConfigMap-only pass. Read-only live refresh remains fail-closed: OTEL/observability pass; signature provider, Pod Security, replicated CSI, Argo CD and host Azure-backup evidence are blocked/fail; go-live additionally reports image drift, stale appointment runtime mapping, local-path database-continuity storage and five missing DR drills. No cluster mutation or synthetic production alert was sent.
- 2026-08-10T08:21Z — Tightened the Alertmanager probe to require both firing and resolved delivery for the same correlation id, matching `send_resolved: true`; dry-run, observability contract, workflow pins and protected-workflow contract pass. No live receiver was contacted.
- 2026-08-10T08:22Z — Added a unit test for DORA failure-rate, lead-time and recovery calculations; `python -m unittest discover -s scripts/tests -p 'test_*.py' -v` passes. The scheduled DORA workflow now runs this test before collecting production promotion metrics.
- 2026-08-10T08:23Z — Production go-live workflow now runs observability/DORA static contracts and the DORA unit suite, and aggregates that result with runtime gates. Workflow YAML, action pins (25/115), protected-workflow contract and diff checks pass.
- 2026-08-10T08:28Z — Added protected control-plane rebuild and application-restore smoke drills. The control-plane drill uses a reviewed serial Ansible cluster-reset playbook and verifies API readiness/all nodes; the application drill enqueues an isolated continuity restore, then checks readiness, OIDC discovery, 401/403 authorization-negative, authenticated API 2xx and Deployment availability. Both default to dry-run, require explicit production approval, and write measured evidence only after real execution. PowerShell parsers, workflow YAML, static DR contract, workflow pins (27/121) and protected-workflow contract (13) pass. No destructive rebuild or restore was executed.
- 2026-08-10T08:31Z — Tightened protected-workflow checks to reject bearer-token command-line injection in Alertmanager/application restore workflows and require protected Ansible vault inputs for control-plane rebuild. Workflow pins (27/121), protected workflows (13) and workflow YAML parsing pass. No secrets were printed or used.
- 2026-08-10T08:29Z — Fresh read-only production go-live validation after the DR additions: API, five nodes, application readiness, immutable rendered images, secret scan, Linkerd and baseline pass. The live gate remains exit 80 on `his-hope` Pod Security=`privileged`, workload image drift, stale appointment mapping, reviewed `local-path` storage and five unavailable measured DR evidence files. No mutation, rebuild or restore was performed.
- 2026-08-10T08:35Z — Added the K3s host security contract and explicit `terminated-pod-gc-threshold=10` to server/agent kubelet configuration. The contract checks secrets encryption, NodeRestriction/EventRateLimit, audit policy, PSA, swap/sysctl, kubelet timeout/GC and production node label governance; static validation passes.
- 2026-08-10T08:36Z — Added the Linkerd policy contract. It verifies every declared Server has a matching ServerAuthorization, mesh TLS is fail-closed, gRPC policies have no unauthenticated network exception, control-plane webhook failure policy/resources/identity TLS are present, and K3s CNI paths/rollout ordering are wired. Static validation passes; live positive/negative mTLS probe remains unexecuted.
- 2026-08-10T08:37Z — Post-change static verification: workflow action pins 27/121, K3s host-security contract, Linkerd policy contract, production runtime contract and 161-document Kustomize render all pass. No production mutation was performed.
- 2026-08-10T08:45Z — Added protected K3s Secrets Encryption rotation automation: serial Ansible procedure, reviewed etcd-snapshot prerequisite, dry-run/apply guard, production environment protection and redacted evidence. The procedure follows the HA order (rotate on S1, wait for `reencrypt_finished`, restart S1 then remaining servers); no rotation was executed.
- 2026-08-10T08:46Z — Added explicit system node labels to the K3s server template/inventory and kept worker app/data/observability labels. Host-security contract, protected-workflow contract (14 workflows), action pins (28/123), PowerShell parser and production Kustomize/runtime checks pass. Ansible syntax check is environment-blocked on Windows by Ansible console I/O initialization; no live host mutation was attempted.
- 2026-08-10T08:41Z — Rotation script dry-run passed and emitted only `status=skipped`; no SSH key, Vault password, snapshot content or cluster mutation was used. Current read-only go-live remains exit 80 with Pod Security, image drift, local-path storage, appointment runtime mapping and five unavailable DR evidence files; remaining-gates remains exit 30 with signature provider, CSI storage, Argo CD and host backup evidence blocked.
- 2026-08-10T08:44Z — Production Linkerd/SPIRE mTLS runtime probe passed read-only against `his-hope`: NetworkPolicy ports 4140/4143, init reasons for destination/identity/injector all `Completed`, identity-service injector canary contains `linkerd-proxy`, six backend liveness requests return HTTP 200 through the mesh, and proxy metrics contain `tls="true"`. The previous false failure on `/health` was corrected to use `/health/live`; negative gRPC authorization and admission-enforcement probes remain unexecuted.
- 2026-08-10T08:50Z — Added protected `linkerd-mtls-policy-e2e.yml` and `test-linkerd-mtls-policy.ps1`. When explicitly applied with a digest-pinned grpcurl image, it creates temporary authorized/unauthorized service-account probes, requires injector proxies, verifies positive gRPC authorization and rejects the unauthorized call, then cleans up. Default dry-run is non-mutating; static workflow/action-pin/protected contracts remain required before execution.
- 2026-08-10T08:52Z — `dotnet build His.Hope.sln --no-restore --verbosity:minimal` passed with 0 warnings and 0 errors. This validates repository compilation only; it does not replace protected cluster rollout, authenticated API smoke, negative mTLS E2E or DR evidence.
- 2026-08-10T09:00Z — `npm run lint` passed across frontend-foundation, mobile-foundation, admin, dashboard, clinical and mobile workspaces (0 errors; existing lint warnings remain and are reported by CI).
- 2026-08-10T09:05Z — `dotnet test His.Hope.sln --no-build --filter "FullyQualifiedName!~Integration"` passed all 632 non-integration tests (0 failed, 0 skipped). Integration/Testcontainers coverage remains separate and requires its external dependencies.
- 2026-08-10T09:12Z — Playwright full run was environment-blocked: all initial navigations hit `ERR_CONNECTION_REFUSED` because localhost:8081–8083 app servers were not running, then the 300-second command timeout occurred. Fixed E2E URL hard-coding by adding environment-driven `E2E_CLINICAL_URL`, `E2E_DASHBOARD_URL`, `E2E_ADMIN_URL` (localhost defaults retained), updated CI variables and prerequisite runbook; Node syntax, Playwright test discovery (399 tests) and URL configuration checks pass. A real E2E run still requires reachable app URLs and protected auth probe secrets.
- 2026-08-10T08:55Z — Tightened Linkerd static policy contract to require pinned semantic chart versions for CRDs/control-plane/Viz, Helm `--wait/--timeout`, and reject `edge`; contract passes with configured versions 1.8.0, 1.16.11 and 30.12.11. Runtime image digest verification remains a deployment-time check.
- 2026-08-10T08:58Z — Added `validate-k3s-secrets-rotation-contract.py`; it verifies the reviewed snapshot prerequisite, HA serial sequence, `reencrypt_finished` wait, dry-run/apply guard and protected credential handling. Contract, protected-workflow and action-pin checks pass.
- 2026-08-10T09:15Z — Cleaned Playwright-generated report/test artefacts after the environment-blocked full run. Re-validated workflow action pins (29/126), protected workflows (15), K3s host-security contract, Linkerd policy contract, secrets-rotation contract, Node syntax and `git diff --check`; all pass. Playwright discovery remains 399 tests; execution still requires reachable configured app URLs and protected authentication.
- 2026-08-10T09:20Z — Tightened `platform-quality-gates.yml`: the backend gate now runs the non-integration .NET suite, and the authenticated browser gate now executes the complete Chromium Playwright suite (399 discovered tests) after protected URL/auth prerequisites. Action pins, protected-workflow contract, production Kustomize render/runtime contract and Playwright discovery pass; real E2E execution remains environment-blocked until protected endpoints are configured and reachable.
- 2026-08-10T09:25Z — Verification refresh: .NET build passed with 0 warnings/0 errors; non-integration test filter passed (632 tests previously enumerated; integration assemblies had no matching tests); frontend lint passed with 0 errors and existing warnings; migration, reliability, observability, GitOps, admission and storage static contracts passed. Read-only ingress smoke returned HTTP 200 for the app shell and HTTP 401 for protected dashboard/search/SignalR endpoints. Production go-live remains fail-closed on live Pod Security, image/storage/runtime drift and missing measured DR evidence.
- 2026-08-10T09:30Z — Protected Pod Security rollout preflight was re-run read-only. Production render remains compliant (`TOTAL_NONCOMPLIANT_CONTAINERS=0`), but the live namespace snapshot reports 23 non-compliant containers (old SPIRE sidecars, data stores, seccomp/CSI agents). The script correctly refused to proceed because the reviewed production workload revision has not first been synchronized; no labels, restarts or applies were performed. Security-boundary bootstrap dry-run confirms `his-hope-data` and `his-hope-system` are still pending.
- 2026-08-10T09:35Z — Corrected the platform CI configuration mismatch: Release build now precedes the Release `--no-build` test command. Local Release build passed with 0 errors (11 existing compiler warnings), Release non-integration tests passed, and action-pin/protected-workflow contracts remain green.
- 2026-08-10T09:40Z — Fixed the production backend liveness contract for billing, clinical, lab, patient and pharmacy services: liveness now uses `/health/live`, while `/health/ready` remains responsible for dependency readiness. Runtime validator now fails if any production service regresses to aggregate `/health`; prod/staging/dev renders and runtime contracts pass. This change is source-only and awaits the reviewed image/digest rollout.
- 2026-08-10T09:50Z — Hardened Harbor supply-chain workflows for the internal TLS registry. Container release and digest-only GitOps promotion now require the protected `production` secret `HARBOR_CA_CHAIN_B64`, validate its X.509 payload, install it into the GitHub runner trust store, and only then login/pull/verify with Cosign. Container-release/GitOps contracts, protected-workflow contract and action-pin validation pass; the secret value and any registry operation were not accessed from this workstation.
- 2026-08-10T09:55Z — Read-only production refresh with `artifacts/kubeconfig-production.yaml`: go-live exit 80 and remaining-gates exit 30. API connectivity, five Ready nodes, application readiness, immutable rendered refs, secret scan, Linkerd and OTEL pass. Pod Security (`privileged`), live image drift, reviewed `local-path` storage, appointment runtime mapping, missing five DR evidence files, signature admission, CSI, Argo CD and host backup evidence remain fail/blocked. No apply, label, rollout or restart was performed.
- 2026-08-10T10:05Z — Corrected Harbor CA placement: only the matrix release job (not source-only quality preflight) now runs in protected `production` environment and installs `HARBOR_CA_CHAIN_B64` before Harbor login, digest scan, Cosign signing and attestation. Container-release contract now requires this environment boundary; container/GitOps contracts, YAML, action pins and protected-workflow checks pass.
- 2026-08-10T10:15Z — Certificate inventory clarified without mutation: `vault_pki_ca_chain.pem` contains one intermediate (`His.Hope Internal Intermediate CA`, thumbprint `EAB538702B76EBEBEF916BEE061C4720124164BE`) and is not Harbor trust; `harbor_cert.pem` is issued by `His.Hope Local CA`, so CI must use `his_hope_ca.pem` for `HARBOR_CA_CHAIN_B64`. The intermediate is already present in the current-user stores; no new certificate was installed.
- 2026-08-10T10:25Z — Aligned admission policy authority with the actual release pipeline: replaced the stale embedded public-key authority with Sigstore keyless verification restricted to `https://token.actions.githubusercontent.com`, the exact `Hung6066/micro` `container-release.yml` workflow on `main`/semver tags, and the SLSA provenance predicate. Signature-controller/admission source contracts and Argo scope pass; live controller/admission enforcement remains blocked because the controller is not installed.
- 2026-08-10T10:30Z — Runtime refresh after policy changes remains unchanged and fail-closed: go-live exit 80, remaining-gates exit 30. API/five nodes/application readiness/immutable render/secret scan/Linkerd/OTEL pass; live Pod Security, image drift, local-path storage, appointment mapping, missing DR evidence, signature provider, CSI, Argo CD and host backup remain fail/blocked. No cluster mutation was performed.
- 2026-08-10T10:40Z — Added `Persistence:MigrationOnly=true` to all eight EF owners (including Identity seed bypass) so a reviewed digest can run one-shot migrations and exit without serving API traffic. The migration contract now checks all eight source paths; Release build passed with 0 errors and the non-integration suite passed (632 tests). No migration Job was applied and no production database was touched.
- 2026-08-10T10:45Z — Migration contract refresh passed: 8-context artifact coverage, manifest hashes, destructive-SQL review, all eight migration-only source paths, API startup migration isolation and migration-only isolation. Production Kustomize/runtime render remains pass.
- 2026-08-10T10:50Z — Added seven digest-transformed Argo `PreSync` migration Jobs (`k8s/jobs/production-migration-job.yaml`) with per-service ServiceAccounts, SPIRE JWT bootstrap, Vault database leases, EF one-shot flags, `backoffLimit: 0`, 900-second deadline and explicit security-exception inventory. Rendered production manifest has 168 documents; migration contract, runtime Kustomize, release render, restricted-workload static check and Argo project scope pass. Jobs were not applied; live migration completion remains unproven.
- 2026-08-10T10:55Z — Read-only runtime refresh after adding migration hooks: rendered images remain 22 immutable and five nodes/application/Linkerd/OTEL checks pass. Go-live remains exit 80 because live image drift, privileged namespace, local-path storage, appointment runtime mapping and five missing measured DR artifacts are unchanged; remaining-gates remains exit 30 because signature provider, CSI, Argo CD and host backup evidence are unavailable. No production mutation was performed.
- 2026-08-10T11:00Z — Migration hook render was corrected so namespace-wide Linkerd annotations cannot inject a proxy into one-shot Jobs; rendered Job templates now explicitly show `linkerd.io/inject: disabled` and inject Redis TLS connection/CA inputs. Migration contract, restricted-workload static check, Kustomize runtime and release render remain pass. This is source-only; no Job or rollout was applied.
- 2026-08-10T11:05Z — Corrected Argo ordering: migration hooks are `Sync` wave 20, serving Deployments wave 30 and Ingress wave 40, allowing in-application configuration/secret resources to exist before migration while preventing API rollout before DDL. All seven rendered hooks are Sync wave 20; migration contract remains pass. Runtime execution is still pending.
- 2026-08-10T11:15Z — Static verification refresh after the migration ordering fix: container-release, GitOps promotion, protected-workflow, action-pin (29 workflows/126 references), Argo project scope, signature-controller/admission source, migration, runtime Kustomize and release-render contracts all pass. Fresh render contains 168 documents, seven Sync wave-20 migration Jobs, Deployment wave 30 and Ingress wave 40; restricted-workload check reports `TOTAL_NONCOMPLIANT_CONTAINERS=0`, and `git diff --check` exits 0. This remains repository evidence only; no production apply or rollout was performed.
- 2026-08-10T11:20Z — Live refresh against the explicit production kubeconfig remains fail-closed: go-live exit 80 (live privileged Pod Security, image drift, local-path continuity PVC, stale appointment mapping and five unavailable DR artifacts); remaining-gates exit 30 (no signature provider, no replicated CSI, Argo CD absent, host Azure-backup evidence unavailable; OTEL collectors pass). Reliability also detects live API startup-migration drift. No production apply, label, rollout or restart was performed.
- 2026-08-10T11:35Z — The non-integration .NET gate was hardened against runner VSTest handshake flakiness by setting `VSTEST_CONNECTION_TIMEOUT=300` and `RunConfiguration.MaxCpuCount=1`; the prior abort reproduced only under parallel execution, while the serial run passes. Build=0, serial non-integration tests=0, npm lint=0, workflow action pins=29/126, protected workflows=15 and YAML/diff checks pass.
- 2026-08-10T11:40Z — GitOps plane validation now passes for data (`his-hope-data`, 3 digest images), system (`his-hope-system`) and bootstrap (`argocd`) overlays. The earlier data-plane failure was an incorrect validator namespace argument, not a manifest defect.
- 2026-08-10T11:45Z — Read-only live inventory confirms five nodes Ready on K3s v1.35.5+k3s1, but `his-hope` still has `pod-security.kubernetes.io/enforce=privileged`, no Argo CD deployment, and workload references remain in the old nested `harbor.../his-hope/his-hope/...` path; appointment-service has no explicit reviewed gRPC environment entry (only envFrom). This evidence requires a controlled GitOps sync/rollout and was not mutated.
- 2026-08-10T12:00Z — Hardened the production go-live workflow: skipped validation steps are now failures (only `success` satisfies the aggregate gate), the workflow has a 45-minute timeout, and it is included in the protected-workflow contract. Protected workflows=16, action pins=29/126, YAML and diff checks pass.
- 2026-08-10T12:10Z — Added a regression assertion to `validate-protected-workflow-contract.py` requiring the production go-live aggregate to reject skipped/unsuccessful steps and retain failed-evidence reporting. Protected-workflow, action-pin, container-release and GitOps-promotion contracts all pass.
- 2026-08-10T12:20Z — Static refresh passes migration, runtime/release Kustomize, restricted workload, host-security, Linkerd policy, observability and script-unit contracts; fresh render has 168 documents and seven Sync wave-20 migration Jobs. Read-only live refresh remains go-live exit 80 and remaining-gates exit 30 with the same Pod Security, image/storage/runtime drift, absent admission/CSI/Argo/backup evidence and five unavailable DR artifacts. No production mutation was performed.
- 2026-08-10T12:30Z — Strict release gate with explicit `-RequireCluster -RequirePodSecurity` returns exit 30 solely on the live namespace label (`privileged` instead of `restricted`), proving the gate is fail-closed rather than silently skipping the production security requirement.
- 2026-08-10T12:40Z — Read-only Pod Security rollout preflight renders compliant production workloads (`TOTAL_NONCOMPLIANT_CONTAINERS=0`) but correctly refuses enforcement because the live old revisions contain 23 non-compliant application/data/CSI containers. No namespace label or restart was issued.
- 2026-08-10T12:50Z — Argo CD HA v3.4.1 production bootstrap preflight with the pinned SHA-256 and `-WhatIf` passes; no namespace creation or manifest apply occurred. Live Argo controller remains absent until the protected change window.
- 2026-08-10T13:00Z — Production Longhorn 1.12.0 and Sigstore Policy Controller 0.10.5 bootstrap scripts both pass protected dry-run/Helm verification; no CRD, storage class, webhook or workload was changed.
- 2026-08-10T13:10Z — Live refresh 6 confirms no external rollout has occurred: go-live remains exit 80 and remaining-gates exit 30 with the same Pod Security/image/storage/runtime drift and unavailable DR/admission/CSI/Argo/backup evidence. No mutation was issued.
- 2026-08-10T13:20Z — Added `validate-production-cutover-inputs.ps1`, a redacted read-only preflight. Current workstation inputs pass kubeconfig, Azure env key-set and both CA-chain checks; the operator-credential mode is correctly blocked because no private `id_deploy`/`id_deploy.pem` exists under `D:\secure\his-hope`. No credential value was printed.
- 2026-08-10T13:35Z — Re-ran the input preflight with explicit `C:\Users\Admin\.ssh\id_deploy`; all inputs pass. Read-only SSH connectivity also passes for all five K3s nodes using inventory users (`master01`, `master02`, `node01`, `node02`, `node03`) and both load balancers as `root`. No remote mutation command was executed.
- 2026-08-10T13:50Z — WSL Ansible validation is now usable with ephemeral mode-0600 copies of the key/Vault password: all K3s and LB inventory `ansible -m ping` checks pass; all enterprise playbooks pass `--syntax-check`; server `00-preflight.yml --check` passes all assertions including NTP, swap and disk. Planned `changed=2` tasks were check-mode diffs only; no apply was run.
- 2026-08-10T14:05Z — HAProxy/Keepalived read-only validation: LB `05-configure-external-lb.yml --check` passes on both nodes; `haproxy -c` reports valid configuration; both services are active; VIP `172.16.102.100` is present on `lb-01` and TCP/6443 succeeds from both LB nodes. Workstation-side TCP to the isolated VIP is not routed, while the production kubeconfig uses the local API proxy `127.0.0.1:16443`; no LB mutation was performed.
- 2026-08-10T14:25Z — Fixed the K3s server readiness probe for Ansible check mode (`check_mode: false` on the read-only `/readyz` command). `10-bootstrap-k3s.yml --check` now passes all three control-plane hosts; planned handler/config changes remain check-mode only and no K3s restart occurred.
- 2026-08-10T14:35Z — Fixed worker preflight fact scope by gathering control-plane facts before the cross-host hostname assertion. `15-bootstrap-workers.yml --check` now passes both workers (`k3s-worker-1` and `k3s-worker-2`), including swap, disk, NTP and production-input assertions; only the planned agent config writes are reported as `changed`, with no remote mutation.
- 2026-08-10T14:40Z — Read-only `20-verify-cluster.yml` passed on `k3s-server-1`: five expected Ready nodes, Kubernetes Secrets encryption enabled, audit/PSA artifacts present and API `/readyz`=`ok`. K3s host-security, protected-workflow (16), action-pin (29/126) contracts and `git diff --check` pass; LF/CRLF notices are non-failing diagnostics.
- 2026-08-10T14:50Z — Corrected `validate-reliability-platform.ps1` so migration isolation scopes the startup-migration check to API `Deployment` documents and does not reject the seven intentional one-shot migration `Job` hooks. Fresh runtime validation now passes migration isolation, API connectivity, 48 Ready pods and 20 available Deployments. GitOps data/system/bootstrap planes render and validate successfully; all Python DevSecOps contracts (container build/release, promotion, backup, host security, Linkerd, secrets rotation, manifest secrets, protected workflows and action pins) pass, and all 94 PowerShell scripts parse.
- 2026-08-10T15:00Z — Fresh live gate refresh remains fail-closed: go-live exit 80 with `his-hope` Pod Security=`privileged`, all 27 live workload references drifting from the reviewed immutable render, reviewed continuity storage=`local-path`, stale live appointment runtime mapping and five missing measured DR artifacts. Remaining-gates exit 30 with no live signature provider, replicated CSI, Argo CD or host Azure-backup evidence; OTEL (2 collectors) passes. No production apply, label, rollout or restart was performed.
- 2026-08-10T15:10Z — Re-rendered production Kustomize from the current GitOps overlay: 168 documents, no nested Harbor image path, explicit appointment Patient gRPC mapping and seven migration Jobs. Runtime Kustomize, release-render, migration, container-release and GitOps-promotion contracts pass; the render is repository evidence only and was not applied to production.
- 2026-08-10T15:20Z — Reviewed protected production orchestration: all 16 mutation/evidence workflows declare concurrency, protected environments, bounded timeouts and explicit apply/run guards; production go-live aggregates every required step and rejects skipped outcomes. Protected-workflow, action-pin (29/126) and container-release contracts pass. No workflow was dispatched.
- 2026-08-10T15:30Z — Hardened `validate-protected-workflow-contract.py` to require every protected `apply`/`run_test` dispatch input to be boolean and default `false`. The protected workflow contract passes for all 16 workflows; no mutation workflow was dispatched.
- 2026-08-10T15:40Z — Replaced production frontend `newTag: latest` values with the actual release SHA `7b77f09d8a38393df3a8cf0ab2e99bd6e6f1d9be`, retaining the verified digests. Fresh prod render has 168 documents, zero `latest` image references and zero nested Harbor paths; Kustomize runtime/release, container-release, GitOps-promotion and manifest-secret contracts pass.
- 2026-08-10T15:50Z — Fixed digest-only promotion drift: `update-gitops-digest.ps1` now updates the target image `newTag` to the supplied `ReleaseSha` together with its digest, and the GitOps promotion contract verifies this behavior. A temporary-file promotion test passed; no repository release or cluster mutation was performed.
- 2026-08-10T16:00Z — Fixed a production overlay omission: `database-continuity-storage-patch.yaml` existed but was not included by `k8s/overlays/prod/kustomization.yaml`. After adding it, the production render explicitly selects `longhorn` for the continuity backup PVC; Kustomize runtime/release and storage-backup contracts pass. This is source-only; the existing live PVC still requires the protected migration workflow and restore drill.
- 2026-08-10T16:10Z — Live refresh after the storage overlay fix: source-side storage drift is cleared, but the bound live `database-continuity` PVC remains `local-path` (immutable and migration-gated). Go-live remains exit 80 only on live Pod Security, image drift, live PVC, appointment mapping and five missing DR artifacts; no production mutation was performed.
- 2026-08-10T16:20Z — Read-only PVC migration preflight passed against production: source `his-hope-database-continuity-backups` is `local-path`/10Gi, target is pinned `longhorn`, copy node is `k3s-worker-2`, and the script confirms no PVC/pod/scale mutation. Apply remains protected by `-Apply -AllowProduction` and the change window.
- 2026-08-10T16:35Z — Regression verification after overlay/promotion fixes: `dotnet build His.Hope.sln --no-restore` passed with 0 errors (existing warnings remain), and non-integration .NET tests passed (632 passed, 0 failed, 0 skipped). No production mutation was performed.
- 2026-08-10T16:45Z — Frontend regression verification passed: `npm run lint` completed with 0 errors across frontend-foundation, mobile-foundation, admin, dashboard, clinical and mobile workspaces; existing lint warnings remain non-failing.
- 2026-08-10T17:00Z — Fresh protected bootstrap dry-runs: Argo CD HA v3.4.1 checksum, Longhorn 1.12.0, Sigstore Policy Controller 0.10.5 and K3s security-boundary manifests all validate without mutation. Pod Security rollout intentionally refuses to continue because live old revisions still contain 23 non-compliant containers; rendered production workloads remain compliant. No workflow/apply was executed.
- 2026-08-10T17:10Z — Bổ sung runbook cutover tuần tự trong change window: Longhorn → Sigstore admission → Argo CD → migration PVC → digest promotion/sync waves → Pod Security rollout → năm DR drills → aggregate go-live. Runbook yêu cầu owner approval, evidence từng bước, dừng khi gate không pass và cấm manual `kubectl apply`; static validators và `git diff --check` được giữ nguyên trạng thái pass. Đây là tài liệu điều phối, chưa dispatch workflow hay thay đổi production.
- 2026-08-10T17:25Z — Fail-closed evidence hardening: các workflow `database-continuity-pvc-migration.yml` và `pod-security-production-rollout.yml` nay dùng `if-no-files-found: error`; `validate-protected-workflow-contract.py` có regression check bắt buộc mọi protected workflow upload evidence phải lỗi khi artifact thiếu. Toàn bộ Python validators, runtime Kustomize và production release render pass; production vẫn chưa được mutate.
- 2026-08-10T13:13Z — Read-only runtime refresh bằng `artifacts/kubeconfig-production.yaml`: reliability platform pass (48 Ready pods, 20 available Deployments), API/nodes/application health pass và Linkerd/OTEL pass. Go-live vẫn fail-closed (exit 80): namespace `his-hope` còn `privileged`, 27 workload image refs drift so với reviewed render, continuity PVC còn `local-path`, appointment runtime mapping live còn stale, và thiếu 5 DR evidence files. Remaining-gates exit 30: chưa có live signature provider, replicated CSI, Argo CD hoặc host Azure-backup evidence. Không có apply/restart/label/rollout.
- 2026-08-10T13:14Z — Static contract refresh: database migration, observability/DORA, Sigstore admission source và Argo CD bootstrap contracts đều pass. Các kết quả này chỉ chứng minh source/workflow contract; admission/CSI/Argo và DR vẫn chưa có runtime evidence.
- 2026-08-10T13:15Z — Hardened DR evidence freshness: `validate-dr-evidence.ps1` now rejects evidence older than 168 hours or timestamps more than five minutes in the future; missing evidence remains `blocked` (exit 70). Current production directory still lacks all five required DR files, so no go-live claim is possible. Protected workflow contract remains pass.
- 2026-08-10T13:15Z — Freshness regression verification: PowerShell parse, DR static mode, protected-workflow contract (16) and action-pin gate (29 workflows/126 references) all pass; `git diff --check` has no content errors (only existing LF/CRLF notices).
- 2026-08-10T13:17Z — Read-only SSH backup audit found the concrete Azure failure: snapshot timers on all three control-plane nodes are enabled/active, but the last service runs exited status 1 with Azure Blob `403 AuthenticationFailed`. The local secure SAS expires 2027-08-06 and its SHA-256 differs from the remote `/etc/his-hope/backup.env` value on all nodes. `30-backup-agents.yml --check` passes and reports only the protected env/service/script updates; no apply or restart was executed.
- 2026-08-10T13:20Z — Corrected backup rollout verification for the oneshot systemd service: protected `apply=true` now runs one controlled snapshot smoke, checks timer state plus `systemctl show ... Result=success`, then checks fresh etcd snapshot age. The backup-agent contract, YAML parse, protected-workflow and action-pin gates pass; no workflow was dispatched.
- 2026-08-10T13:22Z — Redacted remote comparison confirms endpoint/container match the secure env, while the remote SAS fingerprint differs; the remote account field is absent but the configured endpoint is fully qualified. The failure is therefore credential propagation, not DNS/container routing. No secret value was printed and no host mutation was performed.
- 2026-08-10T13:24Z — Updated the backup-agent runbook to document correct oneshot verification (`Result=success`) and the protected post-apply snapshot smoke. This closes the workflow/operational ambiguity without changing live hosts.
- 2026-08-10T13:26Z — Confirmed secret-source drift without exposing values: `vault_backup_sas_token` is loaded by `30-backup-agents.yml` from encrypted `ansible/enterprise-k3s/group_vars/vault.yml`; its redacted SHA-256 differs from the local secure env SAS. The runbook now explicitly warns that editing `D:\secure\his-hope\azure-production.env` alone cannot change the GitHub/Ansible deployment input.
- 2026-08-10T13:28Z — Corrected the backup runbook credential path from the incorrect `group_vars/all/vault.yml` to the actual encrypted `ansible/enterprise-k3s/group_vars/vault.yml`; repository search confirms no remaining incorrect reference.
- 2026-08-10T13:30Z — Added `verify-production-backup-restore.ps1` and its static contract test. The wrapper validates Azure Blob presence through the REST listing without exposing SAS, then delegates to the existing isolated CNPG restore drill; default mode is dry-run and production apply requires `-Apply -AllowProduction`. Against the current secure env, a non-empty backup object was found and the dry-run evidence was written; no Kubernetes namespace or production resource was changed.
- 2026-08-10T13:32Z — Completed ordered phase selection for the production runner: orchestrator imports now expose six phase tags, `-FromPhase/-ToPhase` accepts only a contiguous validated range, and `summary.json` records `requestedPhases`. Ansible syntax check and `--list-tags` show the expected phase order; validation-only run for `control-plane..workers` passes without contacting or mutating hosts.
- 2026-08-10T13:40Z — Hardened CI fail-closed behavior: container release `publish` now has a real boolean `false` default, and all DevSecOps evidence uploads now use `if-no-files-found: error`. Protected-workflow, container-release/build, action-pin and YAML validators pass; no workflow was dispatched.
- 2026-08-10T13:45Z — Added a container-release contract assertion for the boolean `publish=false` dispatch input. Container-release, protected-workflow, action-pin and Python unit gates pass after the change; no build/publish workflow was dispatched.
- 2026-08-10T13:55Z — Read-only SSH reconfirmed the Azure backup failure on all three control-plane servers `.7`, `.8` and `.9`: timers enabled/active but oneshot `Result=exit-code`, `ExecMainStatus=1`, Azure `403 AuthenticationFailed`. Hardened `k3s-backup-agent-rollout.yml` to require `AZURE_PRODUCTION_ENV_B64` and compare its SAS to decrypted `vault_backup_sas_token` before any Ansible syntax/check/apply. Backup-agent contract, protected workflow, action-pin and YAML validators pass; no host mutation occurred.
- 2026-08-10T13:42Z — Fresh repository/runtime gate refresh: backup-agent, protected-workflow and action-pin contracts pass; Argo CD bootstrap and admission source validators pass; prod Kustomize renders 168 documents with immutable image references and the appointment Patient gRPC mapping. Reliability runtime remains pass (48 Ready pods, 20 available Deployments), while go-live remains fail-closed (exit 80) on live `privileged` Pod Security, workload image drift, immutable local-path continuity PVC, stale live appointment mapping and five missing DR evidence files. No production apply, rollout or restart was performed.
- 2026-08-10T13:43Z — Production cutover preflight passes with the explicit encrypted Ansible Vault password path: kubeconfig parses, secure root and Azure env key set are present, and both CA files contain valid PEM blocks; values/private keys were not emitted. Full `run-k3s-production.ps1 -ValidationOnly -FromPhase preflight -ToPhase backup` also passes and records the six ordered phases without contacting or mutating hosts.
- 2026-08-10T13:44Z — Harbor public HTTPS validator passes read-only: `harbor.myduchospital.com` resolves to VIP `172.16.102.100`, TCP/443 is reachable, the `harbor-public` ingress has the expected host/TLS secret, and `harbor-public-tls` is present. No Harbor or cluster mutation was performed.
- 2026-08-10T13:44Z — TLS client verification using `D:\secure\his-hope\vault_pki_ca_chain.pem` and SNI/resolve to `172.16.102.100` returned HTTP 200 from `https://harbor.myduchospital.com/`; the enterprise CA chain is trusted for the Harbor endpoint on this workstation.
- 2026-08-10T13:45Z — Direct read-only TLS certificate inspection confirms `harbor.myduchospital.com` is issued by `His.Hope Internal Intermediate CA` (the issuer contained in `vault_pki_ca_chain.pem`), so the earlier inventory note claiming a different Harbor CA is stale for the current public endpoint; no certificate or secret was changed.
- 2026-08-10T13:46Z — Corrected the CI trust runbook to distinguish the public `harbor.myduchospital.com:443` CA chain from the legacy `harbor.his-hope.local:9443` registry CA. The documented `HARBOR_CA_CHAIN_B64` input must match the actual `HARBOR_REGISTRY` hostname; container-release and GitOps promotion contracts remain PASS.
- 2026-08-10T13:47Z — Fresh remaining-gates refresh is unchanged: observability (2 OTEL collectors) and reliability platform pass (48 Ready pods, 20 available Deployments), while signature provider, Pod Security (`privileged`), replicated CSI, Argo CD and Azure host-backup evidence remain blocked/fail. No production mutation occurred.
- 2026-08-10T13:48Z — Release gate refresh: production Kubernetes manifest validation passes and live K3s release validation passes toolchain skew, immutable images, API connectivity, five Ready nodes, application health and Linkerd. Storage/backup contract remains blocked only on runtime Azure injection, CSI restore evidence and protected migration; no mutation occurred.
- 2026-08-10T13:49Z — Reviewed `k3s-production-go-live-gate.yml`: all 14 production checks are collected with `continue-on-error`, then a final fail-closed step rejects every outcome other than `success`; evidence upload is `if-no-files-found: error`, and protected runtime inputs are removed in `always()`. Workflow/action-pin contracts remain pass.
- 2026-08-10T13:50Z — Full CI integrity refresh passes: all 29 workflow YAML files parse, protected workflow contract (16), action pins (29/126), manifest secret scan and production Kustomize render (168 documents) pass. No workflow dispatch or cluster mutation occurred.
- 2026-08-10T13:52Z — Protected bootstrap dry-runs pass without mutation: Argo CD HA v3.4.1 manifest checksum verified, Sigstore Policy Controller 0.10.5 chart verified, and security-boundary namespaces render correctly. All three commands require explicit production approval/`-Apply` before any cluster change.
- 2026-08-10T13:53Z — Read-only readiness snapshot passes: production cutover inputs (kubeconfig, Azure env, Vault password path and both CA files), Harbor public DNS/VIP/TLS checks, and full six-phase Ansible orchestration validation-only. No host or cluster mutation occurred.
- 2026-08-10T13:54Z — Saved fresh runtime artifacts `artifacts/evidence/production-go-live-latest.json`, `production-remaining-gates-latest.json` and `production-reliability-latest.json`. Results: go-live exit 80; remaining gates exit 30; reliability pass. The artifacts preserve the current fail-closed blockers and the passing 48-pod/20-deployment reliability evidence.
- 2026-08-10T13:55Z — Read-only SSH audit saved as `artifacts/evidence/azure-backup-host-audit-latest.json`: `.7`, `.8` and `.9` all have enabled/active timers, but the oneshot service returns `Result=exit-code`, `ExecMainStatus=1` and Azure `AuthenticationFailed`. No backup environment values were read, and no restart/apply was performed.
- 2026-08-10T13:56Z — Sanitized backup-host artifact scan found no SAS/token/password markers; backup-agent contract remains PASS. The runtime credential mismatch remains an explicit protected remediation, not an ignored failure.
- 2026-08-10T13:58Z — Local source consistency check now passes: `AZURE_STORAGE_SAS_TOKEN` from `D:\secure\his-hope\azure-production.env` matches decrypted `vault_backup_sas_token` without emitting values. The three hosts still require the protected backup-agent rollout to receive this corrected source.
- 2026-08-10T14:00Z — Re-ran `30-backup-agents.yml --check` with the synchronized source and ephemeral 0600 credentials: all three servers reachable, `failed=0`, `unreachable=0`; only the three expected agent file updates are planned. No apply/restart occurred.
- 2026-08-10T14:02Z — `verify-production-backup-restore.ps1` dry-run passes against the Azure env and reviewed CNPG manifest; Azure object listing succeeded and the restore target is isolated `his-hope-restore-drill`. Evidence saved to `artifacts/evidence/production-backup-restore-contract-latest.json`; no namespace, cluster or data was created.
- 2026-08-10T14:02Z — Added `scripts/validate-azure-blob-access.py` and wired it before every backup-agent Ansible syntax/check/apply path. The validator checks HTTPS endpoint/account, container-scoped SAS read/list permissions and performs a redacted Blob listing; current `D:\secure\his-hope\azure-production.env` returns `Azure Blob access: PASS`. Backup-agent contract, Python compilation, workflow YAML and `git diff --check` pass; no host or cluster mutation occurred.
- 2026-08-10T14:02Z — Wired the same redacted Azure Blob preflight into `cnpg-azure-backup-bootstrap.yml`, so CNPG ObjectStore apply cannot start with an expired/invalid SAS. Azure script contract, protected-workflow contract (16), all workflow YAML parsing and `git diff --check` pass; no workflow was dispatched.
- 2026-08-10T14:02Z — Added the redacted Blob access preflight to `k3s-production-go-live-gate.yml` before the storage/backup contract. The aggregate gate now fails before evidence collection can claim Azure backup readiness when the protected SAS cannot list the container; backup-agent/protected-workflow/YAML contracts pass.
- 2026-08-10T14:05Z — Corrected `validate-k3s-remaining-gates.ps1` to inspect the actual `his-hope-k3s-etcd-snapshot.timer/service` units and evaluate oneshot `Result=success` plus `ExecMainStatus=0`, instead of incorrectly using `is-active` on the service. Static contract and PowerShell parse pass; live gate still reports backup `blocked` because this workstation cannot execute remote systemd checks.
- 2026-08-10T14:08Z — Added optional protected SSH inputs to the remaining-gates validator and production go-live workflow. The read-only audit now parses the production inventory, checks all three control-plane timer/service states, records only host/unit status and fails on `Result=exit-code`/`ExecMainStatus=1`. Local audit reached all three hosts and confirmed the concrete Azure backup failure; no restart or apply was issued. Backup-agent/protected-workflow/YAML contracts pass.
- 2026-08-10T14:09Z — Extended the backup contract regression checks to require the remote inventory/key path and sanitized host evidence fields. Backup-agent contract, action-pin (29/126), protected-workflow (16) and diff checks pass.
- 2026-08-10T14:09Z — Fresh read-only production refresh: reliability remains pass; go-live remains fail-closed on live `privileged` PSA, 27 image/runtime drifts, immutable `local-path` continuity PVC, stale appointment mapping and five missing DR artifacts. SSH audit reaches all three control-plane hosts and reports timer active but backup oneshot `exit-code=1`; no mutation was issued.
- 2026-08-10T14:10Z — Static DevSecOps refresh passes action pins (29/126), protected workflows (16), container release, GitOps promotion, manifest-secret, backup-agent, production Kustomize (168 docs) and runtime Kustomize contracts. Strict DR evidence validator remains correctly blocked on the five missing measured drill artifacts; none were fabricated.
- 2026-08-10T14:11Z — Hardened backup source consistency: protected rollout now compares account, container, endpoint and SAS against `group_vars/all.yml` plus encrypted Vault before Ansible. The runbook documents the two-source model; backup-agent/protected-workflow/action-pin/YAML contracts pass, and no secret values were emitted.
- 2026-08-10T14:12Z — Verified the source-field contract without reading secret values: secure Azure env has account/container/endpoint/SAS, Ansible defaults have endpoint/container/prefix, and the runtime prefix remains intentionally more specific (`.../postgres`) than the common prefix. Backup-agent/protected-workflow/action-pin gates pass; no production mutation.
- 2026-08-10T14:13Z — Strengthened `validate-azure-blob-access.py` to require container-scoped SAS permissions `r,a,c,w,l`, matching CNPG/etcd backup write requirements while still performing only a read/list request. Current secure env passes; Python compile, backup-agent contract and diff checks pass.
- 2026-08-10T14:14Z — Added SAS expiry (`se`) validation to the Blob preflight; expired or malformed tokens now fail before CI/Ansible. Current secure env passes permission, expiry and list checks; no secret value or write operation was emitted.
- 2026-08-10T14:15Z — Regression refresh: Python script tests, all workflow YAML parsing and recursive PowerShell parsing pass after backup hardening. This validates source integrity only; production runtime blockers remain fail-closed.
- 2026-08-10T15:07Z — Approved production change window used for the backup-agent rollout. The first smoke exposed a checksum URL construction defect: `.sha256` was appended after the SAS query, causing Azure `403 AuthenticationFailed` on the second AzCopy operation. Corrected `scripts/k3s-etcd-snapshot-to-azure.sh` to build the checksum blob path before attaching the SAS; backup-agent contract, Bash syntax and diff checks pass.
- 2026-08-10T15:09Z — Re-applied `30-backup-agents.yml` to `.7`, `.8` and `.9` with ephemeral SSH/Vault credentials: all three hosts reachable, `failed=0`. Controlled `systemctl start --no-block` smoke completed successfully on all three (`Result=success`, `ExecMainStatus=0`, `ActiveState=inactive`). Local snapshots are non-empty (approximately 52–57 MB) and Azure listing shows 18 non-empty objects under `his-hope/production/k3s/`, including the three fresh snapshots and checksum objects. Backup gate is now PASS; no Longhorn/Argo/Sigstore rollout has started yet.
- 2026-08-10T15:10Z — Security follow-up: the previously exposed SAS was rotated to a new user-delegation SAS (expiry 2026-08-16T14:54Z) and deployed through encrypted Vault. The old token must still be revoked/invalidated at the Azure control plane if its issuance mechanism permits; short seven-day expiry requires automated rotation before expiration. Secret values remain excluded from evidence.
- 2026-08-10T15:12Z — Before the next approved phase, read-only node prerequisite audit found only one OS disk per node (`250G` on `.7`, `.8`, `.12`; `300G` on `.9`, `.10`) and `iscsid=inactive` on all five nodes. Longhorn/CSI bootstrap was intentionally not started: enterprise replicated storage requires a dedicated data disk/path and node prerequisites, and installing on the OS disk would create an unapproved durability and capacity risk.
- 2026-08-10T15:16Z — Hardened `bootstrap-longhorn-storage.ps1` and the storage contract: production now fails before Helm unless every node has the explicit `his-hope.io/longhorn-data-ready=true` label, which must be applied only after the dedicated-disk/iSCSI audit. Production preflight was executed with approval flags and correctly failed closed for all five unlabeled nodes; no cluster mutation occurred. PowerShell parse, storage-backup contract and diff checks pass.
- 2026-08-10T15:22Z — Added read-only `ansible/enterprise-k3s/playbooks/25-validate-storage-prerequisites.yml`. It resolves the physical root disk through LVM, checks for at least one non-root data disk, active `iscsid` and shared root mount propagation. The audit reached all five production nodes and failed closed on each (`data_disks=none`, `iscsid=False`, `root_propagation=shared`); no package, mount, label or cluster mutation occurred.
- 2026-08-10T15:25Z — Integrated the storage prerequisite audit into the protected Longhorn workflow for production, using `ANSIBLE_SSH_PRIVATE_KEY` only in-memory on the runner and cleaning it in `always()`. Protected-workflow (16), action-pin (29/126), workflow YAML and diff checks pass; the workflow will stop before Helm on the current five-node inventory.
- 2026-08-10T15:30Z — Static release refresh: Argo CD bootstrap, Sigstore policy, observability, GitOps promotion, production Kustomize and workflow contracts pass. Runtime admission/storage/rollout gates remain intentionally unclaimed until the host prerequisite audit and subsequent protected workflows complete.
- 2026-08-10T15:34Z — Pod Security preflight: rendered production manifests pass (`TOTAL_NONCOMPLIANT_CONTAINERS=0`), but live preflight correctly fails with 23 non-compliant containers (mainly missing `runAsNonRoot`, plus Vault/RabbitMQ/Redis and the host seccomp installer). Namespace enforcement remains `privileged`; no label change or restart was performed.
- 2026-08-10T15:40Z — Added `validate-storage-host-audit-contract.py` and wired it plus Ansible syntax validation into `k3s-devsecops-gate.yml`. The contract rejects formatting/mount/package mutations and requires disk, iSCSI, mount-propagation and fail-closed assertions. Contract, action-pin, protected-workflow, YAML and Ansible syntax checks pass.
- 2026-08-10T15:48Z — Full static regression refresh passes: Python tests, Azure production script contracts, production runner, container build, GitOps promotion, manifest-secret, K3s host security, secrets rotation and Linkerd policy contracts. Live release validator passes API connectivity, five Ready nodes, application health, Linkerd and immutable images; it correctly fails only on the live `privileged` Pod Security label in this check.
- 2026-08-10T15:55Z — Added `scripts/prepare-longhorn-nodes.ps1`: a protected wrapper that reruns the read-only Ansible disk/iSCSI audit and labels nodes only with explicit `-Apply -AllowProduction`; it never formats disks, mounts filesystems or installs packages. The Longhorn workflow now uses this wrapper before Helm and cleans the ephemeral SSH key. Workflow YAML, protected-workflow, action-pin, PowerShell parse and diff checks pass.
- 2026-08-10T16:05Z — Added `scripts/validate-live-image-drift.ps1` and made the protected Pod Security rollout call it before any boundary/namespace mutation. The live check currently fails closed on the reviewed application image drift (API gateway, services, BFFs, frontend, PgBouncer and continuity), while action-pin/protected-workflow/YAML/PowerShell/diff checks pass. No production mutation occurred.
- 2026-08-10T16:12Z — Runtime observability check found `monitoring/jaeger` OOMKilled and its HPA memory target `<unknown>`. Increased the reviewed Jaeger manifest resources from `512Mi/1Gi` to `1Gi/2Gi` (request/limit) and rendered monitoring manifests successfully; observability contract remains PASS. The change is source-only until Argo/GitOps sync is available, so no live restart was performed.
- 2026-08-10T16:18Z — Extended `validate-observability-contract.ps1` with a Jaeger memory-budget regression gate requiring the reviewed `1Gi` request/`2Gi` limit. Observability contract and action-pin checks pass; live Jaeger remains pending GitOps sync.
- 2026-08-10T16:24Z — Added `HisHopeJaegerOomKilled` critical Prometheus alert scoped to `monitoring/jaeger` and required its runbook link through the observability contract. The observability production render and contract now pass with 7 required alerts; no live alert rule was applied.
- 2026-08-10T15:26Z — Corrected Jaeger resource coverage: production renders Jaeger from `k8s/observability/k3s-observability.yaml`, while the standalone monitoring manifest is used by the monitoring kustomization. Both manifests now retain dedicated Jaeger storage and use the reviewed `1Gi` request/`2Gi` limit; the observability contract checks both paths. No live rollout was performed; runtime Jaeger remains unhealthy until the protected GitOps sync executes.
- 2026-08-10T15:26Z — Read-only runtime check confirms the source/runtime distinction: live `monitoring/jaeger` is still `CrashLoopBackOff` (149 restarts), with deployed `512Mi/1Gi` resources and a Bound `local-path` `10Gi` PVC. The source render and static gates pass, but observability runtime remains fail-closed until the approved GitOps rollout updates the deployment.
- 2026-08-10T15:27Z — Regression sweep passes container-release, GitOps promotion, protected-workflow (16), action-pin (29/126), manifest-secret, backup-agent, storage-host-audit, observability, production runtime-Kustomize, signature-controller and Linkerd policy contracts; Python/PowerShell focused tests also pass. Required signed-image verification remains environment-blocked because `cosign` is not installed on this workstation, and no production mutation was performed.
- 2026-08-10T15:30Z — Production storage inventory confirms only `local-path` and `secrets-store.csi.k8s.io`; no external block/file CSI, Longhorn or VolumeSnapshotClass exists. Added a fail-closed shared CSI validator and separate application/observability `prod-shared-storage` render overlays using the reviewed `viettel-shared` class placeholder. Both overlays render successfully, but the live shared-storage gate is correctly `blocked` until Viettel supplies and approves a vSAN/NFS CSI StorageClass; no disk, PVC or cluster resource was changed.
- 2026-08-10T15:33Z — Hardened Argo bootstrap for the no-new-disk profile: the pinned HA manifest is verified to be cache-only (`emptyDir`) and bootstrap now fails closed if a future upstream manifest introduces a PVC or persistent volume template. Argo bootstrap, protected-workflow, action-pin and diff checks pass; a staging `-WhatIf` against the reviewed checksum passed. No production apply occurred.
- 2026-08-10T15:35Z — Added protected `shared-storage-csi-gate.yml`: it materializes only the selected environment kubeconfig, runs the read-only shared CSI validator with snapshot/owner-approval checks, uploads redacted evidence and removes the kubeconfig. Action-pin gate now passes 30 workflows/129 refs; no storage mutation is possible through this workflow.
- 2026-08-10T15:36Z — Extended `validate-k3s-go-live.ps1` and the protected go-live workflow with an explicit reviewed `StorageClassName`. Production can now select the external Viettel CSI overlay instead of hard-coded Longhorn, while missing class/local-path PVC/immutable migration still fail-closed. A live `viettel-shared` run correctly failed on absent class, local-path PVC, image drift and existing production gates; no mutation occurred.
- 2026-08-10T15:37Z — Shared-storage regression refresh passes action pins (30/129), protected workflows (16), observability contract, production runtime-Kustomize render and both shared-storage overlay renders. The go-live script parses successfully and records `storageClass` in evidence; no production resources were changed.
- 2026-08-10T15:39Z — Aligned `validate-k3s-release.ps1` with the same reviewed `StorageClassName` used by go-live. Production Pod Security remains required even for the shared overlay, and the release evidence now records the selected storage backend. A read-only `viettel-shared` release run correctly fails on current cluster state; pins/protected-workflow/diff checks pass.
- 2026-08-10T15:41Z — Hardened storage-class workflow inputs with Kubernetes DNS-1123 validation and removed direct shell interpolation by passing the reviewed class through environment variables. Invalid `bad_name` is rejected; script parsing, action pins (30/129), protected workflows (16) and diff checks pass.
- 2026-08-10T16:10Z — Continued the production DevSecOps audit against the implementation plan. Static gates pass for workflow action pins (30/129), protected workflows (16), observability and rendered production manifests. The live release remains fail-closed: `his-hope` Pod Security is still `privileged`, `viettel-shared` CSI is not installed (only `local-path` is available), live images have not converged to the reviewed immutable digests, and the five measured DR evidence artifacts are unavailable. No production mutation was performed.
- 2026-08-10T16:18Z — Refreshed the production Kustomize render before validating migration isolation. The previous artifact was stale and incorrectly showed zero migration Jobs; the current render passes all migration gates: eight DbContext artifacts, seven API startup-migration flags disabled, seven bounded digest-pinned Argo Sync wave-20 Jobs, and no destructive SQL pattern. GitOps promotion also passes; no production mutation was performed.
- 2026-08-10T16:23Z — Re-ran the remaining-gates audit with the explicit deployment SSH key. Azure backup audit is now PASS on all three control-plane hosts (`timer=active`, `result=success`, `exitStatus=0`); the overall gate remains fail-closed only for missing signature provider, `privileged` Pod Security, absent external CSI and absent live Argo CD. No production mutation was performed.
