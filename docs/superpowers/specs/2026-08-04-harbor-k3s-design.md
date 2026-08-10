# Harbor K3s Image Supply-Chain Design

## Goal

Run a self-hosted Harbor registry in K3s at `https://harbor.his-hope.local`,
persist images and metadata, provide least-privilege K3s pull access, and make
Cosign verification a release gate before production rollout.

## Architecture

Harbor is installed in namespace `harbor` with persistent `local-path` volumes
for registry, database, Redis, and job service. An Ingress exposes the core
service through the existing local TLS/host workflow. A robot account is stored
only as a Kubernetes pull secret and a separate signing identity is kept under
`D:\\secure\\his-hope`; no registry password or private key is committed.

Images are built and pushed as `harbor.his-hope.local/his-hope/<service>`,
resolved to immutable digests, signed with Cosign, verified, and only then
referenced by the production Kustomize overlay.

## Security boundaries

- Harbor admin password and robot credentials are runtime-only Kubernetes
  secrets.
- Production workloads receive pull-only credentials; push is limited to the
  release job.
- Harbor, Vault, and CNPG remain separate stateful platforms.
- Cosign verification fails closed when a signature, key, or digest is absent.

## Acceptance criteria

- `harbor.his-hope.local` serves HTTPS through Ingress.
- Harbor registry/database/Redis/job service are ready with persistent PVCs.
- A test image can be pushed, pulled by K3s using the pull secret, and verified
  by digest.
- A Cosign-signed digest verifies successfully; an unsigned digest is rejected.
- Production image references are changed only after verification.
