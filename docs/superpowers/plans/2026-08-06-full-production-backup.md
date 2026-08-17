# Full Production Backup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Provide an evidence-backed backup and restore workflow for K3s control-plane state, PostgreSQL, Kubernetes/PVC resources, Vault, Harbor, Redis, and configuration before production host deployment.

**Architecture:** Azure Blob is the off-site system of record for backups. MinIO remains local for fast recovery and staging, but is not falsely treated as a second WAL stream. Each backup component has an explicit writer, retention, and restore gate.

**Tech Stack:** K3s embedded-etcd snapshots, CloudNativePG Barman Cloud, Velero-compatible object storage, Vault snapshots, Harbor backup tooling, Redis RDB/AOF, Azure CLI/AzCopy, PowerShell and Bash.

## Global Constraints

- Never print or commit SAS tokens, passwords, client secrets, private keys, or Kubernetes Secret values.
- Never apply to `k3d-his-hope`; production context must be explicit and reachable.
- Local-path PVCs are not disaster-recovery storage; migrate to CSI-capable replicated storage before claiming PVC protection.
- Every backup class requires a restore test; a successful upload alone is not production evidence.

### Task 1: Backup inventory and gates

**Files:**
- Create: `docs/operations/full-production-backup-matrix.vi.md`

Produce a matrix with owner, source, Azure destination, local MinIO role, retention, RPO/RTO, backup command, restore command, and evidence status for each component. Mark PostgreSQL Azure backup as implemented, all remaining live gates as pending until executed.

### Task 2: K3s and platform snapshots

**Files:**
- Create: `scripts/k3s-etcd-snapshot-to-azure.sh`
- Create: `scripts/vault-snapshot-to-azure.sh`
- Create: `scripts/export-kubernetes-config-to-azure.sh`

Each script validates required environment variables, refuses placeholders, writes only to a dated prefix, uses TLS, emits redacted status, and fails closed on upload errors. K3s uses local embedded-etcd snapshots then AzCopy/Azure CLI upload because Azure Blob is not an S3 endpoint for native K3s `--etcd-s3`.

### Task 3: Kubernetes resources and PVC workflow

**Files:**
- Create: `k8s/backup/velero-azure-values.yaml`
- Create: `docs/operations/pvc-backup-migration.vi.md`

Document Velero/resource backup and the requirement to use CSI snapshots or filesystem backup for PVCs. Reject `local-path` as a DR guarantee and list the migration gate to Longhorn/Ceph/Azure Disk.

### Task 4: Service backups

**Files:**
- Create: `scripts/harbor-backup-to-azure.ps1`
- Create: `scripts/redis-backup-to-azure.sh`

Scripts validate source health, create encrypted/exported artifacts, upload to Azure, and report checksums without leaking credentials. Restore commands must target an isolated namespace/host.

### Task 5: CNPG Azure primary and MinIO local strategy

**Files:**
- Existing: `k8s/production-ha/cnpg-barman-object-store-azure.yaml`
- Existing: `k8s/overlays/prod-spire-azure/kustomization.yaml`
- Existing: `scripts/bootstrap-cnpg-azure-object-store.ps1`

Keep Azure as the active `barmanObjectName`, retain MinIO infrastructure, and do not claim MinIO has a second WAL stream until an independent copy/restore pipeline passes.

### Task 6: Verification and deployment gate

**Files:**
- Create: `scripts/verify-full-production-backup.ps1`
- Modify: `docs/operations/cnpg-azure-minio-backup-strategy.vi.md`

The verifier checks static configuration, required tools, kube-context reachability, object store readiness, recent backups, and restore evidence. It must report `PASS`, `FAIL`, `SKIPPED`, or `BLOCKED`, never infer success from configuration alone.
