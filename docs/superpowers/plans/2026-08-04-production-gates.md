# Production Gates: Signed Images, Vault Secrets, and CNPG Backups

## Goal

Close the remaining production-readiness gates without committing credentials or
pretending that local image digests are signed:

1. Resolve immutable image digests from the local/runtime registry and provide a
   fail-closed cosign verification path for production.
2. Deploy production SecretProviderClass/Vault objects using workload identity
   and validate mounted/synchronised secrets without storing secret values in Git.
3. Install/configure the CloudNativePG Barman Cloud plugin with a self-hosted
   S3-compatible object store, scheduled backups, WAL archiving, retention, and
   a real backup/restore smoke test.

## Verification gates

- Kustomize renders with no zero digests and no plaintext secret values.
- Image gate reports `signed` only when cosign verifies a configured registry
  reference; local-only digests are reported as immutable but unsigned.
- Vault gate proves SecretProviderClass objects exist, Vault auth/policies are
  present, and a workload can mount/sync the expected secret keys.
- CNPG gate proves plugin/CRDs are ready, object-store connectivity works, a
  Backup reaches `completed`, ScheduledBackup is present, WAL archiving is
  enabled, and restore validation is executed or explicitly blocked with the
  exact missing prerequisite.
