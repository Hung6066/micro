# Production HA SPIRE on K3s Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** Deploy a production-shaped `his-hope` namespace with self-hosted PostgreSQL HA for SPIRE, three SPIRE Server replicas, native SPIRE workload identity, and verified Vault/Linkerd failover.

**Architecture:** CloudNativePG manages a three-instance PostgreSQL cluster in the `spire` namespace. SPIRE Server replicas share that PostgreSQL datastore while each keeps durable server key material on its own PVC; the SPIRE Service load-balances agents across servers. Production backends in `his-hope` use SPIRE JWT-SVID for Vault and Linkerd X509-SVID for service mTLS. Dev resources remain unchanged until migration and smoke gates pass.

**Tech Stack:** K3s, CloudNativePG 1.30.0, PostgreSQL 16, SPIRE 1.15.2, Linkerd CNI 1.6.8, Vault JWT auth, Kustomize, PowerShell validators.

## Global Constraints

- Do not delete or overwrite the existing dev PostgreSQL or `spiredb` before a verified dump, SHA-256 checksum, and restore check exist.
- Production secrets must come from Vault/Kubernetes Secret injection; the `cG9zdGdyZXM=` placeholder is forbidden.
- SPIRE Server replicas must use one shared PostgreSQL datastore and stable service discovery.
- Production workloads must not use `Vault__AuthMount=kubernetes`, `Vault__JwtTokenFile`, Docker socket attestation, or static Vault tokens.
- Every deployment step must have a rollback command and a fresh verification command.

---

### Task 1: Add production HA manifests and validators

**Files:**
- Create: `k8s/production-ha/cnpg-operator-version.yaml`
- Create: `k8s/production-ha/spire-postgres-cluster.yaml`
- Create: `k8s/production-ha/spire-ha-patches.yaml`
- Create: `k8s/production-ha/kustomization.yaml`
- Create: `scripts/validate-production-ha-k3s.ps1`
- Modify: `k8s/spire/server-config.yaml`
- Modify: `k8s/spire/server-statefulset.yaml`

**Interfaces:**
- `spire-postgres-rw.spire.svc.cluster.local:5432` is the production SPIRE datastore endpoint.
- `validate-production-ha-k3s.ps1` must fail on missing namespace, fewer than three ready PostgreSQL instances, fewer than three SPIRE Servers, default secret markers, legacy Vault auth markers, or failed SVID/Vault/mTLS smoke.

- [ ] Create a CloudNativePG `Cluster` with `instances: 3`, PostgreSQL 16, anti-affinity, a dedicated `spiredb` database and `spire_server` role, and a primary read/write service.
- [ ] Add a production SPIRE overlay that changes the server workload to three replicas with pod anti-affinity, a PDB, and the production datastore endpoint.
- [ ] Keep the dev `StatefulSet` at one replica until migration is complete; do not change dev service names.
- [ ] Add validator checks for CNPG cluster readiness, SPIRE replica count, datastore endpoint, production namespace, secret markers, and workload identity markers.
- [ ] Render dev and production manifests; expected result: both render successfully with the repository-approved production load restriction.

### Task 2: Install CloudNativePG and prepare production secrets

**Files:**
- Modify: `docs/operations/k3s-deployment.md`
- Create: `scripts/bootstrap-production-ha-secrets.ps1`

**Interfaces:**
- The operator installation uses the pinned CloudNativePG 1.30.0 release manifest.
- The bootstrap script accepts no secret values on the command line; it verifies Vault-backed or pre-created Kubernetes Secrets and exits nonzero when placeholders are present.

- [ ] Install the pinned operator manifest server-side and wait for `cnpg-controller-manager` readiness.
- [ ] Create the production `spire-postgres-superuser` and `spire-postgres-app` Secrets from the approved secret source.
- [ ] Apply the CNPG cluster and wait for `Cluster` status `Cluster in healthy state` with three instances.
- [ ] Verify the generated `spire-postgres-rw` service has one primary endpoint and the replica services have two eligible replicas.
- [ ] Record the operator version, cluster generation, and Secret resource names in the deployment evidence output.

### Task 3: Dump, restore, and cut SPIRE to PostgreSQL HA

**Files:**
- Create: `scripts/migrate-spire-datastore-to-cnpg.ps1`
- Create: `docs/operations/spire-postgres-ha-migration.md`

**Interfaces:**
- The migration script writes dumps only below `artifacts/spire-migration/<timestamp>/` and emits `dump.sha256` and `restore-check.json`.
- The migration script must use `pg_dump`/`pg_restore` without printing passwords or JWTs.

- [ ] Run a schema/data dump of dev `spiredb` and verify the SHA-256 checksum.
- [ ] Restore into the production CNPG `spiredb` and compare table names, row counts, and SPIRE registration-entry count.
- [ ] Patch the production SPIRE config Secret/ConfigMap to `spire-postgres-rw.spire.svc.cluster.local`.
- [ ] Restart one SPIRE Server at a time and verify all three become ready with no datastore errors in the last five minutes.
- [ ] Run `spire-server entry list`/agent list through the server API and verify entries survive each restart.
- [ ] Keep dev SPIRE active until production smoke passes; rollback means restoring the previous SPIRE datastore endpoint and restarting the dev server.

### Task 4: Deploy production namespace and workload identity

**Files:**
- Modify: `k8s/overlays/prod/kustomization.yaml`
- Modify: `k8s/overlays/prod/workload-spiffe-patches.yaml`
- Modify: `scripts/bootstrap-spire-k3s.ps1`

- [ ] Apply the production namespace and production overlay only after Task 3 passes.
- [ ] Register `spiffe://his-hope.local/ns/his-hope/sa/<service>` entries with the correct production parent agents.
- [ ] Verify every production backend has `spire-jwt-fetcher`, Linkerd proxy init, JWT-SVID file mode `0440`, and `Vault__AuthMethod=spiffe-jwt`.
- [ ] Verify no production manifest contains Kubernetes Vault auth, projected Vault token, Docker socket, or static token markers.
- [ ] Verify Vault JWT login for every production service using the real SVID and revoke one test Vault client token.

### Task 5: Production failover validation and sign-off

**Files:**
- Modify: `scripts/validate-production-ha-k3s.ps1`
- Modify: `docs/security/k3s-spire-workload-identity.md`
- Modify: `docs/operations/k3s-deployment.md`

- [ ] Delete one CNPG primary pod; verify a new primary is elected and SPIRE registration/SVID issuance continues.
- [ ] Delete one SPIRE Server pod at a time; verify agents reconnect through the Service and new JWT/X509 SVID requests succeed.
- [ ] Delete one production backend replica; verify Linkerd mTLS health calls continue through the remaining replica.
- [ ] Verify Vault remains unsealed and dynamic credentials continue after one PostgreSQL/SPIRE failure.
- [ ] Run the complete validator and record PASS/FAIL/SKIP for every production gate.
- [ ] Sign off only when the production namespace is deployed, secrets are non-default, HA replicas are ready, migration evidence exists, and all failover tests pass.

