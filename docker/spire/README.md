# SPIRE runtime adapters

This directory contains the host-independent contract for non-Kubernetes
workloads. SPIRE Server establishes the trust domain; a SPIRE Agent runs next
to each workload and exposes the Workload API over a Unix socket.

For a VM or bare-metal service:

1. Install `spire-server` centrally and `spire-agent` with systemd on the host.
2. Use a node attestor appropriate for the host (TPM is preferred; join token
   is acceptable only for controlled bootstrap).
3. Register the service's SPIFFE ID and the `vault` audience.
4. Periodically execute `spire-agent api fetch jwt -audience vault` and write
   the result to a root-readable, service-readable file such as
   `/run/spire/jwt/vault.jwt`.
5. Set `Vault:AuthMethod=spiffe-jwt`, `Vault:AuthMount=jwt-spiffe`, and
   `Vault:SpiffeJwtTokenFile` to that file.

The file is ephemeral and must be on tmpfs with mode `0640`; it must never be
committed or copied into an image.

For Docker Compose, run the complete E2E profile from the repository root:

```powershell
docker compose -f docker/docker-compose.yml -f docker/docker-compose.spiffe.yml `
  --profile spiffe-e2e up -d patientservice
```

The profile includes SPIRE Server/Agent, the SPIRE OIDC Discovery Provider,
Vault JWT auth, PostgreSQL management bootstrap, all service database roles,
and dynamic-credential probes. The wrapper images are used only because the
official SPIRE images are distroless and cannot execute shell bootstrap
scripts. The actual Server, Agent and OIDC Provider binaries remain the
official SPIRE binaries.

The Compose fetcher uses a shared PID namespace so the Docker Workload
Attestor can resolve the caller of the Agent Workload API. This is a local E2E
adapter; in production run the fetcher with the workload or use a native
SPIFFE library/Workload API client, and do not mount the Docker socket into
application containers. Do not use this profile as a Production HA topology
substitute.
